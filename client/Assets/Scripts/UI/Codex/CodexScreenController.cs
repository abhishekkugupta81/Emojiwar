using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EmojiWar.Client.Core;
using EmojiWar.Client.Gameplay.Contracts;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using EmojiWar.Client.UI.Common;

namespace EmojiWar.Client.UI.Codex
{
    public sealed class CodexScreenController : MonoBehaviour
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
        private readonly List<RectTransform> cardMotionTargets = new();
        private readonly List<RectTransform> badgeMotionTargets = new();
        private RectTransform latestCardTarget;
        private RectTransform latestChipTarget;

        private void Awake()
        {
            AutoWireSceneReferences();
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
            SetView("Loading Codex...", "Checking your unlocked interactions.");

            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null || bootstrap.SupabaseConfig == null || !bootstrap.SupabaseConfig.IsConfigured)
            {
                SetView("Codex unavailable.", "Open the game through the Bootstrap scene with Supabase configured.");
                yield break;
            }

            if (!bootstrap.SessionState.HasSession)
            {
                SetView("Codex unavailable.", "Guest session is missing. Relaunch through Bootstrap.");
                yield break;
            }

            var payload = JsonUtility.ToJson(new GetCodexRequestDto
            {
                limit = 50,
            });

            using var request = bootstrap.FunctionClient.BuildJsonRequest("get_codex", payload, bootstrap.SessionState.AccessToken);
            request.timeout = 8;
            if (!TryBeginWebRequest(request, out var operation, out var startError))
            {
                SetView("Codex lookup failed.", startError);
                yield break;
            }

            yield return operation;

            if (request.result != UnityWebRequest.Result.Success)
            {
                SetView("Codex lookup failed.", request.error ?? "Unknown network error.");
                yield break;
            }

            var response = JsonUtility.FromJson<GetCodexResponseDto>(request.downloadHandler.text);
            if (response == null)
            {
                SetView("Codex lookup failed.", "Could not parse Codex response.");
                yield break;
            }

            if (response.entries == null || response.entries.Length == 0)
            {
                SetView("Codex", string.IsNullOrWhiteSpace(response.note)
                    ? "No Codex entries yet.\nPlay Quick Clash to discover interactions."
                    : response.note);
                yield break;
            }

            SetView(BuildSummary(response), string.Empty);
            RenderEntries(response.entries, response.latestEntry);
            PlayCodexEntrance(response);
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
                }
                else
                {
                    ClearCards();
                    ResetMotionTargets();
                }
            }
        }

        private static string BuildSummary(GetCodexResponseDto response)
        {
            if (response.latestEntry != null && !string.IsNullOrWhiteSpace(response.latestEntry.interaction_key))
            {
                return
                    $"Codex\n" +
                    $"Unlocked {response.totalUnlocked} interaction{(response.totalUnlocked == 1 ? string.Empty : "s")}\n" +
                    $"Latest: {FormatInteractionKey(response.latestEntry.interaction_key)}";
            }

            return
                $"Codex\n" +
                $"Unlocked {response.totalUnlocked} interaction{(response.totalUnlocked == 1 ? string.Empty : "s")}";
        }

        private static string BuildEntriesText(CodexUnlockEntryDto[] entries, CodexUnlockEntryDto latestEntry)
        {
            var builder = new StringBuilder();
            if (latestEntry != null && !string.IsNullOrWhiteSpace(latestEntry.interaction_key))
            {
                builder.AppendLine("LATEST UNLOCK");
                builder.AppendLine(FormatInteractionKey(latestEntry.interaction_key));
                builder.Append("Tip: ").AppendLine(latestEntry.tip);
                builder.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
                builder.AppendLine();
            }

            builder.AppendLine("UNLOCKED ENTRIES");
            builder.AppendLine();

            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];

                builder.Append(index + 1)
                    .Append(") ")
                    .AppendLine(FormatInteractionKey(entry.interaction_key));
                builder.AppendLine(entry.summary);
                builder.Append("Tip: ").AppendLine(entry.tip);

                if (!string.IsNullOrWhiteSpace(entry.unlocked_at))
                {
                    builder.Append("Unlocked: ").AppendLine(FormatTimestamp(entry.unlocked_at));
                }

                if (index < entries.Length - 1)
                {
                    builder.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━");
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        private void AutoWireSceneReferences()
        {
            if (summaryLabel == null)
            {
                summaryLabel = GameObject.Find("CodexSummary")?.GetComponent<Text>();
            }

            if (entriesLabel == null)
            {
                entriesLabel = GameObject.Find("CodexEntries")?.GetComponent<Text>();
            }

            if (backButton == null)
            {
                backButton = FindObjectsOfType<Button>(true)
                    .FirstOrDefault(button => button.name.IndexOf("Back", StringComparison.OrdinalIgnoreCase) >= 0);
            }

            if (panelBackground == null)
            {
                panelBackground = GameObject.Find("CodexPanel")?.GetComponent<Image>();
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

            EnsureRuntimeTheme();
            StyleCodexListSurface(entriesLabel.transform.parent);

            var scrollView = new GameObject("CodexCardScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollView.transform.SetParent(entriesLabel.transform.parent, false);
            var scrollRectTransform = scrollView.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0f, 0f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.offsetMin = new Vector2(0f, 0f);
            scrollRectTransform.offsetMax = new Vector2(0f, 0f);

            var scrollImage = scrollView.GetComponent<Image>();
            scrollImage.color = new Color(0.08f, 0.04f, 0.25f, 0.72f);

            var scrollOutline = scrollView.AddComponent<Outline>();
            scrollOutline.effectColor = new Color(0.30f, 0.95f, 1f, 0.18f);
            scrollOutline.effectDistance = new Vector2(2f, -2f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollView.transform, false);
            var viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-8f, -8f);
            viewport.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.02f);

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
            contentLayout.padding = new RectOffset(10, 10, 10, 24);
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

        private void EnsureRuntimeTheme()
        {
            if (runtimeThemeInitialized || panelBackground == null)
            {
                return;
            }

            runtimeThemeInitialized = true;
            panelBackground.color = new Color(0.10f, 0.06f, 0.28f, 1f);

            if (panelBackground.transform.Find("CodexStickerPopGradient") == null)
            {
                var gradient = RescueStickerFactory.CreateGradientLikeBackground(
                    panelBackground.transform,
                    "CodexStickerPopGradient",
                    RescueStickerFactory.Palette.DeepIndigo,
                    RescueStickerFactory.Palette.Aqua);
                gradient.raycastTarget = false;
                gradient.transform.SetSiblingIndex(0);
            }

            CreateThemeGlow("CodexGlowTop", new Vector2(0f, 280f), new Vector2(500f, 180f), RescueStickerFactory.Palette.ElectricPurple, 0.16f);
            CreateThemeGlow("CodexGlowMid", new Vector2(170f, 70f), new Vector2(310f, 230f), RescueStickerFactory.Palette.HotPink, 0.09f);
            CreateThemeGlow("CodexGlowBottom", new Vector2(-150f, -300f), new Vector2(380f, 240f), RescueStickerFactory.Palette.Mint, 0.08f);
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

        private static void StyleCodexListSurface(Transform listSurface)
        {
            if (listSurface == null)
            {
                return;
            }

            var image = listSurface.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.08f, 0.04f, 0.25f, 0.80f);
            }

            var outline = listSurface.GetComponent<Outline>();
            if (outline == null)
            {
                outline = listSurface.gameObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.30f, 0.95f, 1f, 0.18f);
            outline.effectDistance = new Vector2(2f, -2f);
        }

        private void RenderEntries(CodexUnlockEntryDto[] entries, CodexUnlockEntryDto latestEntry)
        {
            if (cardListContainer == null)
            {
                if (entriesLabel != null)
                {
                    entriesLabel.text = BuildEntriesText(entries, latestEntry);
                }

                return;
            }

            ClearCards();
            ResetMotionTargets();
            if (latestEntry != null && !string.IsNullOrWhiteSpace(latestEntry.interaction_key))
            {
                CreateLatestUnlockCard(latestEntry);
            }

            CreateSectionHeader("Discovered Moments");
            for (var index = 0; index < entries.Length; index++)
            {
                CreateEntryCard(index + 1, entries[index]);
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
            CreateInfoCard(string.IsNullOrWhiteSpace(details) ? "No details available." : details);
            PlayInfoEntrance();
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
            cardMotionTargets.Clear();
            badgeMotionTargets.Clear();
            latestCardTarget = null;
            latestChipTarget = null;
        }

        private void CreateSectionHeader(string title)
        {
            if (cardListContainer == null)
            {
                return;
            }

            var headerObject = new GameObject("CodexSectionHeader", typeof(RectTransform), typeof(LayoutElement), typeof(Text));
            headerObject.transform.SetParent(cardListContainer, false);
            var rect = headerObject.GetComponent<RectTransform>();
            cardMotionTargets.Add(rect);

            var layout = headerObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 34f;
            layout.minHeight = 34f;

            var label = headerObject.GetComponent<Text>();
            label.text = title;
            label.font = summaryLabel != null ? summaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = UiThemeRuntime.Theme.BodyFontSize + 1;
            label.fontStyle = FontStyle.Bold;
            label.color = new Color(1f, 0.88f, 0.32f, 1f);
            label.alignment = TextAnchor.MiddleLeft;
        }

        private void CreateLatestUnlockCard(CodexUnlockEntryDto entry)
        {
            if (cardListContainer == null || entry == null)
            {
                return;
            }

            var card = new GameObject("CodexLatestUnlockCard", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Outline));
            card.transform.SetParent(cardListContainer, false);

            var layoutElement = card.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 300f;
            layoutElement.minHeight = 280f;

            var image = card.GetComponent<Image>();
            image.color = new Color(0.42f, 0.18f, 0.74f, 0.98f);
            var outline = card.GetComponent<Outline>();
            outline.effectColor = new Color(0.32f, 0.98f, 1f, 0.48f);
            outline.effectDistance = new Vector2(2f, -2f);

            var rect = card.GetComponent<RectTransform>();
            latestCardTarget = rect;
            cardMotionTargets.Add(rect);

            var glow = RescueStickerFactory.CreateBlob(
                rect,
                "LatestGlow",
                RescueStickerFactory.Palette.SunnyYellow,
                new Vector2(144f, 42f),
                new Vector2(180f, 104f),
                0.05f);
            glow.transform.SetAsFirstSibling();

            latestChipTarget = CreateChip(rect, "LATEST", new Vector2(18f, -16f), new Vector2(88f, 34f), RescueStickerFactory.Palette.SunnyYellow);
            badgeMotionTargets.Add(latestChipTarget);

            CreateAnchoredText(rect, "LatestTitle", FormatInteractionKey(entry.interaction_key), UiThemeRuntime.Theme.BodyFontSize + 4, FontStyle.Bold, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(18f, 212f), new Vector2(-18f, -40f));
            CreateAnchoredText(rect, "LatestSummary", SafeText(entry.summary, "New interaction discovered."), UiThemeRuntime.Theme.BodyFontSize, FontStyle.Bold, new Color(0.88f, 1f, 1f, 0.94f), TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(18f, 126f), new Vector2(-18f, -104f));
            CreateAnchoredText(rect, "LatestTip", $"Tip: {SafeText(entry.tip, "Try this interaction in Quick Clash.")}", UiThemeRuntime.Theme.ChipFontSize + 2, FontStyle.Normal, new Color(1f, 0.92f, 0.62f, 0.96f), TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(18f, 24f), new Vector2(-18f, -188f));
        }

        private void CreateEntryCard(int entryNumber, CodexUnlockEntryDto entry)
        {
            if (cardListContainer == null || entry == null)
            {
                return;
            }

            var hasUnlockedAt = !string.IsNullOrWhiteSpace(entry.unlocked_at);
            var card = new GameObject("CodexEntryCard", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Outline));
            card.transform.SetParent(cardListContainer, false);

            var layoutElement = card.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = hasUnlockedAt ? 310f : 270f;
            layoutElement.minHeight = hasUnlockedAt ? 292f : 252f;

            var image = card.GetComponent<Image>();
            image.color = new Color(0.09f, 0.15f, 0.34f, 0.94f);
            var outline = card.GetComponent<Outline>();
            outline.effectColor = new Color(0.54f, 0.36f, 1f, 0.18f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var rect = card.GetComponent<RectTransform>();
            cardMotionTargets.Add(rect);

            var badge = CreateChip(rect, $"#{entryNumber}", new Vector2(14f, -14f), new Vector2(58f, 34f), RescueStickerFactory.Palette.Aqua);
            badgeMotionTargets.Add(badge);

            CreateAnchoredText(rect, "EntryTitle", FormatInteractionKey(entry.interaction_key), UiThemeRuntime.Theme.BodyFontSize + 2, FontStyle.Bold, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(82f, hasUnlockedAt ? 250f : 214f), new Vector2(-16f, -18f));
            CreateAnchoredText(rect, "EntrySummary", SafeText(entry.summary, "Interaction discovered."), UiThemeRuntime.Theme.BodyFontSize, FontStyle.Normal, new Color(0.88f, 1f, 1f, 0.92f), TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(16f, hasUnlockedAt ? 160f : 128f), new Vector2(-16f, hasUnlockedAt ? -72f : -68f));
            CreateAnchoredText(rect, "EntryTip", $"Tip: {SafeText(entry.tip, "Try it in Quick Clash.")}", UiThemeRuntime.Theme.ChipFontSize + 2, FontStyle.Normal, new Color(1f, 0.90f, 0.56f, 0.92f), TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(16f, hasUnlockedAt ? 64f : 20f), new Vector2(-16f, hasUnlockedAt ? -162f : -154f));

            if (hasUnlockedAt)
            {
                CreateAnchoredText(rect, "EntryUnlocked", $"Unlocked: {FormatTimestamp(entry.unlocked_at)}", UiThemeRuntime.Theme.ChipFontSize, FontStyle.Normal, new Color(0.78f, 0.88f, 1f, 0.68f), TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(16f, 14f), new Vector2(-16f, -260f));
            }
        }

        private void CreateInfoCard(string details)
        {
            if (cardListContainer == null)
            {
                return;
            }

            var card = new GameObject("CodexInfoCard", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Outline));
            card.transform.SetParent(cardListContainer, false);
            var layout = card.GetComponent<LayoutElement>();
            layout.preferredHeight = 150f;
            layout.minHeight = 130f;
            card.GetComponent<Image>().color = new Color(0.13f, 0.09f, 0.33f, 0.88f);
            var outline = card.GetComponent<Outline>();
            outline.effectColor = new Color(0.30f, 0.95f, 1f, 0.28f);
            outline.effectDistance = new Vector2(2f, -2f);
            var rect = card.GetComponent<RectTransform>();
            cardMotionTargets.Add(rect);
            CreateAnchoredText(rect, "InfoText", details, UiThemeRuntime.Theme.BodyFontSize, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(18f, 12f), new Vector2(-18f, -12f));
        }

        private void CreateCardLabel(Transform parent, string name, string text, int fontSize, FontStyle style, float preferredHeight)
        {
            var labelObject = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            var layout = labelObject.GetComponent<LayoutElement>();
            layout.preferredHeight = preferredHeight;
            layout.minHeight = preferredHeight;

            var label = labelObject.GetComponent<Text>();
            label.text = text;
            label.font = summaryLabel != null ? summaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.alignment = TextAnchor.UpperLeft;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private RectTransform CreateChip(RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size, Color color)
        {
            var chip = new GameObject("CodexChip", typeof(RectTransform), typeof(Image), typeof(Outline));
            chip.transform.SetParent(parent, false);
            var rect = chip.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            chip.GetComponent<Image>().color = color;
            var outline = chip.GetComponent<Outline>();
            outline.effectColor = new Color(0.12f, 0.06f, 0.26f, 0.46f);
            outline.effectDistance = new Vector2(2f, -2f);

            CreateAnchoredText(
                rect,
                "ChipText",
                text,
                UiThemeRuntime.Theme.ChipFontSize + 1,
                FontStyle.Bold,
                new Color(0.15f, 0.07f, 0.30f, 1f),
                TextAnchor.MiddleCenter,
                Vector2.zero,
                Vector2.one,
                Vector2.zero,
                Vector2.zero);

            return rect;
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

            var image = backButton.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0.18f, 0.66f, 1f, 0.96f);
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
                label.fontSize = UiThemeRuntime.Theme.BodyFontSize + 2;
            }
        }

        private void PlayCodexEntrance(GetCodexResponseDto response)
        {
            var key = BuildEntranceKey(response);
            if (string.Equals(lastEntranceKey, key, StringComparison.Ordinal))
            {
                return;
            }

            lastEntranceKey = key;
            CancelEntranceMotion();
            PrepareEntranceTargets();
            entranceRoutine = StartCoroutine(PlayCodexEntranceRoutine());
        }

        private void PlayInfoEntrance()
        {
            CancelEntranceMotion();
            PrepareEntranceTargets();
            entranceRoutine = StartCoroutine(PlayCodexEntranceRoutine());
        }

        private void PrepareEntranceTargets()
        {
            PrepareMotionTarget(summaryLabel != null ? summaryLabel.transform as RectTransform : null);
            PrepareMotionTarget(backButton != null ? backButton.transform as RectTransform : null);
            foreach (var card in cardMotionTargets)
            {
                PrepareMotionTarget(card);
            }
        }

        private IEnumerator PlayCodexEntranceRoutine()
        {
            var summaryRect = summaryLabel != null ? summaryLabel.transform as RectTransform : null;
            if (summaryRect != null)
            {
                NativeMotionKit.StampSlam(this, summaryRect, 1.05f, 0.18f);
                var group = EnsureCanvasGroup(summaryRect);
                if (group != null)
                {
                    group.alpha = 1f;
                }
            }

            yield return new WaitForSecondsRealtime(0.07f);

            if (latestCardTarget != null)
            {
                NativeMotionKit.StampSlam(this, latestCardTarget, 1.06f, 0.20f);
                var group = EnsureCanvasGroup(latestCardTarget);
                if (group != null)
                {
                    group.alpha = 1f;
                }

                if (latestChipTarget != null)
                {
                    NativeMotionKit.StampSlam(this, latestChipTarget, 1.14f, 0.16f);
                }

                yield return new WaitForSecondsRealtime(0.10f);
            }

            for (var index = 0; index < cardMotionTargets.Count; index++)
            {
                var card = cardMotionTargets[index];
                if (card == null || card == latestCardTarget)
                {
                    continue;
                }

                NativeMotionKit.SlideFadeIn(this, card, EnsureCanvasGroup(card), new Vector2(0f, -16f), 0.18f);
                yield return new WaitForSecondsRealtime(0.04f);
            }

            foreach (var badge in badgeMotionTargets)
            {
                if (badge != null)
                {
                    NativeMotionKit.PunchScale(this, badge, 0.07f, 0.14f);
                }
            }

            var backRect = backButton != null ? backButton.transform as RectTransform : null;
            if (backRect != null)
            {
                NativeMotionKit.SlideFadeIn(this, backRect, EnsureCanvasGroup(backRect), new Vector2(0f, -12f), 0.16f);
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

        private static string BuildEntranceKey(GetCodexResponseDto response)
        {
            if (response == null)
            {
                return "empty";
            }

            var builder = new StringBuilder();
            builder.Append(response.totalUnlocked).Append('|');
            if (response.latestEntry != null)
            {
                builder.Append(response.latestEntry.interaction_key).Append('|');
            }

            if (response.entries != null)
            {
                foreach (var entry in response.entries)
                {
                    builder.Append(entry.interaction_key)
                        .Append(':')
                        .Append(entry.unlocked_at)
                        .Append(';');
                }
            }

            return builder.ToString();
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
            catch (Exception exception)
            {
                var message = exception.Message ?? string.Empty;
                if (message.IndexOf("Insecure connection", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    error = "Insecure connection not allowed. Set Player Settings > Allow downloads over HTTP to Always Allowed for local Supabase.";
                    return false;
                }

                error = $"Request start failed: {message}";
                return false;
            }
        }

        private static string FormatInteractionKey(string interactionKey)
        {
            if (string.IsNullOrWhiteSpace(interactionKey))
            {
                return "Unknown interaction";
            }

            return interactionKey
                .Replace("_", " ")
                .Replace("-", " ")
                .Trim();
        }

        private static string SafeText(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string FormatTimestamp(string timestamp)
        {
            return DateTime.TryParse(timestamp, out var value)
                ? value.ToLocalTime().ToString("MMM d, yyyy h:mm tt")
                : timestamp;
        }
    }
}
