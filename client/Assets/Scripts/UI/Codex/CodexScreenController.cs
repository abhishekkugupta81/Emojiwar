using System;
using System.Collections;
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
                SetView("No Codex entries yet.", response.note);
                yield break;
            }

            SetView(BuildSummary(response), string.Empty);
            RenderEntries(response.entries, response.latestEntry);
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
                var top = UiThemeRuntime.Theme.FormationGradient.Top;
                var bottom = UiThemeRuntime.Theme.FormationGradient.Bottom;
                panelBackground.color = Color.Lerp(top, bottom, 0.45f);
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

        private static string BuildSummary(GetCodexResponseDto response)
        {
            if (response.latestEntry != null && !string.IsNullOrWhiteSpace(response.latestEntry.interaction_key))
            {
                return
                    $"Unlocked {response.totalUnlocked} interaction{(response.totalUnlocked == 1 ? string.Empty : "s")}.\n" +
                    $"Latest: {FormatInteractionKey(response.latestEntry.interaction_key)}";
            }

            return $"Unlocked {response.totalUnlocked} interaction{(response.totalUnlocked == 1 ? string.Empty : "s")}.";
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

            var scrollView = new GameObject("CodexCardScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollView.transform.SetParent(entriesLabel.transform.parent, false);
            var scrollRectTransform = scrollView.GetComponent<RectTransform>();
            scrollRectTransform.anchorMin = new Vector2(0f, 0f);
            scrollRectTransform.anchorMax = new Vector2(1f, 1f);
            scrollRectTransform.offsetMin = new Vector2(0f, 0f);
            scrollRectTransform.offsetMax = new Vector2(0f, 0f);

            var scrollImage = scrollView.GetComponent<Image>();
            scrollImage.color = new Color(0f, 0f, 0f, 0f);

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
            if (latestEntry != null && !string.IsNullOrWhiteSpace(latestEntry.interaction_key))
            {
                CreateCard(
                    "Latest unlock",
                    $"{FormatInteractionKey(latestEntry.interaction_key)}\n{latestEntry.summary}\nTip: {latestEntry.tip}",
                    UiThemeRuntime.Theme.PrimaryCtaColor * new Color(1f, 1f, 1f, 0.28f));
            }

            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                var body = $"{entry.summary}\nTip: {entry.tip}";
                if (!string.IsNullOrWhiteSpace(entry.unlocked_at))
                {
                    body += $"\nUnlocked: {FormatTimestamp(entry.unlocked_at)}";
                }

                CreateCard(
                    $"{index + 1}. {FormatInteractionKey(entry.interaction_key)}",
                    body,
                    UiThemeRuntime.Theme.SurfaceColor * new Color(1f, 1f, 1f, 0.88f));
            }
        }

        private void RenderSingleInfoCard(string details)
        {
            if (cardListContainer == null)
            {
                return;
            }

            ClearCards();
            CreateCard(
                "Codex",
                string.IsNullOrWhiteSpace(details) ? "No details available." : details,
                UiThemeRuntime.Theme.SurfaceColor * new Color(1f, 1f, 1f, 0.88f));
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

        private void CreateCard(string title, string body, Color tint)
        {
            if (cardListContainer == null)
            {
                return;
            }

            var card = new GameObject("CodexEntryCard", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(VerticalLayoutGroup));
            card.transform.SetParent(cardListContainer, false);

            var layoutElement = card.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 190f;
            layoutElement.minHeight = 140f;

            var image = card.GetComponent<Image>();
            image.color = tint;

            var layout = card.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.padding = new RectOffset(12, 12, 10, 10);
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateCardLabel(card.transform, "Title", title, UiThemeRuntime.Theme.BodyFontSize, FontStyle.Bold, 44f);
            CreateCardLabel(card.transform, "Body", body, UiThemeRuntime.Theme.ChipFontSize + 2, FontStyle.Normal, 120f);
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

        private static string FormatTimestamp(string timestamp)
        {
            return DateTime.TryParse(timestamp, out var value)
                ? value.ToLocalTime().ToString("MMM d, yyyy h:mm tt")
                : timestamp;
        }
    }
}
