// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using System.Threading;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Skinning
{
    /// <summary>Exports the verified ordinary archive through the normal skin export UI and destination.</summary>
    internal sealed class CanonicalSkinExporter : LegacySkinExporter
    {
        private readonly Skin canonical;

        public CanonicalSkinExporter(Storage storage, Skin canonical)
            : base(storage)
        {
            this.canonical = canonical;
        }

        public override void ExportToStream(SkinInfo model, Stream outputStream, ProgressNotification? notification, CancellationToken cancellationToken = default)
        {
            if (model.ID == canonical.SkinInfo.ID && SkinManagedFolderDeleteOperation.IsExactProtectedFallbackRecord(model))
            {
                CanonicalSkinPackage.Export(canonical, outputStream, cancellationToken);
                return;
            }
            base.ExportToStream(model, outputStream, notification, cancellationToken);
        }
    }
}
