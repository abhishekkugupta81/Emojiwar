using System;
using System.Collections;
using System.Reflection;
using EmojiWar.Client.Content;
using EmojiWar.Client.Core.Decks;
using EmojiWar.Client.Core.Session;
using EmojiWar.Client.Core.Supabase;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace EmojiWar.Client.Core
{
    public sealed class AppBootstrap : MonoBehaviour
    {
        private const string LocalGuestUserIdKey = "emojiwar.local_guest_user_id";
        private const string SessionStorageKey = "emojiwar.supabase_session";
        private const string LocalSupabaseUrlOverrideKey = "emojiwar.supabase_url_override";
        private const string LocalSupabaseAnonOverrideKey = "emojiwar.supabase_anon_override";

        private static AppBootstrap instance;
        private bool isRecoveringSession;

        [SerializeField] private SupabaseProjectConfig supabaseConfig;
        [SerializeField] private StaticEmojiCatalog emojiCatalog;

        public static AppBootstrap Instance => instance;
        public GuestSessionState SessionState { get; } = new();
        public ActiveDeckService ActiveDeckService { get; } = new();

        public SupabaseProjectConfig SupabaseConfig
        {
            get
            {
                if (supabaseConfig == null)
                {
                    ResolveMissingReferences();
                }

                return supabaseConfig;
            }
        }

        public SupabaseFunctionClient FunctionClient => new(SupabaseConfig);
        public SupabaseAuthClient AuthClient => new(SupabaseConfig);
        public SupabaseProfileSyncService ProfileSyncService => new(SupabaseConfig);
        public SupabaseDeckSyncService DeckSyncService => new(SupabaseConfig);
        public StaticEmojiCatalog EmojiCatalog => emojiCatalog;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            ResolveMissingReferences();

            if (supabaseConfig == null)
            {
                Debug.LogWarning("SupabaseProjectConfig is not assigned. Network features remain unavailable until configured.");
            }

            if (emojiCatalog == null)
            {
                Debug.LogWarning("StaticEmojiCatalog is not assigned. Create catalog assets in the Unity Editor.");
            }
        }

        private IEnumerator Start()
        {
            yield return BootstrapSessionAndDeck();

            if (SceneManager.GetActiveScene().name == SceneNames.Bootstrap)
            {
                SceneManager.LoadScene(SceneNames.Home);
            }
        }

        public IEnumerator SaveActiveDeck(System.Collections.Generic.IReadOnlyList<EmojiId> emojiIds, System.Action<string> onCompleted)
        {
            if (!ActiveDeckService.TrySaveActiveDeck(emojiIds, out var validationError))
            {
                onCompleted?.Invoke(validationError);
                yield break;
            }

            if (supabaseConfig == null || !supabaseConfig.IsConfigured || !SessionState.HasSession)
            {
                onCompleted?.Invoke("Deck saved locally. Supabase sync is unavailable.");
                yield break;
            }

            yield return DeckSyncService.SaveActiveDeck(SessionState, ActiveDeckService, emojiIds, onCompleted);
        }

        public IEnumerator RecoverSessionAfterUnauthorized(System.Action<bool> onCompleted = null)
        {
            if (isRecoveringSession)
            {
                var waitFrames = 0;
                while (isRecoveringSession && waitFrames < 120)
                {
                    waitFrames++;
                    yield return null;
                }

                onCompleted?.Invoke(HasUsableAccessToken());
                yield break;
            }

            if (supabaseConfig == null || !supabaseConfig.IsConfigured)
            {
                onCompleted?.Invoke(false);
                yield break;
            }

            isRecoveringSession = true;

            SessionState.AccessToken = string.Empty;
            SessionState.RefreshToken = string.Empty;
            SessionState.ExpiresAtUnix = 0;

            var recovered = false;
            yield return CreateAnonymousSession(success => recovered = success);

            if (recovered)
            {
                ActiveDeckService.EnsureInitialized(SessionState.UserId);
                yield return ProfileSyncService.EnsureProfileSynced(SessionState, _ => { });
                yield return DeckSyncService.EnsureActiveDeckSynced(SessionState, ActiveDeckService, _ => { });
                SaveSession();
            }
            else
            {
                EnsureLocalGuestIdentity();
                ActiveDeckService.EnsureInitialized(SessionState.UserId);
            }

            isRecoveringSession = false;
            onCompleted?.Invoke(recovered);
        }

        private IEnumerator BootstrapSessionAndDeck()
        {
            if (supabaseConfig == null || !supabaseConfig.IsConfigured)
            {
                EnsureLocalGuestIdentity();
                ActiveDeckService.EnsureInitialized(SessionState.UserId);
                yield break;
            }

            LoadStoredSession();

            var hasOnlineSession = HasUsableAccessToken();

            if (!hasOnlineSession && !string.IsNullOrWhiteSpace(SessionState.RefreshToken))
            {
                yield return RefreshAnonymousSession(success => hasOnlineSession = success);
            }

            if (!hasOnlineSession)
            {
                yield return CreateAnonymousSession(success => hasOnlineSession = success);
            }

            if (!hasOnlineSession)
            {
                Debug.LogWarning("Supabase guest auth failed. Falling back to local-only guest state.");
                EnsureLocalGuestIdentity();
                ActiveDeckService.EnsureInitialized(SessionState.UserId);
                yield break;
            }

            SaveSession();
            ActiveDeckService.EnsureInitialized(SessionState.UserId);

            string profileSyncMessage = string.Empty;
            yield return ProfileSyncService.EnsureProfileSynced(SessionState, message => profileSyncMessage = message ?? string.Empty);
            SaveSession();

            if (!string.IsNullOrWhiteSpace(profileSyncMessage))
            {
                Debug.LogWarning(profileSyncMessage);
            }

            string syncMessage = string.Empty;
            yield return DeckSyncService.EnsureActiveDeckSynced(SessionState, ActiveDeckService, message => syncMessage = message ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(syncMessage))
            {
                Debug.LogWarning(syncMessage);
            }
        }

        private bool HasUsableAccessToken()
        {
            if (!SessionState.HasSession || SessionState.ExpiresAtUnix <= 0)
            {
                return false;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // Keep a small safety margin before expiry.
            return SessionState.ExpiresAtUnix > now + 60;
        }

        private IEnumerator CreateAnonymousSession(System.Action<bool> onCompleted)
        {
            using var request = AuthClient.BuildAnonymousSignupRequest();
            request.timeout = 8;
            if (!TryBeginWebRequest(request, out var operation))
            {
                Debug.LogWarning("Supabase anonymous signup failed: request could not start (check HTTP settings).");
                onCompleted?.Invoke(false);
                yield break;
            }

            yield return operation;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Supabase anonymous signup failed: {request.error}");
                onCompleted?.Invoke(false);
                yield break;
            }

            var success = SupabaseAuthClient.TryApplyAuthResponse(request.downloadHandler.text, SessionState);
            if (success)
            {
                SaveSession();
            }

            onCompleted?.Invoke(success);
        }

        private IEnumerator RefreshAnonymousSession(System.Action<bool> onCompleted)
        {
            using var request = AuthClient.BuildRefreshRequest(SessionState.RefreshToken);
            request.timeout = 8;
            if (!TryBeginWebRequest(request, out var operation))
            {
                Debug.LogWarning("Supabase session refresh failed: request could not start (check HTTP settings).");
                onCompleted?.Invoke(false);
                yield break;
            }

            yield return operation;

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"Supabase session refresh failed: {request.error}");
                onCompleted?.Invoke(false);
                yield break;
            }

            var success = SupabaseAuthClient.TryApplyAuthResponse(request.downloadHandler.text, SessionState);
            if (success)
            {
                SaveSession();
            }

            onCompleted?.Invoke(success);
        }

        private void EnsureLocalGuestIdentity()
        {
            var userId = PlayerPrefs.GetString(LocalGuestUserIdKey, string.Empty);

            if (string.IsNullOrWhiteSpace(userId))
            {
                userId = System.Guid.NewGuid().ToString("N");
                PlayerPrefs.SetString(LocalGuestUserIdKey, userId);
                PlayerPrefs.Save();
            }

            SessionState.UserId = userId;
            SessionState.DisplayName = BuildFallbackDisplayName(userId);
            SessionState.IsAnonymous = true;
            SessionState.AccessToken = string.Empty;
            SessionState.RefreshToken = string.Empty;
            SessionState.ExpiresAtUnix = 0;
        }

        private void LoadStoredSession()
        {
            if (!PlayerPrefs.HasKey(SessionStorageKey))
            {
                return;
            }

            var json = PlayerPrefs.GetString(SessionStorageKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var storedState = JsonUtility.FromJson<GuestSessionState>(json);
            if (storedState == null)
            {
                return;
            }

            SessionState.UserId = storedState.UserId;
            SessionState.DisplayName = !string.IsNullOrWhiteSpace(storedState.DisplayName)
                ? storedState.DisplayName
                : BuildFallbackDisplayName(storedState.UserId);
            SessionState.AccessToken = storedState.AccessToken;
            SessionState.RefreshToken = storedState.RefreshToken;
            SessionState.IsAnonymous = storedState.IsAnonymous;
            SessionState.ExpiresAtUnix = storedState.ExpiresAtUnix;
        }

        private void SaveSession()
        {
            PlayerPrefs.SetString(SessionStorageKey, JsonUtility.ToJson(SessionState));
            PlayerPrefs.Save();
        }

        private static string BuildFallbackDisplayName(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return "Player";
            }

            var sanitized = userId.Replace("-", string.Empty);
            if (sanitized.Length > 8)
            {
                sanitized = sanitized.Substring(0, 8);
            }

            return $"Player-{sanitized}";
        }

        private void ResolveMissingReferences()
        {
            if (supabaseConfig == null)
            {
                supabaseConfig = Resources.Load<SupabaseProjectConfig>("Data/Config/SupabaseProjectConfig")
                    ?? Resources.Load<SupabaseProjectConfig>("SupabaseProjectConfig");
            }

            if (supabaseConfig == null)
            {
                supabaseConfig = BuildLocalFallbackSupabaseConfig();
            }

            if (emojiCatalog == null)
            {
                emojiCatalog = Resources.Load<StaticEmojiCatalog>("Data/Content/EmojiCatalog")
                    ?? Resources.Load<StaticEmojiCatalog>("EmojiCatalog");
            }
        }

        private static SupabaseProjectConfig BuildLocalFallbackSupabaseConfig()
        {
            var config = ScriptableObject.CreateInstance<SupabaseProjectConfig>();
            var type = typeof(SupabaseProjectConfig);
            var defaultUrl = PlayerPrefs.GetString(LocalSupabaseUrlOverrideKey, "http://127.0.0.1:54321");
            var defaultAnon = PlayerPrefs.GetString(LocalSupabaseAnonOverrideKey, "sb_publishable_ACJWlzQHlZjBrEguHvfOxg_3BJgxAaH");
            type.GetField("projectUrl", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(config, defaultUrl);
            type.GetField("anonKey", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(config, defaultAnon);
            type.GetField("functionsPath", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(config, "/functions/v1/");
            return config;
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
            catch (Exception exception)
            {
                Debug.LogWarning($"Supabase request start failed: {exception.Message}");
                return false;
            }
        }
    }
}
