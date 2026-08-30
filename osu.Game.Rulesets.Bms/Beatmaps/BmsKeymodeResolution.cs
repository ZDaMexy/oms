// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Rulesets.Bms.Difficulty;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// The single parser authority which selected a BMS keymode.
    /// </summary>
    public enum BmsKeymodeResolutionSource
    {
        Programmatic,
        ExplicitOverride,
        FileExtension,
        Player2ChannelEvidence,
        DistinctiveNineKeyChannelEvidence,
        CompleteChannelSet,
    }

    /// <summary>
    /// Non-sensitive evidence retained with a resolved keymode. Raw paths, filenames and channel payloads are
    /// intentionally excluded so this value is safe to include in diagnostics.
    /// </summary>
    [Flags]
    public enum BmsKeymodeEvidence
    {
        None = 0,
        ExplicitOverride = 1 << 0,
        PmsFileExtension = 1 << 1,
        BmeFileExtension = 1 << 2,
        Player2Channel = 1 << 3,
        DistinctiveNineKeyChannel = 1 << 4,
        CompleteFiveKeyChannelSet = 1 << 5,
        CompleteSevenKeyChannelSet = 1 << 6,
        CompleteNineKeyChannelSet = 1 << 7,
    }

    /// <summary>
    /// Stable reason codes for successful keymode resolution and fail-closed rejection.
    /// </summary>
    public enum BmsKeymodeDiagnosticCode
    {
        ProgrammaticContext,
        ExplicitOverrideApplied,
        PmsExtensionApplied,
        BmeExtensionApplied,
        Player2ChannelEvidenceApplied,
        NineKeyChannelEvidenceApplied,
        CompleteFiveKeyChannelSetApplied,
        CompleteSevenKeyChannelSetApplied,
        CompleteNineKeyChannelSetApplied,
        SparseBmsRequiresExplicitOverride,
        NoLaneEvidenceRequiresExplicitOverride,
        OverrideConflictsWithChannelEvidence,
        ExtensionConflictsWithChannelEvidence,
    }

    /// <summary>
    /// Immutable parser result which binds the selected keymode to its exact source, evidence and stable diagnostic.
    /// </summary>
    public sealed class BmsKeymodeResolution
    {
        public BmsKeymode Keymode { get; }

        public BmsKeymodeResolutionSource Source { get; }

        public BmsKeymodeEvidence Evidence { get; }

        public BmsKeymodeDiagnosticCode DiagnosticCode { get; }

        public string StableDiagnostic => BmsKeymodeDiagnostics.GetStableDiagnostic(DiagnosticCode);

        internal BmsKeymodeResolution(BmsKeymode keymode, BmsKeymodeResolutionSource source, BmsKeymodeEvidence evidence, BmsKeymodeDiagnosticCode diagnosticCode)
        {
            if (!Enum.IsDefined(keymode))
                throw new ArgumentOutOfRangeException(nameof(keymode));

            if (!Enum.IsDefined(source))
                throw new ArgumentOutOfRangeException(nameof(source));

            if (!Enum.IsDefined(diagnosticCode))
                throw new ArgumentOutOfRangeException(nameof(diagnosticCode));

            Keymode = keymode;
            Source = source;
            Evidence = evidence;
            DiagnosticCode = diagnosticCode;
        }

        internal static BmsKeymodeResolution CreateProgrammatic(BmsKeymode keymode)
            => new BmsKeymodeResolution(keymode, BmsKeymodeResolutionSource.Programmatic, BmsKeymodeEvidence.None, BmsKeymodeDiagnosticCode.ProgrammaticContext);

        public override string ToString() => StableDiagnostic;
    }

    /// <summary>
    /// Explicit correction supplied by a production decoder caller. This does not define a UI, sidecar or private BMS
    /// header; those remain separate product decisions. Contradictory lane evidence is still rejected fail-closed.
    /// </summary>
    public sealed class BmsBeatmapDecoderOptions
    {
        public BmsKeymode? KeymodeOverride { get; }

        public BmsBeatmapDecoderOptions(BmsKeymode? keymodeOverride = null)
        {
            if (keymodeOverride.HasValue && !Enum.IsDefined(keymodeOverride.Value))
                throw new ArgumentOutOfRangeException(nameof(keymodeOverride));

            KeymodeOverride = keymodeOverride;
        }
    }

    /// <summary>
    /// Fail-closed keymode rejection carrying only a stable, non-sensitive diagnostic.
    /// </summary>
    public sealed class BmsKeymodeResolutionException : FormatException
    {
        public BmsKeymodeDiagnosticCode DiagnosticCode { get; }

        public string StableDiagnostic { get; }

        internal BmsKeymodeResolutionException(BmsKeymodeDiagnosticCode diagnosticCode)
            : base(BmsKeymodeDiagnostics.GetStableDiagnostic(diagnosticCode))
        {
            DiagnosticCode = diagnosticCode;
            StableDiagnostic = BmsKeymodeDiagnostics.GetStableDiagnostic(diagnosticCode);
        }
    }

    internal static class BmsKeymodeDiagnostics
    {
        public static string GetStableDiagnostic(BmsKeymodeDiagnosticCode code)
            => code switch
            {
                BmsKeymodeDiagnosticCode.ProgrammaticContext => "bms.keymode.programmatic-context",
                BmsKeymodeDiagnosticCode.ExplicitOverrideApplied => "bms.keymode.explicit-override-applied",
                BmsKeymodeDiagnosticCode.PmsExtensionApplied => "bms.keymode.pms-extension-applied",
                BmsKeymodeDiagnosticCode.BmeExtensionApplied => "bms.keymode.bme-extension-applied",
                BmsKeymodeDiagnosticCode.Player2ChannelEvidenceApplied => "bms.keymode.player2-channel-evidence-applied",
                BmsKeymodeDiagnosticCode.NineKeyChannelEvidenceApplied => "bms.keymode.nine-key-channel-evidence-applied",
                BmsKeymodeDiagnosticCode.CompleteFiveKeyChannelSetApplied => "bms.keymode.complete-five-key-channel-set-applied",
                BmsKeymodeDiagnosticCode.CompleteSevenKeyChannelSetApplied => "bms.keymode.complete-seven-key-channel-set-applied",
                BmsKeymodeDiagnosticCode.CompleteNineKeyChannelSetApplied => "bms.keymode.complete-nine-key-channel-set-applied",
                BmsKeymodeDiagnosticCode.SparseBmsRequiresExplicitOverride => "bms.keymode.sparse-bms-requires-explicit-override",
                BmsKeymodeDiagnosticCode.NoLaneEvidenceRequiresExplicitOverride => "bms.keymode.no-lane-evidence-requires-explicit-override",
                BmsKeymodeDiagnosticCode.OverrideConflictsWithChannelEvidence => "bms.keymode.override-conflicts-with-channel-evidence",
                BmsKeymodeDiagnosticCode.ExtensionConflictsWithChannelEvidence => "bms.keymode.extension-conflicts-with-channel-evidence",
                _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unsupported BMS keymode diagnostic code."),
            };
    }
}
