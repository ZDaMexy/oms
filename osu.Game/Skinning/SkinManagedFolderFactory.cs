// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Reflection;
using osu.Framework.IO.Stores;
using osu.Game.IO;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Stable, non-sensitive reasons why a captured managed folder could not become a skin instance.
    /// </summary>
    internal enum SkinManagedFolderFactoryRejectionReason
    {
        None,
        InstantiationInfoNotAllowed,
        AllowedTypeUnavailable,
        AllowedConstructorUnavailable,
        RequiredConfigurationMissing,
        InstantiationFailed,
    }

    internal sealed class SkinManagedFolderFactoryResult
    {
        public SkinManagedFolderFactoryRejectionReason RejectionReason { get; }

        public Skin? Skin { get; }

        public bool IsSuccess => Skin != null;

        private SkinManagedFolderFactoryResult(SkinManagedFolderFactoryRejectionReason rejectionReason, Skin? skin)
        {
            RejectionReason = rejectionReason;
            Skin = skin;
        }

        public static SkinManagedFolderFactoryResult Success(Skin skin)
            => new SkinManagedFolderFactoryResult(SkinManagedFolderFactoryRejectionReason.None, skin ?? throw new ArgumentNullException(nameof(skin)));

        public static SkinManagedFolderFactoryResult Reject(SkinManagedFolderFactoryRejectionReason reason)
        {
            if (!Enum.IsDefined(reason) || reason == SkinManagedFolderFactoryRejectionReason.None)
                throw new ArgumentOutOfRangeException(nameof(reason));

            return new SkinManagedFolderFactoryResult(reason, null);
        }

        public override string ToString() => $"{nameof(SkinManagedFolderFactoryResult)}:{RejectionReason}";
    }

    /// <summary>
    /// Closed factory for the first managed-folder skin type.
    /// </summary>
    /// <remarks>
    /// The record never chooses a CLR type. It must match the one canonical allowlisted string, after which this factory
    /// resolves that same constant and requires its exact public managed-folder constructor.
    /// </remarks>
    internal static class SkinManagedFolderFactory
    {
        internal const string ALLOWED_INSTANTIATION_INFO =
            "osu.Game.Rulesets.Bms.Skinning.BmsLegacySkin, osu.Game.Rulesets.Bms";

        public static bool IsInstantiationInfoAllowed(string? instantiationInfo)
            => string.Equals(instantiationInfo, ALLOWED_INSTANTIATION_INFO, StringComparison.Ordinal);

        /// <summary>
        /// Transfers ownership of <paramref name="capsule"/> on every return path.
        /// </summary>
        public static SkinManagedFolderFactoryResult Create(
            SkinInfo skinInfoSnapshot,
            IStorageResourceProvider resources,
            SkinPackageRevisionCapsule capsule)
        {
            ArgumentNullException.ThrowIfNull(skinInfoSnapshot);
            ArgumentNullException.ThrowIfNull(resources);
            ArgumentNullException.ThrowIfNull(capsule);

            if (!IsInstantiationInfoAllowed(skinInfoSnapshot.InstantiationInfo))
            {
                capsule.Dispose();
                return SkinManagedFolderFactoryResult.Reject(SkinManagedFolderFactoryRejectionReason.InstantiationInfoNotAllowed);
            }

            if (!capsule.Files.Any(file => string.Equals(file.ResourceName, "skin.ini", StringComparison.OrdinalIgnoreCase)))
            {
                capsule.Dispose();
                return SkinManagedFolderFactoryResult.Reject(SkinManagedFolderFactoryRejectionReason.RequiredConfigurationMissing);
            }

            Type? allowedType = Type.GetType(ALLOWED_INSTANTIATION_INFO, throwOnError: false, ignoreCase: false);

            if (allowedType == null
                || !string.Equals(allowedType.FullName, "osu.Game.Rulesets.Bms.Skinning.BmsLegacySkin", StringComparison.Ordinal)
                || !string.Equals(allowedType.Assembly.GetName().Name, "osu.Game.Rulesets.Bms", StringComparison.Ordinal)
                || !typeof(Skin).IsAssignableFrom(allowedType))
            {
                capsule.Dispose();
                return SkinManagedFolderFactoryResult.Reject(SkinManagedFolderFactoryRejectionReason.AllowedTypeUnavailable);
            }

            ConstructorInfo? constructor = allowedType.GetConstructor(
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                new[] { typeof(SkinInfo), typeof(IStorageResourceProvider), typeof(IResourceStore<byte[]>), typeof(bool) },
                modifiers: null);

            if (constructor == null)
            {
                capsule.Dispose();
                return SkinManagedFolderFactoryResult.Reject(SkinManagedFolderFactoryRejectionReason.AllowedConstructorUnavailable);
            }

            var owningStore = new SkinPackageRevisionResourceStore(capsule);

            try
            {
                object? instance = constructor.Invoke(new object[] { skinInfoSnapshot, resources, owningStore, true });

                if (instance is not Skin skin || instance.GetType() != allowedType)
                {
                    (instance as IDisposable)?.Dispose();
                    owningStore.Dispose();
                    return SkinManagedFolderFactoryResult.Reject(SkinManagedFolderFactoryRejectionReason.InstantiationFailed);
                }

                return SkinManagedFolderFactoryResult.Success(skin);
            }
            catch
            {
                owningStore.Dispose();
                return SkinManagedFolderFactoryResult.Reject(SkinManagedFolderFactoryRejectionReason.InstantiationFailed);
            }
        }
    }
}
