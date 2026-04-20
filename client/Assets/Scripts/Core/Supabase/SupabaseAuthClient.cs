using System;
using System.Text;
using EmojiWar.Client.Core.Session;
using UnityEngine;
using UnityEngine.Networking;

namespace EmojiWar.Client.Core.Supabase
{
    public sealed class SupabaseAuthClient
    {
        private readonly SupabaseProjectConfig config;

        public SupabaseAuthClient(SupabaseProjectConfig config)
        {
            this.config = config;
        }

        public UnityWebRequest BuildAnonymousSignupRequest()
        {
            return BuildAuthJsonRequest("signup", "{}", config.AnonKey);
        }

        public UnityWebRequest BuildRefreshRequest(string refreshToken)
        {
            var payload = JsonUtility.ToJson(new RefreshTokenRequestDto
            {
                refresh_token = refreshToken
            });

            return BuildAuthJsonRequest("token?grant_type=refresh_token", payload, config.AnonKey);
        }

        public static bool TryApplyAuthResponse(string json, GuestSessionState sessionState)
        {
            if (sessionState == null || string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            var response = JsonUtility.FromJson<AuthResponseDto>(json);
            if (response == null || response.user == null || string.IsNullOrWhiteSpace(response.user.id))
            {
                return false;
            }

            sessionState.UserId = response.user.id;
            sessionState.AccessToken = response.access_token ?? string.Empty;
            sessionState.RefreshToken = response.refresh_token ?? string.Empty;
            sessionState.IsAnonymous = response.user.is_anonymous;
            sessionState.ExpiresAtUnix = response.expires_at;
            return !string.IsNullOrWhiteSpace(sessionState.AccessToken);
        }

        private UnityWebRequest BuildAuthJsonRequest(string relativePath, string jsonPayload, string bearerToken)
        {
            var request = new UnityWebRequest(config.BuildAuthUrl(relativePath), UnityWebRequest.kHttpVerbPOST);
            var body = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("apikey", config.AnonKey);
            request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
            return request;
        }

        [Serializable]
        private sealed class RefreshTokenRequestDto
        {
            public string refresh_token = string.Empty;
        }

        [Serializable]
        private sealed class AuthResponseDto
        {
            public string access_token = string.Empty;
            public string refresh_token = string.Empty;
            public long expires_at;
            public AuthUserDto user = new();
        }

        [Serializable]
        private sealed class AuthUserDto
        {
            public string id = string.Empty;
            public bool is_anonymous;
        }
    }
}
