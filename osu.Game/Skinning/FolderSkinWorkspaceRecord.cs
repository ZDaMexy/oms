// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A deliberately path-free row exposed by the folder-skin workspace.
    /// </summary>
    internal sealed class FolderSkinWorkspaceRecord
    {
        public Guid RecordId { get; }

        public string DisplayLabel { get; }

        public FolderSkinWorkspaceRecordKind Kind { get; }

        public bool CanOpenFolder { get; }

        public bool CanImportManagedCopy { get; }

        public bool CanUnregister { get; }

        public bool CanRename { get; }

        public bool CanDelete { get; }

        public FolderSkinWorkspaceRecord(
            Guid recordId,
            string displayLabel,
            FolderSkinWorkspaceRecordKind kind,
            bool canOpenFolder,
            bool canImportManagedCopy,
            bool canUnregister,
            bool canRename,
            bool canDelete)
        {
            if (recordId == Guid.Empty)
                throw new ArgumentException("A workspace row requires a committed record identifier.", nameof(recordId));

            if (string.IsNullOrWhiteSpace(displayLabel))
                throw new ArgumentException("A workspace row requires an immutable display label.", nameof(displayLabel));

            RecordId = recordId;
            DisplayLabel = displayLabel;
            Kind = kind;
            CanOpenFolder = canOpenFolder;
            CanImportManagedCopy = canImportManagedCopy;
            CanUnregister = canUnregister;
            CanRename = canRename;
            CanDelete = canDelete;
        }
    }

    internal enum FolderSkinWorkspaceRecordKind
    {
        External,
        Managed,
    }

    /// <summary>
    /// A redacted, immutable support projection. It intentionally contains no journal object,
    /// record identifier, operation identifier, physical identity, path, entry name or native exception text.
    /// </summary>
    internal sealed class FolderSkinJournalSupportSnapshot
    {
        public string Status { get; }

        public string Reason { get; }

        public string DiagnosticBundle { get; }

        public bool CanRetry { get; }

        public FolderSkinJournalSupportSnapshot(string status, string reason, string diagnosticBundle, bool canRetry)
        {
            Status = status ?? throw new ArgumentNullException(nameof(status));
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            DiagnosticBundle = diagnosticBundle ?? throw new ArgumentNullException(nameof(diagnosticBundle));
            CanRetry = canRetry;
        }
    }
}
