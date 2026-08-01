// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Durable, bounded set of exact physical-node fingerprints owned by one managed delete.
    /// </summary>
    /// <remarks>
    /// The manifest deliberately stores a set rather than only a whole-tree hash so crash recovery can prove that a
    /// partially removed tombstone contains only surviving nodes from the original captured tree. Paths and native
    /// identities are folded into each versioned node fingerprint by the Windows authority.
    /// </remarks>
    internal static class SkinManagedFolderDeleteManifest
    {
        private const string version_prefix = "v1:";
        private const int fingerprint_characters = 64;

        // One package root plus every logical entry allowed by the immutable capsule contract.
        internal static int MaximumNodeCount => SkinPackageRevisionCapsuleLimits.Default.MaxEntryCount + 1;

        public static string Create(IEnumerable<string> nodeFingerprints)
        {
            ArgumentNullException.ThrowIfNull(nodeFingerprints);

            string[] fingerprints = nodeFingerprints
                                    .OrderBy(fingerprint => fingerprint, StringComparer.Ordinal)
                                    .ToArray();

            if (fingerprints.Length == 0
                || fingerprints.Length > MaximumNodeCount
                || fingerprints.Any(fingerprint =>
                    !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(fingerprint))
                || fingerprints.Distinct(StringComparer.Ordinal).Count() != fingerprints.Length)
            {
                throw new ArgumentException("The managed-folder delete manifest is invalid.", nameof(nodeFingerprints));
            }

            string manifest = version_prefix + string.Concat(fingerprints);

            if (!IsValid(manifest))
                throw new InvalidOperationException("The managed-folder delete manifest could not be canonicalised.");

            return manifest;
        }

        public static bool IsValid(string? manifest)
        {
            if (manifest == null
                || !manifest.StartsWith(version_prefix, StringComparison.Ordinal))
            {
                return false;
            }

            int payloadLength = manifest.Length - version_prefix.Length;

            if (payloadLength < fingerprint_characters
                || payloadLength % fingerprint_characters != 0
                || payloadLength / fingerprint_characters > MaximumNodeCount)
            {
                return false;
            }

            string? previous = null;

            for (int offset = version_prefix.Length;
                 offset < manifest.Length;
                 offset += fingerprint_characters)
            {
                string current = manifest.Substring(offset, fingerprint_characters);

                if (!SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(current)
                    || (previous != null
                        && string.CompareOrdinal(previous, current) >= 0))
                {
                    return false;
                }

                previous = current;
            }

            return true;
        }

        public static bool IsSubset(string? candidate, string? authority)
        {
            if (!IsValid(candidate) || !IsValid(authority))
                return false;

            int candidateOffset = version_prefix.Length;
            int authorityOffset = version_prefix.Length;

            while (candidateOffset < candidate!.Length
                   && authorityOffset < authority!.Length)
            {
                int comparison = string.CompareOrdinal(
                    candidate,
                    candidateOffset,
                    authority,
                    authorityOffset,
                    fingerprint_characters);

                if (comparison == 0)
                {
                    candidateOffset += fingerprint_characters;
                    authorityOffset += fingerprint_characters;
                }
                else if (comparison > 0)
                    authorityOffset += fingerprint_characters;
                else
                    return false;
            }

            return candidateOffset == candidate.Length;
        }
    }

    internal enum SkinManagedFolderDeleteFallbackDisposition
    {
        NotRequired = 1,
        ProtectedPairCommitted = 2,
    }
}
