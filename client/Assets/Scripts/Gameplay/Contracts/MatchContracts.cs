using System;
using System.Collections.Generic;
using EmojiWar.Client.Content;

namespace EmojiWar.Client.Gameplay.Contracts
{
    public enum MatchMode
    {
        PvpRanked,
        BotPractice,
        BotSmart,
        EmojiClashPvp
    }

    [Serializable]
    public sealed class DeckDto
    {
        public string DeckId = string.Empty;
        public string UserId = string.Empty;
        public List<EmojiId> EmojiIds = new();
        public bool IsActive;
        public string UpdatedAt = string.Empty;
    }

    [Serializable]
    public sealed class BotProfileDto
    {
        public string id = string.Empty;
        public string difficulty = string.Empty;
        public float aggression;
        public float defenseBias;
        public float comboBias;
    }

    [Serializable]
    public sealed class FormationPlacementDto
    {
        public string slot = string.Empty;
        public string emojiId = string.Empty;
    }

    [Serializable]
    public sealed class FormationDto
    {
        public FormationPlacementDto[] placements = Array.Empty<FormationPlacementDto>();
    }

    [Serializable]
    public sealed class BattleUnitStateDto
    {
        public string unitId = string.Empty;
        public string team = string.Empty;
        public string emojiId = string.Empty;
        public string slot = string.Empty;
        public int hp;
        public int maxHp;
        public int attack;
        public int speed;
        public bool alive;
        public int shield;
        public int growth;
        public int burn;
        public int poison;
        public int stun;
        public int freeze;
        public int bind;
        public int disrupt;
        public bool firstHitAvailable;
        public bool mirrorArmed;
    }

    [Serializable]
    public sealed class BattleEventDto
    {
        public int cycle;
        public string type = string.Empty;
        public string actor = string.Empty;
        public string target = string.Empty;
        public string reasonCode = string.Empty;
        public string caption = string.Empty;
    }

    [Serializable]
    public sealed class BattleStateDto
    {
        public int cycle;
        public BattleUnitStateDto[] teamA = Array.Empty<BattleUnitStateDto>();
        public BattleUnitStateDto[] teamB = Array.Empty<BattleUnitStateDto>();
        public BattleEventDto[] eventLog = Array.Empty<BattleEventDto>();
        public string winner = string.Empty;
        public string whySummary = string.Empty;
        public string[] whyChain = Array.Empty<string>();
    }

    [Serializable]
    public sealed class StartBotMatchRequestDto
    {
        public string mode = string.Empty;
        public string activeDeckId = string.Empty;
        public string[] playerDeck = Array.Empty<string>();
    }

    [Serializable]
    public sealed class StartBotMatchResponseDto
    {
        public string matchId = string.Empty;
        public string mode = string.Empty;
        public string playerDeckId = string.Empty;
        public BotProfileDto botProfile = new();
        public string[] playerDeck = Array.Empty<string>();
        public string[] botDeck = Array.Empty<string>();
        public string[] playerTeam = Array.Empty<string>();
        public string[] botTeam = Array.Empty<string>();
        public string benchEmojiId = string.Empty;
        public string suggestedBan = string.Empty;
        public string playerBannedEmojiId = string.Empty;
        public string opponentBannedEmojiId = string.Empty;
        public string[] playerFinalTeam = Array.Empty<string>();
        public string[] opponentFinalTeam = Array.Empty<string>();
        public string rulesVersion = string.Empty;
        public string status = string.Empty;
        public string phase = string.Empty;
        public FormationDto playerFormation = new();
        public FormationDto botFormation = new();
        public BattleStateDto battleState = new();
        public string winner = string.Empty;
        public string whySummary = string.Empty;
        public string[] whyChain = Array.Empty<string>();
        public string note = string.Empty;
    }

    [Serializable]
    public sealed class QueueOrJoinMatchRequestDto
    {
        public string userId = string.Empty;
        public string deckId = string.Empty;
        public string[] playerDeck = Array.Empty<string>();
        public string matchId = string.Empty;
        public bool forceFreshEntry;
    }

    [Serializable]
    public sealed class QueueOrJoinMatchResponseDto
    {
        public string status = string.Empty;
        public string queueTicket = string.Empty;
        public string userId = string.Empty;
        public string deckId = string.Empty;
        public int estimatedWaitSeconds;
        public string phaseDeadlineAt = string.Empty;
        public int phaseTimeoutSecondsRemaining;
        public string note = string.Empty;
        public string matchId = string.Empty;
        public string opponentUserId = string.Empty;
        public string rulesVersion = string.Empty;
        public string playerSide = string.Empty;
        public string phase = string.Empty;
        public string[] playerDeck = Array.Empty<string>();
        public string[] opponentDeck = Array.Empty<string>();
        public string playerBannedEmojiId = string.Empty;
        public string opponentBannedEmojiId = string.Empty;
        public string[] playerFinalTeam = Array.Empty<string>();
        public string[] opponentFinalTeam = Array.Empty<string>();
        public FormationDto playerFormation = new();
        public FormationDto opponentFormation = new();
        public BattleStateDto battleState = new();
        public string winner = string.Empty;
        public string whySummary = string.Empty;
        public string[] whyChain = Array.Empty<string>();
    }

    [Serializable]
    public sealed class SubmitBanRequestDto
    {
        public string matchId = string.Empty;
        public string playerId = string.Empty;
        public string bannedEmojiId = string.Empty;
    }

    [Serializable]
    public sealed class SubmitBanResponseDto
    {
        public bool accepted;
        public string matchId = string.Empty;
        public string playerId = string.Empty;
        public string bannedEmojiId = string.Empty;
        public string status = string.Empty;
        public string phase = string.Empty;
        public string note = string.Empty;
        public string playerBannedEmojiId = string.Empty;
        public string opponentBannedEmojiId = string.Empty;
        public string[] playerFinalTeam = Array.Empty<string>();
        public string[] opponentFinalTeam = Array.Empty<string>();
        public FormationDto playerFormation = new();
        public FormationDto opponentFormation = new();
    }

    [Serializable]
    public sealed class SubmitFormationRequestDto
    {
        public string matchId = string.Empty;
        public string playerId = string.Empty;
        public FormationDto formation = new();
    }

    [Serializable]
    public sealed class SubmitFormationResponseDto
    {
        public bool accepted;
        public string matchId = string.Empty;
        public string playerId = string.Empty;
        public string status = string.Empty;
        public string phase = string.Empty;
        public string note = string.Empty;
        public string[] playerFinalTeam = Array.Empty<string>();
        public string[] opponentFinalTeam = Array.Empty<string>();
        public FormationDto playerFormation = new();
        public FormationDto opponentFormation = new();
        public BattleStateDto battleState = new();
        public string winner = string.Empty;
        public string whySummary = string.Empty;
        public string[] whyChain = Array.Empty<string>();
    }

    [Serializable]
    public sealed class CancelRankedQueueRequestDto
    {
        public string userId = string.Empty;
        public string matchId = string.Empty;
    }

    [Serializable]
    public sealed class CancelRankedQueueResponseDto
    {
        public bool cancelled;
        public string matchId = string.Empty;
        public string status = string.Empty;
        public string note = string.Empty;
    }

    [Serializable]
    public sealed class QueueOrJoinClashRequestDto
    {
        public string userId = string.Empty;
        public string matchId = string.Empty;
        public bool forceFreshEntry;
    }

    [Serializable]
    public sealed class QueueOrJoinClashResponseDto
    {
        public string status = string.Empty;
        public string mode = string.Empty;
        public string serverNow = string.Empty;
        public string queueTicket = string.Empty;
        public string matchId = string.Empty;
        public string userId = string.Empty;
        public string opponentUserId = string.Empty;
        public string opponent_type = string.Empty;
        public string bot_profile_id = string.Empty;
        public string bot_fill_reason = string.Empty;
        public string display_name_resolved = string.Empty;
        public string avatar_key_resolved = string.Empty;
        public string playerSide = string.Empty;
        public string rulesVersion = string.Empty;
        public string phase = string.Empty;
        public int matchmaking_rating_at_queue;
        public string queueStartedAt = string.Empty;
        public string queueExpiresAt = string.Empty;
        public int currentTurnIndex;
        public int totalTurns;
        public int[] turnValues = Array.Empty<int>();
        public string turnDeadlineAt = string.Empty;
        public int phaseTimeoutSecondsRemaining;
        public int playerScore;
        public int opponentScore;
        public string[] playerUsedUnits = Array.Empty<string>();
        public string[] opponentUsedUnits = Array.Empty<string>();
        public int timeoutStrikesPlayer;
        public int timeoutStrikesOpponent;
        public ClashTurnRecordDto[] resolvedTurnHistory = Array.Empty<ClashTurnRecordDto>();
        public bool playerPickLocked;
        public bool opponentPickLocked;
        public string winner = string.Empty;
        public string finalOutcome = string.Empty;
        public string finishReason = string.Empty;
        public string note = string.Empty;
    }

    [Serializable]
    public sealed class ClashTurnRecordDto
    {
        public int turnNumber;
        public int turnValue;
        public string playerUnitKey = string.Empty;
        public string opponentUnitKey = string.Empty;
        public bool playerTimedOut;
        public bool opponentTimedOut;
        public string playerTimeoutBurnUnitKey = string.Empty;
        public string opponentTimeoutBurnUnitKey = string.Empty;
        public int playerCombatPower;
        public int opponentCombatPower;
        public string outcome = string.Empty;
        public int playerScoreAfter;
        public int opponentScoreAfter;
        public string reason = string.Empty;
    }

    [Serializable]
    public sealed class SubmitClashPickRequestDto
    {
        public string matchId = string.Empty;
        public string playerId = string.Empty;
        public int turnNumber;
        public string emojiId = string.Empty;
    }

    [Serializable]
    public sealed class SubmitClashPickResponseDto
    {
        public bool accepted;
        public string matchId = string.Empty;
        public string playerId = string.Empty;
        public int turnNumber;
        public string emojiId = string.Empty;
        public string status = string.Empty;
        public string phase = string.Empty;
        public bool opponentReady;
        public string note = string.Empty;
    }

    [Serializable]
    public sealed class CancelClashQueueRequestDto
    {
        public string userId = string.Empty;
        public string matchId = string.Empty;
    }

    [Serializable]
    public sealed class CancelClashQueueResponseDto
    {
        public bool cancelled;
        public string matchId = string.Empty;
        public string status = string.Empty;
        public string note = string.Empty;
    }

    // Legacy duel-era compatibility types retained so older local preview helpers still compile
    // while the launch flow moves to 5v5 auto-battle.

    [Serializable]
    public sealed class SideStateDto
    {
        public int Burn;
        public int Poison;
        public int Growth;
        public bool Cleanse;
        public int ShieldCharge;
        public List<string> DelayedEffects = new();
    }

    [Serializable]
    public sealed class EffectLogEntryDto
    {
        public string Actor = string.Empty;
        public string Target = string.Empty;
        public string EffectType = string.Empty;
        public string Detail = string.Empty;
    }

    [Serializable]
    public sealed class ReplayEventDto
    {
        public string EventType = string.Empty;
        public string Source = string.Empty;
        public string Target = string.Empty;
        public string Caption = string.Empty;
    }

    [Serializable]
    public sealed class MatchStateDto
    {
        public string MatchId = string.Empty;
        public MatchMode Mode;
        public string RulesVersion = string.Empty;
        public int RoundNumber;
        public string Status = string.Empty;
        public int ScoreA;
        public int ScoreB;
        public List<EmojiId> RemainingA = new();
        public List<EmojiId> RemainingB = new();
        public SideStateDto SideStateA = new();
        public SideStateDto SideStateB = new();
        public List<EmojiId> BansA = new();
        public List<EmojiId> BansB = new();
        public string WinnerId = string.Empty;
    }

    [Serializable]
    public sealed class RoundResolveResultDto
    {
        public string Winner = string.Empty;
        public bool IsDraw;
        public string ReasonCode = string.Empty;
        public string WhyText = string.Empty;
        public List<string> WhyChain = new();
        public List<EffectLogEntryDto> EffectLog = new();
        public List<ReplayEventDto> ReplayEvents = new();
        public MatchStateDto NextState = new();
    }

    [Serializable]
    public sealed class SubmitPickRequestDto
    {
        public string matchId = string.Empty;
        public int roundNumber;
        public string playerId = string.Empty;
        public string emojiId = string.Empty;
    }

    [Serializable]
    public sealed class SubmitPickResponseDto
    {
        public bool accepted;
        public string matchId = string.Empty;
        public int roundNumber;
        public string playerId = string.Empty;
        public string emojiId = string.Empty;
        public string status = string.Empty;
        public bool opponentReady;
    }

    [Serializable]
    public sealed class ResolveSideStateDto
    {
        public int burn;
        public int poison;
        public int growth;
        public bool cleanse;
        public int shieldCharge;
        public string[] delayedEffects = Array.Empty<string>();
    }

    [Serializable]
    public sealed class ResolveRoundRequestDto
    {
        public string matchId = string.Empty;
        public string rulesVersion = string.Empty;
        public int roundNumber;
        public string playerAPick = string.Empty;
        public string playerBPick = string.Empty;
        public ResolveSideStateDto sideStateA = new();
        public ResolveSideStateDto sideStateB = new();
    }

    [Serializable]
    public sealed class ResolveRoundServiceResponseDto
    {
        public string winner = string.Empty;
        public bool isDraw;
        public string reasonCode = string.Empty;
        public string whyText = string.Empty;
        public string[] whyChain = Array.Empty<string>();
        public string resolvedPlayerAPick = string.Empty;
        public string resolvedPlayerBPick = string.Empty;
        public ResolveSideStateDto nextSideStateA = new();
        public ResolveSideStateDto nextSideStateB = new();
    }
}
