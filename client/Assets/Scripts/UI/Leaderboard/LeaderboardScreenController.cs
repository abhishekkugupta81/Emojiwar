using System.Collections;
using System.Collections.Generic;
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
        private Coroutine entranceRoutine;
        private bool runtimeCardsInitialized;
        private bool runtimeThemeInitialized;
        private string lastEntranceKey = string.Empty;
        private readonly List<RectTransform> rowMotionTargets = new();
        private readonly List<RectTransform> rankBadgeTargets = new();
        private readonly List<RectTransform> scoreChipTargets = new();
        private RectTransform currentUserRowTarget;
        private RectTransform currentUserRankBadgeTarget;
        private RectTransform currentUserScoreChipTarget;

        private void Awake()
        {
            AutoWireSceneReferences();
            EnsureRuntimeTheme();
            EnsureRuntimeCardList();
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(() =>
                {
                    NativeMotionKit.PunchScale(this, backButton.transform as RectTransform, 0.065f, 0.13f);
                    ReturnHome();
                });
            }

            StyleBackButton();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnDisable()
        {
            CancelEntranceMotion();
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
            SetView("Loading ranks...", "Getting Quick Clash standings.");

            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null || bootstrap.SupabaseConfig == null || !bootstrap.SupabaseConfig.IsConfigured)
            {
                SetView("Leaderboard unavailable.", "Open the game through the Bootstrap scene with Supabase configured.");
                yield break;
            }

            var sessionReady = false;
            yield return bootstrap.EnsureOnlineSession(success => sessionReady = success);
            if (!sessionReady || !bootstrap.SessionState.HasSession)
            {
                SetView("Leaderboard unavailable.", "Could not refresh the guest session.");
                yield break;
            }

            var payload = JsonUtility.ToJson(new GetLeaderboardRequestDto
            {
                limit = 25,
            });

            using var request = CreateLeaderboardRequest(bootstrap, payload);
            if (!TryRunLeaderboardRequest(request, out var operation, out var startError))
            {
                SetView("Leaderboard lookup failed.", startError);
                yield break;
            }

            yield return operation;

            if (request.responseCode == 401)
            {
                var recovered = false;
                yield return bootstrap.RecoverSessionAfterUnauthorized(success => recovered = success);
                if (recovered && bootstrap.SessionState.HasSession)
                {
                    using var retryRequest = CreateLeaderboardRequest(bootstrap, payload);
                    if (!TryRunLeaderboardRequest(retryRequest, out var retryOperation, out var retryStartError))
                    {
                        SetView("Leaderboard lookup failed.", retryStartError);
                        yield break;
                    }

                    yield return retryOperation;
                    if (retryRequest.result != UnityWebRequest.Result.Success)
                    {
                        SetView("Leaderboard lookup failed.", retryRequest.error ?? $"HTTP {retryRequest.responseCode}");
                        yield break;
                    }

                    ApplyLeaderboardResponse(retryRequest.downloadHandler.text);
                    yield break;
                }
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                SetView("Leaderboard lookup failed.", request.error ?? "Unknown network error.");
                yield break;
            }

            ApplyLeaderboardResponse(request.downloadHandler.text);
        }

        private UnityWebRequest CreateLeaderboardRequest(AppBootstrap bootstrap, string payload)
        {
            var request = bootstrap.FunctionClient.BuildJsonRequest("get_clash_leaderboard", payload, bootstrap.SessionState.AccessToken);
            request.timeout = 8;
            return request;
        }

        private bool TryRunLeaderboardRequest(
            UnityWebRequest request,
            out UnityWebRequestAsyncOperation operation,
            out string startError)
        {
            return TryBeginWebRequest(request, out operation, out startError);
        }

        private void ApplyLeaderboardResponse(string json)
        {
            var response = JsonUtility.FromJson<GetLeaderboardResponseDto>(json);
            if (response == null)
            {
                SetView("Leaderboard lookup failed.", "Could not parse leaderboard response.");
                return;
            }

            SetView(BuildSummary(response), string.Empty);
            RenderLeaderboardCards(response);
            PlayLeaderboardEntrance(response);
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
                summaryLabel.fontStyle = FontStyle.Bold;
                summaryLabel.color = Color.white;
            }

            if (entriesLabel != null)
            {
                entriesLabel.text = details;
                entriesLabel.fontSize = UiThemeRuntime.Theme.BodyFontSize;
                entriesLabel.color = Color.white;
            }

            if (panelBackground != null)
            {
                panelBackground.color = new Color(0.10f, 0.06f, 0.28f, 1f);
            }

            if (cardListContainer != null)
            {
                if (!string.IsNullOrWhiteSpace(details))
                {
                    RenderSingleInfoCard(details);
                    PlayInfoStateEntrance();
                }
                else
                {
                    ClearCards();
                    ResetMotionTargets();
                }
            }
        }

        private static string BuildSummary(GetLeaderboardResponseDto response)
        {
            if (response.myEntry != null)
            {
                return
                    $"Quick Clash Ranks\n" +
                    $"#{response.myEntry.rank} · {GetVisiblePoints(response.myEntry)} pts";
            }

            return
                $"Quick Clash Ranks\n" +
                $"{response.totalRatedPlayers} ranked player{(response.totalRatedPlayers == 1 ? string.Empty : "s")}\n" +
                "Play Quick Clash to appear on the board.";
        }

        private static string BuildEntries(GetLeaderboardResponseDto response)
        {
            if (response.entries == null || response.entries.Length == 0)
            {
                return string.IsNullOrWhiteSpace(response.note)
                    ? "No ranks yet.\nPlay Quick Clash to appear on the board."
                    : response.note;
            }

            var builder = new StringBuilder();
            builder.AppendLine("TOP QUICK CLASH");
            builder.AppendLine();

            for (var index = 0; index < response.entries.Length; index++)
            {
                var entry = response.entries[index];
                builder.Append('#').Append(entry.rank).Append(' ')
                    .Append(GetLeaderboardDisplayName(entry))
                    .Append("  ")
                    .Append(GetVisiblePoints(entry)).Append(" pts");

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
                        .Append(GetLeaderboardDisplayName(entry))
                        .Append("  ")
                        .Append(GetVisiblePoints(entry)).Append(" pts");

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

        private void EnsureRuntimeTheme()
        {
            if (runtimeThemeInitialized || panelBackground == null)
            {
                return;
            }

            runtimeThemeInitialized = true;
            panelBackground.color = new Color(0.10f, 0.06f, 0.28f, 1f);

            if (panelBackground.transform.Find("LeaderboardStickerPopGradient") == null)
            {
                var gradient = RescueStickerFactory.CreateGradientLikeBackground(
                    panelBackground.transform,
                    "LeaderboardStickerPopGradient",
                    RescueStickerFactory.Palette.DeepIndigo,
                    RescueStickerFactory.Palette.Aqua);
                gradient.raycastTarget = false;
            }

            CreateThemeGlow("LeaderboardGlowTop", new Vector2(0f, 300f), new Vector2(520f, 190f), RescueStickerFactory.Palette.ElectricPurple, 0.20f);
            CreateThemeGlow("LeaderboardGlowMid", new Vector2(150f, 90f), new Vector2(340f, 260f), RescueStickerFactory.Palette.HotPink, 0.11f);
            CreateThemeGlow("LeaderboardGlowBottom", new Vector2(-130f, -330f), new Vector2(420f, 260f), RescueStickerFactory.Palette.Mint, 0.10f);
            CreateTitlePlate();
            CreateThemeSpark("LeaderboardStarA", new Vector2(-180f, 258f), RescueStickerFactory.Palette.SunnyYellow, 18);
            CreateThemeSpark("LeaderboardStarB", new Vector2(192f, 202f), RescueStickerFactory.Palette.HotPink, 14);
            CreateThemeSpark("LeaderboardStarC", new Vector2(-214f, -214f), RescueStickerFactory.Palette.Aqua, 12);
            SendThemeDecorBehindContent();
        }

        private void CreateThemeGlow(string name, Vector2 anchoredPosition, Vector2 size, Color color, float alpha)
        {
            if (panelBackground == null || panelBackground.transform.Find(name) != null)
            {
                return;
            }

            var glow = RescueStickerFactory.CreateBlob(panelBackground.transform, name, color, anchoredPosition, size, alpha);
            glow.transform.SetSiblingIndex(1);
        }

        private void CreateThemeSpark(string name, Vector2 anchoredPosition, Color color, int fontSize)
        {
            if (panelBackground == null || panelBackground.transform.Find(name) != null)
            {
                return;
            }

            var spark = new GameObject(name, typeof(RectTransform), typeof(Text));
            spark.transform.SetParent(panelBackground.transform, false);
            spark.transform.SetSiblingIndex(2);

            var rect = spark.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(36f, 36f);

            var label = spark.GetComponent<Text>();
            label.text = "+";
            label.font = summaryLabel != null ? summaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = FontStyle.Bold;
            label.color = color;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
        }

        private void CreateTitlePlate()
        {
            if (panelBackground == null || panelBackground.transform.Find("LeaderboardTitlePlate") != null)
            {
                return;
            }

            var plate = new GameObject("LeaderboardTitlePlate", typeof(RectTransform), typeof(Image), typeof(Outline));
            plate.transform.SetParent(panelBackground.transform, false);
            plate.transform.SetSiblingIndex(3);

            var rect = plate.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -88f);
            rect.sizeDelta = new Vector2(360f, 82f);

            var image = plate.GetComponent<Image>();
            image.color = new Color(0.34f, 0.13f, 0.76f, 0.40f);
            image.raycastTarget = false;

            var outline = plate.GetComponent<Outline>();
            outline.effectColor = new Color(0.25f, 0.95f, 1f, 0.26f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private void SendThemeDecorBehindContent()
        {
            if (panelBackground == null)
            {
                return;
            }

            var decor = new List<Transform>();
            for (var index = 0; index < panelBackground.transform.childCount; index++)
            {
                var child = panelBackground.transform.GetChild(index);
                if (IsThemeDecor(child.name))
                {
                    decor.Add(child);
                }
            }

            for (var nextIndex = 0; nextIndex < decor.Count; nextIndex++)
            {
                var child = decor[nextIndex];
                child.SetSiblingIndex(nextIndex);

                foreach (var graphic in child.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
            }
        }

        private static bool IsThemeDecor(string objectName)
        {
            return objectName.StartsWith("LeaderboardStickerPopGradient", System.StringComparison.Ordinal) ||
                   objectName.StartsWith("LeaderboardGlow", System.StringComparison.Ordinal) ||
                   objectName.StartsWith("LeaderboardStar", System.StringComparison.Ordinal) ||
                   objectName.StartsWith("LeaderboardTitlePlate", System.StringComparison.Ordinal);
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

            StyleLeaderboardListSurface(entriesLabel.transform.parent);

            var scrollView = new GameObject("LeaderboardCardScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollView.transform.SetParent(entriesLabel.transform.parent, false);
            var scrollRectTransform = scrollView.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0f, 0f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.offsetMin = new Vector2(0f, 0f);
            scrollRectTransform.offsetMax = new Vector2(0f, 0f);
            var scrollImage = scrollView.GetComponent<Image>();
            scrollImage.color = new Color(0.09f, 0.05f, 0.27f, 0.82f);

            var scrollOutline = scrollView.AddComponent<Outline>();
            scrollOutline.effectColor = new Color(0.95f, 0.74f, 0.23f, 0.20f);
            scrollOutline.effectDistance = new Vector2(2f, -2f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollView.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-8f, -8f);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.03f);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            cardListContainer = content.GetComponent<RectTransform>();
            cardListContainer.anchorMin = new Vector2(0f, 1f);
            cardListContainer.anchorMax = new Vector2(1f, 1f);
            cardListContainer.pivot = new Vector2(0.5f, 1f);
            cardListContainer.anchoredPosition = Vector2.zero;
            cardListContainer.sizeDelta = Vector2.zero;

            var contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 12f;
            contentLayout.padding = new RectOffset(12, 12, 12, 28);
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

        private void StyleLeaderboardListSurface(Transform listSurface)
        {
            if (listSurface == null)
            {
                return;
            }

            var image = listSurface.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.08f, 0.04f, 0.25f, 0.84f);
            }

            var outline = listSurface.GetComponent<Outline>();
            if (outline == null)
            {
                outline = listSurface.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.30f, 0.95f, 1f, 0.18f);
            outline.effectDistance = new Vector2(2f, -2f);
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
            ResetMotionTargets();
            CreatePlayerSummaryCard(response);
            if (response.entries == null || response.entries.Length == 0)
            {
                CreateLeaderboardFillerCard(
                    string.IsNullOrWhiteSpace(response.note) ? "Play Quick Clash to appear on the board." : response.note,
                    string.Empty);
                return;
            }

            CreateSectionHeader("Top Quick Clash");
            foreach (var entry in response.entries)
            {
                CreateLeaderboardCard(entry, entry.isCurrentUser);
            }

            var nearbyEntries = FilterDuplicateNearbyEntries(response);
            if (nearbyEntries.Length > 0)
            {
                CreateSectionHeader("Your Area");
                foreach (var entry in nearbyEntries)
                {
                    CreateLeaderboardCard(entry, entry.isCurrentUser);
                }
            }

            var visibleRows = response.entries.Length + nearbyEntries.Length;
            if (visibleRows <= 3)
            {
                CreateLeaderboardFillerCard(
                    "Play Quick Clash to defend your rank.",
                    string.Empty);
            }
        }

        private void CreatePlayerSummaryCard(GetLeaderboardResponseDto response)
        {
            if (cardListContainer == null)
            {
                return;
            }

            var card = new GameObject("PlayerRankSummaryCard", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Outline));
            card.transform.SetParent(cardListContainer, false);

            var layout = card.GetComponent<LayoutElement>();
            layout.preferredHeight = 188f;
            layout.minHeight = 178f;

            var image = card.GetComponent<Image>();
            image.color = new Color(0.34f, 0.14f, 0.68f, 0.98f);

            var outline = card.GetComponent<Outline>();
            outline.effectColor = new Color(0.33f, 0.98f, 1f, 0.38f);
            outline.effectDistance = new Vector2(2f, -2f);

            var rect = card.GetComponent<RectTransform>();
            rowMotionTargets.Add(rect);

            var glow = RescueStickerFactory.CreateBlob(
                rect,
                "SummaryGlow",
                RescueStickerFactory.Palette.SunnyYellow,
                new Vector2(128f, 16f),
                new Vector2(168f, 88f),
                0.06f);
            glow.transform.SetAsFirstSibling();

            CreateAnchoredText(
                rect,
                "SummaryTitle",
                "Quick Clash Ranks",
                UiThemeRuntime.Theme.BodyFontSize + 2,
                FontStyle.Bold,
                new Color(1f, 0.90f, 0.30f, 1f),
                TextAnchor.UpperLeft,
                Vector2.zero,
                Vector2.one,
                new Vector2(22f, 144f),
                new Vector2(-20f, -14f));

            if (response?.myEntry != null)
            {
                CreateAnchoredText(
                    rect,
                    "SummaryRankLabel",
                    "YOUR RANK",
                    UiThemeRuntime.Theme.BodyFontSize - 1,
                    FontStyle.Bold,
                    new Color(0.84f, 1f, 1f, 0.86f),
                    TextAnchor.MiddleLeft,
                    Vector2.zero,
                    Vector2.zero,
                    new Vector2(24f, 114f),
                    new Vector2(168f, 138f));

                var rankBadge = CreateSummaryRankBadge(rect, response.myEntry.rank);
                rankBadgeTargets.Add(rankBadge);
                currentUserRankBadgeTarget = rankBadge;

                var scoreChip = CreateSummaryPointsPill(rect, GetVisiblePoints(response.myEntry));
                scoreChipTargets.Add(scoreChip);
                currentUserScoreChipTarget = scoreChip;

                CreateAnchoredText(
                    rect,
                    "SummarySubtitle",
                    "Keep climbing the board.",
                    UiThemeRuntime.Theme.BodyFontSize,
                    FontStyle.Bold,
                    new Color(0.84f, 1f, 1f, 0.92f),
                    TextAnchor.LowerLeft,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(24f, 12f),
                    new Vector2(-24f, -150f));
            }
            else
            {
                CreateAnchoredText(
                    rect,
                    "SummaryFallback",
                    "Your rank appears after your first Quick Clash match.",
                    UiThemeRuntime.Theme.BodyFontSize,
                    FontStyle.Bold,
                    new Color(0.84f, 1f, 1f, 0.92f),
                    TextAnchor.MiddleLeft,
                    Vector2.zero,
                    Vector2.one,
                    new Vector2(22f, 28f),
                    new Vector2(-22f, -54f));
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
            rowMotionTargets.Add(headerObject.GetComponent<RectTransform>());
            var layout = headerObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 38f;
            layout.minHeight = 38f;

            var label = headerObject.GetComponent<Text>();
            label.text = title;
            label.font = summaryLabel != null ? summaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = UiThemeRuntime.Theme.BodyFontSize + 1;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(1f, 0.88f, 0.32f, 1f);
            label.alignment = TextAnchor.MiddleLeft;
        }

        private void CreateLeaderboardCard(LeaderboardEntryDto entry, bool isCurrentUser)
        {
            if (cardListContainer == null || entry == null)
            {
                return;
            }

            var card = new GameObject("LeaderboardEntryCard", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Outline));
            card.transform.SetParent(cardListContainer, false);

            var layoutElement = card.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 88f;
            layoutElement.minHeight = 84f;

            var image = card.GetComponent<Image>();
            image.color = GetLeaderboardRowColor(entry.rank, isCurrentUser);

            var outline = card.GetComponent<Outline>();
            outline.effectColor = GetLeaderboardAccentColor(entry.rank, isCurrentUser);
            outline.effectDistance = isCurrentUser ? new Vector2(3f, -3f) : new Vector2(1.5f, -1.5f);

            var cardRect = card.GetComponent<RectTransform>();
            rowMotionTargets.Add(cardRect);
            if (isCurrentUser || entry.rank <= 3)
            {
                var glow = RescueStickerFactory.CreateBlob(
                    cardRect,
                    "RowGlow",
                    GetLeaderboardAccentColor(entry.rank, isCurrentUser),
                    new Vector2(-120f, 0f),
                    new Vector2(190f, 74f),
                    isCurrentUser ? 0.16f : 0.06f);
                glow.transform.SetAsFirstSibling();
            }

            if (isCurrentUser)
            {
                currentUserRowTarget = cardRect;
            }

            var rankBadge = CreateRankBadge(cardRect, entry.rank, isCurrentUser);
            rankBadgeTargets.Add(rankBadge);
            if (isCurrentUser)
            {
                currentUserRankBadgeTarget = rankBadge;
            }

            CreateAnchoredText(
                cardRect,
                "PlayerName",
                GetLeaderboardDisplayName(entry),
                UiThemeRuntime.Theme.BodyFontSize + 2,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleLeft,
                new Vector2(0f, 0f),
                new Vector2(1f, 1f),
                new Vector2(94f, 18f),
                new Vector2(isCurrentUser ? -210f : -148f, -18f));

            if (isCurrentUser)
            {
                CreateYouChip(cardRect);
            }

            var scoreChip = CreatePointsPill(cardRect, GetVisiblePoints(entry));
            scoreChipTargets.Add(scoreChip);
            if (isCurrentUser)
            {
                currentUserScoreChipTarget = scoreChip;
            }
        }

        private void CreateLeaderboardFillerCard(string title, string subtitle)
        {
            if (cardListContainer == null)
            {
                return;
            }

            var card = new GameObject("LeaderboardFillerCard", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Outline));
            card.transform.SetParent(cardListContainer, false);
            rowMotionTargets.Add(card.GetComponent<RectTransform>());

            var layout = card.GetComponent<LayoutElement>();
            layout.preferredHeight = 64f;
            layout.minHeight = 60f;

            var image = card.GetComponent<Image>();
            image.color = new Color(0.09f, 0.14f, 0.34f, 0.58f);
            var outline = card.GetComponent<Outline>();
            outline.effectColor = new Color(0.28f, 0.91f, 1f, 0.12f);
            outline.effectDistance = new Vector2(1f, -1f);

            var rect = card.GetComponent<RectTransform>();
            CreateAnchoredText(
                rect,
                "FillerMessage",
                string.IsNullOrWhiteSpace(title) ? subtitle : title,
                UiThemeRuntime.Theme.BodyFontSize + 1,
                FontStyle.Bold,
                new Color(0.88f, 1f, 1f, 0.86f),
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                new Vector2(18f, 10f),
                new Vector2(-18f, -10f));
        }

        private static int GetVisiblePoints(LeaderboardEntryDto entry)
        {
            if (entry == null)
            {
                return 0;
            }

            return entry.visiblePoints != 0 || entry.visible_points == 0 ? entry.visiblePoints : entry.visible_points;
        }

        private static LeaderboardEntryDto[] FilterDuplicateNearbyEntries(GetLeaderboardResponseDto response)
        {
            if (response?.nearbyEntries == null || response.nearbyEntries.Length == 0)
            {
                return new LeaderboardEntryDto[0];
            }

            var topKeys = new HashSet<string>();
            if (response.entries != null)
            {
                foreach (var entry in response.entries)
                {
                    var key = GetEntryKey(entry);
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        topKeys.Add(key);
                    }
                }
            }

            return response.nearbyEntries
                .Where(entry => !topKeys.Contains(GetEntryKey(entry)))
                .ToArray();
        }

        private static string GetEntryKey(LeaderboardEntryDto entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(entry.userId))
            {
                return entry.userId;
            }

            if (!string.IsNullOrWhiteSpace(entry.user_id))
            {
                return entry.user_id;
            }

            return entry.rank.ToString();
        }

        private static string GetLeaderboardDisplayName(LeaderboardEntryDto entry)
        {
            if (entry == null)
            {
                return "Challenger";
            }

            if (entry.isCurrentUser)
            {
                return "You";
            }

            var raw = !string.IsNullOrWhiteSpace(entry.displayName) ? entry.displayName : entry.display_handle;
            if (string.IsNullOrWhiteSpace(raw) || LooksLikeIdFallback(raw))
            {
                return $"Challenger #{entry.rank}";
            }

            return raw.Trim();
        }

        private static bool LooksLikeIdFallback(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return value.Trim().StartsWith("Player-", System.StringComparison.OrdinalIgnoreCase);
        }

        private static Color GetLeaderboardAccentColor(int rank, bool isCurrentUser)
        {
            if (isCurrentUser)
            {
                return new Color(0.30f, 0.98f, 1f, 0.86f);
            }

            return rank switch
            {
                1 => new Color(1f, 0.82f, 0.20f, 0.48f),
                2 => new Color(0.38f, 0.95f, 1f, 0.38f),
                3 => new Color(1f, 0.38f, 0.78f, 0.34f),
                _ => new Color(0.54f, 0.36f, 1f, 0.14f),
            };
        }

        private static Color GetLeaderboardRowColor(int rank, bool isCurrentUser)
        {
            if (isCurrentUser)
            {
                return new Color(0.45f, 0.21f, 0.78f, 0.98f);
            }

            return rank switch
            {
                1 => new Color(0.19f, 0.13f, 0.40f, 0.98f),
                2 => new Color(0.08f, 0.23f, 0.42f, 0.96f),
                3 => new Color(0.20f, 0.13f, 0.38f, 0.96f),
                _ => new Color(0.08f, 0.15f, 0.33f, 0.94f),
            };
        }

        private RectTransform CreateSummaryRankBadge(RectTransform parent, int rank)
        {
            var badge = new GameObject("SummaryRankBadge", typeof(RectTransform), typeof(Image), typeof(Outline));
            badge.transform.SetParent(parent, false);
            var rect = badge.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(24f, 44f);
            rect.sizeDelta = new Vector2(136f, 66f);

            badge.GetComponent<Image>().color = new Color(1f, 0.82f, 0.20f, 1f);
            var outline = badge.GetComponent<Outline>();
            outline.effectColor = new Color(0.22f, 0.10f, 0.36f, 0.52f);
            outline.effectDistance = new Vector2(3f, -3f);

            CreateAnchoredText(
                rect,
                "SummaryRankText",
                $"#{rank}",
                UiThemeRuntime.Theme.HeadingFontSize,
                FontStyle.Bold,
                new Color(0.18f, 0.08f, 0.34f, 1f),
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            return rect;
        }

        private RectTransform CreateSummaryPointsPill(RectTransform parent, int points)
        {
            var pill = new GameObject("SummaryPointsPill", typeof(RectTransform), typeof(Image), typeof(Outline));
            pill.transform.SetParent(parent, false);
            var rect = pill.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-24f, 56f);
            rect.sizeDelta = new Vector2(140f, 58f);

            pill.GetComponent<Image>().color = new Color(0.22f, 0.95f, 1f, 1f);
            var outline = pill.GetComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.06f, 0.28f, 0.55f);
            outline.effectDistance = new Vector2(3f, -3f);

            CreateAnchoredText(
                rect,
                "SummaryPointsText",
                $"{points} pts",
                UiThemeRuntime.Theme.BodyFontSize + 3,
                FontStyle.Bold,
                new Color(0.14f, 0.08f, 0.33f, 1f),
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            return rect;
        }

        private RectTransform CreateRankBadge(RectTransform parent, int rank, bool isCurrentUser)
        {
            var badge = new GameObject("RankBadge", typeof(RectTransform), typeof(Image), typeof(Outline));
            badge.transform.SetParent(parent, false);
            var rect = badge.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(43f, 0f);
            rect.sizeDelta = new Vector2(62f, 50f);

            badge.GetComponent<Image>().color = rank switch
            {
                1 => new Color(1f, 0.82f, 0.20f, 1f),
                2 => new Color(0.22f, 0.94f, 1f, 0.96f),
                3 => new Color(1f, 0.42f, 0.78f, 0.96f),
                _ => isCurrentUser ? new Color(1f, 0.82f, 0.22f, 1f) : new Color(0.13f, 0.86f, 1f, 0.92f),
            };

            var outline = badge.GetComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.45f);
            outline.effectDistance = new Vector2(2f, -2f);

            CreateAnchoredText(
                rect,
                "RankText",
                $"#{rank}",
                UiThemeRuntime.Theme.BodyFontSize,
                FontStyle.Bold,
                new Color(0.12f, 0.07f, 0.34f, 1f),
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            return rect;
        }

        private RectTransform CreatePointsPill(RectTransform parent, int points)
        {
            var pill = new GameObject("PointsPill", typeof(RectTransform), typeof(Image), typeof(Outline));
            pill.transform.SetParent(parent, false);
            var rect = pill.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-16f, 0f);
            rect.sizeDelta = new Vector2(124f, 46f);

            pill.GetComponent<Image>().color = new Color(1f, 0.79f, 0.18f, 1f);
            var outline = pill.GetComponent<Outline>();
            outline.effectColor = new Color(0.34f, 0.17f, 0.02f, 0.45f);
            outline.effectDistance = new Vector2(2f, -2f);

            CreateAnchoredText(
                rect,
                "PointsText",
                $"{points} pts",
                UiThemeRuntime.Theme.BodyFontSize,
                FontStyle.Bold,
                new Color(0.22f, 0.11f, 0.38f, 1f),
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            return rect;
        }

        private void CreateYouChip(RectTransform parent)
        {
            var chip = new GameObject("YouChip", typeof(RectTransform), typeof(Image), typeof(Outline));
            chip.transform.SetParent(parent, false);
            var rect = chip.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-150f, 0f);
            rect.sizeDelta = new Vector2(54f, 34f);

            chip.GetComponent<Image>().color = new Color(1f, 0.88f, 0.28f, 1f);
            var outline = chip.GetComponent<Outline>();
            outline.effectColor = new Color(0.15f, 0.06f, 0.28f, 0.45f);
            outline.effectDistance = new Vector2(2f, -2f);

            CreateAnchoredText(
                rect,
                "YouText",
                "YOU",
                UiThemeRuntime.Theme.BodyFontSize - 1,
                FontStyle.Bold,
                new Color(0.18f, 0.08f, 0.34f, 1f),
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);
        }

        private void CreateAnchoredText(
            RectTransform parent,
            string objectName,
            string textValue,
            int fontSize,
            FontStyle style,
            Color color,
            TextAnchor alignment,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            var labelObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;

            var label = labelObject.GetComponent<Text>();
            label.text = textValue;
            label.font = summaryLabel != null ? summaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private void StyleBackButton()
        {
            if (backButton == null)
            {
                return;
            }

            var rect = backButton.transform as RectTransform;
            if (rect != null && rect.anchorMin.y <= 0.05f && rect.anchorMax.y <= 0.20f)
            {
                rect.offsetMin = new Vector2(rect.offsetMin.x, Mathf.Max(rect.offsetMin.y, 28f));
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Max(rect.sizeDelta.y, 58f));
            }

            var image = backButton.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.18f, 0.66f, 1f, 0.98f);
            }

            var outline = backButton.GetComponent<Outline>();
            if (outline == null)
            {
                outline = backButton.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.96f, 0.78f, 0.22f, 0.28f);
            outline.effectDistance = new Vector2(2f, -2f);

            var label = backButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "Back Home";
                label.fontStyle = FontStyle.Bold;
                label.color = Color.white;
                label.fontSize = UiThemeRuntime.Theme.BodyFontSize + 3;
            }
        }

        private void RenderSingleInfoCard(string details)
        {
            if (cardListContainer == null)
            {
                return;
            }

            ClearCards();
            ResetMotionTargets();
            var card = new GameObject("LeaderboardInfoCard", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            card.transform.SetParent(cardListContainer, false);
            rowMotionTargets.Add(card.GetComponent<RectTransform>());
            var layout = card.GetComponent<LayoutElement>();
            layout.preferredHeight = 150f;
            layout.minHeight = 130f;
            card.GetComponent<Image>().color = new Color(0.13f, 0.09f, 0.33f, 0.88f);
            var outline = card.GetComponent<Outline>();
            if (outline == null)
            {
                outline = card.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.30f, 0.95f, 1f, 0.32f);
            outline.effectDistance = new Vector2(2f, -2f);

            var textObject = new GameObject("InfoText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(card.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 10f);
            textRect.offsetMax = new Vector2(-14f, -10f);

            var text = textObject.GetComponent<Text>();
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

        private void ResetMotionTargets()
        {
            rowMotionTargets.Clear();
            rankBadgeTargets.Clear();
            scoreChipTargets.Clear();
            currentUserRowTarget = null;
            currentUserRankBadgeTarget = null;
            currentUserScoreChipTarget = null;
        }

        private void PlayLeaderboardEntrance(GetLeaderboardResponseDto response)
        {
            var key = BuildEntranceKey(response);
            if (string.Equals(lastEntranceKey, key, System.StringComparison.Ordinal))
            {
                return;
            }

            lastEntranceKey = key;
            CancelEntranceMotion();
            PrepareLeaderboardEntrance();
            entranceRoutine = StartCoroutine(PlayLeaderboardEntranceRoutine());
        }

        private void PlayInfoStateEntrance()
        {
            CancelEntranceMotion();
            PrepareLeaderboardEntrance();
            entranceRoutine = StartCoroutine(PlayLeaderboardEntranceRoutine());
        }

        private void PrepareLeaderboardEntrance()
        {
            PrepareMotionTarget(summaryLabel != null ? summaryLabel.transform as RectTransform : null);
            PrepareMotionTarget(backButton != null ? backButton.transform as RectTransform : null);
            foreach (var row in rowMotionTargets)
            {
                PrepareMotionTarget(row);
            }
        }

        private IEnumerator PlayLeaderboardEntranceRoutine()
        {
            var summaryRect = summaryLabel != null ? summaryLabel.transform as RectTransform : null;
            if (summaryRect != null)
            {
                NativeMotionKit.StampSlam(this, summaryRect, 1.06f, 0.20f);
                var group = EnsureCanvasGroup(summaryRect);
                if (group != null)
                {
                    group.alpha = 1f;
                }
            }

            yield return new WaitForSecondsRealtime(0.08f);

            for (var index = 0; index < rowMotionTargets.Count; index++)
            {
                var row = rowMotionTargets[index];
                if (row == null)
                {
                    continue;
                }

                NativeMotionKit.SlideFadeIn(this, row, EnsureCanvasGroup(row), new Vector2(0f, -18f), 0.18f);
                yield return new WaitForSecondsRealtime(0.04f);
            }

            foreach (var badge in rankBadgeTargets)
            {
                if (badge != null)
                {
                    NativeMotionKit.StampSlam(this, badge, 1.10f, 0.15f);
                }
            }

            foreach (var chip in scoreChipTargets)
            {
                if (chip != null)
                {
                    NativeMotionKit.PunchScale(this, chip, chip == currentUserScoreChipTarget ? 0.10f : 0.065f, 0.16f);
                }
            }

            if (currentUserRowTarget != null)
            {
                NativeMotionKit.PunchScale(this, currentUserRowTarget, 0.035f, 0.18f);
                var rowImage = currentUserRowTarget.GetComponent<Image>();
                if (rowImage != null)
                {
                    yield return FlashGraphicOnce(
                        rowImage,
                        new Color(0.44f, 0.22f, 0.74f, 0.96f),
                        new Color(0.24f, 0.74f, 0.78f, 0.98f),
                        0.30f);
                }
            }

            if (currentUserRankBadgeTarget != null)
            {
                NativeMotionKit.StampSlam(this, currentUserRankBadgeTarget, 1.15f, 0.18f);
            }

            var backRect = backButton != null ? backButton.transform as RectTransform : null;
            if (backRect != null)
            {
                NativeMotionKit.SlideFadeIn(this, backRect, EnsureCanvasGroup(backRect), new Vector2(0f, -14f), 0.18f);
            }

            entranceRoutine = null;
        }

        private void CancelEntranceMotion()
        {
            if (entranceRoutine != null)
            {
                StopCoroutine(entranceRoutine);
                entranceRoutine = null;
            }
        }

        private static void PrepareMotionTarget(RectTransform target)
        {
            if (target == null)
            {
                return;
            }

            var group = EnsureCanvasGroup(target);
            if (group != null)
            {
                group.alpha = 0f;
            }

            target.localScale = Vector3.one;
        }

        private static CanvasGroup EnsureCanvasGroup(Component target)
        {
            if (target == null)
            {
                return null;
            }

            var group = target.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = target.gameObject.AddComponent<CanvasGroup>();
            }

            return group;
        }

        private static string BuildEntranceKey(GetLeaderboardResponseDto response)
        {
            if (response == null)
            {
                return "empty";
            }

            var builder = new StringBuilder();
            builder.Append(response.totalRatedPlayers).Append('|');
            if (response.myEntry != null)
            {
                builder.Append(response.myEntry.rank).Append(':').Append(GetVisiblePoints(response.myEntry)).Append('|');
            }

            AppendEntryKeys(builder, response.entries);
            builder.Append('|');
            AppendEntryKeys(builder, response.nearbyEntries);
            return builder.ToString();
        }

        private static void AppendEntryKeys(StringBuilder builder, LeaderboardEntryDto[] entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (var entry in entries)
            {
                builder.Append(entry.rank)
                    .Append(':')
                    .Append(GetLeaderboardDisplayName(entry))
                    .Append(':')
                    .Append(GetVisiblePoints(entry))
                    .Append(';');
            }
        }

        private static IEnumerator FlashGraphicOnce(Graphic graphic, Color from, Color to, float duration)
        {
            if (graphic == null)
            {
                yield break;
            }

            var safeDuration = Mathf.Max(0.05f, duration);
            var elapsed = 0f;
            while (elapsed < safeDuration && graphic != null)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / safeDuration);
                var wave = Mathf.Sin(t * Mathf.PI);
                graphic.color = Color.Lerp(from, to, wave);
                yield return null;
            }

            if (graphic != null)
            {
                graphic.color = from;
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
