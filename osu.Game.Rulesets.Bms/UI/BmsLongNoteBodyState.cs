// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Visual lifecycle state of a long-note body, surfaced by <see cref="DrawableBmsHoldNote"/> so skins can react
    /// to gameplay without reaching into judgement internals. Derived purely from the live hold state, so an HCN
    /// re-grab transitions <see cref="Broken"/> back to <see cref="Holding"/> on its own (LN/CN cannot re-grab).
    /// </summary>
    public enum BmsLongNoteBodyState
    {
        /// <summary>
        /// Head not yet hit — the note is still approaching (unactivated).
        /// </summary>
        Idle,

        /// <summary>
        /// Head hit and currently held (activated), or held cleanly through to the tail.
        /// </summary>
        Holding,

        /// <summary>
        /// Head missed, or the hold was released before completion (missed / broken).
        /// </summary>
        Broken,
    }
}
