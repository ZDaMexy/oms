// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using JetBrains.Annotations;
using osu.Framework.IO.Stores;
using osu.Game.Extensions;
using osu.Game.IO;

namespace osu.Game.Skinning
{
    /// <summary>
    /// OMS built-in skin host backed by the current candidate baseline resources.
    /// </summary>
    public class OmsSkin : LegacySkin
    {
        public static SkinInfo CreateInfo() => new SkinInfo
        {
            ID = Skinning.SkinInfo.OMS_SKIN,
            Name = "OMS \"SimpleTou\" (preview)",
            Creator = "OMS contributors",
            Protected = true,
            InstantiationInfo = typeof(OmsSkin).GetInvariantInstantiationInfo()
        };

        public OmsSkin(IStorageResourceProvider resources)
            : this(CreateInfo(), resources)
        {
        }

        [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
        public OmsSkin(SkinInfo skin, IStorageResourceProvider resources)
            : base(
                skin,
                resources,
                new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(OmsSkin).Assembly), "Skins/Oms")
            )
        {
        }
    }
}
