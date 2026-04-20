using System;
using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Content;
using UnityEngine;

namespace EmojiWar.Client.Core.Decks
{
    public sealed class ActiveDeckService
    {
        private const string StorageKey = "emojiwar.active_deck";

        private static readonly EmojiId[] StarterDeck =
        {
            EmojiId.Fire,
            EmojiId.Water,
            EmojiId.Lightning,
            EmojiId.Shield,
            EmojiId.Heart,
            EmojiId.Wind
        };

        private ActiveDeckSaveData saveData = new();
        private readonly List<EmojiId> activeDeckEmojiIds = new();

        public string DeckId => saveData.deckId;
        public string UserId => saveData.userId;
        public bool HasActiveDeck => activeDeckEmojiIds.Count == 6;
        public bool ShouldShowStarterPrompt => HasActiveDeck && !saveData.hasSeenStarterPrompt;
        public IReadOnlyList<EmojiId> ActiveDeckEmojiIds => activeDeckEmojiIds;
        public string UpdatedAt => saveData.updatedAt;

        public void EnsureInitialized(string userId)
        {
            Load();

            var hasChanges = false;

            if (!string.Equals(saveData.userId, userId, StringComparison.Ordinal))
            {
                saveData.userId = userId;
                hasChanges = true;
            }

            if (!TryApplyDeck(saveData.emojiIds))
            {
                CreateStarterDeck();
                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(saveData.deckId))
            {
                saveData.deckId = Guid.NewGuid().ToString("N");
                hasChanges = true;
            }

            if (string.IsNullOrWhiteSpace(saveData.updatedAt))
            {
                saveData.updatedAt = DateTime.UtcNow.ToString("O");
                hasChanges = true;
            }

            if (hasChanges)
            {
                Save();
            }
        }

        public bool TrySaveActiveDeck(IReadOnlyList<EmojiId> emojiIds, out string validationError)
        {
            if (!ValidateDeck(emojiIds, out validationError))
            {
                return false;
            }

            activeDeckEmojiIds.Clear();
            activeDeckEmojiIds.AddRange(emojiIds);

            saveData.emojiIds = activeDeckEmojiIds.Select(emojiId => (int)emojiId).ToArray();
            saveData.updatedAt = DateTime.UtcNow.ToString("O");

            if (string.IsNullOrWhiteSpace(saveData.deckId))
            {
                saveData.deckId = Guid.NewGuid().ToString("N");
            }

            Save();
            return true;
        }

        public bool TryHydrateFromRemoteDeck(string deckId, string userId, IReadOnlyList<EmojiId> emojiIds, string updatedAt)
        {
            if (!ValidateDeck(emojiIds, out _))
            {
                return false;
            }

            activeDeckEmojiIds.Clear();
            activeDeckEmojiIds.AddRange(emojiIds);

            saveData.deckId = deckId;
            saveData.userId = userId;
            saveData.emojiIds = activeDeckEmojiIds.Select(emojiId => (int)emojiId).ToArray();
            saveData.updatedAt = string.IsNullOrWhiteSpace(updatedAt)
                ? DateTime.UtcNow.ToString("O")
                : updatedAt;
            Save();
            return true;
        }

        public void MarkStarterPromptSeen()
        {
            if (saveData.hasSeenStarterPrompt)
            {
                return;
            }

            saveData.hasSeenStarterPrompt = true;
            Save();
        }

        public void ResetStarterPrompt()
        {
            saveData.hasSeenStarterPrompt = false;
            Save();
        }

        public static bool ValidateDeck(IReadOnlyList<EmojiId> emojiIds, out string validationError)
        {
            if (emojiIds == null)
            {
                validationError = "Active deck data is missing.";
                return false;
            }

            if (emojiIds.Count < 6)
            {
                validationError = $"Choose {6 - emojiIds.Count} more emoji{(emojiIds.Count == 5 ? string.Empty : "s")} to complete your deck.";
                return false;
            }

            if (emojiIds.Count > 6)
            {
                validationError = "Decks use exactly 6 emojis.";
                return false;
            }

            if (emojiIds.Distinct().Count() != emojiIds.Count)
            {
                validationError = "Each deck slot must be a different emoji.";
                return false;
            }

            validationError = string.Empty;
            return true;
        }

        private void CreateStarterDeck()
        {
            activeDeckEmojiIds.Clear();
            activeDeckEmojiIds.AddRange(StarterDeck);
            saveData.deckId = Guid.NewGuid().ToString("N");
            saveData.emojiIds = activeDeckEmojiIds.Select(emojiId => (int)emojiId).ToArray();
            saveData.updatedAt = DateTime.UtcNow.ToString("O");
            saveData.hasSeenStarterPrompt = false;
        }

        private bool TryApplyDeck(int[] serializedEmojiIds)
        {
            activeDeckEmojiIds.Clear();

            if (serializedEmojiIds == null || serializedEmojiIds.Length == 0)
            {
                return false;
            }

            foreach (var serializedEmojiId in serializedEmojiIds)
            {
                if (!Enum.IsDefined(typeof(EmojiId), serializedEmojiId))
                {
                    activeDeckEmojiIds.Clear();
                    return false;
                }

                activeDeckEmojiIds.Add((EmojiId)serializedEmojiId);
            }

            if (!ValidateDeck(activeDeckEmojiIds, out _))
            {
                activeDeckEmojiIds.Clear();
                return false;
            }

            return true;
        }

        private void Load()
        {
            if (!PlayerPrefs.HasKey(StorageKey))
            {
                saveData = new ActiveDeckSaveData();
                activeDeckEmojiIds.Clear();
                return;
            }

            var json = PlayerPrefs.GetString(StorageKey, string.Empty);
            saveData = string.IsNullOrWhiteSpace(json)
                ? new ActiveDeckSaveData()
                : JsonUtility.FromJson<ActiveDeckSaveData>(json) ?? new ActiveDeckSaveData();

            TryApplyDeck(saveData.emojiIds);
        }

        private void Save()
        {
            PlayerPrefs.SetString(StorageKey, JsonUtility.ToJson(saveData));
            PlayerPrefs.Save();
        }

        [Serializable]
        private sealed class ActiveDeckSaveData
        {
            public string deckId = string.Empty;
            public string userId = string.Empty;
            public int[] emojiIds = Array.Empty<int>();
            public string updatedAt = string.Empty;
            public bool hasSeenStarterPrompt;
        }
    }
}
