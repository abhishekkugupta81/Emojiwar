using System.Collections.Generic;
using EmojiWar.Client.Content;
using UnityEngine;

namespace EmojiWar.Client.Core
{
    public static class LaunchSelections
    {
        private const string SelectedModeKey = "emojiwar.selected_mode";
        private const string DeckBuilderFlowKey = "emojiwar.deck_builder_flow";
        private const string PendingSquadKey = "emojiwar.pending_squad";
        private const string RankedResumeMatchIdKey = "emojiwar.ranked_resume_match_id";
        private const string RankedResumeDeckIdKey = "emojiwar.ranked_resume_deck_id";
        private const string RankedResumeSquadKey = "emojiwar.ranked_resume_squad";
        private const string RankedResumeRequestedKey = "emojiwar.ranked_resume_requested";

        public const string PvpRanked = "pvp_ranked";
        public const string BotPractice = "bot_practice";
        public const string BotSmart = "bot_smart";
        public const string EmojiClashLocal = "emoji_clash_local";
        public const string DeckBuilderFlowEdit = "edit";
        public const string DeckBuilderFlowRankedEntry = "ranked_entry";
        public const string DeckBuilderFlowBotPracticeEntry = "bot_practice_entry";
        public const string DeckBuilderFlowBotSmartEntry = "bot_smart_entry";

        public static void SetSelectedMode(string mode)
        {
            PlayerPrefs.SetString(SelectedModeKey, mode);
            PlayerPrefs.Save();
        }

        public static string GetSelectedMode()
        {
            return PlayerPrefs.GetString(SelectedModeKey, BotPractice);
        }

        public static void SetDeckBuilderFlow(string flow)
        {
            PlayerPrefs.SetString(DeckBuilderFlowKey, flow);
            PlayerPrefs.Save();
        }

        public static string GetDeckBuilderFlow()
        {
            return PlayerPrefs.GetString(DeckBuilderFlowKey, DeckBuilderFlowEdit);
        }

        public static bool IsRankedEntryDeckBuilderFlow()
        {
            return GetDeckBuilderFlow() == DeckBuilderFlowRankedEntry;
        }

        public static bool IsBotEntryDeckBuilderFlow()
        {
            var flow = GetDeckBuilderFlow();
            return flow == DeckBuilderFlowBotPracticeEntry || flow == DeckBuilderFlowBotSmartEntry;
        }

        public static void BeginRankedMatchSelection()
        {
            SetSelectedMode(PvpRanked);
            SetDeckBuilderFlow(DeckBuilderFlowRankedEntry);
            ClearPendingSquad();
            ClearRankedResumeRequested();
        }

        public static void BeginRankedResume()
        {
            SetSelectedMode(PvpRanked);
            PlayerPrefs.SetInt(RankedResumeRequestedKey, 1);
            PlayerPrefs.Save();
        }

        public static void BeginBotMatchSelection(string mode)
        {
            SetSelectedMode(mode == BotSmart ? BotSmart : BotPractice);
            SetDeckBuilderFlow(mode == BotSmart ? DeckBuilderFlowBotSmartEntry : DeckBuilderFlowBotPracticeEntry);
        }

        public static void BeginEmojiClash()
        {
            SetSelectedMode(EmojiClashLocal);
            SetDeckBuilderFlow(DeckBuilderFlowEdit);
            ClearPendingSquad();
            ClearRankedResume();
        }

        public static void BeginDeckEdit()
        {
            SetDeckBuilderFlow(DeckBuilderFlowEdit);
            ClearRankedResumeRequested();
        }

        public static void SetPendingSquad(IReadOnlyList<EmojiId> emojiIds)
        {
            var encoded = emojiIds == null || emojiIds.Count == 0
                ? string.Empty
                : string.Join(",", EmojiIdUtility.ToApiIds(emojiIds));
            PlayerPrefs.SetString(PendingSquadKey, encoded);
            PlayerPrefs.Save();
        }

        public static IReadOnlyList<EmojiId> GetPendingSquad()
        {
            var encoded = PlayerPrefs.GetString(PendingSquadKey, string.Empty);
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return new List<EmojiId>();
            }

            return EmojiIdUtility.ParseApiIds(encoded.Split(','));
        }

        public static void ClearPendingSquad()
        {
            PlayerPrefs.DeleteKey(PendingSquadKey);
            PlayerPrefs.Save();
        }

        public static void StoreRankedResume(string matchId, string deckId, IReadOnlyList<string> playerDeckApiIds)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                return;
            }

            PlayerPrefs.SetString(RankedResumeMatchIdKey, matchId);
            PlayerPrefs.SetString(RankedResumeDeckIdKey, deckId ?? string.Empty);
            PlayerPrefs.SetString(
                RankedResumeSquadKey,
                playerDeckApiIds == null || playerDeckApiIds.Count == 0
                    ? string.Empty
                    : string.Join(",", playerDeckApiIds));
            PlayerPrefs.Save();
        }

        public static bool HasRankedResume()
        {
            return !string.IsNullOrWhiteSpace(PlayerPrefs.GetString(RankedResumeMatchIdKey, string.Empty));
        }

        public static bool ShouldResumeRankedMatch()
        {
            return PlayerPrefs.GetInt(RankedResumeRequestedKey, 0) == 1 && HasRankedResume();
        }

        public static string GetRankedResumeMatchId()
        {
            return PlayerPrefs.GetString(RankedResumeMatchIdKey, string.Empty);
        }

        public static string GetRankedResumeDeckId()
        {
            return PlayerPrefs.GetString(RankedResumeDeckIdKey, string.Empty);
        }

        public static IReadOnlyList<EmojiId> GetRankedResumeSquad()
        {
            var encoded = PlayerPrefs.GetString(RankedResumeSquadKey, string.Empty);
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return new List<EmojiId>();
            }

            return EmojiIdUtility.ParseApiIds(encoded.Split(','));
        }

        public static void ClearRankedResumeRequested()
        {
            PlayerPrefs.DeleteKey(RankedResumeRequestedKey);
            PlayerPrefs.Save();
        }

        public static void ClearRankedResume()
        {
            PlayerPrefs.DeleteKey(RankedResumeMatchIdKey);
            PlayerPrefs.DeleteKey(RankedResumeDeckIdKey);
            PlayerPrefs.DeleteKey(RankedResumeSquadKey);
            PlayerPrefs.DeleteKey(RankedResumeRequestedKey);
            PlayerPrefs.Save();
        }
    }
}
