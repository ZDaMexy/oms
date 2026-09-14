// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Skinning
{
    /// <summary>
    /// One product authority for legacy skin-authoring entry points while they remain outside the protected revision protocol.
    /// </summary>
    internal static class SkinAuthoringAvailability
    {
        internal const string UPDATE_IMPORT_DISABLED_DIAGNOSTIC =
            "Skin update-import is disabled until it can publish through the current revision protocol.";

        internal const string EXTERNAL_EDITING_DISABLED_DIAGNOSTIC =
            "Skin external editing is disabled until update-import can publish through the current revision protocol.";

        /// <summary>
        /// The layout editor saves an independent draft and applies it through ordinary revision publication.
        /// </summary>
        internal static bool LegacyEditorAvailable => true;
    }
}
