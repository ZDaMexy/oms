// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Platform;
using osu.Game.Skinning.Gameplay.Scripting;

namespace osu.Game.Skinning.Gameplay
{
    internal enum GameplaySkinScriptAuthorizationChoice
    {
        NotDecided,
        Granted,
        Denied,
    }

    internal readonly record struct GameplaySkinScriptRuntimeReport(
        string Status, long Callbacks, long Instructions, long ElapsedTicks, int HeapBytes, string? FaultCode, int FaultLine);

    /// <summary>
    /// A live revocation token for one engine record and exact package/compiler/runtime identity. It is never an author ABI.
    /// Its immutable decision is exchanged when consent changes, independently of the package reload admission gate.
    /// </summary>
    internal sealed class GameplaySkinScriptAuthorization
    {
        private readonly GameplaySkinScriptAuthorizationStore owner;
        private readonly string[] hostFeatures;
        private Decision decision = new Decision(0, 0, 0, new Dictionary<string, GameplaySkinScriptAuthorizationChoice>(), new HashSet<string>(), false);
        private readonly object reportGate = new object();
        private GameplaySkinScriptRuntimeReport report = new GameplaySkinScriptRuntimeReport("not-attached", 0, 0, 0, 0, null, 0);

        internal event Action? Changed;

        internal string Key { get; }
        internal IReadOnlyList<ScriptCapabilityRequest> Requests { get; }
        public long Version => Volatile.Read(ref decision).Version;
        internal (long Epoch, int GrantMask, bool RequiredSatisfied) RuntimeRights
        {
            get
            {
                Decision current = Volatile.Read(ref decision);
                return (current.RuntimeEpoch, current.GrantMask, current.RequiredSatisfied);
            }
        }
        public bool RequiredSatisfied => Volatile.Read(ref decision).RequiredSatisfied;
        public bool IsGranted(string capabilityId) => Volatile.Read(ref decision).Granted.Contains(capabilityId);
        public GameplaySkinScriptAuthorizationChoice GetChoice(string capabilityId)
            => Volatile.Read(ref decision).Choices.GetValueOrDefault(capabilityId);
        public string? PersistenceError => owner.PersistenceError;
        public GameplaySkinScriptRuntimeReport RuntimeReport { get { lock (reportGate) return report; } }

        internal GameplaySkinScriptAuthorization(GameplaySkinScriptAuthorizationStore owner, string key,
                                                 IReadOnlyList<ScriptCapabilityRequest> requests, IEnumerable<string> hostFeatures)
        {
            this.owner = owner;
            Key = key;
            Requests = requests;
            this.hostFeatures = hostFeatures.ToArray();
        }

        public bool CanGrant(string id)
            => Requests.Any(request => request.Id == id && request.Mode != ScriptCapabilityMode.Deny)
               && GameplaySkinScriptAuthorizationStore.IsAllowed(id);

        public Task<bool> SetAsync(string id, GameplaySkinScriptAuthorizationChoice choice) => owner.SetAsync(this, id, choice);

        public void ReportRuntime(string status, long callbacks, long instructions, long elapsedTicks, int heapBytes,
                                  string? faultCode = null, int faultLine = 0)
        {
            lock (reportGate)
                report = new GameplaySkinScriptRuntimeReport(status, callbacks, instructions, elapsedTicks, heapBytes, faultCode, faultLine);
        }

        internal void Publish(long version, IReadOnlyDictionary<string, GameplaySkinScriptAuthorizationChoice> choices, bool shutdown)
        {
            var requested = GameplaySkinCapabilityRequest.Create(Requests.Where(request => request.Mode != ScriptCapabilityMode.Deny)
                .Select(request => GameplaySkinCapabilityId.Create(request.Id)));
            var authorised = choices.Where(item => item.Value == GameplaySkinScriptAuthorizationChoice.Granted)
                .Select(item => GameplaySkinCapabilityId.Create(item.Key));
            GameplaySkinCapabilityNegotiation negotiated = GameplaySkinCapabilityNegotiator.Negotiate(requested,
                GameplaySkinScriptAuthorizationStore.DEFINITIONS, shutdown ? Array.Empty<string>() : hostFeatures, authorised);
            var granted = new HashSet<string>(negotiated.GrantedCapabilityIds.Select(id => id.Value), StringComparer.Ordinal);
            bool required = !shutdown && Requests.Where(request => request.Mode == ScriptCapabilityMode.Required).All(request => granted.Contains(request.Id));
            Decision previous = Volatile.Read(ref decision);
            bool rightsChanged = previous.RequiredSatisfied != required || !previous.Granted.SetEquals(granted);
            int grantMask = (granted.Contains("gameplay.snapshot.read") ? 1 : 0)
                            | (granted.Contains("gameplay.events.read") ? 2 : 0)
                            | (granted.Contains("scene.numeric.write") ? 4 : 0)
                            | (granted.Contains("math.random.read") ? 8 : 0);
            Volatile.Write(ref decision, new Decision(version, previous.RuntimeEpoch + (rightsChanged ? 1 : 0), grantMask,
                new Dictionary<string, GameplaySkinScriptAuthorizationChoice>(choices), granted, required));
            if (rightsChanged)
                Changed?.Invoke();
        }

        private sealed record Decision(long Version, long RuntimeEpoch, int GrantMask, IReadOnlyDictionary<string, GameplaySkinScriptAuthorizationChoice> Choices,
                                       HashSet<string> Granted, bool RequiredSatisfied);
    }

    /// <summary>
    /// Engine-owned consent storage. Package IDs are not trusted: only a stable engine record plus exact content and VM
    /// versions can reuse consent. All disk work runs on a worker; revocation takes effect before the write is queued.
    /// </summary>
    internal sealed class GameplaySkinScriptAuthorizationStore
    {
        internal const string FILE_NAME = "skin-script-authorizations.json";
        private const string contract = "oms-skin-script-authorizations.v1";
        private const int max_file_bytes = 1024 * 1024;
        private const int max_entries = 1024;
        private static readonly UTF8Encoding utf8 = new UTF8Encoding(false, true);
        internal static readonly string[] HOST_FEATURES = { "snapshot.v1", "events.v1", "scene.numeric.v1", "random.v1" };
        internal static readonly GameplaySkinCapabilityDefinition[] DEFINITIONS =
        {
            definition("gameplay.snapshot.read", "snapshot.v1"),
            definition("gameplay.events.read", "events.v1"),
            definition("scene.numeric.write", "scene.numeric.v1"),
            definition("math.random.read", "random.v1"),
        };

        private readonly object gate = new object();
        private readonly string path;
        private readonly List<WeakReference<GameplaySkinScriptAuthorization>> tokens = new List<WeakReference<GameplaySkinScriptAuthorization>>();
        private Dictionary<string, Dictionary<string, GameplaySkinScriptAuthorizationChoice>> desired = new Dictionary<string, Dictionary<string, GameplaySkinScriptAuthorizationChoice>>(StringComparer.Ordinal);
        private Dictionary<string, Dictionary<string, GameplaySkinScriptAuthorizationChoice>> effective = new Dictionary<string, Dictionary<string, GameplaySkinScriptAuthorizationChoice>>(StringComparer.Ordinal);
        private readonly List<(long Version, string Key, string Id, GameplaySkinScriptAuthorizationChoice Choice)> pendingRevocations =
            new List<(long, string, string, GameplaySkinScriptAuthorizationChoice)>();
        private Task<bool> writeTail = Task.FromResult(true);
        private long version;
        private bool stopped;
        internal Action? BeforePersistence;
        public Task Ready { get; }
        public string? PersistenceError { get; private set; }

        public GameplaySkinScriptAuthorizationStore(Storage storage)
        {
            path = storage.GetFullPath(FILE_NAME);
            Ready = Task.Run(load);
        }

        internal static bool IsAllowed(string id)
            => DEFINITIONS.Any(item => item.CapabilityId.Value == id)
               && !GameplaySkinCapabilityHardDenyCatalog.IsHardDenied(GameplaySkinCapabilityId.Create(id));

        private static GameplaySkinCapabilityDefinition definition(string id, string feature)
            => new GameplaySkinCapabilityDefinition(GameplaySkinCapabilityId.Create(id), feature, GameplaySkinCapabilityAccessPolicy.PerSkinAuthorization);

        public GameplaySkinScriptAuthorization Bind(Guid recordId, string fingerprint, IReadOnlyList<ScriptCapabilityRequest> requests,
                                                   IEnumerable<string>? hostFeatures = null)
        {
            // Binding is part of the existing background package preparation, never a renderer callback.
            Ready.GetAwaiter().GetResult();
            if (recordId == Guid.Empty || fingerprint.Length != 64 || !fingerprint.All(Uri.IsHexDigit))
                throw new ArgumentException("An authorization requires an engine record and a SHA-256 package/VM identity.");

            lock (gate)
            {
                var token = new GameplaySkinScriptAuthorization(this, recordId.ToString("N") + ":" + fingerprint.ToLowerInvariant(), requests, hostFeatures ?? HOST_FEATURES);
                tokens.RemoveAll(weak => !weak.TryGetTarget(out _));
                tokens.Add(new WeakReference<GameplaySkinScriptAuthorization>(token));
                token.Publish(version, effective.GetValueOrDefault(token.Key) ?? new Dictionary<string, GameplaySkinScriptAuthorizationChoice>(), stopped);
                return token;
            }
        }

        internal Task<bool> SetAsync(GameplaySkinScriptAuthorization token, string id, GameplaySkinScriptAuthorizationChoice choice)
        {
            if (!Enum.IsDefined(choice) || !token.Requests.Any(request => request.Id == id))
                throw new ArgumentException("Consent must address a capability declared by this exact package.");
            if (choice == GameplaySkinScriptAuthorizationChoice.Granted && !token.CanGrant(id))
                return Task.FromResult(false);

            lock (gate)
            {
                if (stopped || (!desired.ContainsKey(token.Key) && desired.Count >= max_entries))
                    return Task.FromResult(false);

                setChoice(desired, token.Key, id, choice);
                long operationVersion = ++version;
                // An old VM loses access now, even while a prior consent write is still in flight.
                if (choice != GameplaySkinScriptAuthorizationChoice.Granted)
                {
                    setChoice(effective, token.Key, id, choice);
                    pendingRevocations.Add((operationVersion, token.Key, id, choice));
                    publishTokens();
                }

                Task<bool> previous = writeTail;
                return writeTail = Task.Run(async () =>
                {
                    await previous.ConfigureAwait(false);
                    BeforePersistence?.Invoke();
                    Dictionary<string, Dictionary<string, GameplaySkinScriptAuthorizationChoice>> snapshot;
                    lock (gate)
                    {
                        // Start from decisions whose grants actually succeeded. A queued operation must not copy
                        // another operation's still-pending grant into its own durable snapshot.
                        snapshot = copy(effective);
                        setChoice(snapshot, token.Key, id, choice);
                        applyPendingRevocations(snapshot, operationVersion);
                    }
                    bool saved = persist(snapshot);
                    lock (gate)
                    {
                        if (saved)
                        {
                            // A revocation can arrive while this exact write is in flight. Preserve its immediate
                            // effect even when an earlier grant is now durable; later grants still await their write.
                            applyPendingRevocations(snapshot, operationVersion);
                            effective = snapshot;
                            publishTokens();
                        }
                        // All earlier writers have joined. Their revocations are now reflected in the effective
                        // baseline, including revocations whose disk write failed; only later barriers remain needed.
                        pendingRevocations.RemoveAll(item => item.Version <= operationVersion);
                        if (version == operationVersion)
                            desired = copy(effective);
                    }
                    return saved;
                });
            }
        }

        private void applyPendingRevocations(Dictionary<string, Dictionary<string, GameplaySkinScriptAuthorizationChoice>> snapshot, long operationVersion)
        {
            // A later queued re-grant does not cancel the revocation barrier: only that re-grant's own successful
            // persistence can restore access. In particular A grant -> revoke -> B grant cannot be enabled by A.
            foreach (var revocation in pendingRevocations)
            {
                if (revocation.Version > operationVersion)
                    setChoice(snapshot, revocation.Key, revocation.Id, revocation.Choice);
            }
        }

        public void Shutdown()
        {
            Task<bool> pending;
            lock (gate)
            {
                stopped = true;
                version++;
                publishTokens();
                pending = writeTail;
            }
            Ready.GetAwaiter().GetResult();
            pending.GetAwaiter().GetResult();
        }

        private void publishTokens()
        {
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                if (tokens[i].TryGetTarget(out GameplaySkinScriptAuthorization? token))
                    token.Publish(version, effective.GetValueOrDefault(token.Key) ?? new Dictionary<string, GameplaySkinScriptAuthorizationChoice>(), stopped);
                else
                    tokens.RemoveAt(i);
            }
        }

        private static void setChoice(Dictionary<string, Dictionary<string, GameplaySkinScriptAuthorizationChoice>> target,
                                      string key, string id, GameplaySkinScriptAuthorizationChoice choice)
        {
            if (!target.TryGetValue(key, out Dictionary<string, GameplaySkinScriptAuthorizationChoice>? entry))
                target.Add(key, entry = new Dictionary<string, GameplaySkinScriptAuthorizationChoice>(StringComparer.Ordinal));
            if (choice == GameplaySkinScriptAuthorizationChoice.NotDecided)
                entry.Remove(id);
            else
                entry[id] = choice;
            if (entry.Count == 0)
                target.Remove(key);
        }

        private static Dictionary<string, Dictionary<string, GameplaySkinScriptAuthorizationChoice>> copy(
            Dictionary<string, Dictionary<string, GameplaySkinScriptAuthorizationChoice>> input)
            => input.ToDictionary(item => item.Key, item => new Dictionary<string, GameplaySkinScriptAuthorizationChoice>(item.Value), StringComparer.Ordinal);

        private void load()
        {
            try
            {
                // A process exit or failed atomic replacement may leave a newer revocation uncommitted. Never
                // resurrect permissions from the older file while that evidence remains present.
                if (File.Exists(path + ".pending") || Directory.Exists(path + ".pending"))
                    throw new InvalidDataException();
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length > max_file_bytes)
                    throw new InvalidDataException();
                byte[] bytes = new byte[(int)stream.Length];
                stream.ReadExactly(bytes);
                using var reader = new JsonTextReader(new StringReader(utf8.GetString(bytes))) { MaxDepth = 8, DateParseHandling = DateParseHandling.None };
                var document = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read() || document.Properties().Count() != 2 || document["contract"]?.Type != JTokenType.String || document.Value<string>("contract") != contract
                    || document["entries"] is not JObject entries || entries.Count > max_entries)
                    throw new InvalidDataException();

                var loaded = new Dictionary<string, Dictionary<string, GameplaySkinScriptAuthorizationChoice>>(StringComparer.Ordinal);
                foreach (JProperty entry in entries.Properties())
                {
                    string[] key = entry.Name.Split(':');
                    if (key.Length != 2 || !Guid.TryParseExact(key[0], "N", out Guid record) || record == Guid.Empty
                        || key[1].Length != 64 || !key[1].All(Uri.IsHexDigit) || entry.Value is not JObject choices || choices.Count > GameplaySkinScriptProgram.MAX_REQUESTS)
                        throw new InvalidDataException();
                    var values = new Dictionary<string, GameplaySkinScriptAuthorizationChoice>(StringComparer.Ordinal);
                    foreach (JProperty item in choices.Properties())
                    {
                        if (!IsAllowed(item.Name) || item.Value.Type != JTokenType.String || item.Value.Value<string>() is not ("granted" or "denied"))
                            throw new InvalidDataException();
                        values.Add(item.Name, item.Value.Value<string>() == "granted" ? GameplaySkinScriptAuthorizationChoice.Granted : GameplaySkinScriptAuthorizationChoice.Denied);
                    }
                    loaded.Add(entry.Name, values);
                }
                lock (gate)
                {
                    desired = loaded;
                    effective = copy(loaded);
                }
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or DecoderFallbackException)
            {
                PersistenceError = "OMS-SKIN-AUTH-INVALID-STORAGE";
            }
        }

        private bool persist(Dictionary<string, Dictionary<string, GameplaySkinScriptAuthorizationChoice>> snapshot)
        {
            try
            {
                var entries = new JObject(snapshot.OrderBy(item => item.Key, StringComparer.Ordinal).Select(entry => new JProperty(entry.Key,
                    new JObject(entry.Value.Where(item => IsAllowed(item.Key)).OrderBy(item => item.Key, StringComparer.Ordinal).Select(item =>
                        new JProperty(item.Key, item.Value == GameplaySkinScriptAuthorizationChoice.Granted ? "granted" : "denied"))))));
                byte[] bytes = utf8.GetBytes(new JObject { ["contract"] = contract, ["entries"] = entries }.ToString(Formatting.None));
                if (bytes.Length > max_file_bytes)
                    throw new InvalidDataException();
                // A private engine-owned file in the selected data root. No author-controlled path reaches this store.
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                using (var output = new FileStream(path + ".pending", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    output.Write(bytes);
                    output.Flush(true);
                }
                File.Move(path + ".pending", path, true);
                PersistenceError = null;
                return true;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                PersistenceError = "OMS-SKIN-AUTH-WRITE-FAILED";
                return false;
            }
        }
    }
}
