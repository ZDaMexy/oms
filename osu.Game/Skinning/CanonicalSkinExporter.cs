// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Skinning
{
    /// <summary>Exports the verified ordinary archive through the normal skin export UI and destination.</summary>
    internal sealed class CanonicalSkinExporter : LegacySkinExporter
    {
        private readonly Skin[] builtIns;

        public CanonicalSkinExporter(Storage storage, params Skin[] builtIns)
            : base(storage)
        {
            this.builtIns = builtIns;
        }

        public override void ExportToStream(SkinInfo model, Stream outputStream, ProgressNotification? notification, CancellationToken cancellationToken = default)
        {
            Skin? canonical = builtIns.FirstOrDefault(skin => skin.SkinInfo.ID == model.ID);
            if (canonical != null && canonical.SkinInfo.PerformRead(info => SkinManagedFolderDeleteOperation.IsExactProtectedRecord(model, info)))
            {
                CanonicalSkinPackage.Export(canonical, outputStream, cancellationToken);
                return;
            }
            base.ExportToStream(model, outputStream, notification, cancellationToken);
        }
    }
}
