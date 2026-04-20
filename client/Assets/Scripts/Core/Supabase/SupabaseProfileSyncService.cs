using System;
using System.Collections;
using System.Linq;
using EmojiWar.Client.Core.Session;
using UnityEngine;
using UnityEngine.Networking;

namespace EmojiWar.Client.Core.Supabase
{
    public sealed class SupabaseProfileSyncService
    {
        private readonly SupabaseRestClient restClient;

        public SupabaseProfileSyncService(SupabaseProjectConfig config)
        {
            restClient = new SupabaseRestClient(config);
        }

        public IEnumerator EnsureProfileSynced(GuestSessionState sessionState, Action<string> onCompleted)
        {
            if (sessionState == null || !sessionState.HasSession)
            {
                onCompleted?.Invoke("Supabase session is unavailable. Player profile sync was skipped.");
                yield break;
            }

            ProfileRecordDto remoteProfile = null;
            string requestError = null;

            using (var request = restClient.BuildSelectRequest(
                       "profiles",
                       $"select=user_id,display_name&user_id=eq.{sessionState.UserId}&limit=1",
                       sessionState.AccessToken))
            {
                request.timeout = 8;
                if (!TryBeginWebRequest(request, out var operation))
                {
                    requestError = "request could not start";
                }
                else
                {
                    yield return operation;

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        remoteProfile = ParseProfileArray(request.downloadHandler.text).FirstOrDefault();
                    }
                    else
                    {
                        requestError = request.error;
                    }
                }
            }

            if (remoteProfile != null)
            {
                sessionState.DisplayName = NormalizeDisplayName(remoteProfile.display_name, sessionState.UserId);
                onCompleted?.Invoke(string.Empty);
                yield break;
            }

            if (!string.IsNullOrWhiteSpace(requestError))
            {
                onCompleted?.Invoke($"Profile sync skipped: {requestError}");
                yield break;
            }

            var desiredDisplayName = BuildDefaultDisplayName(sessionState.UserId);
            var payload = JsonUtility.ToJson(new ProfileMutationDto
            {
                user_id = sessionState.UserId,
                display_name = desiredDisplayName,
            });

            using var insertRequest = restClient.BuildInsertRequest("profiles", payload, sessionState.AccessToken);
            insertRequest.timeout = 8;
            if (!TryBeginWebRequest(insertRequest, out var insertOperation))
            {
                onCompleted?.Invoke("Profile sync failed: request could not start");
                yield break;
            }

            yield return insertOperation;

            if (insertRequest.result != UnityWebRequest.Result.Success)
            {
                using var retryRequest = restClient.BuildSelectRequest(
                    "profiles",
                    $"select=user_id,display_name&user_id=eq.{sessionState.UserId}&limit=1",
                    sessionState.AccessToken);
                retryRequest.timeout = 8;
                if (!TryBeginWebRequest(retryRequest, out var retryOperation))
                {
                    onCompleted?.Invoke($"Profile sync failed: {insertRequest.error ?? "unknown error"}");
                    yield break;
                }

                yield return retryOperation;

                if (retryRequest.result == UnityWebRequest.Result.Success)
                {
                    var existingProfile = ParseProfileArray(retryRequest.downloadHandler.text).FirstOrDefault();
                    if (existingProfile != null)
                    {
                        sessionState.DisplayName = NormalizeDisplayName(existingProfile.display_name, sessionState.UserId);
                        onCompleted?.Invoke(string.Empty);
                        yield break;
                    }
                }

                onCompleted?.Invoke($"Profile sync failed: {insertRequest.error ?? "unknown error"}");
                yield break;
            }

            var savedProfile = ParseProfileArray(insertRequest.downloadHandler.text).FirstOrDefault();
            sessionState.DisplayName = NormalizeDisplayName(savedProfile?.display_name, sessionState.UserId);
            onCompleted?.Invoke(string.Empty);
        }

        private static ProfileRecordDto[] ParseProfileArray(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "null")
            {
                return Array.Empty<ProfileRecordDto>();
            }

            var wrappedJson = $"{{\"items\":{json}}}";
            var wrapper = JsonUtility.FromJson<ProfileRecordListDto>(wrappedJson);
            return wrapper?.items ?? Array.Empty<ProfileRecordDto>();
        }

        private static string NormalizeDisplayName(string displayName, string userId)
        {
            return !string.IsNullOrWhiteSpace(displayName)
                ? displayName.Trim()
                : BuildDefaultDisplayName(userId);
        }

        private static string BuildDefaultDisplayName(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return "Player";
            }

            var suffix = userId.Replace("-", string.Empty);
            if (suffix.Length > 8)
            {
                suffix = suffix.Substring(0, 8);
            }

            return $"Player-{suffix}";
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
        private sealed class ProfileRecordListDto
        {
            public ProfileRecordDto[] items = Array.Empty<ProfileRecordDto>();
        }

        [Serializable]
        private sealed class ProfileRecordDto
        {
            public string user_id = string.Empty;
            public string display_name = string.Empty;
        }

        [Serializable]
        private sealed class ProfileMutationDto
        {
            public string user_id = string.Empty;
            public string display_name = string.Empty;
        }
    }
}
