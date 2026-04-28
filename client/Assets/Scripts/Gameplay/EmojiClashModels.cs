using System;
using System.Collections.Generic;

namespace EmojiWar.Client.Gameplay.Clash
{
    [Serializable]
    public enum EmojiClashTurnOutcome
    {
        PlayerWin,
        OpponentWin,
        Draw
    }

    [Serializable]
    public sealed class EmojiClashProfile
    {
        public string UnitKey = string.Empty;
        public string DisplayName = string.Empty;
        public int BasePower;
        public string Role = string.Empty;
        public string[] Tags = Array.Empty<string>();
        public string[] CounterTags = Array.Empty<string>();
        public int EarlyBonus;
        public int LateBonus;
        public int FinalTurnBonus;
        public int BehindBonus;
    }

    [Serializable]
    public sealed class EmojiClashPowerBreakdown
    {
        public string UnitKey = string.Empty;
        public int BasePower;
        public int MatchupBonus;
        public int TimingBonus;
        public int BehindBonus;
        public int TotalPower;
        public string PrimaryReason = string.Empty;
    }

    [Serializable]
    public sealed class EmojiClashTurnRecord
    {
        public int TurnNumber;
        public int TurnValue;
        public string PlayerUnitKey = string.Empty;
        public string OpponentUnitKey = string.Empty;
        public int PlayerCombatPower;
        public int OpponentCombatPower;
        public EmojiClashTurnOutcome Outcome;
        public int PlayerScoreAfter;
        public int OpponentScoreAfter;
        public string PlayerFacingReason = string.Empty;
    }

    [Serializable]
    public sealed class EmojiClashResolvedTurn
    {
        public int TurnNumber;
        public int TurnValue;
        public EmojiClashTurnOutcome Outcome;
        public string PlayerUnitKey = string.Empty;
        public string OpponentUnitKey = string.Empty;
        public EmojiClashPowerBreakdown PlayerBreakdown = new();
        public EmojiClashPowerBreakdown OpponentBreakdown = new();
        public string PlayerFacingReason = string.Empty;
    }

    [Serializable]
    public sealed class EmojiClashMatchState
    {
        public int CurrentTurnIndex;
        public int PlayerScore;
        public int OpponentScore;
        public int MatchSeed;
        public HashSet<string> PlayerUsedUnitKeys = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> OpponentUsedUnitKeys = new(StringComparer.OrdinalIgnoreCase);
        public string PendingPlayerPick = string.Empty;
        public string PendingOpponentPick = string.Empty;
        public List<EmojiClashTurnRecord> TurnHistory = new();

        public bool IsComplete => CurrentTurnIndex >= EmojiClashRules.TotalTurns;
    }
}
