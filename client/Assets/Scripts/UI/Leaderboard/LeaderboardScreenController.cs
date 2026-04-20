using System.Collections;
using System.Linq;
using System.Text;
using EmojiWar.Client.Core;
using EmojiWar.Client.Gameplay.Contracts;
using EmojiWar.Client.UI.Common;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Leaderboard
{
    public sealed class LeaderboardScreenController : MonoBehaviour
    {
        [SerializeField] private Text summaryLabel;
        [SerializeField] private Text entriesLabel;
        [SerializeField] private Button backButton;
        [SerializeField] private Image panelBackground;
        [SerializeField] private RectTransform cardListContainer;

        private Coroutine refreshRoutine;
        private bool runtimeCardsInitialized;

        private void Awake()
        {
            AutoWireSceneReferences();
            EnsureRuntimeCardList();
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(ReturnHome);
            }
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            if (refreshRoutine != null)
            {
                StopCoroutine(refreshRoutine);
                refreshRoutine = null;
            }
        }

        public void Refresh()
        {
            if (refreshRoutine != null)
            {
                StopCoroutine(refreshRoutine);
            }

            refreshRoutine = StartCoroutine(RefreshRoutine());
        }

        private IEnumerator RefreshRoutine()
        {
            SetView("Loading leaderboard...", "Fetching ranked standings from Supabase.");

            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null || bootstrap.SupabaseConfig == null || !bootstrap.SupabaseConfig.IsConfigured)
            {
                SetView("Leaderboard unavailable.", "Open the game through the Bootstrap scene with Supabase configured.");
                yield break;
            }

            if (!bootstrap.SessionState.HasSession)
            {
                SetView("Leaderboard unavailable.", "Guest session is missing. Relaunch through Bootstrap.");
                yield break;
            }

            var payload = JsonUtility.ToJson(new GetLeaderboardRequestDto
            {
                limit = 25,
            });

            using var request = bootstrap.FunctionClient.BuildJsonRequest("get_leaderboard", payload, bootstrap.SessionState.AccessToken);
            request.timeout = 8;
            if (!TryBeginWebRequest(request, out var operation, out var startError))
            {
                SetView("Leaderboard lookup failed.", startError);
                yield break;
            }

            yield return operation;

            if (request.result != UnityWebRequest.Result.Success)
            {
                SetView("Leaderboard lookup failed.", request.error ?? "Unknown network error.");
                yield break;
            }

            var response = JsonUtility.FromJson<GetLeaderboardResponseDto>(request.downloadHandler.text);
            if (response == null)
            {
                SetView("Leaderboard lookup failed.", "Could not parse leaderboard response.");
                yield break;
            }

            SetView(BuildSummary(response), string.Empty);
            RenderLeaderboardCards(response);
        }

        private void ReturnHome()
        {
            SceneManager.LoadScene(SceneNames.Home);
        }

        private void SetView(string summary, string details)
        {
            if (summaryLabel != null)
            {
                summaryLabel.text = summary;
                summaryLabel.fontSize = UiThemeRuntime.Theme.HeadingFontSize;
            }

            if (entriesLabel != null)
            {
                entriesLabel.text = details;
                entriesLabel.fontSize = UiThemeRuntime.Theme.BodyFontSize;
            }

            if (panelBackground != null)
            {
                panelBackground.color = Color.Lerp(UiThemeRuntime.Theme.HomeGradient.Top, UiThemeRuntime.Theme.HomeGradient.Bottom, 0.42f);
            }

            if (cardListContainer != null)
            {
                if (!string.IsNullOrWhiteSpace(details))
                {
                    RenderSingleInfoCard(details);
                }
                else
                {
                    ClearCards();
                }
            }
        }

        private static string BuildSummary(GetLeaderboardResponseDto response)
        {
            if (response.myEntry != null)
            {
                return
                    $"My Standing\n" +
                    $"#{response.myEntry.rank}  {response.myEntry.currentElo} Elo\n" +
                    $"{response.myEntry.wins}W - {response.myEntry.losses}L";
            }

            return
                $"Leaderboard\n" +
                $"{response.totalRatedPlayers} rated player{(response.totalRatedPlayers == 1 ? string.Empty : "s")}\n" +
                "Finish a ranked match to add your own standing.";
        }

        private static string BuildEntries(GetLeaderboardResponseDto response)
        {
            if (response.entries == null || response.entries.Length == 0)
            {
                return string.IsNullOrWhiteSpace(response.note)
                    ? "No ranked results yet."
                    : response.note;
            }

            var builder = new StringBuilder();
            builder.AppendLine("TOP PLAYERS");
            builder.AppendLine();

            for (var index = 0; index < response.entries.Length; index++)
            {
                var entry = response.entries[index];
                builder.Append('#').Append(entry.rank).Append(' ')
                    .Append(entry.displayName)
                    .Append("  ")
                    .Append(entry.currentElo).Append(" Elo")
                    .Append("  ")
                    .Append(entry.wins).Append('W')
                    .Append('-')
                    .Append(entry.losses).Append('L');

                if (entry.isCurrentUser)
                {
                    builder.Append("  <- You");
                }

                if (index < response.entries.Length - 1)
                {
                    builder.AppendLine();
                    builder.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
                }
            }

            if (response.nearbyEntries != null && response.nearbyEntries.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine();
                builder.AppendLine("NEAR YOU");
                builder.AppendLine();

                for (var index = 0; index < response.nearbyEntries.Length; index++)
                {
                    var entry = response.nearbyEntries[index];
                    builder.Append('#').Append(entry.rank).Append(' ')
                        .Append(entry.displayName)
                        .Append("  ")
                        .Append(entry.currentElo).Append(" Elo")
                        .Append("  ")
                        .Append(entry.wins).Append('W')
                        .Append('-')
                        .Append(entry.losses).Append('L');

                    if (entry.isCurrentUser)
                    {
                        builder.Append("  <- You");
                    }

                    if (index < response.nearbyEntries.Length - 1)
                    {
                        builder.AppendLine();
                        builder.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(response.note))
            {
                builder.AppendLine();
                builder.AppendLine();
                builder.Append(response.note);
            }

            return builder.ToString();
        }

        private void AutoWireSceneReferences()
        {
            if (summaryLabel == null)
            {
                summaryLabel = GameObject.Find("LeaderboardSummary")?.GetComponent<Text>();
            }

            if (entriesLabel == null)
            {
                entriesLabel = GameObject.Find("LeaderboardEntries")?.GetComponent<Text>();
            }

            if (backButton == null)
            {
                backButton = FindObjectsOfType<Button>(true)
                    .FirstOrDefault(button => button.name.IndexOf("Back", System.StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (panelBackground == null)
            {
                panelBackground = GameObject.Find("LeaderboardPanel")?.GetComponent<Image>();
            }
        }

        private void EnsureRuntimeCardList()
        {
            if (runtimeCardsInitialized)
            {
                return;
            }

            runtimeCardsInitialized = true;
            if (entriesLabel == null || entriesLabel.transform.parent == null)
            {
                return;
            }

            var scrollView = new GameObject("LeaderboardCardScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollView.transform.SetParent(entriesLabel.transform.parent, false);
            var scrollRectTransform = scrollView.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0f, 0f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.offsetMin = new Vector2(0f, 0f);
            scrollRectTransform.offsetMax = new Vector2(0f, 0f);
            scrollView.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollView.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-8f, -8f);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            cardListContainer = content.GetComponent<RectTransform>();
            cardListContainer.anchorMin = new Vector2(0f, 1f);
            cardListContainer.anchorMax = new Vector2(1f, 1f);
            cardListContainer.pivot = new Vector2(0.5f, 1f);
            cardListContainer.anchoredPosition = Vector2.zero;
            cardListContainer.sizeDelta = Vector2.zero;

            var contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 10f;
            contentLayout.padding = new RectOffset(6, 6, 6, 18);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollView.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.viewport = viewportRect;
            scroll.content = cardListContainer;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32f;

            entriesLabel.gameObject.SetActive(false);
        }

        private void RenderLeaderboardCards(GetLeaderboardResponseDto response)
        {
            if (cardListContainer == null)
            {
                if (entriesLabel != null)
                {
                    entriesLabel.text = BuildEntries(response);
                }

                return;
            }

            ClearCards();
            if (response.entries == null || response.entries.Length == 0)
            {
                RenderSingleInfoCard(string.IsNullOrWhiteSpace(response.note) ? "No ranked results yet." : response.note);
                return;
            }

            CreateSectionHeader("Top players");
            foreach (var entry in response.entries)
            {
                CreateLeaderboardCard(entry, entry.isCurrentUser ? UiThemeRuntime.Theme.PrimaryCtaColor * new Color(1f, 1f, 1f, 0.30f) : UiThemeRuntime.Theme.SurfaceColor * new Color(1f, 1f, 1f, 0.88f));
            }

            if (response.nearbyEntries != null && response.nearbyEntries.Length > 0)
            {
                CreateSectionHeader("Near you");
                foreach (var entry in response.nearbyEntries)
                {
                    CreateLeaderboardCard(entry, entry.isCurrentUser ? UiThemeRuntime.Theme.SupportAccent * new Color(1f, 1f, 1f, 0.30f) : UiThemeRuntime.Theme.SurfaceColor * new Color(1f, 1f, 1f, 0.88f));
                }
            }
        }

        private void CreateSectionHeader(string title)
        {
            if (cardListContainer == null)
            {
                return;
            }

            var headerObject = new GameObject("SectionHeader", typeof(RectTransform), typeof(LayoutElement), typeof(Text));
            headerObject.transform.SetParent(cardListContainer, false);
            var layout = headerObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 38f;
            layout.minHeight = 38f;

            var label = headerObject.GetComponent<Text>();
            label.text = title;
            label.font = summaryLabel != null ? summaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = UiThemeRuntime.Theme.BodyFontSize;
            label.fontStyle = FontStyle.Bold;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
        }

        private void CreateLeaderboardCard(LeaderboardEntryDto entry, Color tint)
        {
            if (cardListContainer == null || entry == null)
            {
                return;
            }

            var card = new GameObject("LeaderboardEntryCard", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(cardListContainer, false);

            var layoutElement = card.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 92f;
            layoutElement.minHeight = 92f;

            var image = card.GetComponent<Image>();
            image.color = tint;

            var layout = card.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 4f;
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var rowOne = $"#{entry.rank}  {entry.displayName}{(entry.isCurrentUser ? "  • You" : string.Empty)}";
            var rowTwo = $"{entry.currentElo} Elo   {entry.wins}W-{entry.losses}L";
            CreateCardLabel(card.transform, rowOne, UiThemeRuntime.Theme.BodyFontSize, FontStyle.Bold, 36f);
            CreateCardLabel(card.transform, rowTwo, UiThemeRuntime.Theme.ChipFontSize + 1, FontStyle.Normal, 30f);
        }

        private void RenderSingleInfoCard(string details)
        {
            if (cardListContainer == null)
            {
                return;
            }

            ClearCards();
            var card = new GameObject("LeaderboardInfoCard", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Text));
            card.transform.SetParent(cardListContainer, false);
            var layout = card.GetComponent<LayoutElement>();
            layout.preferredHeight = 140f;
            layout.minHeight = 120f;
            card.GetComponent<Image>().color = UiThemeRuntime.Theme.SurfaceColor * new Color(1f, 1f, 1f, 0.88f);

            var text = card.GetComponent<Text>();
            text.text = details;
            text.font = summaryLabel != null ? summaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = UiThemeRuntime.Theme.BodyFontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
        }

        private void CreateCardLabel(Transform parent, string textValue, int fontSize, FontStyle style, float preferredHeight)
        {
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(LayoutElement), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            var layout = labelObject.GetComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            layout.minHeight = preferredHeight;

            var label = labelObject.GetComponent<Text>();
            label.text = textValue;
            label.font = summaryLabel != null ? summaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = Color.white;
            label.alignment = TextAnchor.UpperLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private void ClearCards()
        {
            if (cardListContainer == null)
            {
                return;
            }

            for (var index = cardListContainer.childCount - 1; index >= 0; index--)
            {
                Destroy(cardListContainer.GetChild(index).gameObject);
            }
        }

        private static bool TryBeginWebRequest(
            UnityWebRequest request,
            out UnityWebRequestAsyncOperation operation,
            out string error)
        {
            operation = null;
            error = string.Empty;

            if (request == null)
            {
                error = "Request could not be created.";
                return false;
            }

            try
            {
                operation = request.SendWebRequest();
                return true;
            }
            catch (System.Exception exception)
            {
                var message = exception.Message ?? string.Empty;
                if (message.IndexOf("Insecure connection", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    error = "Insecure connection not allowed. Set Player Settings > Allow downloads over HTTP to Always Allowed for local Supabase.";
                    return false;
                }

                error = $"Request start failed: {message}";
                return false;
            }
        }
    }
}
