// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Platform;

namespace osu.Game.Online.IR
{
    /// <summary>A saved, deterministic request. Tokens never enter this store.</summary>
    internal sealed record OmsIrQueueEntry(Guid SubmissionId, OmsIrSubmissionTarget Target, string Body, int Attempts,
                                          DateTimeOffset NextAttempt, string? BlockedReason);

    internal sealed class OmsIrQueue
    {
        internal const int MaximumPayloadBytes = 64 * 1024;

        private readonly string directory;
        private readonly Dictionary<string, OmsIrQueueEntry> entries = new Dictionary<string, OmsIrQueueEntry>();

        public IReadOnlyCollection<OmsIrQueueEntry> Entries => entries.Values;
        public string? RecoveryNotice { get; private set; }

        public OmsIrQueue(Storage storage)
        {
            directory = storage.GetFullPath("pending", true);
            Directory.CreateDirectory(directory);

            string[] names = Directory.EnumerateFiles(directory, "*.json*")
                                      .Select(Path.GetFileName)
                                      .OfType<string>()
                                      .Where(name => name.EndsWith(".json", StringComparison.Ordinal)
                                          || name.EndsWith(".json.backup", StringComparison.Ordinal) || name.Contains(".json.tmp-", StringComparison.Ordinal))
                                      .Select(name => name[..(name.IndexOf(".json", StringComparison.Ordinal) + 5)])
                                      .Distinct(StringComparer.Ordinal).ToArray();

            foreach (string name in names)
            {
                if (name.Length != 69 || !name[..64].All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
                    throw new InvalidDataException("The OMS IR queue has an invalid file identity.");

                OmsIrQueueEntry entry = OmsIrAtomicFile.Read(Path.Combine(directory, name), decode, out bool recovered);
                string key = keyFor(entry.Target, entry.SubmissionId);
                if (name != key + ".json")
                    throw new InvalidDataException("The OMS IR queue file does not match its owner.");

                entries.Add(key, entry);
                if (recovered)
                    RecoveryNotice = "已恢复上次完整的待交文件；异常文件保留在保存目录。";
            }
        }

        public void Enqueue(OmsIrSubmissionTarget target, OmsIrSubmission submission)
        {
            string body = submission.Payload.ToString(Formatting.None);
            if (submission.SubmissionId == Guid.Empty || Encoding.UTF8.GetByteCount(body) > MaximumPayloadBytes
                || (string?)submission.Payload["submission_id"] != submission.SubmissionId.ToString())
                throw new InvalidDataException("The OMS IR submission has an invalid identity or exceeds its payload budget.");

            string key = keyFor(target, submission.SubmissionId);
            if (entries.TryGetValue(key, out OmsIrQueueEntry? existing))
            {
                if (!string.Equals(existing.Body, body, StringComparison.Ordinal))
                    throw new InvalidDataException("A different payload already owns this OMS IR submission ID.");
                return;
            }

            var entry = new OmsIrQueueEntry(submission.SubmissionId, target, body, 0, DateTimeOffset.UtcNow, null);
            persist(entry);
            entries.Add(key, entry);
        }

        public void Replace(OmsIrQueueEntry entry)
        {
            persist(entry);
            entries[keyFor(entry.Target, entry.SubmissionId)] = entry;
        }

        public OmsIrQueueEntry Get(OmsIrQueueEntry entry) => entries[keyFor(entry.Target, entry.SubmissionId)];

        public void Remove(OmsIrQueueEntry entry)
        {
            string key = keyFor(entry.Target, entry.SubmissionId);
            string path = pathFor(key);
            // Remove recovery copies before the authoritative file, so a completed request cannot be resurrected.
            File.Delete(path + ".backup");
            foreach (string temporary in Directory.EnumerateFiles(directory, key + ".json.tmp-*"))
                File.Delete(temporary);
            File.Delete(path);
            entries.Remove(key);
        }

        private void persist(OmsIrQueueEntry entry)
        {
            var document = new JObject
            {
                ["version"] = 1,
                ["service_address"] = entry.Target.ServiceUri.AbsoluteUri,
                ["user_id"] = entry.Target.UserId,
                ["submission_id"] = entry.SubmissionId.ToString(),
                ["body"] = entry.Body,
                ["attempts"] = entry.Attempts,
                ["next_attempt"] = entry.NextAttempt.ToString("O", CultureInfo.InvariantCulture),
                ["blocked_reason"] = entry.BlockedReason,
            };
            OmsIrAtomicFile.Write(pathFor(keyFor(entry.Target, entry.SubmissionId)), document.ToString(Formatting.None));
        }

        private string pathFor(string key) => Path.Combine(directory, key + ".json");

        private static string keyFor(OmsIrSubmissionTarget target, Guid submissionId) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(target.ServiceUri.AbsoluteUri + "\n"
                + target.UserId.ToString(CultureInfo.InvariantCulture) + "\n" + submissionId))).ToLowerInvariant();

        private static OmsIrQueueEntry decode(string contents)
        {
            JObject document = OmsIrJson.Parse(contents);
            if (OmsIrJson.Integer(document, "version") != 1 || !Guid.TryParseExact(OmsIrJson.String(document, "submission_id"), "D", out Guid id) || id == Guid.Empty)
                throw new InvalidDataException("The OMS IR queue record has an unsupported version or identity.");

            Uri origin;
            try
            {
                origin = OmsIrService.ParseServiceAddress(OmsIrJson.String(document, "service_address"));
            }
            catch (OmsIrException exception)
            {
                throw new InvalidDataException("The OMS IR queue record has an invalid service origin.", exception);
            }

            long userId = OmsIrJson.Integer(document, "user_id");
            long attempts = OmsIrJson.Integer(document, "attempts");
            string body = OmsIrJson.String(document, "body");
            if (userId <= 0 || attempts is < 0 or > 5 || Encoding.UTF8.GetByteCount(body) > MaximumPayloadBytes
                || !DateTimeOffset.TryParseExact(OmsIrJson.String(document, "next_attempt"), "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset nextAttempt)
                || (string?)OmsIrJson.Parse(body)["submission_id"] != id.ToString())
                throw new InvalidDataException("The OMS IR queue record has invalid ownership, timing or payload.");

            JToken? blockedToken = document["blocked_reason"];
            if (blockedToken == null || blockedToken.Type is not (JTokenType.Null or JTokenType.String))
                throw new InvalidDataException("The OMS IR queue record has an invalid failure description.");

            return new OmsIrQueueEntry(id, new OmsIrSubmissionTarget(origin, userId), body, (int)attempts, nextAttempt, (string?)blockedToken);
        }
    }

    internal static class OmsIrAtomicFile
    {
        private const int maximum_file_bytes = 256 * 1024;

        public static void Write(string path, string contents)
        {
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            byte[] bytes = Encoding.UTF8.GetBytes(contents);
            if (bytes.Length > maximum_file_bytes)
                throw new InvalidDataException("The OMS IR data file exceeds its storage budget.");

            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }

            if (File.Exists(path))
            {
                File.Delete(path + ".backup");
                File.Replace(temporary, path, path + ".backup");
            }
            else
                File.Move(temporary, path);
        }

        public static T Read<T>(string path, Func<string, T> decode, out bool recovered)
        {
            recovered = false;
            if (File.Exists(path))
            {
                try
                {
                    return readFile(path, decode);
                }
                catch (InvalidDataException) when (File.Exists(path + ".backup"))
                {
                    T previous = readFile(path + ".backup", decode);
                    File.Move(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                    File.Move(path + ".backup", path);
                    recovered = true;
                    return previous;
                }
            }

            string recoveryPath = File.Exists(path + ".backup") ? path + ".backup"
                : Directory.EnumerateFiles(Path.GetDirectoryName(path)!, Path.GetFileName(path) + ".tmp-*").Order(StringComparer.Ordinal).FirstOrDefault()
                  ?? throw new FileNotFoundException("The OMS IR data file has no complete recovery source.", path);
            T result = readFile(recoveryPath, decode);
            File.Move(recoveryPath, path);
            recovered = true;
            return result;
        }

        private static T readFile<T>(string path, Func<string, T> decode)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > maximum_file_bytes)
                throw new InvalidDataException("The OMS IR data file exceeds its storage budget.");
            try
            {
                using var reader = new StreamReader(stream, new UTF8Encoding(false, true));
                return decode(reader.ReadToEnd());
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("The OMS IR data file is not valid UTF-8.", exception);
            }
        }
    }

    internal static class OmsIrJson
    {
        public static JObject Parse(string contents)
        {
            try
            {
                using var input = new StringReader(contents);
                using var reader = new JsonTextReader(input) { DateParseHandling = DateParseHandling.None, MaxDepth = 32 };
                JObject result = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read())
                    throw new InvalidDataException("The OMS IR JSON contains multiple documents.");
                return result;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("The OMS IR JSON cannot be decoded.", exception);
            }
        }

        public static string String(JObject document, string name) => document[name]?.Type == JTokenType.String
            ? document[name]!.Value<string>()! : throw new InvalidDataException("The OMS IR JSON is missing a string field.");

        public static long Integer(JObject document, string name)
        {
            if (document[name]?.Type != JTokenType.Integer)
                throw new InvalidDataException("The OMS IR JSON is missing an integer field.");
            try
            {
                return document[name]!.Value<long>();
            }
            catch (Exception exception) when (exception is OverflowException or InvalidCastException or FormatException)
            {
                throw new InvalidDataException("The OMS IR integer exceeds its supported range.", exception);
            }
        }
    }
}
