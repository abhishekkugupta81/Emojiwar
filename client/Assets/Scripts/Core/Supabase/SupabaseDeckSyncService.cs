using System;
using System.Collections;
using System.Linq;
using EmojiWar.Client.Content;
using EmojiWar.Client.Core.Decks;
using EmojiWar.Client.Core.Session;
using UnityEngine;
using UnityEngine.Networking;

namespace EmojiWar.Client.Core.Supabase
{
    public sealed class SupabaseDeckSyncService
    {
        private readonly SupabaseRestClient restClient;

        public SupabaseDeckSyncService(SupabaseProjectConfig config)
        {
            restClient = new SupabaseRestClient(config);
        }

        public IEnumerator EnsureActiveDeckSynced(
            GuestSessionState sessionState,
            ActiveDeckService activeDeckService,
            Action<string> onCompleted)
        {
            if (sessionState == null || !sessionState.HasSession)
            {
                onCompleted?.Invoke("Supabase session is unavailable. Using local deck state.");
                yield break;
            }

            activeDeckService.EnsureInitialized(sessionState.UserId);

            DeckRecordDto remoteDeck = null;
            string failureMessage = null;

            using (var request = restClient.BuildSelectRequest(
                       "decks",
                       "select=id,user_id,emoji_ids,is_active,updated_at&is_active=eq.true&order=updated_at.desc&limit=1",
                       sessionState.AccessToken))
            {
                request.timeout = 8;
                if (!TryBeginWebRequest(request, out var operation))
                {
                    failureMessage = "Deck sync fallback: request could not start (check HTTP settings).";
                }
                else
                {
                    yield return operation;

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        remoteDeck = ParseDeckArray(request.downloadHandler.text).FirstOrDefault();
                    }
                    else
                    {
                        failureMessage = $"Deck sync fallback: {request.error}";
                    }
                }
            }

            if (remoteDeck != null)
            {
                var remoteEmojiIds = EmojiIdUtility.ParseApiIds(remoteDeck.emoji_ids);
                if (activeDeckService.TryHydrateFromRemoteDeck(remoteDeck.id, sessionState.UserId, remoteEmojiIds, remoteDeck.updated_at))
                {
                    onCompleted?.Invoke(string.Empty);
                    yield break;
                }

                onCompleted?.Invoke("Deck sync fallback: remote active deck was invalid. Using local deck state.");
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(failureMessage))
            {
                onCompleted?.Invoke(failureMessage);
                yield break;
            }

            yield return SaveActiveDeck(sessionState, activeDeckService, activeDeckService.ActiveDeckEmojiIds, onCompleted);
        }

        public IEnumerator SaveActiveDeck(
            GuestSessionState sessionState,
            ActiveDeckService activeDeckService,
            System.Collections.Generic.IReadOnlyList<EmojiId> emojiIds,
            Action<string> onCompleted)
        {
            if (sessionState == null || !sessionState.HasSession)
            {
                onCompleted?.Invoke("Saved locally. Supabase session is unavailable, so the deck was not synced.");
                yield break;
            }

            if (!ActiveDeckService.ValidateDeck(emojiIds, out var validationError))
            {
                onCompleted?.Invoke(validationError);
                yield break;
            }

            var payload = JsonUtility.ToJson(new DeckMutationDto
            {
                user_id = sessionState.UserId,
                name = "Main Deck",
                emoji_ids = EmojiIdUtility.ToApiIds(emojiIds),
                is_active = true
            });

            DeckRecordDto savedDeck = null;
            string requestError = null;

            if (!string.IsNullOrWhiteSpace(activeDeckService.DeckId))
            {
                using var patchRequest = restClient.BuildPatchRequest(
                    "decks",
                    $"id=eq.{activeDeckService.DeckId}",
                    payload,
                    sessionState.AccessToken);
                patchRequest.timeout = 8;
                if (!TryBeginWebRequest(patchRequest, out var patchOperation))
                {
                    requestError = "request could not start";
                }
                else
                {
                    yield return patchOperation;
                }

                if (patchRequest.result == UnityWebRequest.Result.Success)
                {
                    savedDeck = ParseDeckArray(patchRequest.downloadHandler.text).FirstOrDefault();
                }
                else
                {
                    requestError = patchRequest.error;
                }
            }

            if (savedDeck == null)
            {
                using var insertRequest = restClient.BuildInsertRequest("decks", payload, sessionState.AccessToken);
                insertRequest.timeout = 8;
                if (!TryBeginWebRequest(insertRequest, out var insertOperation))
                {
                    requestError = "request could not start";
                }
                else
                {
                    yield return insertOperation;
                }

                if (insertRequest.result == UnityWebRequest.Result.Success)
                {
                    savedDeck = ParseDeckArray(insertRequest.downloadHandler.text).FirstOrDefault();
                }
                else
                {
                    requestError = insertRequest.error;
                }
            }

            if (savedDeck == null)
            {
                onCompleted?.Invoke($"Saved locally. Deck sync failed: {requestError ?? "unknown error"}.");
                yield break;
            }

            var syncedEmojiIds = EmojiIdUtility.ParseApiIds(savedDeck.emoji_ids);
            if (!activeDeckService.TryHydrateFromRemoteDeck(savedDeck.id, sessionState.UserId, syncedEmojiIds, savedDeck.updated_at))
            {
                onCompleted?.Invoke("Saved locally. Deck sync response was invalid.");
                yield break;
            }

            onCompleted?.Invoke("Deck saved. Supabase active deck is up to date.");
        }

        private static DeckRecordDto[] ParseDeckArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "null")
            {
                return Array.Empty<DeckRecordDto>();
            }

            var wrappedJson = $"{{\"items\":{json}}}";
            var wrapper = JsonUtility.FromJson<DeckRecordListDto>(wrappedJson);
            return wrapper?.items ?? Array.Empty<DeckRecordDto>();
        }

        private static bool TryBeginWebRequest(UnityWebRequest request, out UnityWebRequestAsyncOperation operation)
        {
            operation = null;
            if (request == null)
            {
                return false;
            }

            try
            {
                operation = request.SendWebRequest();
                return true;
            }
            catch
            {
                return false;
            }
        }

        [Serializable]
        private sealed class DeckRecordListDto
        {
            public DeckRecordDto[] items = Array.Empty<DeckRecordDto>();
        }

        [Serializable]
        private sealed class DeckRecordDto
        {
            public string id = string.Empty;
            public string user_id = string.Empty;
            public string[] emoji_ids = Array.Empty<string>();
            public bool is_active;
            public string updated_at = string.Empty;
        }

        [Serializable]
        private sealed class DeckMutationDto
        {
            public string user_id = string.Empty;
            public string name = "Main Deck";
            public string[] emoji_ids = Array.Empty<string>();
            public bool is_active = true;
        }
    }
}
