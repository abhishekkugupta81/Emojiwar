using System;

namespace EmojiWar.Client.Gameplay.Contracts
{
    [Serializable]
    public sealed class CodexUnlockEntryDto
    {
        public string interaction_key = string.Empty;
        public string reason_code = string.Empty;
        public string summary = string.Empty;
        public string tip = string.Empty;
        public string unlocked_at = string.Empty;
    }

    [Serializable]
    public sealed class GetCodexRequestDto
    {
        public int limit = 50;
    }

    [Serializable]
    public sealed class GetCodexResponseDto
    {
        public CodexUnlockEntryDto[] entries = Array.Empty<CodexUnlockEntryDto>();
        public int totalUnlocked;
        public CodexUnlockEntryDto latestEntry;
        public string note = string.Empty;
    }

    [Serializable]
    public sealed class LeaderboardEntryDto
    {
        public int rank;
        public string userId = string.Empty;
        public string user_id = string.Empty;
        public string displayName = string.Empty;
        public string display_handle = string.Empty;
        public string ladder_mode = string.Empty;
        public int currentElo;
        public int rating;
        public int hidden_mmr;
        public int hiddenMmr;
        public int visible_points;
        public int visiblePoints;
        public int games_played;
        public int wins;
        public int losses;
        public int draws;
        public int timeout_forfeits;
        public int matches_vs_humans;
        public int matches_vs_bots;
        public int bot_fill_wins;
        public int bot_fill_losses;
        public int bot_fill_points_earned;
        public string updated_at = string.Empty;
        public bool isCurrentUser;
    }

    [Serializable]
    public sealed class GetLeaderboardRequestDto
    {
        public int limit = 25;
    }

    [Serializable]
    public sealed class GetLeaderboardResponseDto
    {
        public string ladder_mode = string.Empty;
        public LeaderboardEntryDto[] entries = Array.Empty<LeaderboardEntryDto>();
        public LeaderboardEntryDto[] nearbyEntries = Array.Empty<LeaderboardEntryDto>();
        public LeaderboardEntryDto myEntry;
        public int totalRatedPlayers;
        public string note = string.Empty;
    }
}
