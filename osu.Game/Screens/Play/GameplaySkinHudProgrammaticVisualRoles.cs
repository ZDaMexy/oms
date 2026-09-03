// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Skinning.Gameplay;
using osu.Game.Screens.Play;

namespace osu.Game.Screens.Play
{
    /// <summary>
    /// A compile-time, allowlisted HUD role supplied by the real engine owner. The runtime adapter reads this token;
    /// it never reflects over, serialises or guesses a drawable type.
    /// </summary>
    internal interface IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole GameplaySkinHudRole { get; }
    }
}

namespace osu.Game.Screens.Play.HUD
{
    public abstract partial class HealthDisplay : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Gauge;
    }

    public abstract partial class ComboCounter : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Combo;
    }

    public abstract partial class GameplayScoreCounter : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public abstract partial class GameplayAccuracyCounter : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public abstract partial class SongProgress : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public abstract partial class PerformancePointsCounter : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public abstract partial class KeyCounterDisplay : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public partial class BPMCounter : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public abstract partial class UnstableRateCounter : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public partial class DefaultRankDisplay : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public partial class DrawableGameplayLeaderboard : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public partial class SpectatorList : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public partial class PlayerAvatar : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public partial class PlayerFlag : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public partial class PlayerTeamFlag : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public partial class ArgonWedgePiece : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Decoration;
    }
}

namespace osu.Game.Screens.Play.HUD.ClicksPerSecond
{
    public partial class ClicksPerSecondCounter : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }
}

namespace osu.Game.Screens.Play.HUD.HitErrorMeters
{
    public abstract partial class HitErrorMeter : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Judgement;
    }
}

namespace osu.Game.Screens.Play.HUD.JudgementCounter
{
    public partial class JudgementCounterDisplay : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }
}

namespace osu.Game.Skinning
{
    public partial class LegacyRankDisplay : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }
}

namespace osu.Game.Skinning.Components
{
    public partial class ArgonJudgementCounterDisplay : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public partial class PlayerName : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Text;
    }

    public partial class BoxElement : IGameplaySkinHudProgrammaticVisualSource
    {
        GameplaySkinPreparedHudRole IGameplaySkinHudProgrammaticVisualSource.GameplaySkinHudRole => GameplaySkinPreparedHudRole.Decoration;
    }
}
