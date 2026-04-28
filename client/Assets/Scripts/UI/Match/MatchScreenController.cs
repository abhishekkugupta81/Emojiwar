using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EmojiWar.Client.Content;
using EmojiWar.Client.Core;
using EmojiWar.Client.Gameplay.Contracts;
using EmojiWar.Client.Gameplay.Clash;
using EmojiWar.Client.UI.Common;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Match
{
    public sealed class MatchScreenController : MonoBehaviour
    {
        private const int MatchQueueSlideIndex = 3;
        private const int MatchBanSlideIndex = 4;
        private const int MatchFormationSlideIndex = 5;
        private const int MatchResultSlideIndex = 6;
        private const int MatchChoiceSlotCount = 6;
        private const bool UseSlideBackground = false;
        private const bool UsePrefabFirstV2Layout = true;
        private const bool UseRescueBlindBan = true;
        private const bool UseRescueFormation = true;
        private const bool UseRescueBattlePresentation = true;
        private const bool UseRescueResult = true;
        private const float BlindBanRevealInterstitialSeconds = 1.35f;

        private static readonly string[] FormationSlotOrder =
        {
            "front_left",
            "front_center",
            "front_right",
            "back_left",
            "back_right"
        };

        private static readonly string[] FormationSlotLabels =
        {
            "Front Left",
            "Front Center",
            "Front Right",
            "Back Left",
            "Back Right"
        };

        private static readonly string[] FormationSlotShortLabels =
        {
            "FL",
            "FC",
            "FR",
            "BL",
            "BR"
        };

        private enum FormationContext
        {
            None,
            Bot,
            Pvp
        }

        [SerializeField] private Text scoreLabel;
        [SerializeField] private Text whyLabel;
        [SerializeField] private Text whyChainLabel;
        [SerializeField] private Image panelBackground;
        [SerializeField] private PhaseBar phaseStepper;
        [SerializeField] private StatusChip statusChip;
        [SerializeField] private RectTransform decisiveMomentsStrip;
        [SerializeField] private StickyFooterAction stickyFooterAction;
        [SerializeField] private StickyPrimaryAction stickyPrimaryAction;
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private Text[] choiceButtonLabels;
        [SerializeField] private Button actionButton;
        [SerializeField] private Text actionButtonLabel;

        private string selectedMode = string.Empty;
        private string currentMatchId = string.Empty;
        private QueueOrJoinMatchRequestDto currentPvpQueueRequest;
        private QueueOrJoinMatchResponseDto currentPvpMatch;
        private StartBotMatchResponseDto currentBotMatch;
        private string lastPvpRequestError = string.Empty;
        private string[] currentChoices = Array.Empty<string>();
        private Action<int> choiceHandler;
        private Action actionHandler;
        private Coroutine pollingRoutine;
        private Coroutine uiRefreshRoutine;
        private Coroutine replayRoutine;
        private Coroutine initialRefreshRoutine;
        private bool isLeavingMatch;
        private MatchUiPanelState currentPanelState = MatchUiPanelState.Queue;
        private ReplayMoment[] replayMoments = Array.Empty<ReplayMoment>();
        private const int RuntimeDetailCardCount = 3;
        private readonly List<Image> runtimeDetailCards = new();
        private readonly List<Text> runtimeDetailCardTitles = new();
        private readonly List<Text> runtimeDetailCardBodies = new();
        private RectTransform runtimeDetailCardsRoot;
        private string lastRenderedDetails = string.Empty;
        private DateTime? localPhaseDeadlineUtc;
        private string localPhaseDeadlineKey = string.Empty;
        private readonly HashSet<int> choiceStickerInitialized = new();

        private readonly List<string> formationDraft = new();
        private string[] formationTeam = Array.Empty<string>();
        private string formationDraftKey = string.Empty;
        private FormationContext activeFormationContext = FormationContext.None;
        private BlindBanRescueScreen rescueBlindBanScreen;
        private FormationRescueScreen rescueFormationScreen;
        private BattlePresentationRescueScreen rescueBattlePresentationScreen;
        private ResultRescueScreen rescueResultScreen;
        private EmojiClashRescueScreen rescueEmojiClashScreen;
        private string rescueSelectedBanId = string.Empty;
        private string rescuePendingLockedBanId = string.Empty;
        private string[] rescueFormationAssignments = Array.Empty<string>();
        private Coroutine rescueBanRevealRoutine;
        private Coroutine emojiClashResolveRoutine;
        private EmojiClashController emojiClashController;
        private string rescueActiveBanRevealKey = string.Empty;
        private string rescueCompletedBanRevealKey = string.Empty;
        private bool v2FoundationReady = true;
        private string v2FoundationMessage = string.Empty;
        // V2 recovery milestone: Match is prefab-first only.
        private static bool ShouldUseRuntimeLayout => !UsePrefabFirstV2Layout;

        private void Awake()
        {
            AutoWireSceneReferences();
            if (choiceButtons != null)
            {
                for (var index = 0; index < choiceButtons.Length; index++)
                {
                    if (choiceButtons[index] == null)
                    {
                        continue;
                    }

                    var capturedIndex = index;
                    choiceButtons[index].onClick.RemoveAllListeners();
                    choiceButtons[index].onClick.AddListener(() => choiceHandler?.Invoke(capturedIndex));
                }
            }

            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(() => actionHandler?.Invoke());
            }
        }

        private void Start()
        {
            if (StickerPopArenaFlow.AttachMatch(this))
            {
                return;
            }

            v2FoundationReady = V2BootstrapGuard.EnsureReady(out v2FoundationMessage, requireSlides: true);
            EnsureV2MetaWidgets();
            EnsureDecisiveMomentStrip();
            EnsureUiRefresh();
            if (ShouldUseRuntimeLayout)
            {
                ApplyReadableLabelLayout();
                ApplyReadableMetaLayout();
                EnsureRuntimeDetailCards();
            }
            else
            {
                DisableRuntimeDetailCardsIfPresent();
                ApplyReadableLabelLayout();
                ApplyReadableMetaLayout();
            }
            StartCoroutine(BeginSelectedFlowSafe());
        }

        private void OnDisable()
        {
            HideBlindBanRescue();
            HideFormationRescue();
            HideResultRescue();
            HideEmojiClashRescue();
            StopBanRevealRoutine();
            StopEmojiClashResolveRoutine();
            StopPolling();
            StopUiRefresh();
            StopReplay();
            if (initialRefreshRoutine != null)
            {
                StopCoroutine(initialRefreshRoutine);
                initialRefreshRoutine = null;
            }
        }

        private void AutoWireSceneReferences()
        {
            var allowGlobalFallback = ShouldUseRuntimeLayout;
            var panelScope = panelBackground != null
                ? panelBackground.transform
                : FindObjectOfType<Canvas>()?.transform;

            if (scoreLabel == null)
            {
                scoreLabel = FindTextByObjectName("ScoreLabel", panelScope, allowGlobalFallback);
            }

            if (whyLabel == null)
            {
                whyLabel = FindTextByObjectName("WhyLabel", panelScope, allowGlobalFallback);
            }

            if (whyChainLabel == null)
            {
                whyChainLabel = FindTextByObjectName("WhyChainLabel", panelScope, allowGlobalFallback);
            }

            if (panelBackground == null)
            {
                panelBackground = FindImageByObjectName("MatchPanel", panelScope, allowGlobalFallback);
            }

            if (phaseStepper == null)
            {
                phaseStepper = panelScope != null
                    ? panelScope.GetComponentInChildren<PhaseBar>(true)
                    : null;
                if (phaseStepper == null)
                {
                    phaseStepper = FindObjectOfType<PhaseBar>(true);
                }
            }

            if (statusChip == null)
            {
                statusChip = panelScope != null
                    ? panelScope.GetComponentInChildren<StatusChip>(true)
                    : null;
                if (statusChip == null)
                {
                    statusChip = FindObjectOfType<StatusChip>(true);
                }
            }

            if (choiceButtons == null || choiceButtons.Length == 0)
            {
                if (!TryDiscoverChoiceButtonsBySceneIndex(panelScope) &&
                    !TryDiscoverChoiceButtonsByName(panelScope))
                {
                    if (allowGlobalFallback && whyChainLabel != null && whyChainLabel.transform.parent != null)
                    {
                        choiceButtons = whyChainLabel.transform.parent.GetComponentsInChildren<Button>(true)
                            .Where(button =>
                            {
                                var lowerName = button.name.ToLowerInvariant();
                                return lowerName.Contains("choice");
                            })
                            .OrderBy(button => button.name, StringComparer.Ordinal)
                            .ToArray();
                    }
                }
            }

            if ((choiceButtonLabels == null || choiceButtonLabels.Length == 0) && choiceButtons != null)
            {
                choiceButtonLabels = choiceButtons
                    .Select(button => button != null ? button.GetComponentInChildren<Text>(true) : null)
                    .ToArray();
            }

            if (actionButton == null)
            {
                var scopedButtons = panelScope != null
                    ? panelScope.GetComponentsInChildren<Button>(true)
                    : Array.Empty<Button>();
                actionButton = scopedButtons.FirstOrDefault(button =>
                    button != null &&
                    (button.name.IndexOf("Continue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     button.name.IndexOf("Return", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     button.name.IndexOf("Rematch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     button.name.IndexOf("Lock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     button.name.IndexOf("Reset", StringComparison.OrdinalIgnoreCase) >= 0));

                if (actionButton == null && allowGlobalFallback)
                {
                    actionButton = FindObjectsOfType<Button>(true)
                        .FirstOrDefault(button => button.name.IndexOf("Continue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                  button.name.IndexOf("Return", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                  button.name.IndexOf("Rematch", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                  button.name.IndexOf("Lock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                  button.name.IndexOf("Reset", StringComparison.OrdinalIgnoreCase) >= 0);
                }
            }

            if (actionButtonLabel == null && actionButton != null)
            {
                actionButtonLabel = actionButton.GetComponentInChildren<Text>(true);
            }

            if (stickyPrimaryAction == null && actionButton != null)
            {
                stickyPrimaryAction = actionButton.GetComponent<StickyPrimaryAction>();
            }
        }

        private bool TryDiscoverChoiceButtonsBySceneIndex(Transform scopeRoot)
        {
            if (scopeRoot == null)
            {
                return false;
            }

            var mapped = new List<Button>(MatchChoiceSlotCount);
            var labels = new List<Text>(MatchChoiceSlotCount);
            for (var slot = 1; slot <= MatchChoiceSlotCount; slot++)
            {
                var expectedName = $"Choice {slot}Button";
                var buttonObject = FindChildByName(expectedName, scopeRoot, allowGlobalFallback: false);
                if (buttonObject == null)
                {
                    return false;
                }

                var button = buttonObject.GetComponent<Button>();
                if (button == null)
                {
                    return false;
                }

                mapped.Add(button);
                labels.Add(button.GetComponentInChildren<Text>(true));
            }

            choiceButtons = mapped.ToArray();
            choiceButtonLabels = labels.ToArray();
            return true;
        }

        private bool TryDiscoverChoiceButtonsByName(Transform scopeRoot)
        {
            if (scopeRoot == null)
            {
                return false;
            }

            var scopedButtons = scopeRoot.GetComponentsInChildren<Button>(true)
                .Where(button =>
                {
                    if (button == null)
                    {
                        return false;
                    }

                    var lowerName = button.name.ToLowerInvariant();
                    if (!lowerName.Contains("choice"))
                    {
                        return false;
                    }

                    return !lowerName.Contains("continue") &&
                           !lowerName.Contains("return") &&
                           !lowerName.Contains("rematch");
                })
                .OrderBy(button => button.name, StringComparer.Ordinal)
                .Take(MatchChoiceSlotCount)
                .ToArray();

            if (scopedButtons.Length == 0)
            {
                return false;
            }

            choiceButtons = scopedButtons;
            choiceButtonLabels = choiceButtons
                .Select(button => button != null ? button.GetComponentInChildren<Text>(true) : null)
                .ToArray();
            return true;
        }

        private void EnsureV2MetaWidgets()
        {
            var root = ResolvePanelRoot();
            if (root == null)
            {
                return;
            }

            if (!ShouldUseRuntimeLayout)
            {
                if (phaseStepper == null)
                {
                    phaseStepper = FindObjectOfType<PhaseBar>(true);
                }

                if (statusChip == null)
                {
                    statusChip = FindObjectOfType<StatusChip>(true);
                }

                return;
            }

            if (phaseStepper == null)
            {
                phaseStepper = CreateRuntimePhaseBar(root);
            }

            if (statusChip == null)
            {
                statusChip = CreateRuntimeStatusChip(root);
            }
        }

        private RectTransform ResolvePanelRoot()
        {
            if (panelBackground != null)
            {
                return panelBackground.rectTransform;
            }

            if (scoreLabel != null && scoreLabel.transform.parent is RectTransform scoreParent)
            {
                return scoreParent;
            }

            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas != null)
            {
                var matchPanelTransform = canvas.transform
                    .GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(node => string.Equals(node.name, "MatchPanel", StringComparison.Ordinal));
                if (matchPanelTransform is RectTransform rectTransform)
                {
                    var matchPanelImage = matchPanelTransform.GetComponent<Image>();
                    if (matchPanelImage != null)
                    {
                        panelBackground = matchPanelImage;
                    }

                    return rectTransform;
                }
            }

            return canvas != null ? canvas.transform as RectTransform : null;
        }

        private static Text FindTextByObjectName(string objectName, Transform scopeRoot = null, bool allowGlobalFallback = true)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            return FindChildByName(objectName, scopeRoot, allowGlobalFallback)?.GetComponent<Text>();
        }

        private static Image FindImageByObjectName(string objectName, Transform scopeRoot = null, bool allowGlobalFallback = true)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            return FindChildByName(objectName, scopeRoot, allowGlobalFallback)?.GetComponent<Image>();
        }

        private static GameObject FindChildByName(string objectName, Transform scopeRoot = null, bool allowGlobalFallback = true)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            if (scopeRoot != null)
            {
                var transforms = scopeRoot.GetComponentsInChildren<Transform>(true);
                var scoped = transforms.FirstOrDefault(node =>
                    node != null &&
                    string.Equals(node.name, objectName, StringComparison.Ordinal));
                if (scoped != null)
                {
                    return scoped.gameObject;
                }
            }

            return allowGlobalFallback ? GameObject.Find(objectName) : null;
        }

        private PhaseBar CreateRuntimePhaseBar(RectTransform root)
        {
            var phaseObject = new GameObject("RuntimePhaseBar", typeof(RectTransform), typeof(Image), typeof(PhaseBar));
            phaseObject.transform.SetParent(root, false);
            var phaseRect = phaseObject.GetComponent<RectTransform>();
            phaseRect.anchorMin = new Vector2(0.12f, 0.83f);
            phaseRect.anchorMax = new Vector2(0.88f, 0.87f);
            phaseRect.offsetMin = Vector2.zero;
            phaseRect.offsetMax = Vector2.zero;

            var phaseBackground = phaseObject.GetComponent<Image>();
            phaseBackground.color = UiThemeRuntime.Theme.SurfaceColor * new Color(1f, 1f, 1f, 0.86f);

            var phaseLabelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            phaseLabelObject.transform.SetParent(phaseObject.transform, false);
            var phaseLabelRect = phaseLabelObject.GetComponent<RectTransform>();
            phaseLabelRect.anchorMin = Vector2.zero;
            phaseLabelRect.anchorMax = Vector2.one;
            phaseLabelRect.offsetMin = new Vector2(8f, 2f);
            phaseLabelRect.offsetMax = new Vector2(-8f, -2f);

            var phaseLabel = phaseLabelObject.GetComponent<Text>();
            phaseLabel.alignment = TextAnchor.MiddleCenter;
            phaseLabel.color = Color.white;
            phaseLabel.fontSize = Mathf.Max(UiThemeRuntime.Theme.ChipFontSize + 1, 18);
            phaseLabel.resizeTextForBestFit = false;
            phaseLabel.font = scoreLabel != null
                ? scoreLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return phaseObject.GetComponent<PhaseBar>();
        }

        private StatusChip CreateRuntimeStatusChip(RectTransform root)
        {
            var chipObject = new GameObject("RuntimeStatusChip", typeof(RectTransform), typeof(Image), typeof(StatusChip));
            chipObject.transform.SetParent(root, false);
            var chipRect = chipObject.GetComponent<RectTransform>();
            chipRect.anchorMin = new Vector2(0.18f, 0.74f);
            chipRect.anchorMax = new Vector2(0.82f, 0.78f);
            chipRect.offsetMin = Vector2.zero;
            chipRect.offsetMax = Vector2.zero;

            var chipBackground = chipObject.GetComponent<Image>();
            chipBackground.color = UiThemeRuntime.Theme.SurfaceColor * new Color(1f, 1f, 1f, 0.90f);

            var chipLabelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            chipLabelObject.transform.SetParent(chipObject.transform, false);
            var chipLabelRect = chipLabelObject.GetComponent<RectTransform>();
            chipLabelRect.anchorMin = Vector2.zero;
            chipLabelRect.anchorMax = Vector2.one;
            chipLabelRect.offsetMin = new Vector2(8f, 2f);
            chipLabelRect.offsetMax = new Vector2(-8f, -2f);

            var chipLabel = chipLabelObject.GetComponent<Text>();
            chipLabel.alignment = TextAnchor.MiddleCenter;
            chipLabel.color = Color.white;
            chipLabel.fontSize = Mathf.Max(UiThemeRuntime.Theme.ChipFontSize + 1, 17);
            chipLabel.resizeTextForBestFit = false;
            chipLabel.font = scoreLabel != null
                ? scoreLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return chipObject.GetComponent<StatusChip>();
        }

        private void EnsureDecisiveMomentStrip()
        {
            if (decisiveMomentsStrip != null)
            {
                return;
            }

            if (!ShouldUseRuntimeLayout)
            {
                decisiveMomentsStrip = GameObject.Find("DecisiveMomentStrip")?.GetComponent<RectTransform>();
                return;
            }

            if (whyChainLabel == null || whyChainLabel.transform.parent == null)
            {
                return;
            }

            var root = whyChainLabel.transform.parent;
            var stripObject = new GameObject("DecisiveMomentStrip", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            stripObject.transform.SetParent(root, false);
            stripObject.transform.SetSiblingIndex(whyChainLabel.transform.GetSiblingIndex() + 1);
            decisiveMomentsStrip = stripObject.GetComponent<RectTransform>();

            var layoutElement = stripObject.GetComponent<LayoutElement>();
            layoutElement.preferredHeight = 54f;
            layoutElement.minHeight = 54f;

            var layout = stripObject.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            stripObject.SetActive(false);
        }

        private void EnsureRuntimeDetailCards()
        {
            if (runtimeDetailCardsRoot != null)
            {
                return;
            }

            var root = ResolvePanelRoot();
            if (root == null)
            {
                return;
            }

            var cardsRootObject = new GameObject(
                "RuntimeDetailCardsRoot",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup),
                typeof(LayoutElement));
            cardsRootObject.transform.SetParent(root, false);

            runtimeDetailCardsRoot = cardsRootObject.GetComponent<RectTransform>();
            runtimeDetailCardsRoot.anchorMin = new Vector2(0.08f, 0.32f);
            runtimeDetailCardsRoot.anchorMax = new Vector2(0.92f, 0.76f);
            runtimeDetailCardsRoot.offsetMin = Vector2.zero;
            runtimeDetailCardsRoot.offsetMax = Vector2.zero;

            var layout = cardsRootObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = true;
            layout.childAlignment = TextAnchor.UpperCenter;

            var rootLayoutElement = cardsRootObject.GetComponent<LayoutElement>();
            rootLayoutElement.minHeight = 220f;
            rootLayoutElement.preferredHeight = 260f;

            for (var index = 0; index < RuntimeDetailCardCount; index++)
            {
                CreateRuntimeDetailCard(index);
            }
        }

        private void DisableRuntimeDetailCardsIfPresent()
        {
            if (runtimeDetailCardsRoot != null)
            {
                runtimeDetailCardsRoot.gameObject.SetActive(false);
            }

            var runtimeCards = GameObject.Find("RuntimeDetailCardsRoot");
            if (runtimeCards != null)
            {
                runtimeCards.SetActive(false);
            }
        }

        private void CreateRuntimeDetailCard(int index)
        {
            if (runtimeDetailCardsRoot == null)
            {
                return;
            }

            var cardObject = new GameObject(
                $"RuntimeDetailCard{index + 1}",
                typeof(RectTransform),
                typeof(Image),
                typeof(LayoutElement));
            cardObject.transform.SetParent(runtimeDetailCardsRoot, false);

            var cardImage = cardObject.GetComponent<Image>();
            cardImage.color = UiThemeRuntime.Theme.SurfaceColor * new Color(1f, 1f, 1f, 0.92f);

            var cardLayoutElement = cardObject.GetComponent<LayoutElement>();
            cardLayoutElement.minHeight = 64f;
            cardLayoutElement.preferredHeight = 84f;
            cardLayoutElement.flexibleHeight = 1f;

            var titleObject = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObject.transform.SetParent(cardObject.transform, false);
            var titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.62f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(10f, -4f);
            titleRect.offsetMax = new Vector2(-10f, -4f);

            var titleLabel = titleObject.GetComponent<Text>();
            titleLabel.font = scoreLabel != null
                ? scoreLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleLabel.fontSize = Mathf.Max(UiThemeRuntime.Theme.ChipFontSize + 2, 16);
            titleLabel.alignment = TextAnchor.UpperLeft;
            titleLabel.color = Color.white;
            titleLabel.resizeTextForBestFit = false;
            titleLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleLabel.verticalOverflow = VerticalWrapMode.Truncate;

            var bodyObject = new GameObject("Body", typeof(RectTransform), typeof(Text));
            bodyObject.transform.SetParent(cardObject.transform, false);
            var bodyRect = bodyObject.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 0.66f);
            bodyRect.offsetMin = new Vector2(10f, 8f);
            bodyRect.offsetMax = new Vector2(-10f, -2f);

            var bodyLabel = bodyObject.GetComponent<Text>();
            bodyLabel.font = scoreLabel != null
                ? scoreLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bodyLabel.fontSize = Mathf.Max(UiThemeRuntime.Theme.BodyFontSize - 2, 15);
            bodyLabel.alignment = TextAnchor.UpperLeft;
            bodyLabel.color = Color.white;
            bodyLabel.resizeTextForBestFit = false;
            bodyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyLabel.verticalOverflow = VerticalWrapMode.Truncate;

            runtimeDetailCards.Add(cardImage);
            runtimeDetailCardTitles.Add(titleLabel);
            runtimeDetailCardBodies.Add(bodyLabel);
        }

        private void RenderRuntimeDetailCards(string details)
        {
            if (!ShouldUseRuntimeLayout)
            {
                if (runtimeDetailCardsRoot != null)
                {
                    runtimeDetailCardsRoot.gameObject.SetActive(false);
                }

                return;
            }

            EnsureRuntimeDetailCards();
            if (runtimeDetailCardsRoot == null || runtimeDetailCards.Count == 0)
            {
                return;
            }

            var sections = BuildDetailSections(details);
            if (sections.Count == 0)
            {
                for (var index = 0; index < runtimeDetailCards.Count; index++)
                {
                    runtimeDetailCards[index].gameObject.SetActive(false);
                }

                runtimeDetailCardsRoot.gameObject.SetActive(false);
                return;
            }

            runtimeDetailCardsRoot.gameObject.SetActive(true);
            for (var index = 0; index < runtimeDetailCards.Count; index++)
            {
                if (index >= sections.Count)
                {
                    runtimeDetailCards[index].gameObject.SetActive(false);
                    continue;
                }

                var section = sections[index];
                runtimeDetailCards[index].gameObject.SetActive(true);
                runtimeDetailCardTitles[index].text = section.Title;
                runtimeDetailCardBodies[index].text = section.Body;
            }
        }

        private List<DetailSection> BuildDetailSections(string details)
        {
            var sections = new List<DetailSection>();
            if (string.IsNullOrWhiteSpace(details))
            {
                return sections;
            }

            var blocks = details
                .Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(block => block.Trim())
                .Where(block => !string.IsNullOrWhiteSpace(block))
                .ToArray();

            foreach (var block in blocks)
            {
                var title = "Details";
                if (block.IndexOf("Queue", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    block.IndexOf("ticket", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    title = "Queue";
                }
                else if (block.IndexOf("Your 6", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         block.IndexOf("Enemy 6", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    title = "Squads";
                }
                else if (block.IndexOf("Formation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         block.IndexOf("Your board", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         block.IndexOf("Enemy board", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    title = "Formation";
                }
                else if (block.IndexOf("Decisive moments", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         block.IndexOf("Result", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    title = "Result";
                }
                else if (block.IndexOf("HTTP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         block.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    title = "Connection";
                }
                else if (block.IndexOf("Auto-lock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         block.IndexOf("Timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    title = "Timer";
                }
                else if (sections.Count == 0)
                {
                    title = "Summary";
                }

                sections.Add(new DetailSection(title, block));
            }

            if (sections.Count > RuntimeDetailCardCount)
            {
                var foldedBody = new StringBuilder();
                for (var index = RuntimeDetailCardCount - 1; index < sections.Count; index++)
                {
                    if (foldedBody.Length > 0)
                    {
                        foldedBody.AppendLine();
                        foldedBody.AppendLine();
                    }

                    foldedBody.Append(sections[index].Body);
                }

                sections = sections.Take(RuntimeDetailCardCount - 1).ToList();
                sections.Add(new DetailSection("More", foldedBody.ToString()));
            }

            return sections;
        }

        private IEnumerator BeginSelectedFlowSafe()
        {
            IEnumerator flow = null;
            try
            {
                flow = BeginSelectedFlow();
            }
            catch (Exception exception)
            {
                ShowError("Match", "Initialization failed.", exception.Message);
            }

            if (flow == null)
            {
                yield break;
            }

            while (true)
            {
                object current = null;
                try
                {
                    if (!flow.MoveNext())
                    {
                        yield break;
                    }

                    current = flow.Current;
                }
                catch (Exception exception)
                {
                    ShowError("Match", "Initialization failed.", exception.Message);
                    yield break;
                }

                yield return current;
            }
        }

        private IEnumerator BeginSelectedFlow()
        {
            if (!v2FoundationReady)
            {
                ShowError("Match", "V2 setup incomplete.", v2FoundationMessage);
                yield break;
            }

            selectedMode = LaunchSelections.GetSelectedMode();

            if (selectedMode == LaunchSelections.PvpRanked)
            {
                yield return BeginPvpQueue();
                yield break;
            }

            if (selectedMode == LaunchSelections.EmojiClashLocal)
            {
                yield return BeginEmojiClashLocal();
                yield break;
            }

            yield return BeginBotMatch(selectedMode);
        }

        private IEnumerator BeginEmojiClashLocal()
        {
            currentPanelState = MatchUiPanelState.Ban;
            SetHeader(
                "Emoji Clash",
                "Quick Play - 5 Turns",
                "Pick one emoji each turn. Reveals resolve together. No randomness.");
            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);

            EnsureEmojiClashRescueScreen();
            if (rescueEmojiClashScreen == null)
            {
                ShowError("Emoji Clash", "Screen unavailable.", "Could not mount the Emoji Clash rescue screen.");
                yield break;
            }

            emojiClashController = new EmojiClashController();
            emojiClashController.StartEmojiClash();
            RenderEmojiClashTurn();
        }

        private IEnumerator BeginBotMatch(string mode)
        {
            currentPanelState = MatchUiPanelState.Queue;
            SetHeader(
                "Battle Bot",
                "Step 1 of 3 • Squad",
                "Preparing selected 5 for battle.");
            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);

            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null)
            {
                ShowError("Battle Bot", "Bootstrap unavailable.", "Open the game through the Bootstrap scene.");
                yield break;
            }

            if (!TryValidateFunctionsRuntime(bootstrap, out var runtimeError))
            {
                ShowError("Battle Bot", "Supabase unavailable.", runtimeError);
                yield break;
            }

            if (!TryResolveEntryDeck(bootstrap, true, out var selectedDeck, out var deckId, out var deckError))
            {
                ShowError("Battle Bot", "No squad selected.", deckError);
                yield break;
            }

            var requestPayload = new StartBotMatchRequestDto
            {
                mode = mode,
                activeDeckId = deckId,
                playerDeck = selectedDeck,
            };

            using var request = bootstrap.FunctionClient.BuildJsonRequest("start_bot_match", JsonUtility.ToJson(requestPayload), bootstrap.SessionState.AccessToken);
            request.timeout = 10;
            if (!TryBeginWebRequest(request, out var operation, out var startError))
            {
                ShowError("Battle Bot", "Could not start the bot battle.", startError);
                yield break;
            }

            yield return operation;

            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowError("Battle Bot", "Could not start the bot battle.", BuildRequestErrorDetails(request));
                yield break;
            }

            if (!TryParseJsonResponse(request, out currentBotMatch, out var parseError))
            {
                ShowError("Battle Bot", "Could not read bot battle state.", parseError);
                yield break;
            }

            if (!IsBotMatchFinished(currentBotMatch) && string.IsNullOrWhiteSpace(currentBotMatch.matchId))
            {
                ShowError("Battle Bot", "Invalid bot battle response.", "Server response did not include a match id.");
                yield break;
            }
            currentMatchId = currentBotMatch.matchId;
            LaunchSelections.ClearPendingSquad();

            if (IsBotMatchFinished(currentBotMatch))
            {
                RenderBotResult(currentBotMatch);
                yield break;
            }

            ShowBotFormationPhase();
        }

        private IEnumerator BeginPvpQueue()
        {
            currentPanelState = MatchUiPanelState.Queue;
            SetHeader(
                "Ranked PvP",
                "Step 2 of 4 • Queue",
                "Preparing your selected 6-emoji squad for blind ban matchmaking.");
            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);

            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null)
            {
                ShowError("Ranked PvP", "Queue unavailable.", "App bootstrap is not available.");
                yield break;
            }

            if (!TryValidateFunctionsRuntime(bootstrap, out var runtimeError))
            {
                ShowError("Ranked PvP", "Queue unavailable.", runtimeError);
                yield break;
            }

            var hasPendingEntrySquad = LaunchSelections.GetPendingSquad().Count > 0;
            var isResumeRequested = LaunchSelections.ShouldResumeRankedMatch();

            string[] selectedDeck = Array.Empty<string>();
            string deckId = string.Empty;
            string deckError = string.Empty;
            var storedResumeMatchId = string.Empty;

            if (isResumeRequested)
            {
                storedResumeMatchId = LaunchSelections.GetRankedResumeMatchId();
                deckId = LaunchSelections.GetRankedResumeDeckId();
                selectedDeck = EmojiIdUtility.ToApiIds(LaunchSelections.GetRankedResumeSquad());
                deckError = string.Empty;

                if (string.IsNullOrWhiteSpace(storedResumeMatchId) || string.IsNullOrWhiteSpace(deckId) || selectedDeck.Length != 6)
                {
                    LaunchSelections.ClearRankedResume();
                    isResumeRequested = false;
                }
            }

            if (!isResumeRequested &&
                !TryResolveEntryDeck(bootstrap, false, out selectedDeck, out deckId, out deckError))
            {
                ShowError("Ranked PvP", "Queue unavailable.", deckError);
                yield break;
            }

            currentPvpQueueRequest = new QueueOrJoinMatchRequestDto
            {
                userId = bootstrap.SessionState.UserId,
                deckId = deckId,
                playerDeck = selectedDeck,
                matchId = isResumeRequested ? storedResumeMatchId : string.Empty,
                forceFreshEntry = hasPendingEntrySquad,
            };

            QueueOrJoinMatchResponseDto response = null;
            yield return FetchPvpSnapshot(
                snapshot => response = snapshot,
                false,
                attempts: 3,
                timeoutSeconds: 20);

            if (response == null)
            {
                ShowError("Ranked PvP", "Queue request failed.", BuildQueueRequestFailureDetails());
                yield break;
            }

            ApplyPvpSnapshot(response);
            LaunchSelections.ClearPendingSquad();
            LaunchSelections.ClearRankedResumeRequested();
            SafeRenderCurrentPvpState("initial ranked snapshot");
        }

        private void RenderCurrentPvpState()
        {
            if (currentPvpMatch == null)
            {
                ShowError("Ranked PvP", "Match state unavailable.", "No ranked snapshot is loaded.");
                return;
            }

            if (currentPvpMatch.status == "queued")
            {
                ShowQueuedState(currentPvpMatch);
                EnsurePolling();
                return;
            }

            if (IsQueuePhase(currentPvpMatch))
            {
                ShowQueuedState(currentPvpMatch);
                EnsurePolling();
                return;
            }

            if (IsMatchFinished(currentPvpMatch))
            {
                StopPolling();
                ClearFormationDraft();
                LaunchSelections.ClearRankedResume();
                RenderPvpResult(currentPvpMatch);
                return;
            }

            if (IsBanPhase(currentPvpMatch))
            {
                if (!HasOpponentDeckChoices(currentPvpMatch))
                {
                    ShowSyncingOpponentDeck();
                    EnsurePolling();
                    return;
                }

                if (PlayerBanIsLocked(currentPvpMatch))
                {
                    ShowWaitingForOpponentBan();
                    EnsurePolling();
                    return;
                }

                ShowBanPhase();
                EnsurePolling();
                return;
            }

            if (IsFormationPhase(currentPvpMatch))
            {
                if (ShouldShowBanRevealBeforeFormation(currentPvpMatch))
                {
                    ShowBanRevealBeforeFormation();
                    EnsurePolling();
                    return;
                }

                if (!HasPlayerFinalTeam(currentPvpMatch))
                {
                    ShowSyncingFormation();
                    EnsurePolling();
                    return;
                }

                if (PlayerFormationIsLocked(currentPvpMatch))
                {
                    ShowWaitingForOpponentFormation();
                    EnsurePolling();
                    return;
                }

                ShowPvpFormationPhase();
                EnsurePolling();
                return;
            }

            if (IsLegacyResolvingPhase(currentPvpMatch))
            {
                if (currentPvpMatch.battleState != null || !string.IsNullOrWhiteSpace(currentPvpMatch.winner))
                {
                    StopPolling();
                    ClearFormationDraft();
                    RenderPvpResult(currentPvpMatch);
                    return;
                }

                currentPanelState = MatchUiPanelState.Waiting;
                SetHeader(
                    "Ranked PvP",
                    "Step 4 of 4 • Resolving",
                    "The match is finishing on the server. This screen will update automatically.\n\n" +
                    $"{BuildBanPanelContext(currentPvpMatch)}");
                SetChoiceButtons(Array.Empty<string>(), null);
                SetActionButton(true, "Return Home", ReturnHome);
                EnsurePolling();
                return;
            }

            ShowError("Ranked PvP", "Unknown match phase.", currentPvpMatch.note);
        }

        private void ShowQueuedState(QueueOrJoinMatchResponseDto response)
        {
            HideBlindBanRescue();
            HideFormationRescue();
            HideResultRescue();
            if (response == null)
            {
                ShowError("Ranked PvP", "Queue state unavailable.", "No queue snapshot is loaded.");
                return;
            }

            currentPanelState = MatchUiPanelState.Queue;
            SetHeader(
                "Ranked PvP",
                "Step 2 of 4 • Queue",
                BuildQueueDetails(response));
            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(true, "Return Home", ReturnHome);
        }

        private void ShowBanPhase()
        {
            HideFormationRescue();
            HideResultRescue();
            if (currentPvpMatch == null)
            {
                ShowError("Ranked PvP", "Ban state unavailable.", "Match snapshot is missing.");
                return;
            }

            currentPanelState = MatchUiPanelState.Ban;
            if (UseRescueBlindBan && TryRenderBlindBanRescue(locked: false))
            {
                return;
            }

            SetHeader(
                "Ranked PvP",
                "Step 2 of 4 • Blind Ban",
                BuildBanDetails(currentPvpMatch, locked: false));

            SetChoiceButtons(currentPvpMatch.opponentDeck ?? Array.Empty<string>(), OnBanChoiceSelected);
            SetActionButton(true, "Return Home", ReturnHome);
        }

        private void ShowWaitingForOpponentBan()
        {
            HideFormationRescue();
            HideResultRescue();
            if (currentPvpMatch == null)
            {
                ShowError("Ranked PvP", "Ban wait state unavailable.", "Match snapshot is missing.");
                return;
            }

            currentPanelState = MatchUiPanelState.Waiting;
            if (UseRescueBlindBan && TryRenderBlindBanRescue(locked: true))
            {
                return;
            }

            SetHeader(
                "Ranked PvP",
                "Step 2 of 4 • Ban Locked",
                BuildBanDetails(currentPvpMatch, locked: true));
            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(true, "Return Home", ReturnHome);
        }

        private bool TryRenderBlindBanRescue(bool locked)
        {
            if (currentPvpMatch == null || !HasOpponentDeckChoices(currentPvpMatch))
            {
                return false;
            }

            var enemyUnits = (currentPvpMatch.opponentDeck ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(6)
                .Select(value => BlindBanRescueScreen.UnitView.FromApiId(value, enemyTone: true))
                .ToArray();
            if (enemyUnits.Length == 0)
            {
                return false;
            }

            var playerSource = currentPvpMatch.playerDeck != null && currentPvpMatch.playerDeck.Length > 0
                ? currentPvpMatch.playerDeck
                : currentPvpQueueRequest?.playerDeck ?? Array.Empty<string>();
            var playerUnits = playerSource
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(6)
                .Select(value => BlindBanRescueScreen.UnitView.FromApiId(value, enemyTone: false))
                .ToArray();

            var serverLockedBan = NormalizeOptionalUnitKey(currentPvpMatch.opponentBannedEmojiId);
            var lockedBan = !string.IsNullOrWhiteSpace(serverLockedBan)
                ? serverLockedBan
                : locked
                    ? NormalizeOptionalUnitKey(rescuePendingLockedBanId)
                    : string.Empty;
            var selectedBan = !string.IsNullOrWhiteSpace(lockedBan)
                ? lockedBan
                : NormalizeOptionalUnitKey(rescueSelectedBanId);

            EnsureBlindBanRescueScreen();
            if (rescueBlindBanScreen == null)
            {
                return false;
            }

            rescueBlindBanScreen.gameObject.SetActive(true);
            rescueBlindBanScreen.transform.SetAsLastSibling();
            rescueBlindBanScreen.Bind(
                enemyUnits,
                playerUnits,
                ResolveBlindBanVisibilityMode(),
                selectedBan,
                lockedBan,
                currentPvpMatch.playerBannedEmojiId,
                OnRescueBanTargetChanged,
                OnRescueBanLocked,
                ResolveBanSecondsRemaining());

            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);
            return true;
        }

        private void EnsureBlindBanRescueScreen()
        {
            if (rescueBlindBanScreen != null)
            {
                return;
            }

            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var mount = new GameObject("BlindBanRescueMount", typeof(RectTransform));
            mount.transform.SetParent(canvas.transform, false);
            var rect = mount.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rescueBlindBanScreen = mount.AddComponent<BlindBanRescueScreen>();
        }

        private void HideBlindBanRescue()
        {
            StopBanRevealRoutine();

            if (rescueBlindBanScreen == null)
            {
                return;
            }

            rescueBlindBanScreen.Hide();
            rescueBlindBanScreen.gameObject.SetActive(false);
        }

        private void OnRescueBanTargetChanged(string targetId)
        {
            rescueSelectedBanId = NormalizeOptionalUnitKey(targetId);
            rescuePendingLockedBanId = string.Empty;
            TryRenderBlindBanRescue(locked: false);
        }

        private void OnRescueBanLocked(string targetId)
        {
            var normalized = NormalizeOptionalUnitKey(targetId);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            rescueSelectedBanId = normalized;
            rescuePendingLockedBanId = normalized;
            HapticFeedback.TriggerLightImpact();
            TryRenderBlindBanRescue(locked: true);
            StartCoroutine(SubmitBanChoice(normalized));
        }

        private int ResolveBanSecondsRemaining()
        {
            if (currentPvpMatch != null && currentPvpMatch.phaseTimeoutSecondsRemaining > 0)
            {
                return currentPvpMatch.phaseTimeoutSecondsRemaining;
            }

            if (localPhaseDeadlineUtc.HasValue)
            {
                return Mathf.Max(1, Mathf.CeilToInt((float)(localPhaseDeadlineUtc.Value - DateTime.UtcNow).TotalSeconds));
            }

            return 27;
        }

        private BlindBanRescueScreen.BlindBanVisibilityMode ResolveBlindBanVisibilityMode()
        {
            // Ranked PvP must not reveal opponent cards during blind ban.
            // Testing reveal mode is reserved for local/dev-only flows.
            return BlindBanRescueScreen.BlindBanVisibilityMode.ProductionHiddenOpponentCards;
        }

        private static string NormalizeOptionalUnitKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : UnitIconLibrary.NormalizeUnitKey(value);
        }

        private void ShowBanRevealBeforeFormation()
        {
            if (currentPvpMatch == null)
            {
                return;
            }

            currentPanelState = MatchUiPanelState.Waiting;
            if (!TryRenderBlindBanRescue(locked: true))
            {
                ShowPvpFormationPhase();
                return;
            }

            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);

            var revealKey = BuildBanRevealKey(currentPvpMatch);
            if (string.Equals(revealKey, rescueActiveBanRevealKey, StringComparison.Ordinal) &&
                rescueBanRevealRoutine != null)
            {
                return;
            }

            StopBanRevealRoutine();
            rescueActiveBanRevealKey = revealKey;
            rescueBanRevealRoutine = StartCoroutine(AdvanceToFormationAfterBanReveal(revealKey));
        }

        private IEnumerator AdvanceToFormationAfterBanReveal(string revealKey)
        {
            yield return new WaitForSeconds(BlindBanRevealInterstitialSeconds);

            rescueCompletedBanRevealKey = revealKey;
            rescueActiveBanRevealKey = string.Empty;
            rescueBanRevealRoutine = null;

            if (currentPvpMatch != null)
            {
                SafeRenderCurrentPvpState("post ban reveal interstitial");
            }
        }

        private void StopBanRevealRoutine()
        {
            if (rescueBanRevealRoutine == null)
            {
                rescueActiveBanRevealKey = string.Empty;
                return;
            }

            StopCoroutine(rescueBanRevealRoutine);
            rescueBanRevealRoutine = null;
            rescueActiveBanRevealKey = string.Empty;
        }

        private void ShowSyncingOpponentDeck()
        {
            HideBlindBanRescue();
            HideFormationRescue();
            HideResultRescue();
            currentPanelState = MatchUiPanelState.Waiting;
            SetHeader(
                "Ranked PvP",
                "Step 2 of 4 • Syncing",
                "Waiting for the matched enemy deck snapshot so the blind-ban buttons can be shown.");
            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(true, "Return Home", ReturnHome);
        }

        private void ShowSyncingFormation()
        {
            HideBlindBanRescue();
            HideFormationRescue();
            HideResultRescue();
            currentPanelState = MatchUiPanelState.Waiting;
            var timeoutLine = BuildTimeoutLine(currentPvpMatch, "Formation timeout in");
            var details = string.IsNullOrWhiteSpace(timeoutLine)
                ? "Final 5 squads are still syncing.\nThis screen will update automatically."
                : $"{timeoutLine}\nFinal 5 squads are still syncing.\nThis screen will update automatically.";
            SetHeader(
                "Ranked PvP",
                "Step 3 of 4 • Syncing",
                details);
            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(true, "Return Home", ReturnHome);
        }

        private void ShowPvpFormationPhase()
        {
            HideBlindBanRescue();
            HideResultRescue();
            if (currentPvpMatch == null)
            {
                ShowError("Ranked PvP", "Formation state unavailable.", "Match snapshot is missing.");
                return;
            }

            currentPanelState = MatchUiPanelState.Formation;
            activeFormationContext = FormationContext.Pvp;
            var team = currentPvpMatch.playerFinalTeam ?? Array.Empty<string>();
            EnsureFormationDraft($"pvp:{currentMatchId}:{string.Join(",", team)}", team);
            if (UseRescueFormation && TryRenderFormationRescue(locked: false))
            {
                return;
            }

            ShowFormationBuilder(
                "Ranked PvP",
                "Step 3 of 4 • Formation",
                BuildPvpFormationDetailsPrefix(currentPvpMatch),
                SubmitPvpFormation,
                ReturnHome);
        }

        private void ShowBotFormationPhase()
        {
            HideBlindBanRescue();
            HideResultRescue();
            if (currentBotMatch == null)
            {
                ShowError("Battle Bot", "Formation state unavailable.", "Bot match snapshot is missing.");
                return;
            }

            currentPanelState = MatchUiPanelState.Formation;
            activeFormationContext = FormationContext.Bot;
            var team = currentBotMatch.playerTeam ?? Array.Empty<string>();
            EnsureFormationDraft($"bot:{currentMatchId}:{string.Join(",", team)}", team);
            if (UseRescueFormation && TryRenderBotFormationRescue(locked: false))
            {
                return;
            }

            ShowFormationBuilder(
                "Battle Bot",
                "Step 2 of 3 • Formation",
                BuildBotFormationDetailsPrefix(currentBotMatch),
                SubmitBotFormation,
                ReturnHome);
        }

        private void ShowFormationBuilder(string title, string subtitle, string detailsPrefix, Action submitAction, Action leaveAction)
        {
            var nextSlotIndex = Mathf.Clamp(formationDraft.Count, 0, FormationSlotLabels.Length - 1);
            var remainingChoices = formationTeam.Where(emojiId => !formationDraft.Contains(emojiId)).ToArray();

            SetHeader(
                title,
                formationDraft.Count < formationTeam.Length
                    ? $"{subtitle}\nNext slot: {FormationSlotLabels[nextSlotIndex]}"
                    : "Formation ready",
                $"{detailsPrefix}\n" +
                $"{BuildFormationBoardPreview(formationDraft, formationTeam)}\n" +
                $"Placed {formationDraft.Count}/{formationTeam.Length}");

            SetChoiceButtons(formationDraft.Count < formationTeam.Length ? remainingChoices : Array.Empty<string>(), OnFormationChoiceSelected);

            if (formationDraft.Count == 0)
            {
                SetActionButton(true, "Return Home", leaveAction);
            }
            else if (formationDraft.Count < formationTeam.Length)
            {
                SetActionButton(true, "Reset Formation", ResetFormationDraft);
            }
            else
            {
                SetActionButton(true, "Lock Formation", submitAction);
            }
        }

        private void ShowWaitingForOpponentFormation()
        {
            HideBlindBanRescue();
            HideResultRescue();
            if (currentPvpMatch == null)
            {
                ShowError("Ranked PvP", "Formation wait state unavailable.", "Match snapshot is missing.");
                return;
            }

            currentPanelState = MatchUiPanelState.Waiting;
            ClearFormationDraft();
            if (UseRescueFormation && TryRenderFormationRescue(locked: true))
            {
                return;
            }

            SetHeader(
                "Ranked PvP",
                "Step 3 of 4 • Formation Locked",
                BuildFormationLockedDetails(currentPvpMatch));
            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(true, "Return Home", ReturnHome);
        }

        private void RenderBotResult(StartBotMatchResponseDto response)
        {
            HideBlindBanRescue();
            HideFormationRescue();
            HideBattlePresentationRescue();
            currentPanelState = MatchUiPanelState.Result;
            StopReplay();
            replayMoments = BuildReplayMoments(response.battleState?.eventLog, response.whyChain);
            RefreshDecisiveMomentStrip(response.whyChain);

            if (UseRescueResult && TryRenderBotResultRescue(response))
            {
                return;
            }

            SetHeader(
                "Battle Bot",
                "Step 3 of 3 • Result",
                BuildResultSummary(
                    BuildOutcomeLabel(response.winner, true, true),
                    response.whySummary,
                    response.whyChain,
                    BuildEmojiSummary(response.playerTeam),
                    BuildEmojiSummary(response.botTeam),
                    "Bot"));
            SetChoiceButtons(BuildResultActions(true), OnResultActionSelected);
            SetActionButton(true, "Replay Highlights", ReplayHighlights);
        }

        private void RenderPvpResult(QueueOrJoinMatchResponseDto response, bool skipPresentation = false)
        {
            HideBlindBanRescue();
            HideFormationRescue();
            currentPanelState = MatchUiPanelState.Result;
            StopReplay();
            replayMoments = BuildReplayMoments(response.battleState?.eventLog, response.whyChain);
            RefreshDecisiveMomentStrip(response.whyChain);

            if (!skipPresentation && TryRenderPvpBattlePresentation(response))
            {
                return;
            }

            HideBattlePresentationRescue();
            if (UseRescueResult && TryRenderPvpResultRescue(response))
            {
                return;
            }

            SetHeader(
                "Ranked PvP",
                "Step 4 of 4 • Result",
                BuildResultSummary(
                    BuildOutcomeLabel(response.winner, IsCurrentUserPlayerA(), false),
                    response.whySummary,
                    response.whyChain,
                    BuildEmojiSummary(response.playerFinalTeam),
                    BuildEmojiSummary(response.opponentFinalTeam),
                    "Opponent") +
                $"\n\nYour ban: {HumanizeEmojiIdOrPending(response.opponentBannedEmojiId)} | " +
                $"Opponent ban: {HumanizeEmojiIdOrPending(response.playerBannedEmojiId)}");
            SetChoiceButtons(BuildResultActions(false), OnResultActionSelected);
            SetActionButton(true, "Replay Highlights", ReplayHighlights);
        }

        private IEnumerator SubmitBanChoice(string bannedEmojiId)
        {
            SetHeader(
                "Ranked PvP",
                "Step 2 of 4 • Ban locked",
                $"Locking ban on {HumanizeEmojiId(bannedEmojiId)}...\nSubmitting your blind ban to the server.");
            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);

            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null)
            {
                ShowError("Ranked PvP", "Ban failed.", "Bootstrap unavailable.");
                yield break;
            }

            var payload = new SubmitBanRequestDto
            {
                matchId = currentMatchId,
                playerId = bootstrap.SessionState.UserId,
                bannedEmojiId = bannedEmojiId,
            };

            using var request = bootstrap.FunctionClient.BuildJsonRequest("submit_ban", JsonUtility.ToJson(payload), bootstrap.SessionState.AccessToken);
            request.timeout = 10;
            if (!TryBeginWebRequest(request, out var operation, out var startError))
            {
                yield return RecoverPvpSnapshotOrShowError("Ranked PvP", "Ban failed.", startError);
                yield break;
            }

            yield return operation;

            if (request.result != UnityWebRequest.Result.Success)
            {
                yield return RecoverPvpSnapshotOrShowError("Ranked PvP", "Ban failed.", request.error);
                yield break;
            }

            QueueOrJoinMatchResponseDto snapshot = null;
            yield return FetchPvpSnapshot(result => snapshot = result, true);

            if (snapshot != null)
            {
                ApplyPvpSnapshot(snapshot);
                if (!string.IsNullOrWhiteSpace(snapshot.opponentBannedEmojiId))
                {
                    HapticFeedback.TriggerLightImpact();
                }

                SafeRenderCurrentPvpState("post ban submit snapshot");
                yield break;
            }

            HapticFeedback.TriggerLightImpact();
            ShowWaitingForOpponentBan();
            EnsurePolling();
        }

        private IEnumerator SubmitBotFormationRoutine()
        {
            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null)
            {
                ShowError("Battle Bot", "Formation submit failed.", "Bootstrap unavailable.");
                yield break;
            }

            if (currentBotMatch == null)
            {
                ShowError("Battle Bot", "Formation submit failed.", "Bot match snapshot is missing.");
                yield break;
            }

            if (!TryValidateFunctionsRuntime(bootstrap, out var runtimeError))
            {
                ShowError("Battle Bot", "Formation submit failed.", runtimeError);
                yield break;
            }

            var payload = new SubmitFormationRequestDto
            {
                matchId = currentMatchId,
                playerId = bootstrap.SessionState.UserId,
                formation = BuildFormationDtoFromDraft(),
            };

            SetHeader(
                "Battle Bot",
                "Step 2 of 3 • Formation locked",
                "Submitting your final 5-slot layout.");
            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);

            using var request = bootstrap.FunctionClient.BuildJsonRequest("submit_formation", JsonUtility.ToJson(payload), bootstrap.SessionState.AccessToken);
            request.timeout = 10;
            if (!TryBeginWebRequest(request, out var operation, out var startError))
            {
                ShowError("Battle Bot", "Formation submit failed.", startError);
                yield break;
            }

            yield return operation;

            if (request.result != UnityWebRequest.Result.Success)
            {
                ShowError("Battle Bot", "Formation submit failed.", BuildRequestErrorDetails(request));
                yield break;
            }

            if (!TryParseJsonResponse(request, out SubmitFormationResponseDto response, out var parseError))
            {
                ShowError("Battle Bot", "Formation submit failed.", parseError);
                yield break;
            }

            if (!string.Equals(response.phase, "finished", StringComparison.OrdinalIgnoreCase))
            {
                ShowError("Battle Bot", "Formation sync incomplete.", response.note);
                yield break;
            }

            currentBotMatch.status = response.status;
            currentBotMatch.phase = response.phase;
            currentBotMatch.playerFormation = response.playerFormation;
            currentBotMatch.botFormation = response.opponentFormation;
            currentBotMatch.playerTeam = response.playerFinalTeam ?? currentBotMatch.playerTeam;
            currentBotMatch.botTeam = response.opponentFinalTeam ?? currentBotMatch.botTeam;
            currentBotMatch.battleState = response.battleState;
            currentBotMatch.winner = response.winner;
            currentBotMatch.whySummary = response.whySummary;
            currentBotMatch.whyChain = response.whyChain ?? Array.Empty<string>();
            HapticFeedback.TriggerLightImpact();
            ClearFormationDraft();
            RenderBotResult(currentBotMatch);
        }

        private IEnumerator SubmitPvpFormationRoutine()
        {
            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null)
            {
                ShowError("Ranked PvP", "Formation submit failed.", "Bootstrap unavailable.");
                yield break;
            }

            if (!TryValidateFunctionsRuntime(bootstrap, out var runtimeError))
            {
                ShowError("Ranked PvP", "Formation submit failed.", runtimeError);
                yield break;
            }

            var payload = new SubmitFormationRequestDto
            {
                matchId = currentMatchId,
                playerId = bootstrap.SessionState.UserId,
                formation = BuildFormationDtoFromDraft(),
            };

            SetHeader(
                "Ranked PvP",
                "Step 3 of 4 • Formation locked",
                "Submitting your 5-slot battle layout.");
            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);

            using var request = bootstrap.FunctionClient.BuildJsonRequest("submit_formation", JsonUtility.ToJson(payload), bootstrap.SessionState.AccessToken);
            request.timeout = 10;
            if (!TryBeginWebRequest(request, out var operation, out var startError))
            {
                yield return RecoverPvpSnapshotOrShowError("Ranked PvP", "Formation submit failed.", startError);
                yield break;
            }

            yield return operation;

            if (request.result != UnityWebRequest.Result.Success)
            {
                yield return RecoverPvpSnapshotOrShowError("Ranked PvP", "Formation submit failed.", request.error);
                yield break;
            }

            if (!TryParseJsonResponse(request, out SubmitFormationResponseDto response, out var parseError))
            {
                yield return RecoverPvpSnapshotOrShowError("Ranked PvP", "Formation submit failed.", parseError);
                yield break;
            }

            QueueOrJoinMatchResponseDto snapshot = null;
            yield return FetchPvpSnapshot(result => snapshot = result, true);

            if (snapshot != null)
            {
                ClearFormationDraft();
                ApplyPvpSnapshot(snapshot);
                HapticFeedback.TriggerLightImpact();
                SafeRenderCurrentPvpState("post formation submit snapshot");
                yield break;
            }

            if (string.Equals(response.phase, "finished", StringComparison.OrdinalIgnoreCase))
            {
                ShowError("Ranked PvP", "Match sync incomplete.", "The server finished the battle, but the client could not load the final snapshot.");
                yield break;
            }

            if (currentPvpMatch == null)
            {
                ShowError("Ranked PvP", "Formation sync failed.", "Current match snapshot is missing.");
                yield break;
            }

            currentPvpMatch.playerFormation = response.playerFormation;
            currentPvpMatch.opponentFormation = response.opponentFormation;
            HapticFeedback.TriggerLightImpact();
            ClearFormationDraft();
            ShowWaitingForOpponentFormation();
            EnsurePolling();
        }

        private void OnBanChoiceSelected(int index)
        {
            if (index < 0 || index >= currentChoices.Length)
            {
                return;
            }

            HapticFeedback.TriggerLightImpact();
            StartCoroutine(SubmitBanChoice(currentChoices[index]));
        }

        private void OnFormationChoiceSelected(int index)
        {
            if (index < 0 || index >= currentChoices.Length)
            {
                return;
            }

            if (formationDraft.Count >= formationTeam.Length)
            {
                return;
            }

            var chosenEmojiId = currentChoices[index];
            if (formationDraft.Contains(chosenEmojiId))
            {
                return;
            }

            formationDraft.Add(chosenEmojiId);
            HapticFeedback.TriggerLightImpact();
            RefreshFormationView();
        }

        private bool TryRenderFormationRescue(bool locked)
        {
            if (currentPvpMatch == null || !HasPlayerFinalTeam(currentPvpMatch))
            {
                return false;
            }

            EnsureFormationRescueScreen();
            if (rescueFormationScreen == null)
            {
                return false;
            }

            var availableUnits = (currentPvpMatch.playerFinalTeam ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(FormationSlotOrder.Length)
                .Select(value => FormationRescueScreen.UnitView.FromApiId(value, enemyTone: false))
                .ToArray();
            if (availableUnits.Length == 0)
            {
                return false;
            }

            var assignments = PlayerFormationIsLocked(currentPvpMatch)
                ? BuildAssignmentsFromFormation(currentPvpMatch.playerFormation)
                : BuildRescueFormationAssignments();

            rescueFormationScreen.gameObject.SetActive(true);
            rescueFormationScreen.transform.SetAsLastSibling();
            rescueFormationScreen.Bind(
                availableUnits,
                currentPvpMatch.opponentBannedEmojiId,
                currentPvpMatch.playerBannedEmojiId,
                assignments,
                locked || PlayerFormationIsLocked(currentPvpMatch),
                currentPvpMatch.opponentFormation?.placements?.Length ?? 0,
                OnRescueFormationChanged,
                OnRescueFormationLocked,
                FormationRescueScreen.ScreenConfig.RankedDefault);

            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);
            return true;
        }

        private bool TryRenderBotFormationRescue(bool locked)
        {
            if (currentBotMatch == null)
            {
                return false;
            }

            EnsureFormationRescueScreen();
            if (rescueFormationScreen == null)
            {
                return false;
            }

            var availableUnits = (currentBotMatch.playerTeam ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(FormationSlotOrder.Length)
                .Select(value => FormationRescueScreen.UnitView.FromApiId(value, enemyTone: false))
                .ToArray();
            if (availableUnits.Length == 0)
            {
                return false;
            }

            var assignments = currentBotMatch.playerFormation?.placements != null && currentBotMatch.playerFormation.placements.Length == FormationSlotOrder.Length
                ? BuildAssignmentsFromFormation(currentBotMatch.playerFormation)
                : BuildRescueFormationAssignments();

            rescueFormationScreen.gameObject.SetActive(true);
            rescueFormationScreen.transform.SetAsLastSibling();
            rescueFormationScreen.Bind(
                availableUnits,
                string.Empty,
                string.Empty,
                assignments,
                locked,
                currentBotMatch.botFormation?.placements?.Length ?? 0,
                OnRescueFormationChanged,
                OnRescueBotFormationLocked,
                FormationRescueScreen.ScreenConfig.BotDefault);

            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);
            return true;
        }

        private void EnsureFormationRescueScreen()
        {
            if (rescueFormationScreen != null)
            {
                return;
            }

            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var mount = new GameObject("FormationRescueMount", typeof(RectTransform));
            mount.transform.SetParent(canvas.transform, false);
            var rect = mount.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rescueFormationScreen = mount.AddComponent<FormationRescueScreen>();
        }

        private void HideFormationRescue()
        {
            if (rescueFormationScreen == null)
            {
                return;
            }

            rescueFormationScreen.Hide();
            rescueFormationScreen.gameObject.SetActive(false);
        }

        private bool TryRenderBotResultRescue(StartBotMatchResponseDto response)
        {
            if (response == null)
            {
                return false;
            }

            EnsureResultRescueScreen();
            if (rescueResultScreen == null)
            {
                return false;
            }

            var playerWon = DidPlayerWin(response.winner, true);
            var isDraw = IsDrawResult(response.winner);
            var playerBoard = BuildResultBoardUnits(
                BuildAssignmentsFromFormation(response.playerFormation),
                response.playerTeam,
                BuildAliveLookup(response.battleState?.teamA),
                enemyTone: false,
                markWinner: playerWon);
            var botBoard = BuildResultBoardUnits(
                BuildAssignmentsFromFormation(response.botFormation),
                response.botTeam,
                BuildAliveLookup(response.battleState?.teamB),
                enemyTone: true,
                markWinner: !playerWon && !isDraw);
            var replayMoments = BuildResultMoments(response.battleState, response.whyChain);
            var recapHighlights = BuildBotResultRecapHighlights(response, playerBoard, botBoard, replayMoments, playerWon, isDraw);
            var result = new ResultRescueScreen.ResultModel
            {
                ResultKey = $"bot|{response.matchId}|{response.winner}|{response.whySummary}",
                StepText = "STEP 3 OF 3",
                HeaderTitle = "Battle Result",
                ModeChipText = "Bot Battle",
                OutcomeTitle = BuildOutcomeHeroText(response.winner, true),
                IsVictory = playerWon,
                IsDefeat = !playerWon && !isDraw,
                IsDraw = isDraw,
                OutcomeSummary = BuildResultOutcomeSummary(response.whySummary, recapHighlights),
                WhyTitle = BuildWhyHeadline(playerWon, isDraw),
                WhySummary = BuildWhyBody(response.whySummary, recapHighlights),
                HeroUnits = BuildHeroUnits(
                    playerBoard,
                    botBoard,
                    playerWon,
                    isDraw),
                YourBanFallbackText = "No ban phase",
                OpponentBanFallbackText = "No ban phase",
                RecapHighlights = recapHighlights,
                Moments = replayMoments,
                YourTeam = new ResultRescueScreen.TeamView
                {
                    Title = "Your Team",
                    StatusText = playerWon ? "WIN" : isDraw ? "DRAW" : "FINAL BOARD",
                    BoardUnits = playerBoard
                },
                OpponentTeam = new ResultRescueScreen.TeamView
                {
                    Title = "Bot Team",
                    StatusText = !playerWon && !isDraw ? "WIN" : isDraw ? "DRAW" : "FINAL BOARD",
                    BoardUnits = botBoard
                }
            };

            rescueResultScreen.gameObject.SetActive(true);
            rescueResultScreen.transform.SetAsLastSibling();
            rescueResultScreen.Bind(
                result,
                PlayAgainFromResult,
                EditSquadFromResult,
                ReturnHome,
                null);

            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);
            return true;
        }

        private bool TryRenderPvpResultRescue(QueueOrJoinMatchResponseDto response)
        {
            if (response == null)
            {
                return false;
            }

            EnsureResultRescueScreen();
            if (rescueResultScreen == null)
            {
                return false;
            }

            var playerIsA = IsCurrentUserPlayerA();
            var playerWon = DidPlayerWin(response.winner, playerIsA);
            var isDraw = IsDrawResult(response.winner);
            var playerAliveLookup = BuildAliveLookup(playerIsA ? response.battleState?.teamA : response.battleState?.teamB);
            var opponentAliveLookup = BuildAliveLookup(playerIsA ? response.battleState?.teamB : response.battleState?.teamA);
            var yourBoard = BuildResultBoardUnits(
                BuildAssignmentsFromFormation(response.playerFormation),
                response.playerFinalTeam,
                playerAliveLookup,
                enemyTone: false,
                markWinner: playerWon);
            var opponentBoard = BuildResultBoardUnits(
                BuildAssignmentsFromFormation(response.opponentFormation),
                response.opponentFinalTeam,
                opponentAliveLookup,
                enemyTone: true,
                markWinner: !playerWon && !isDraw);
            var replayMoments = BuildResultMoments(response.battleState, response.whyChain);
            var recapHighlights = BuildPvpResultRecapHighlights(response, yourBoard, opponentBoard, replayMoments, playerWon, isDraw);

            var result = new ResultRescueScreen.ResultModel
            {
                ResultKey = $"{response.matchId}|{response.winner}|{response.opponentBannedEmojiId}|{response.playerBannedEmojiId}",
                StepText = "STEP 4 OF 4",
                HeaderTitle = "Battle Result",
                ModeChipText = "Ranked",
                OutcomeTitle = BuildOutcomeHeroText(response.winner, playerIsA),
                IsVictory = playerWon,
                IsDefeat = !playerWon && !isDraw,
                IsDraw = isDraw,
                OutcomeSummary = BuildResultOutcomeSummary(response.whySummary, recapHighlights),
                WhyTitle = BuildWhyHeadline(playerWon, isDraw),
                WhySummary = BuildWhyBody(response.whySummary, recapHighlights),
                HeroUnits = BuildHeroUnits(yourBoard, opponentBoard, playerWon, isDraw),
                YourBanId = response.opponentBannedEmojiId,
                OpponentBanId = response.playerBannedEmojiId,
                YourBanFallbackText = "Ban result unavailable",
                OpponentBanFallbackText = "Ban result unavailable",
                RecapHighlights = recapHighlights,
                Moments = replayMoments,
                YourTeam = new ResultRescueScreen.TeamView
                {
                    Title = "Your Team",
                    StatusText = playerWon ? "WIN" : isDraw ? "DRAW" : "FINAL BOARD",
                    BoardUnits = yourBoard
                },
                OpponentTeam = new ResultRescueScreen.TeamView
                {
                    Title = "Opponent Team",
                    StatusText = !playerWon && !isDraw ? "WIN" : isDraw ? "DRAW" : "FINAL BOARD",
                    BoardUnits = opponentBoard
                }
            };

            rescueResultScreen.gameObject.SetActive(true);
            rescueResultScreen.transform.SetAsLastSibling();
            rescueResultScreen.Bind(
                result,
                PlayAgainFromResult,
                EditSquadFromResult,
                ReturnHome,
                null);

            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);
            return true;
        }

        private void EnsureResultRescueScreen()
        {
            if (rescueResultScreen != null)
            {
                return;
            }

            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var mount = new GameObject("ResultRescueMount", typeof(RectTransform));
            mount.transform.SetParent(canvas.transform, false);
            var rect = mount.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rescueResultScreen = mount.AddComponent<ResultRescueScreen>();
        }

        private void HideResultRescue()
        {
            if (rescueResultScreen != null)
            {
                rescueResultScreen.Hide();
                rescueResultScreen.gameObject.SetActive(false);
            }

            HideBattlePresentationRescue();
        }

        private bool TryRenderPvpBattlePresentation(QueueOrJoinMatchResponseDto response)
        {
            if (!UseRescueBattlePresentation || response == null)
            {
                return false;
            }

            EnsureBattlePresentationRescueScreen();
            if (rescueBattlePresentationScreen == null)
            {
                return false;
            }

            var playerIsA = IsCurrentUserPlayerA();
            var playerAliveLookup = BuildAliveLookup(playerIsA ? response.battleState?.teamA : response.battleState?.teamB);
            var opponentAliveLookup = BuildAliveLookup(playerIsA ? response.battleState?.teamB : response.battleState?.teamA);
            var yourBoard = BuildResultBoardUnits(
                BuildAssignmentsFromFormation(response.playerFormation),
                response.playerFinalTeam,
                playerAliveLookup,
                enemyTone: false,
                markWinner: DidPlayerWin(response.winner, playerIsA));
            var opponentBoard = BuildResultBoardUnits(
                BuildAssignmentsFromFormation(response.opponentFormation),
                response.opponentFinalTeam,
                opponentAliveLookup,
                enemyTone: true,
                markWinner: !DidPlayerWin(response.winner, playerIsA) && !IsDrawResult(response.winner));

            var moments = BuildResultMoments(response.battleState, response.whyChain);
            var hasEventPlayback = response.battleState?.eventLog != null &&
                                   response.battleState.eventLog.Any(evt => evt != null && !string.IsNullOrWhiteSpace(evt.caption));
            var model = new BattlePresentationRescueScreen.PresentationModel
            {
                PresentationKey = $"{response.matchId}|presentation|{response.winner}|{response.opponentBannedEmojiId}|{response.playerBannedEmojiId}",
                StepText = "STEP 4 OF 4",
                ModeChipText = "Ranked PvP",
                HeaderTitle = "Battle Presentation",
                IntroTitle = "Squads Deploy",
                IntroSummary = hasEventPlayback
                    ? "Both formations are locked. Watch the battle log hit the arena."
                    : "Both formations are locked. Quick battle highlights are ready.",
                FinishTitle = BuildOutcomeHeroText(response.winner, playerIsA),
                FinishSummary = BuildWhyBody(response.whySummary),
                IsVictory = DidPlayerWin(response.winner, playerIsA),
                IsDefeat = !DidPlayerWin(response.winner, playerIsA) && !IsDrawResult(response.winner),
                IsDraw = IsDrawResult(response.winner),
                UsedEventPlayback = hasEventPlayback,
                ShowBanRecap = true,
                YourBanId = response.opponentBannedEmojiId,
                OpponentBanId = response.playerBannedEmojiId,
                YourBanFallbackText = "No ban captured",
                OpponentBanFallbackText = "No ban captured",
                YourBoard = yourBoard,
                OpponentBoard = opponentBoard,
                Moments = moments
            };

            rescueBattlePresentationScreen.gameObject.SetActive(true);
            rescueBattlePresentationScreen.transform.SetAsLastSibling();
            rescueBattlePresentationScreen.Bind(
                model,
                () => CompletePvpBattlePresentation(response.matchId));

            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);
            return true;
        }

        private void CompletePvpBattlePresentation(string matchId)
        {
            if (currentPvpMatch != null &&
                string.Equals(currentPvpMatch.matchId, matchId, StringComparison.Ordinal))
            {
                RenderPvpResult(currentPvpMatch, true);
                return;
            }

            HideBattlePresentationRescue();
        }

        private void EnsureBattlePresentationRescueScreen()
        {
            if (rescueBattlePresentationScreen != null)
            {
                return;
            }

            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var mount = new GameObject("BattlePresentationRescueMount", typeof(RectTransform));
            mount.transform.SetParent(canvas.transform, false);
            var rect = mount.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rescueBattlePresentationScreen = mount.AddComponent<BattlePresentationRescueScreen>();
        }

        private void HideBattlePresentationRescue()
        {
            if (rescueBattlePresentationScreen == null)
            {
                return;
            }

            rescueBattlePresentationScreen.Hide();
            rescueBattlePresentationScreen.gameObject.SetActive(false);
        }

        private void EnsureEmojiClashRescueScreen()
        {
            if (rescueEmojiClashScreen != null)
            {
                return;
            }

            var canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var mount = new GameObject("EmojiClashRescueMount", typeof(RectTransform));
            mount.transform.SetParent(canvas.transform, false);
            var rect = mount.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rescueEmojiClashScreen = mount.AddComponent<EmojiClashRescueScreen>();
        }

        private void HideEmojiClashRescue()
        {
            StopEmojiClashResolveRoutine();
            if (rescueEmojiClashScreen == null)
            {
                return;
            }

            rescueEmojiClashScreen.Hide();
            rescueEmojiClashScreen.gameObject.SetActive(false);
        }

        private void StopEmojiClashResolveRoutine()
        {
            if (emojiClashResolveRoutine == null)
            {
                return;
            }

            StopCoroutine(emojiClashResolveRoutine);
            emojiClashResolveRoutine = null;
        }

        private const float EmojiClashLockPauseSeconds = 0.22f;
        private const float EmojiClashResolvedPauseSeconds = 0.95f;

        private void RenderEmojiClashTurn()
        {
            HideBlindBanRescue();
            HideFormationRescue();
            HideResultRescue();
            EnsureEmojiClashRescueScreen();
            if (emojiClashController == null || rescueEmojiClashScreen == null)
            {
                ShowError("Emoji Clash", "Turn unavailable.", "The local clash controller could not render the turn.");
                return;
            }

            currentPanelState = MatchUiPanelState.Ban;
            var turnViewModel = emojiClashController.BuildTurnViewModel();
            rescueEmojiClashScreen.gameObject.SetActive(true);
            rescueEmojiClashScreen.transform.SetAsLastSibling();
            rescueEmojiClashScreen.BindTurn(
                turnViewModel,
                OnEmojiClashPick,
                ReturnHome,
                OpenDeckBuilderFromClash);

            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);
        }

        private void RenderEmojiClashResult()
        {
            HideBlindBanRescue();
            HideFormationRescue();
            HideResultRescue();
            EnsureEmojiClashRescueScreen();
            if (emojiClashController == null || rescueEmojiClashScreen == null)
            {
                ShowError("Emoji Clash", "Result unavailable.", "The local clash result could not be rendered.");
                return;
            }

            currentPanelState = MatchUiPanelState.Result;
            rescueEmojiClashScreen.gameObject.SetActive(true);
            rescueEmojiClashScreen.transform.SetAsLastSibling();
            rescueEmojiClashScreen.BindResult(
                emojiClashController.BuildResultViewModel(),
                PlayAgainFromEmojiClash,
                ReturnHome,
                OpenDeckBuilderFromClash);

            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(false, string.Empty, null);
        }

        private void OnEmojiClashPick(string unitKey)
        {
            if (emojiClashController == null || emojiClashResolveRoutine != null)
            {
                return;
            }

            if (!emojiClashController.HandlePlayerPick(unitKey))
            {
                return;
            }

            HapticFeedback.TriggerLightImpact();
            RenderEmojiClashTurn();
            emojiClashResolveRoutine = StartCoroutine(ResolveEmojiClashTurnRoutine());
        }

        private IEnumerator ResolveEmojiClashTurnRoutine()
        {
            yield return new WaitForSecondsRealtime(EmojiClashLockPauseSeconds);
            emojiClashController?.ResolveLockedTurn();
            HapticFeedback.TriggerLightImpact();
            RenderEmojiClashTurn();

            yield return new WaitForSecondsRealtime(EmojiClashResolvedPauseSeconds);

            if (emojiClashController == null)
            {
                emojiClashResolveRoutine = null;
                yield break;
            }

            if (emojiClashController.IsMatchComplete)
            {
                emojiClashResolveRoutine = null;
                RenderEmojiClashResult();
                yield break;
            }

            emojiClashController.AdvanceToNextTurn();
            emojiClashResolveRoutine = null;
            RenderEmojiClashTurn();
        }

        private void PlayAgainFromEmojiClash()
        {
            if (emojiClashController == null)
            {
                emojiClashController = new EmojiClashController();
            }

            emojiClashController.StartEmojiClash();
            RenderEmojiClashTurn();
        }

        private void OpenDeckBuilderFromClash()
        {
            LaunchSelections.BeginDeckEdit();
            SceneManager.LoadScene(SceneNames.DeckBuilder);
        }

        private void PlayAgainFromResult()
        {
            OnResultActionSelected(0);
        }

        private void EditSquadFromResult()
        {
            OnResultActionSelected(1);
        }

        private static bool IsDrawResult(string winner)
        {
            return string.IsNullOrWhiteSpace(winner) || string.Equals(winner, "draw", StringComparison.OrdinalIgnoreCase);
        }

        private static bool DidPlayerWin(string winner, bool playerIsPlayerA)
        {
            if (IsDrawResult(winner))
            {
                return false;
            }

            return playerIsPlayerA
                ? string.Equals(winner, "player_a", StringComparison.OrdinalIgnoreCase)
                : string.Equals(winner, "player_b", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildOutcomeHeroText(string winner, bool playerIsPlayerA)
        {
            if (IsDrawResult(winner))
            {
                return "DRAW";
            }

            return DidPlayerWin(winner, playerIsPlayerA) ? "VICTORY" : "DEFEAT";
        }

        private static string BuildWhyHeadline(bool playerWon, bool isDraw)
        {
            if (isDraw)
            {
                return "Battle Summary";
            }

            return playerWon ? "Why You Won" : "Why It Slipped";
        }

        private static string BuildWhyBody(string whySummary, IReadOnlyList<ResultRescueScreen.MomentView> recapHighlights = null)
        {
            var recapLines = recapHighlights?
                .Where(moment => moment != null && !string.IsNullOrWhiteSpace(moment.Caption))
                .Select(moment => $"- {moment.Caption.Trim()}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToArray() ?? Array.Empty<string>();
            if (recapLines.Length > 0)
            {
                return string.Join("\n", recapLines);
            }

            return string.IsNullOrWhiteSpace(whySummary)
                ? "Battle recap unavailable. Replay details coming soon."
                : whySummary.Trim();
        }

        private static string BuildResultOutcomeSummary(string whySummary, IReadOnlyList<ResultRescueScreen.MomentView> recapHighlights = null)
        {
            var headline = recapHighlights?
                .FirstOrDefault(moment => moment != null && !string.IsNullOrWhiteSpace(moment.Caption))
                ?.Caption;
            if (!string.IsNullOrWhiteSpace(headline))
            {
                return headline.Trim();
            }

            return string.IsNullOrWhiteSpace(whySummary)
                ? "Battle recap available below."
                : whySummary.Trim();
        }

        private static Dictionary<string, bool> BuildAliveLookup(BattleUnitStateDto[] units)
        {
            var lookup = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            if (units == null)
            {
                return lookup;
            }

            foreach (var unit in units)
            {
                if (unit == null || string.IsNullOrWhiteSpace(unit.emojiId))
                {
                    continue;
                }

                lookup[UnitIconLibrary.NormalizeUnitKey(unit.emojiId)] = unit.alive;
            }

            return lookup;
        }

        private static ResultRescueScreen.UnitView[] BuildResultBoardUnits(
            string[] orderedAssignments,
            string[] fallbackUnits,
            IReadOnlyDictionary<string, bool> aliveLookup,
            bool enemyTone,
            bool markWinner)
        {
            var result = new ResultRescueScreen.UnitView[FormationSlotOrder.Length];
            for (var index = 0; index < result.Length; index++)
            {
                var unitId = orderedAssignments != null && index < orderedAssignments.Length
                    ? orderedAssignments[index]
                    : string.Empty;
                if (string.IsNullOrWhiteSpace(unitId) && fallbackUnits != null && index < fallbackUnits.Length)
                {
                    unitId = fallbackUnits[index];
                }

                if (string.IsNullOrWhiteSpace(unitId))
                {
                    continue;
                }

                var normalized = UnitIconLibrary.NormalizeUnitKey(unitId);
                var alive = false;
                var hasAlive = aliveLookup != null && aliveLookup.TryGetValue(normalized, out alive);
                result[index] = ResultRescueScreen.UnitView.FromApiId(
                    unitId,
                    enemyTone,
                    FormationSlotShortLabels[index],
                    isWinner: markWinner && (!hasAlive || alive),
                    isAliveKnown: hasAlive,
                    isAlive: hasAlive && alive);
            }

            return result;
        }

        private static ResultRescueScreen.UnitView[] BuildHeroUnits(
            IReadOnlyList<ResultRescueScreen.UnitView> yourBoard,
            IReadOnlyList<ResultRescueScreen.UnitView> opponentBoard,
            bool playerWon,
            bool isDraw)
        {
            if (isDraw)
            {
                return yourBoard?
                    .Where(unit => !string.IsNullOrWhiteSpace(unit.Id))
                    .Take(3)
                    .ToArray() ?? Array.Empty<ResultRescueScreen.UnitView>();
            }

            var source = playerWon ? yourBoard : opponentBoard;
            var winners = source?
                .Where(unit => !string.IsNullOrWhiteSpace(unit.Id) && (!unit.IsAliveKnown || unit.IsAlive))
                .Take(3)
                .ToArray() ?? Array.Empty<ResultRescueScreen.UnitView>();

            return winners.Length > 0
                ? winners
                : source?.Where(unit => !string.IsNullOrWhiteSpace(unit.Id)).Take(3).ToArray()
                    ?? Array.Empty<ResultRescueScreen.UnitView>();
        }

        private static ResultRescueScreen.MomentView[] BuildResultMoments(BattleStateDto battleState, string[] whyChain)
        {
            var unitLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (battleState?.teamA != null)
            {
                foreach (var unit in battleState.teamA)
                {
                    if (unit != null && !string.IsNullOrWhiteSpace(unit.unitId) && !string.IsNullOrWhiteSpace(unit.emojiId))
                    {
                        unitLookup[unit.unitId] = unit.emojiId;
                    }
                }
            }

            if (battleState?.teamB != null)
            {
                foreach (var unit in battleState.teamB)
                {
                    if (unit != null && !string.IsNullOrWhiteSpace(unit.unitId) && !string.IsNullOrWhiteSpace(unit.emojiId))
                    {
                        unitLookup[unit.unitId] = unit.emojiId;
                    }
                }
            }

            var list = new List<ResultRescueScreen.MomentView>();
            if (battleState?.eventLog != null)
            {
                foreach (var evt in battleState.eventLog)
                {
                    if (evt == null || string.IsNullOrWhiteSpace(evt.caption))
                    {
                        continue;
                    }

                    list.Add(new ResultRescueScreen.MomentView
                    {
                        Caption = evt.caption,
                        ActorId = ResolveMomentUnitId(evt.actor, unitLookup),
                        TargetId = ResolveMomentUnitId(evt.target, unitLookup)
                    });

                    if (list.Count >= 4)
                    {
                        break;
                    }
                }
            }

            if (list.Count == 0 && whyChain != null)
            {
                foreach (var entry in whyChain)
                {
                    if (string.IsNullOrWhiteSpace(entry))
                    {
                        continue;
                    }

                    list.Add(new ResultRescueScreen.MomentView
                    {
                        Caption = entry.Trim(),
                        ActorId = string.Empty,
                        TargetId = string.Empty
                    });

                    if (list.Count >= 4)
                    {
                        break;
                    }
                }
            }

            return list.ToArray();
        }

        private static string ResolveMomentUnitId(string candidate, IReadOnlyDictionary<string, string> unitLookup)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return string.Empty;
            }

            if (unitLookup != null && unitLookup.TryGetValue(candidate, out var emojiId))
            {
                return emojiId;
            }

            return candidate;
        }

        private static ResultRescueScreen.MomentView[] BuildPvpResultRecapHighlights(
            QueueOrJoinMatchResponseDto response,
            IReadOnlyList<ResultRescueScreen.UnitView> yourBoard,
            IReadOnlyList<ResultRescueScreen.UnitView> opponentBoard,
            IReadOnlyList<ResultRescueScreen.MomentView> replayMoments,
            bool playerWon,
            bool isDraw)
        {
            var highlights = new List<ResultRescueScreen.MomentView>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddRecapHighlight(
                highlights,
                seen,
                BuildOpponentBanRecap(response.playerBannedEmojiId),
                actorId: string.Empty,
                targetId: response.playerBannedEmojiId);
            AddRecapHighlight(
                highlights,
                seen,
                BuildPlayerBanRecap(response.opponentBannedEmojiId),
                actorId: string.Empty,
                targetId: response.opponentBannedEmojiId);
            AddRecapHighlight(
                highlights,
                seen,
                BuildWinnerSurvivorRecap(playerWon, isDraw, yourBoard, opponentBoard, "Your", "Opponent"),
                actorId: ResolveRecapActorId(playerWon, isDraw, yourBoard, opponentBoard),
                targetId: string.Empty);
            AddRecapHighlight(
                highlights,
                seen,
                BuildFormationAnchorRecap(yourBoard, "Your"),
                actorId: ResolveAnchorActorId(yourBoard),
                targetId: string.Empty);
            AddRecapHighlight(
                highlights,
                seen,
                replayMoments?.FirstOrDefault(moment => moment != null && !string.IsNullOrWhiteSpace(moment.Caption))?.Caption,
                actorId: replayMoments?.FirstOrDefault(moment => moment != null && !string.IsNullOrWhiteSpace(moment.Caption))?.ActorId,
                targetId: replayMoments?.FirstOrDefault(moment => moment != null && !string.IsNullOrWhiteSpace(moment.Caption))?.TargetId);

            return highlights.Take(4).ToArray();
        }

        private static ResultRescueScreen.MomentView[] BuildBotResultRecapHighlights(
            StartBotMatchResponseDto response,
            IReadOnlyList<ResultRescueScreen.UnitView> yourBoard,
            IReadOnlyList<ResultRescueScreen.UnitView> botBoard,
            IReadOnlyList<ResultRescueScreen.MomentView> replayMoments,
            bool playerWon,
            bool isDraw)
        {
            var highlights = new List<ResultRescueScreen.MomentView>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddRecapHighlight(
                highlights,
                seen,
                BuildWinnerSurvivorRecap(playerWon, isDraw, yourBoard, botBoard, "Your", "Bot"),
                actorId: ResolveRecapActorId(playerWon, isDraw, yourBoard, botBoard),
                targetId: string.Empty);
            AddRecapHighlight(
                highlights,
                seen,
                BuildFormationAnchorRecap(yourBoard, "Your"),
                actorId: ResolveAnchorActorId(yourBoard),
                targetId: string.Empty);
            AddRecapHighlight(
                highlights,
                seen,
                BuildFormationAnchorRecap(botBoard, "Bot"),
                actorId: ResolveAnchorActorId(botBoard),
                targetId: string.Empty);
            AddRecapHighlight(
                highlights,
                seen,
                replayMoments?.FirstOrDefault(moment => moment != null && !string.IsNullOrWhiteSpace(moment.Caption))?.Caption,
                actorId: replayMoments?.FirstOrDefault(moment => moment != null && !string.IsNullOrWhiteSpace(moment.Caption))?.ActorId,
                targetId: replayMoments?.FirstOrDefault(moment => moment != null && !string.IsNullOrWhiteSpace(moment.Caption))?.TargetId);

            return highlights.Take(4).ToArray();
        }

        private static void AddRecapHighlight(
            ICollection<ResultRescueScreen.MomentView> list,
            ISet<string> seenCaptions,
            string caption,
            string actorId,
            string targetId)
        {
            if (string.IsNullOrWhiteSpace(caption))
            {
                return;
            }

            var normalized = caption.Trim();
            if (!seenCaptions.Add(normalized))
            {
                return;
            }

            list.Add(new ResultRescueScreen.MomentView
            {
                Caption = normalized,
                ActorId = string.IsNullOrWhiteSpace(actorId) ? string.Empty : actorId,
                TargetId = string.IsNullOrWhiteSpace(targetId) ? string.Empty : targetId
            });
        }

        private static string BuildOpponentBanRecap(string bannedOnPlayerId)
        {
            var bannedName = HumanizeOptionalUnitName(bannedOnPlayerId);
            return string.IsNullOrWhiteSpace(bannedName)
                ? string.Empty
                : $"Opponent banned {bannedName}, so it never reached your formation.";
        }

        private static string BuildPlayerBanRecap(string bannedOnOpponentId)
        {
            var bannedName = HumanizeOptionalUnitName(bannedOnOpponentId);
            return string.IsNullOrWhiteSpace(bannedName)
                ? string.Empty
                : $"You banned {bannedName} before squads deployed.";
        }

        private static string BuildWinnerSurvivorRecap(
            bool playerWon,
            bool isDraw,
            IReadOnlyList<ResultRescueScreen.UnitView> yourBoard,
            IReadOnlyList<ResultRescueScreen.UnitView> opponentBoard,
            string yourOwnerLabel,
            string opponentOwnerLabel)
        {
            if (isDraw)
            {
                var yourAlive = CountAliveUnits(yourBoard);
                var opponentAlive = CountAliveUnits(opponentBoard);
                if (yourAlive > 0 || opponentAlive > 0)
                {
                    return $"The match ended in a draw with survivors still on both boards.";
                }

                return string.Empty;
            }

            var source = playerWon ? yourBoard : opponentBoard;
            var ownerLabel = playerWon ? yourOwnerLabel : opponentOwnerLabel;
            var focal = FindFocalSurvivor(source);
            if (string.IsNullOrWhiteSpace(focal.Id))
            {
                return string.Empty;
            }

            var slotName = HumanizeSlotLabel(focal.SlotLabel);
            if (focal.IsAliveKnown && focal.IsAlive)
            {
                if (slotName.StartsWith("Front", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{ownerLabel} {focal.Name} held {slotName} through the finish.";
                }

                return $"{ownerLabel} {focal.Name} survived in {slotName} and finished the match.";
            }

            return string.Empty;
        }

        private static string BuildFormationAnchorRecap(IReadOnlyList<ResultRescueScreen.UnitView> board, string ownerLabel)
        {
            var anchor = FindFormationAnchor(board);
            if (string.IsNullOrWhiteSpace(anchor.Id))
            {
                return string.Empty;
            }

            var slotName = HumanizeSlotLabel(anchor.SlotLabel);
            return $"{ownerLabel} {anchor.Name} started in {slotName}.";
        }

        private static string ResolveRecapActorId(
            bool playerWon,
            bool isDraw,
            IReadOnlyList<ResultRescueScreen.UnitView> yourBoard,
            IReadOnlyList<ResultRescueScreen.UnitView> opponentBoard)
        {
            if (isDraw)
            {
                return string.Empty;
            }

            var source = playerWon ? yourBoard : opponentBoard;
            return FindFocalSurvivor(source).Id;
        }

        private static string ResolveAnchorActorId(IReadOnlyList<ResultRescueScreen.UnitView> board)
        {
            return FindFormationAnchor(board).Id;
        }

        private static ResultRescueScreen.UnitView FindFocalSurvivor(IReadOnlyList<ResultRescueScreen.UnitView> board)
        {
            if (board == null)
            {
                return default;
            }

            var alive = board
                .Where(unit => !string.IsNullOrWhiteSpace(unit.Id) && unit.IsAliveKnown && unit.IsAlive)
                .OrderByDescending(unit => ScoreSlotPriority(unit.SlotLabel, preferBack: true))
                .ThenBy(unit => unit.Name, StringComparer.Ordinal)
                .ToArray();
            if (alive.Length > 0)
            {
                return alive[0];
            }

            return default;
        }

        private static ResultRescueScreen.UnitView FindFormationAnchor(IReadOnlyList<ResultRescueScreen.UnitView> board)
        {
            if (board == null)
            {
                return default;
            }

            var populated = board
                .Where(unit => !string.IsNullOrWhiteSpace(unit.Id))
                .OrderByDescending(unit => ScoreSlotPriority(unit.SlotLabel, preferBack: false))
                .ThenBy(unit => unit.Name, StringComparer.Ordinal)
                .ToArray();
            return populated.Length > 0 ? populated[0] : default;
        }

        private static int CountAliveUnits(IReadOnlyList<ResultRescueScreen.UnitView> board)
        {
            return board?.Count(unit => !string.IsNullOrWhiteSpace(unit.Id) && unit.IsAliveKnown && unit.IsAlive) ?? 0;
        }

        private static int ScoreSlotPriority(string slotLabel, bool preferBack)
        {
            return slotLabel switch
            {
                "FC" => preferBack ? 3 : 10,
                "FL" => preferBack ? 2 : 9,
                "FR" => preferBack ? 2 : 8,
                "BR" => preferBack ? 10 : 4,
                "BL" => preferBack ? 9 : 5,
                _ => 0
            };
        }

        private static string HumanizeSlotLabel(string shortLabel)
        {
            return shortLabel switch
            {
                "FL" => "Front Left",
                "FC" => "Front Center",
                "FR" => "Front Right",
                "BL" => "Back Left",
                "BR" => "Back Right",
                _ => "the board"
            };
        }

        private static string HumanizeOptionalUnitName(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return string.Empty;
            }

            var normalized = UnitIconLibrary.NormalizeUnitKey(unitId);
            return EmojiIdUtility.TryFromApiId(normalized, out var emojiId)
                ? EmojiIdUtility.ToDisplayName(emojiId)
                : normalized;
        }

        private void OnRescueFormationChanged(string[] orderedAssignments)
        {
            rescueFormationAssignments = CloneFormationAssignments(orderedAssignments);
        }

        private void OnRescueFormationLocked(string[] orderedAssignments)
        {
            rescueFormationAssignments = CloneFormationAssignments(orderedAssignments);
            HapticFeedback.TriggerLightImpact();
            TryRenderFormationRescue(locked: true);
            SubmitPvpFormation();
        }

        private void OnRescueBotFormationLocked(string[] orderedAssignments)
        {
            rescueFormationAssignments = CloneFormationAssignments(orderedAssignments);
            HapticFeedback.TriggerLightImpact();
            TryRenderBotFormationRescue(locked: true);
            SubmitBotFormation();
        }

        private void SubmitBotFormation()
        {
            StartCoroutine(SubmitBotFormationRoutine());
        }

        private void SubmitPvpFormation()
        {
            StartCoroutine(SubmitPvpFormationRoutine());
        }

        private void RefreshFormationView()
        {
            switch (activeFormationContext)
            {
                case FormationContext.Bot:
                    ShowBotFormationPhase();
                    break;
                case FormationContext.Pvp:
                    ShowPvpFormationPhase();
                    break;
            }
        }

        private void ResetFormationDraft()
        {
            formationDraft.Clear();
            RefreshFormationView();
        }

        private void ClearFormationDraft()
        {
            formationDraft.Clear();
            formationTeam = Array.Empty<string>();
            formationDraftKey = string.Empty;
            activeFormationContext = FormationContext.None;
            rescueFormationAssignments = Array.Empty<string>();
        }

        private void EnsureFormationDraft(string key, string[] team)
        {
            if (string.Equals(formationDraftKey, key, StringComparison.Ordinal))
            {
                return;
            }

            formationDraftKey = key;
            formationDraft.Clear();
            formationTeam = team ?? Array.Empty<string>();
            rescueFormationAssignments = Array.Empty<string>();
        }

        private FormationDto BuildFormationDtoFromDraft()
        {
            var rescueAssignments = BuildRescueFormationAssignments();
            if (rescueAssignments.Length == FormationSlotOrder.Length &&
                rescueAssignments.All(emojiId => !string.IsNullOrWhiteSpace(emojiId)))
            {
                var rescuePlacements = new FormationPlacementDto[rescueAssignments.Length];
                for (var index = 0; index < rescueAssignments.Length; index++)
                {
                    rescuePlacements[index] = new FormationPlacementDto
                    {
                        slot = FormationSlotOrder[index],
                        emojiId = rescueAssignments[index],
                    };
                }

                return new FormationDto
                {
                    placements = rescuePlacements,
                };
            }

            var placements = new FormationPlacementDto[formationDraft.Count];
            for (var index = 0; index < formationDraft.Count; index++)
            {
                placements[index] = new FormationPlacementDto
                {
                    slot = FormationSlotOrder[index],
                    emojiId = formationDraft[index],
                };
            }

            return new FormationDto
            {
                placements = placements,
            };
        }

        private IEnumerator FetchPvpSnapshot(
            Action<QueueOrJoinMatchResponseDto> onSuccess,
            bool useCurrentMatchId,
            int attempts = 1,
            int timeoutSeconds = 10)
        {
            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null || currentPvpQueueRequest == null)
            {
                yield break;
            }

            var payload = new QueueOrJoinMatchRequestDto
            {
                userId = currentPvpQueueRequest.userId,
                deckId = currentPvpQueueRequest.deckId,
                playerDeck = currentPvpQueueRequest.playerDeck,
                matchId = useCurrentMatchId ? currentMatchId : string.Empty,
                forceFreshEntry = !useCurrentMatchId && currentPvpQueueRequest.forceFreshEntry,
            };

            lastPvpRequestError = string.Empty;
            var maxAttempts = Mathf.Max(1, attempts);

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var request = bootstrap.FunctionClient.BuildJsonRequest(
                    "queue_or_join_match",
                    JsonUtility.ToJson(payload),
                    bootstrap.SessionState.AccessToken);
                request.timeout = timeoutSeconds;
                if (!TryBeginWebRequest(request, out var operation, out var startError))
                {
                    lastPvpRequestError = startError;
                    yield break;
                }

                yield return operation;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    onSuccess?.Invoke(JsonUtility.FromJson<QueueOrJoinMatchResponseDto>(request.downloadHandler.text));
                    yield break;
                }

                lastPvpRequestError = BuildRequestErrorDetails(request);

                if (!useCurrentMatchId && IsUnauthorizedResponse(request))
                {
                    var recovered = false;
                    yield return bootstrap.RecoverSessionAfterUnauthorized(success => recovered = success);
                    if (recovered)
                    {
                        payload.userId = bootstrap.SessionState.UserId;
                        if (currentPvpQueueRequest != null)
                        {
                            currentPvpQueueRequest.userId = bootstrap.SessionState.UserId;
                        }

                        // Retry immediately with a refreshed anonymous token/session.
                        continue;
                    }
                }

                if (attempt >= maxAttempts || !ShouldRetrySnapshotRequest(request))
                {
                    yield break;
                }

                yield return new WaitForSeconds(1f);
            }
        }

        private IEnumerator RecoverPvpSnapshotOrShowError(string title, string subtitle, string fallbackDetails)
        {
            QueueOrJoinMatchResponseDto snapshot = null;
            yield return FetchPvpSnapshot(result => snapshot = result, true);

            if (snapshot != null)
            {
                ClearFormationDraft();
                ApplyPvpSnapshot(snapshot);
                SafeRenderCurrentPvpState("recovered ranked snapshot");
                yield break;
            }

            ShowError(title, subtitle, fallbackDetails);
        }

        private void ApplyPvpSnapshot(QueueOrJoinMatchResponseDto snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            currentPvpMatch = snapshot;
            currentMatchId = snapshot.matchId;
            UpdateLocalPhaseDeadline(snapshot);

            if (IsMatchFinished(snapshot))
            {
                LaunchSelections.ClearRankedResume();
                return;
            }

            var persistedDeck = snapshot.playerDeck != null && snapshot.playerDeck.Length == 6
                ? snapshot.playerDeck
                : currentPvpQueueRequest?.playerDeck ?? Array.Empty<string>();
            var persistedDeckId = !string.IsNullOrWhiteSpace(snapshot.deckId)
                ? snapshot.deckId
                : currentPvpQueueRequest?.deckId ?? string.Empty;

            LaunchSelections.StoreRankedResume(snapshot.matchId, persistedDeckId, persistedDeck);

            if (!HasResolvedBanResults(snapshot))
            {
                rescueCompletedBanRevealKey = string.Empty;
            }

            if (PlayerFormationIsLocked(snapshot))
            {
                rescueFormationAssignments = BuildAssignmentsFromFormation(snapshot.playerFormation);
            }
        }

        private void UpdateLocalPhaseDeadline(QueueOrJoinMatchResponseDto snapshot)
        {
            var key = BuildDeadlineKey(snapshot);
            if (string.Equals(localPhaseDeadlineKey, key, StringComparison.Ordinal))
            {
                return;
            }

            localPhaseDeadlineKey = key;
            localPhaseDeadlineUtc = null;

            if (!string.IsNullOrWhiteSpace(snapshot.phaseDeadlineAt) &&
                DateTime.TryParse(snapshot.phaseDeadlineAt, out var parsedDeadline))
            {
                localPhaseDeadlineUtc = parsedDeadline.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(parsedDeadline, DateTimeKind.Utc)
                    : parsedDeadline.ToUniversalTime();
                return;
            }

            if (snapshot.phaseTimeoutSecondsRemaining > 0)
            {
                localPhaseDeadlineUtc = DateTime.UtcNow.AddSeconds(snapshot.phaseTimeoutSecondsRemaining);
            }
        }

        private static string BuildDeadlineKey(QueueOrJoinMatchResponseDto snapshot)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            return $"{snapshot.matchId}|{snapshot.phase}|{snapshot.status}|{snapshot.playerBannedEmojiId}|{snapshot.opponentBannedEmojiId}|{snapshot.playerFormation?.placements?.Length ?? 0}|{snapshot.opponentFormation?.placements?.Length ?? 0}";
        }

        private void EnsurePolling()
        {
            if (pollingRoutine != null)
            {
                return;
            }

            pollingRoutine = StartCoroutine(PollPvpState());
        }

        private void StopPolling()
        {
            if (pollingRoutine == null)
            {
                return;
            }

            StopCoroutine(pollingRoutine);
            pollingRoutine = null;
        }

        private void EnsureUiRefresh()
        {
            if (uiRefreshRoutine != null)
            {
                return;
            }

            uiRefreshRoutine = StartCoroutine(RefreshVisibleCountdowns());
        }

        private void StopUiRefresh()
        {
            if (uiRefreshRoutine == null)
            {
                return;
            }

            StopCoroutine(uiRefreshRoutine);
            uiRefreshRoutine = null;
        }

        private IEnumerator RefreshVisibleCountdowns()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);

                if (selectedMode != LaunchSelections.PvpRanked || currentPvpMatch == null)
                {
                    continue;
                }

                if (!HasVisibleTimeout(currentPvpMatch))
                {
                    continue;
                }

                if (UseRescueBlindBan &&
                    rescueBlindBanScreen != null &&
                    rescueBlindBanScreen.gameObject.activeInHierarchy &&
                    IsBanPhase(currentPvpMatch))
                {
                    rescueBlindBanScreen.RefreshTimerOnly(ResolveBanSecondsRemaining());
                    continue;
                }

                SafeRenderCurrentPvpState("ui countdown refresh");
            }
        }

        private IEnumerator PollPvpState()
        {
            while (true)
            {
                yield return new WaitForSeconds(1.5f);
                QueueOrJoinMatchResponseDto snapshot = null;
                yield return FetchPvpSnapshot(result => snapshot = result, true);

                if (snapshot == null)
                {
                    continue;
                }

                ApplyPvpSnapshot(snapshot);
                SafeRenderCurrentPvpState("poll snapshot");

                if (!ShouldContinuePolling(snapshot))
                {
                    pollingRoutine = null;
                    yield break;
                }
            }
        }

        private static bool ShouldContinuePolling(QueueOrJoinMatchResponseDto snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            if (snapshot.status == "queued")
            {
                return true;
            }

            if (IsQueuePhase(snapshot))
            {
                return true;
            }

            if (IsMatchFinished(snapshot))
            {
                return false;
            }

            if (IsBanPhase(snapshot))
            {
                return true;
            }

            if (IsFormationPhase(snapshot))
            {
                return true;
            }

            return false;
        }

        private void ReturnHome()
        {
            if (isLeavingMatch)
            {
                return;
            }

            StartCoroutine(ReturnHomeRoutine());
        }

        private IEnumerator ReturnHomeRoutine()
        {
            isLeavingMatch = true;
            StopPolling();
            ClearFormationDraft();
            StopReplay();
            HideEmojiClashRescue();

            if (selectedMode == LaunchSelections.PvpRanked &&
                currentPvpMatch != null &&
                currentPvpMatch.status == "queued")
            {
                var bootstrap = AppBootstrap.Instance;
                if (bootstrap != null)
                {
                    var payload = new CancelRankedQueueRequestDto
                    {
                        userId = bootstrap.SessionState.UserId,
                        matchId = currentPvpMatch.matchId,
                    };

                    using var request = bootstrap.FunctionClient.BuildJsonRequest(
                        "cancel_ranked_queue",
                        JsonUtility.ToJson(payload),
                        bootstrap.SessionState.AccessToken);
                    request.timeout = 8;
                    if (TryBeginWebRequest(request, out var operation, out _))
                    {
                        yield return operation;
                    }
                }

                LaunchSelections.ClearRankedResume();
                LaunchSelections.ClearPendingSquad();
            }
            else if (selectedMode == LaunchSelections.PvpRanked && IsMatchFinished(currentPvpMatch))
            {
                LaunchSelections.ClearRankedResume();
            }

            SceneManager.LoadScene(SceneNames.Home);
        }

        private void ShowError(string title, string subtitle, string details)
        {
            HideBlindBanRescue();
            HideFormationRescue();
            HideResultRescue();
            HideEmojiClashRescue();
            currentPanelState = MatchUiPanelState.Error;
            StopReplay();
            SetHeader(title, subtitle, BuildActionableErrorDetails(details));
            SetChoiceButtons(Array.Empty<string>(), null);
            SetActionButton(true, "Return Home", ReturnHome);
        }

        private void SafeRenderCurrentPvpState(string context)
        {
            try
            {
                RenderCurrentPvpState();
            }
            catch (Exception exception)
            {
                ShowError(
                    "Ranked PvP",
                    "Match panel failed to render.",
                    $"{context}\n{exception.GetType().Name}: {exception.Message}");
            }
        }

        private string BuildQueueRequestFailureDetails()
        {
            if (!string.IsNullOrWhiteSpace(lastPvpRequestError) &&
                lastPvpRequestError.IndexOf("HTTP 401", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return
                    $"{lastPvpRequestError}\n\n" +
                    "Guest auth token was rejected.\n" +
                    "1. Close Editor and standalone client.\n" +
                    "2. Start local backend (`npm run supabase:start` and `npm run supabase:functions:serve`).\n" +
                    "3. Relaunch both clients and queue again.";
            }

            if (string.IsNullOrWhiteSpace(lastPvpRequestError))
            {
                return "The local Supabase function did not respond. Make sure `npm run supabase:functions:serve` is running.";
            }

            return $"{lastPvpRequestError}\n\nMake sure `npm run supabase:functions:serve` is running and the local Supabase stack is healthy.";
        }

        private static string BuildRequestErrorDetails(UnityWebRequest request)
        {
            if (request == null)
            {
                return "The local Supabase function did not respond.";
            }

            if (request.responseCode > 0)
            {
                var errorText = string.IsNullOrWhiteSpace(request.error) ? "Request failed." : request.error;
                return $"HTTP {(int)request.responseCode}: {errorText}";
            }

            return string.IsNullOrWhiteSpace(request.error)
                ? "The local Supabase function did not respond."
                : request.error;
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
                error = "The local Supabase function request could not be created.";
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
                    error = "Insecure connection not allowed. In Unity Player Settings, set 'Allow downloads over HTTP' to 'Always Allowed' for local Supabase testing.";
                    return false;
                }

                error = $"Request start failed: {message}";
                return false;
            }
        }

        private static bool TryParseJsonResponse<T>(UnityWebRequest request, out T payload, out string error)
            where T : class
        {
            payload = null;
            error = string.Empty;

            if (request?.downloadHandler == null)
            {
                error = "The server returned an empty response body.";
                return false;
            }

            var body = request.downloadHandler.text;
            if (string.IsNullOrWhiteSpace(body))
            {
                error = "The server returned an empty response body.";
                return false;
            }

            try
            {
                payload = JsonUtility.FromJson<T>(body);
            }
            catch (Exception exception)
            {
                error = $"The server response could not be parsed: {exception.Message}";
                return false;
            }

            if (payload == null)
            {
                error = "The server response could not be parsed.";
                return false;
            }

            return true;
        }

        private static bool TryValidateFunctionsRuntime(AppBootstrap bootstrap, out string error)
        {
            error = string.Empty;

            if (bootstrap == null)
            {
                error = "App bootstrap is unavailable.";
                return false;
            }

            if (bootstrap.SupabaseConfig == null)
            {
                error = "Supabase project config is not assigned on AppBootstrap.";
                return false;
            }

            if (!bootstrap.SupabaseConfig.IsConfigured)
            {
                error = "Supabase project config is incomplete.";
                return false;
            }

            return true;
        }

        private static bool ShouldRetrySnapshotRequest(UnityWebRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (request.responseCode == 0)
            {
                return true;
            }

            if (request.result == UnityWebRequest.Result.ConnectionError ||
                request.result == UnityWebRequest.Result.DataProcessingError)
            {
                return true;
            }

            var errorText = request.error ?? string.Empty;
            return errorText.IndexOf("timed out", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   errorText.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   errorText.IndexOf("connection", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUnauthorizedResponse(UnityWebRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (request.responseCode == 401)
            {
                return true;
            }

            var errorText = request.error ?? string.Empty;
            return errorText.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   errorText.IndexOf("unauthorized", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SetHeader(string title, string subtitle, string details, string panelStatus = null)
        {
            var cleanedSubtitle = StripPhaseLegend(subtitle);
            var cleanedDetails = CompactDetails(details);
            lastRenderedDetails = cleanedDetails;

            if (scoreLabel != null)
            {
                scoreLabel.text = title;
                scoreLabel.fontSize = title != null && title.Length > 14
                    ? Mathf.Max(UiThemeRuntime.Theme.HeadingFontSize + 10, 34)
                    : Mathf.Max(UiThemeRuntime.Theme.HeroFontSize - 6, 42);
                scoreLabel.resizeTextForBestFit = false;
            }

            if (whyLabel != null)
            {
                whyLabel.text = cleanedSubtitle;
                whyLabel.fontSize = Mathf.Max(20, UiThemeRuntime.Theme.HeadingFontSize - 2);
                whyLabel.resizeTextForBestFit = false;
            }

            if (whyChainLabel != null)
            {
                if (ShouldUseRuntimeLayout && runtimeDetailCardsRoot != null)
                {
                    whyChainLabel.text = string.Empty;
                    whyChainLabel.gameObject.SetActive(false);
                }
                else
                {
                    whyChainLabel.gameObject.SetActive(true);
                    whyChainLabel.text = cleanedDetails;
                    whyChainLabel.fontSize = Mathf.Max(18, UiThemeRuntime.Theme.BodyFontSize - 1);
                    whyChainLabel.resizeTextForBestFit = false;
                }
            }

            if (ShouldUseRuntimeLayout)
            {
                RenderRuntimeDetailCards(cleanedDetails);
            }
            else
            {
                DisableRuntimeDetailCardsIfPresent();
            }

            if (panelBackground != null)
            {
                ApplyMatchPanelStyling();
            }

            if (phaseStepper != null)
            {
                phaseStepper.SetStep(ResolvePhaseStep(currentPanelState));
            }

            if (statusChip != null)
            {
                statusChip.SetPanelState(currentPanelState);
                var statusText = string.IsNullOrWhiteSpace(panelStatus)
                    ? ResolvePanelStatusText()
                    : panelStatus;
                statusChip.SetStatus(statusText);
            }

            if (currentPanelState != MatchUiPanelState.Result)
            {
                RefreshDecisiveMomentStrip(Array.Empty<string>());
            }
        }

        private void ApplyReadableLabelLayout()
        {
            ConfigureLabelRect(scoreLabel, new Vector2(0.07f, 0.88f), new Vector2(0.93f, 0.98f));
            ConfigureLabelRect(whyLabel, new Vector2(0.08f, 0.79f), new Vector2(0.92f, 0.86f));
            ConfigureLabelRect(whyChainLabel, new Vector2(0.08f, 0.32f), new Vector2(0.92f, 0.76f));
        }

        private void ApplyMatchPanelStyling()
        {
            if (panelBackground == null)
            {
                return;
            }

            if (UseSlideBackground &&
                UiThemeRuntime.TryGetSlideSprite(ResolveMatchSlideIndex(currentPanelState), out var slide))
            {
                panelBackground.sprite = slide;
                panelBackground.type = Image.Type.Simple;
                panelBackground.preserveAspect = false;
                panelBackground.color = Color.white;
                return;
            }

            panelBackground.sprite = null;
            var top = UiThemeRuntime.ResolvePanelTop(currentPanelState);
            var bottom = UiThemeRuntime.ResolvePanelBottom(currentPanelState);
            panelBackground.color = Color.Lerp(top, bottom, 0.45f);
        }

        private static int ResolveMatchSlideIndex(MatchUiPanelState panelState)
        {
            return panelState switch
            {
                MatchUiPanelState.Ban => MatchBanSlideIndex,
                MatchUiPanelState.Waiting => MatchBanSlideIndex,
                MatchUiPanelState.Formation => MatchFormationSlideIndex,
                MatchUiPanelState.Result => MatchResultSlideIndex,
                MatchUiPanelState.Error => MatchQueueSlideIndex,
                _ => MatchQueueSlideIndex
            };
        }

        private void ApplyReadableMetaLayout()
        {
            if (phaseStepper != null)
            {
                var phaseRect = phaseStepper.transform as RectTransform;
                if (phaseRect != null)
                {
                    phaseRect.anchorMin = new Vector2(0.12f, 0.83f);
                    phaseRect.anchorMax = new Vector2(0.88f, 0.87f);
                    phaseRect.offsetMin = Vector2.zero;
                    phaseRect.offsetMax = Vector2.zero;
                }
            }

            if (statusChip != null)
            {
                var chipRect = statusChip.transform as RectTransform;
                if (chipRect != null)
                {
                    chipRect.anchorMin = new Vector2(0.18f, 0.74f);
                    chipRect.anchorMax = new Vector2(0.82f, 0.78f);
                    chipRect.offsetMin = Vector2.zero;
                    chipRect.offsetMax = Vector2.zero;
                }
            }
        }

        private static void ConfigureLabelRect(Text label, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (label == null)
            {
                return;
            }

            var rect = label.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.UpperCenter;
        }

        private string StripPhaseLegend(string subtitle)
        {
            if (string.IsNullOrWhiteSpace(subtitle))
            {
                return string.Empty;
            }

            var lines = subtitle
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line => !LooksLikePhaseLegend(line))
                .ToArray();
            return lines.Length == 0 ? string.Empty : string.Join("\n", lines);
        }

        private static bool LooksLikePhaseLegend(string line)
        {
            return line.IndexOf("Squad", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   line.IndexOf("Ban", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   line.IndexOf("Form", StringComparison.OrdinalIgnoreCase) >= 0 &&
                   line.IndexOf("Result", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string CompactDetails(string details)
        {
            if (string.IsNullOrWhiteSpace(details))
            {
                return string.Empty;
            }

            var lines = details
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            var maxLines = currentPanelState switch
            {
                MatchUiPanelState.Result => 20,
                MatchUiPanelState.Formation => 16,
                MatchUiPanelState.Ban => 16,
                MatchUiPanelState.Error => 14,
                MatchUiPanelState.Waiting => 14,
                MatchUiPanelState.Queue => 12,
                _ => 6,
            };
            if (lines.Length <= maxLines)
            {
                return string.Join("\n", lines);
            }

            var compact = lines.Take(maxLines).ToList();
            compact.Add("…");
            return string.Join("\n", compact);
        }

        private static string BuildActionableErrorDetails(string details)
        {
            if (string.IsNullOrWhiteSpace(details))
            {
                return "Unknown error.";
            }

            var normalized = details.Trim();
            if (normalized.IndexOf("HTTP 401", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return
                    "Authentication expired.\n" +
                    "1. Close both clients.\n" +
                    "2. Relaunch as Editor + Standalone build (recommended for 2-player local).\n" +
                    "3. Ensure `npm run supabase:start` and `npm run supabase:functions:serve` are running.\n" +
                    "4. Re-enter ranked from Home.";
            }

            if (normalized.IndexOf("503", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("did not respond", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return
                    "Local Supabase is offline.\n" +
                    "1. Run `npm run supabase:start`\n" +
                    "2. Run `npm run supabase:functions:serve`\n" +
                    "3. Retry matchmaking.";
            }

            return normalized;
        }

        private void SetChoiceButtons(string[] choices, Action<int> handler)
        {
            currentChoices = choices ?? Array.Empty<string>();
            choiceHandler = handler;

            if (choiceButtons == null)
            {
                return;
            }

            for (var index = 0; index < choiceButtons.Length; index++)
            {
                if (choiceButtons[index] == null)
                {
                    continue;
                }

                var isVisible = index < currentChoices.Length;
                choiceButtons[index].gameObject.SetActive(isVisible);
                choiceButtons[index].interactable = isVisible;

                var image = choiceButtons[index].GetComponent<Image>();
                var parsedEmojiId = default(EmojiId);
                var isEmojiChoice = false;
                if (isVisible)
                {
                    isEmojiChoice = EmojiIdUtility.TryFromApiId(currentChoices[index], out parsedEmojiId);
                }
                if (image != null)
                {
                    var choiceState = isVisible ? BuildChoiceState(currentChoices[index]) : UnitCardState.Disabled;
                    image.color = isVisible
                        ? UiThemeRuntime.ResolveCardColor(choiceState)
                        : UiThemeRuntime.ResolveCardColor(UnitCardState.Disabled);
                }

                if (isVisible && isEmojiChoice)
                {
                    ApplyStickerCardVisual(choiceButtons[index], parsedEmojiId, BuildChoiceState(currentChoices[index]));
                }
                else
                {
                    ResetStickerCardVisual(choiceButtons[index]);
                }

                if (isVisible && choiceButtonLabels != null && index < choiceButtonLabels.Length && choiceButtonLabels[index] != null)
                {
                    choiceButtonLabels[index].text = isEmojiChoice ? string.Empty : BuildChoiceLabel(currentChoices[index]);
                    choiceButtonLabels[index].fontSize = isEmojiChoice ? UiThemeRuntime.Theme.BodyFontSize : UiThemeRuntime.Theme.HeadingFontSize;
                    choiceButtonLabels[index].color = Color.white;
                }

                var motion = choiceButtons[index].GetComponent<UiMotionController>();
                if (motion == null)
                {
                    motion = choiceButtons[index].gameObject.AddComponent<UiMotionController>();
                }

                motion.Configure(enableIdle: false, enableCtaBreathe: false);
            }
        }

        private void ApplyStickerCardVisual(Button button, EmojiId emojiId, UnitCardState cardState)
        {
            if (button == null)
            {
                return;
            }

            EnsureStickerCardLayout(button);
            var card = button.GetComponent<StickerUnitCard>();
            if (card == null)
            {
                card = button.gameObject.AddComponent<StickerUnitCard>();
            }

            ActivateIfPresent(button.transform, "Glyph");
            ActivateIfPresent(button.transform, "RolePill");
            ActivateIfPresent(button.transform, "StateBadge");
            ActivateIfPresent(button.transform, "Aura");
            ActivateIfPresent(button.transform, "Title");

            card.SetCompactMode(true);
            card.Bind(emojiId, cardState);
            card.enabled = true;
        }

        private static void ActivateIfPresent(Transform root, string childName)
        {
            var child = root.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(true);
            }
        }

        private void EnsureStickerCardLayout(Button button)
        {
            var buttonId = button.GetInstanceID();
            if (choiceStickerInitialized.Contains(buttonId))
            {
                return;
            }

            choiceStickerInitialized.Add(buttonId);
            var buttonRect = button.transform as RectTransform;
            if (buttonRect == null)
            {
                return;
            }

            buttonRect.sizeDelta = new Vector2(buttonRect.sizeDelta.x, Mathf.Max(84f, buttonRect.sizeDelta.y));

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = UiThemeRuntime.Theme.CardColors.Default;
            }

            var existingText = button.GetComponentInChildren<Text>(true);
            if (existingText != null)
            {
                existingText.name = "Title";
                var existingRect = existingText.rectTransform;
                existingRect.anchorMin = new Vector2(0f, 0f);
                existingRect.anchorMax = new Vector2(1f, 1f);
                existingRect.offsetMin = new Vector2(12f, 8f);
                existingRect.offsetMax = new Vector2(-12f, -8f);
                existingText.alignment = TextAnchor.MiddleCenter;
                existingText.font = scoreLabel != null ? scoreLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                existingText.fontSize = Mathf.Max(UiThemeRuntime.Theme.BodyFontSize - 2, 18);
                existingText.color = Color.white;
            }

            EnsureCardLabel(button.transform, "Glyph", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(28f, 28f), TextAnchor.MiddleLeft, Mathf.Max(UiThemeRuntime.Theme.ChipFontSize - 2, 14));
            EnsureCardLabel(button.transform, "RolePill", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(56f, 18f), TextAnchor.UpperRight, Mathf.Max(UiThemeRuntime.Theme.ChipFontSize - 2, 13));
            EnsureCardLabel(button.transform, "StateBadge", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 8f), new Vector2(56f, 16f), TextAnchor.LowerRight, Mathf.Max(UiThemeRuntime.Theme.ChipFontSize - 2, 13));

            if (button.transform.Find("Aura") == null)
            {
                var auraObject = new GameObject("Aura", typeof(RectTransform), typeof(Image));
                auraObject.transform.SetParent(button.transform, false);
                auraObject.transform.SetSiblingIndex(0);
                var auraRect = auraObject.GetComponent<RectTransform>();
                auraRect.anchorMin = new Vector2(0.02f, 0.1f);
                auraRect.anchorMax = new Vector2(0.98f, 0.9f);
                auraRect.offsetMin = Vector2.zero;
                auraRect.offsetMax = Vector2.zero;

                var aura = auraObject.GetComponent<Image>();
                aura.color = UiThemeRuntime.Theme.ControlAccent * new Color(1f, 1f, 1f, 0.22f);
            }
        }

        private static void ResetStickerCardVisual(Button button)
        {
            if (button == null)
            {
                return;
            }

            var card = button.GetComponent<StickerUnitCard>();
            if (card != null)
            {
                card.enabled = false;
            }

            var glyph = button.transform.Find("Glyph");
            if (glyph != null)
            {
                glyph.gameObject.SetActive(false);
            }

            var rolePill = button.transform.Find("RolePill");
            if (rolePill != null)
            {
                rolePill.gameObject.SetActive(false);
            }

            var stateBadge = button.transform.Find("StateBadge");
            if (stateBadge != null)
            {
                stateBadge.gameObject.SetActive(false);
            }

            var aura = button.transform.Find("Aura");
            if (aura != null)
            {
                aura.gameObject.SetActive(false);
            }

            var title = button.transform.Find("Title");
            if (title != null)
            {
                title.gameObject.SetActive(true);
                if (title.TryGetComponent<Text>(out var titleText))
                {
                    titleText.alignment = TextAnchor.MiddleCenter;
                }
            }
        }

        private void EnsureCardLabel(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 anchoredPosition,
            Vector2 size,
            TextAnchor alignment,
            int fontSize)
        {
            if (parent.Find(name) != null)
            {
                return;
            }

            var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(parent, false);
            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x, anchorMin.y);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            var text = labelObject.GetComponent<Text>();
            text.font = scoreLabel != null ? scoreLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
        }

        private string BuildChoiceLabel(string choice)
        {
            if (!EmojiIdUtility.TryFromApiId(choice, out var emojiId))
            {
                return choice;
            }

            var state = BuildChoiceState(choice);
            return EmojiUiFormatter.BuildUnitCardLabel(emojiId, state);
        }

        private UnitCardState BuildChoiceState(string choice)
        {
            var state = UnitCardState.Default;

            if (currentPanelState == MatchUiPanelState.Formation && formationDraft.Contains(choice))
            {
                state |= UnitCardState.Selected;
            }

            if (currentPanelState == MatchUiPanelState.Ban &&
                currentPvpMatch != null &&
                string.Equals(choice, currentPvpMatch.opponentBannedEmojiId, StringComparison.Ordinal))
            {
                state |= UnitCardState.Banned | UnitCardState.Disabled;
            }

            return state;
        }

        private void SetActionButton(bool visible, string label, Action handler)
        {
            actionHandler = handler;

            if (actionButton != null)
            {
                actionButton.gameObject.SetActive(visible);
            }

            if (actionButtonLabel != null)
            {
                actionButtonLabel.text = label;
                actionButtonLabel.fontSize = UiThemeRuntime.Theme.HeadingFontSize;
                actionButtonLabel.color = Color.white;
            }

            if (actionButton != null)
            {
                var image = actionButton.GetComponent<Image>();
                if (image != null)
                {
                    image.color = string.Equals(label, "Return Home", StringComparison.OrdinalIgnoreCase)
                        ? UiThemeRuntime.Theme.SecondaryCtaColor
                        : UiThemeRuntime.Theme.PrimaryCtaColor;
                }

                var motion = actionButton.GetComponent<UiMotionController>();
                if (motion == null)
                {
                    motion = actionButton.gameObject.AddComponent<UiMotionController>();
                }

                motion.Configure(
                    enableIdle: false,
                    enableCtaBreathe: visible && !string.Equals(label, "Return Home", StringComparison.OrdinalIgnoreCase));
            }

            var interactive = visible && handler != null;
            if (stickyPrimaryAction != null)
            {
                stickyPrimaryAction.Bind(
                    label,
                    interactive,
                    handler == null ? null : new UnityEngine.Events.UnityAction(() => handler.Invoke()),
                    emphasize: !string.Equals(label, "Return Home", StringComparison.OrdinalIgnoreCase));
            }
            else if (stickyFooterAction != null)
            {
                stickyFooterAction.Bind(
                    label,
                    interactive,
                    handler == null ? null : new UnityEngine.Events.UnityAction(() => handler.Invoke()));
            }
        }

        private PhaseStep ResolvePhaseStep(MatchUiPanelState panelState)
        {
            return panelState switch
            {
                MatchUiPanelState.Queue => PhaseStep.Ban,
                MatchUiPanelState.Ban => PhaseStep.Ban,
                MatchUiPanelState.Formation => PhaseStep.Formation,
                MatchUiPanelState.Result => PhaseStep.Result,
                MatchUiPanelState.Waiting => ResolveWaitingPhaseStep(),
                _ => selectedMode == LaunchSelections.PvpRanked ? PhaseStep.Ban : PhaseStep.Squad
            };
        }

        private PhaseStep ResolveWaitingPhaseStep()
        {
            if (currentPvpMatch != null)
            {
                if (IsBanPhase(currentPvpMatch))
                {
                    return PhaseStep.Ban;
                }

                if (IsFormationPhase(currentPvpMatch))
                {
                    return PhaseStep.Formation;
                }

                if (IsMatchFinished(currentPvpMatch) || IsLegacyResolvingPhase(currentPvpMatch))
                {
                    return PhaseStep.Result;
                }
            }

            return selectedMode == LaunchSelections.PvpRanked ? PhaseStep.Ban : PhaseStep.Squad;
        }

        private string ResolvePanelStatusText()
        {
            if (currentPanelState == MatchUiPanelState.Error)
            {
                return "Action required";
            }

            if (currentPvpMatch != null &&
                !string.IsNullOrWhiteSpace(currentPvpMatch.note) &&
                currentPvpMatch.note.IndexOf("resumed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Reconnected to match";
            }

            if (currentPanelState == MatchUiPanelState.Waiting)
            {
                return "Waiting for opponent...";
            }

            if (currentPanelState == MatchUiPanelState.Queue)
            {
                return "Matchmaking";
            }

            if (currentPanelState == MatchUiPanelState.Ban && currentPvpMatch != null && PlayerBanIsLocked(currentPvpMatch))
            {
                return "Ban locked";
            }

            if (currentPanelState == MatchUiPanelState.Formation && currentPvpMatch != null && PlayerFormationIsLocked(currentPvpMatch))
            {
                return "Formation locked";
            }

            if (currentPanelState == MatchUiPanelState.Result)
            {
                return "Result ready";
            }

            if (currentPvpMatch != null && IsLegacyResolvingPhase(currentPvpMatch))
            {
                return "Resolving...";
            }

            return string.Empty;
        }

        private bool IsCurrentUserPlayerA()
        {
            return currentPvpMatch == null || currentPvpMatch.playerSide != "player_b";
        }

        private static bool IsBanPhase(QueueOrJoinMatchResponseDto snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            return string.Equals(snapshot.phase, "ban", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(snapshot.status, "banning", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsQueuePhase(QueueOrJoinMatchResponseDto snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            return string.Equals(snapshot.phase, "queue", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFormationPhase(QueueOrJoinMatchResponseDto snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            return string.Equals(snapshot.phase, "formation", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(snapshot.status, "formation", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLegacyResolvingPhase(QueueOrJoinMatchResponseDto snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            return string.Equals(snapshot.phase, "resolving", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(snapshot.status, "resolving", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMatchFinished(QueueOrJoinMatchResponseDto snapshot)
        {
            return snapshot != null &&
                   (string.Equals(snapshot.phase, "finished", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(snapshot.status, "finished", StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrWhiteSpace(snapshot.winner));
        }

        private static bool IsBotMatchFinished(StartBotMatchResponseDto snapshot)
        {
            return snapshot != null &&
                   (string.Equals(snapshot.phase, "finished", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(snapshot.status, "finished", StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrWhiteSpace(snapshot.winner));
        }

        private static bool PlayerBanIsLocked(QueueOrJoinMatchResponseDto snapshot)
        {
            return snapshot != null && !string.IsNullOrWhiteSpace(snapshot.opponentBannedEmojiId);
        }

        private bool ShouldShowBanRevealBeforeFormation(QueueOrJoinMatchResponseDto snapshot)
        {
            if (!IsFormationPhase(snapshot) || !HasResolvedBanResults(snapshot))
            {
                return false;
            }

            var revealKey = BuildBanRevealKey(snapshot);
            if (string.Equals(revealKey, rescueCompletedBanRevealKey, StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }

        private static bool HasResolvedBanResults(QueueOrJoinMatchResponseDto snapshot)
        {
            return snapshot != null &&
                   !string.IsNullOrWhiteSpace(snapshot.opponentBannedEmojiId) &&
                   !string.IsNullOrWhiteSpace(snapshot.playerBannedEmojiId);
        }

        private static string BuildBanRevealKey(QueueOrJoinMatchResponseDto snapshot)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            return $"{snapshot.matchId}|{snapshot.opponentBannedEmojiId}|{snapshot.playerBannedEmojiId}";
        }

        private static string[] BuildAssignmentsFromFormation(FormationDto formation)
        {
            var assignments = new string[FormationSlotOrder.Length];
            if (formation?.placements == null)
            {
                return assignments;
            }

            for (var index = 0; index < FormationSlotOrder.Length; index++)
            {
                var slot = FormationSlotOrder[index];
                var placement = formation.placements.FirstOrDefault(item =>
                    item != null &&
                    string.Equals(item.slot, slot, StringComparison.OrdinalIgnoreCase));
                assignments[index] = placement?.emojiId ?? string.Empty;
            }

            return assignments;
        }

        private string[] BuildRescueFormationAssignments()
        {
            if (rescueFormationAssignments == null || rescueFormationAssignments.Length == 0)
            {
                return new string[FormationSlotOrder.Length];
            }

            var assignments = new string[FormationSlotOrder.Length];
            for (var index = 0; index < Mathf.Min(assignments.Length, rescueFormationAssignments.Length); index++)
            {
                assignments[index] = rescueFormationAssignments[index];
            }

            return assignments;
        }

        private static string[] CloneFormationAssignments(string[] assignments)
        {
            return assignments == null ? Array.Empty<string>() : (string[])assignments.Clone();
        }

        private static bool PlayerFormationIsLocked(QueueOrJoinMatchResponseDto snapshot)
        {
            return snapshot?.playerFormation?.placements != null && snapshot.playerFormation.placements.Length == 5;
        }

        private static bool HasOpponentDeckChoices(QueueOrJoinMatchResponseDto snapshot)
        {
            return snapshot?.opponentDeck != null && snapshot.opponentDeck.Length > 0;
        }

        private static bool HasPlayerFinalTeam(QueueOrJoinMatchResponseDto snapshot)
        {
            return snapshot?.playerFinalTeam != null && snapshot.playerFinalTeam.Length == 5;
        }

        private static string BuildOutcomeLabel(string winner, bool playerIsPlayerA, bool botMode)
        {
            if (string.IsNullOrWhiteSpace(winner) || winner == "draw")
            {
                return "Result: Draw";
            }

            var playerWon = playerIsPlayerA ? winner == "player_a" : winner == "player_b";
            if (playerWon)
            {
                return "Result: You Win";
            }

            return botMode ? "Result: Bot Wins" : "Result: Opponent Wins";
        }

        private static string BuildFormationSummaryFromDto(FormationDto formation)
        {
            if (formation?.placements == null || formation.placements.Length == 0)
            {
                return "No formation has been locked yet.";
            }

            var placementBySlot = formation.placements.ToDictionary(placement => placement.slot, placement => placement.emojiId);
            var lines = new List<string>(FormationSlotOrder.Length);
            for (var index = 0; index < FormationSlotOrder.Length; index++)
            {
                var slot = FormationSlotOrder[index];
                placementBySlot.TryGetValue(slot, out var emojiId);
                lines.Add($"{FormationSlotShortLabels[index]} {HumanizeEmojiIdOrPending(emojiId)}");
            }

            return string.Join(" • ", lines);
        }

        private static string BuildWhyChain(string[] whyChain)
        {
            if (whyChain == null || whyChain.Length == 0)
            {
                return "No WHY chain was recorded.";
            }

            var capped = whyChain
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Take(4)
                .Select(entry => $"• {entry}")
                .ToArray();
            return capped.Length == 0 ? "No WHY chain was recorded." : string.Join("\n", capped);
        }

        private static string ShortId(string value, int prefixLength = 8)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Pending";
            }

            var trimmed = value.Trim();
            return trimmed.Length <= prefixLength ? trimmed : trimmed.Substring(0, prefixLength);
        }

        private string BuildBanPanelContext(QueueOrJoinMatchResponseDto snapshot)
        {
            if (snapshot == null)
            {
                return "Waiting for both squads to sync.";
            }

            return
                $"Your 6: {BuildEmojiGlyphSummary(snapshot.playerDeck)}\n" +
                $"Enemy 6: {BuildEmojiGlyphSummary(snapshot.opponentDeck)}\n" +
                $"Your lock: {HumanizeEmojiIdOrPending(snapshot.opponentBannedEmojiId)}\n" +
                $"Enemy lock: {HumanizeEmojiIdOrPending(snapshot.playerBannedEmojiId)}";
        }

        private string BuildQueueDetails(QueueOrJoinMatchResponseDto response)
        {
            if (response == null)
            {
                return "Queue snapshot unavailable.";
            }

            var builder = new StringBuilder(256);
            builder.AppendLine("Selected 6");
            builder.AppendLine(BuildEmojiSummary(response.playerDeck));
            builder.AppendLine();
            builder.AppendLine("Queue");
            builder.AppendLine($"Queue Ticket: {ShortId(response.queueTicket)}");
            builder.AppendLine($"Estimated Wait: {Mathf.Max(0, response.estimatedWaitSeconds)}s");

            var timeoutLine = BuildTimeoutLine(response, "Queue expires in");
            if (!string.IsNullOrWhiteSpace(timeoutLine))
            {
                builder.AppendLine(timeoutLine);
            }

            if (!string.IsNullOrWhiteSpace(response.note))
            {
                builder.AppendLine($"Status: {BuildSingleLineNote(response.note)}");
            }
            else
            {
                builder.AppendLine("Status: Searching for opponent.");
            }

            return builder.ToString().TrimEnd();
        }

        private string BuildBanDetails(QueueOrJoinMatchResponseDto snapshot, bool locked)
        {
            if (snapshot == null)
            {
                return "Ban panel unavailable.";
            }

            var builder = new StringBuilder(320);
            builder.AppendLine("Your 6");
            builder.AppendLine(BuildEmojiSummary(snapshot.playerDeck));
            builder.AppendLine("Enemy 6");
            builder.AppendLine(BuildEmojiSummary(snapshot.opponentDeck));
            builder.AppendLine();
            builder.AppendLine($"Your ban on enemy: {HumanizeEmojiIdOrPending(snapshot.opponentBannedEmojiId)}");
            builder.AppendLine($"Enemy ban on you: {HumanizeEmojiIdOrPending(snapshot.playerBannedEmojiId)}");

            var timeoutLine = BuildTimeoutLine(snapshot, "Auto-lock in");
            if (!string.IsNullOrWhiteSpace(timeoutLine))
            {
                builder.AppendLine(timeoutLine);
            }

            if (locked)
            {
                builder.AppendLine();
                builder.Append(string.IsNullOrWhiteSpace(snapshot.playerBannedEmojiId)
                    ? "Ban locked. Waiting for enemy ban lock."
                    : "Both bans locked. Moving to formation.");
            }
            else
            {
                builder.AppendLine();
                builder.Append("Tap exactly 1 enemy emoji to ban.");
            }

            return builder.ToString();
        }

        private string BuildPvpFormationDetailsPrefix(QueueOrJoinMatchResponseDto snapshot)
        {
            if (snapshot == null)
            {
                return "Formation panel unavailable.";
            }

            var builder = new StringBuilder(320);
            builder.AppendLine("Final Teams");
            builder.AppendLine($"You: {BuildEmojiSummary(snapshot.playerFinalTeam)}");
            builder.AppendLine($"Enemy: {BuildEmojiSummary(snapshot.opponentFinalTeam)}");
            builder.AppendLine();
            builder.AppendLine(
                $"Bans • Your ban: {HumanizeEmojiIdOrPending(snapshot.opponentBannedEmojiId)} | Enemy ban: {HumanizeEmojiIdOrPending(snapshot.playerBannedEmojiId)}");
            builder.AppendLine(BuildOpponentFormationProgress(snapshot));

            var timeoutLine = BuildTimeoutLine(snapshot, "Auto-fill in");
            if (!string.IsNullOrWhiteSpace(timeoutLine))
            {
                builder.AppendLine(timeoutLine);
            }

            builder.AppendLine();
            builder.Append("Tap slots in order: Front Left → Front Center → Front Right → Back Left → Back Right");
            return builder.ToString().TrimEnd();
        }

        private static string BuildBotFormationDetailsPrefix(StartBotMatchResponseDto snapshot)
        {
            if (snapshot == null)
            {
                return "Formation panel unavailable.";
            }

            var builder = new StringBuilder(220);
            builder.AppendLine($"Your Final 5: {BuildEmojiSummary(snapshot.playerTeam)}");
            builder.AppendLine($"Bot Final 5: {BuildEmojiSummary(snapshot.botTeam)}");
            builder.Append("Tap slots in order: Front Left → Front Center → Front Right → Back Left → Back Right");

            return builder.ToString();
        }

        private static string BuildSingleLineNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return string.Empty;
            }

            var cleaned = note.Replace('\n', ' ').Replace('\r', ' ').Trim();
            const int maxLength = 96;
            if (cleaned.Length <= maxLength)
            {
                return cleaned;
            }

            return cleaned.Substring(0, maxLength - 1).TrimEnd() + "…";
        }

        private string BuildFormationLockedDetails(QueueOrJoinMatchResponseDto snapshot)
        {
            if (snapshot == null)
            {
                return "Formation snapshot unavailable.";
            }

            var builder = new StringBuilder(320);
            builder.AppendLine("Formation Locked");
            builder.AppendLine($"Your Final 5: {BuildEmojiSummary(snapshot.playerFinalTeam)}");
            builder.AppendLine($"Enemy Final 5: {BuildEmojiSummary(snapshot.opponentFinalTeam)}");
            builder.AppendLine();
            builder.AppendLine($"Your Board: {BuildFormationSummaryFromDto(snapshot.playerFormation)}");
            builder.AppendLine($"Enemy Progress: {BuildOpponentFormationProgress(snapshot)}");
            if (snapshot.opponentFormation?.placements != null && snapshot.opponentFormation.placements.Length > 0)
            {
                builder.AppendLine($"Enemy Board: {BuildFormationSummaryFromDto(snapshot.opponentFormation)}");
            }

            var timeoutLine = BuildTimeoutLine(snapshot, "Auto-fill in");
            if (!string.IsNullOrWhiteSpace(timeoutLine))
            {
                builder.AppendLine(timeoutLine);
            }

            builder.AppendLine();
            builder.Append("Waiting for enemy formation lock.");
            return builder.ToString();
        }

        private static string BuildOpponentFormationProgress(QueueOrJoinMatchResponseDto snapshot)
        {
            var opponentCount = snapshot?.opponentFormation?.placements?.Length ?? 0;
            return $"Opponent formation progress: {opponentCount}/5 locked";
        }

        private static string BuildFormationBoardPreview(IReadOnlyList<string> draft, IReadOnlyList<string> team)
        {
            if (team == null || team.Count == 0)
            {
                return "Formation Board\nFront: [Pending] [Pending] [Pending]\nBack: [Pending] [Pending]";
            }

            string ResolveAt(int index)
            {
                if (draft != null && index < draft.Count)
                {
                    return HumanizeEmojiId(draft[index]);
                }

                return "Pending";
            }

            return
                "Formation Board\n" +
                $"Front: {ResolveAt(0)} | {ResolveAt(1)} | {ResolveAt(2)}\n" +
                $"Back:  {ResolveAt(3)} | {ResolveAt(4)}";
        }

        private string BuildResultSummary(
            string outcome,
            string whySummary,
            string[] whyChain,
            string yourTeam,
            string enemyTeam,
            string enemyLabel)
        {
            var whyCard = string.IsNullOrWhiteSpace(whySummary)
                ? "No WHY summary provided."
                : whySummary;

            var decisiveMoments = BuildWhyChain(whyChain);
            return
                $"{outcome}\n" +
                $"Why: {whyCard}\n\n" +
                $"Decisive moments\n{decisiveMoments}\n\n" +
                $"Team recap\n" +
                $"Your Final Team: {yourTeam}\n" +
                $"{enemyLabel} Final Team: {enemyTeam}";
        }

        private string[] BuildResultActions(bool botMode)
        {
            if (botMode)
            {
                return new[]
                {
                    "Rematch",
                    "Edit Squad",
                    "Home"
                };
            }

            return new[]
            {
                "Rematch",
                "Edit Squad",
                "Home"
            };
        }

        private void OnResultActionSelected(int index)
        {
            if (index < 0 || index >= currentChoices.Length)
            {
                return;
            }

            switch (index)
            {
                case 0:
                    if (selectedMode == LaunchSelections.PvpRanked)
                    {
                        LaunchSelections.BeginRankedMatchSelection();
                        SceneManager.LoadScene(SceneNames.DeckBuilder);
                        return;
                    }

                    var botMode = selectedMode == LaunchSelections.BotSmart
                        ? LaunchSelections.BotSmart
                        : LaunchSelections.BotPractice;
                    LaunchSelections.BeginBotMatchSelection(botMode);
                    SceneManager.LoadScene(SceneNames.DeckBuilder);
                    return;
                case 1:
                    LaunchSelections.BeginDeckEdit();
                    SceneManager.LoadScene(SceneNames.DeckBuilder);
                    return;
                default:
                    ReturnHome();
                    return;
            }
        }

        private void ReplayHighlights()
        {
            if (replayMoments == null || replayMoments.Length == 0)
            {
                SetActionButton(true, "Return Home", ReturnHome);
                SetHeader(
                    scoreLabel != null ? scoreLabel.text : "Replay",
                    whyLabel != null ? whyLabel.text : "Replay unavailable",
                    $"{lastRenderedDetails}\n\nReplay unavailable: no decisive moments were logged.");
                return;
            }

            StopReplay();
            replayRoutine = StartCoroutine(ReplayHighlightsRoutine());
            SetActionButton(true, "Skip Replay", () =>
            {
                StopReplay();
                SetActionButton(true, "Return Home", ReturnHome);
            });
        }

        private IEnumerator ReplayHighlightsRoutine()
        {
            var startTitle = scoreLabel != null ? scoreLabel.text : "Replay";
            var startSubtitle = whyLabel != null ? whyLabel.text : string.Empty;
            var baseDetails = string.IsNullOrWhiteSpace(lastRenderedDetails)
                ? "No details available."
                : lastRenderedDetails;

            var frameCount = Mathf.Min(6, replayMoments.Length);
            for (var index = 0; index < frameCount; index++)
            {
                var moment = replayMoments[index];
                SetHeader(
                    startTitle,
                    $"{startSubtitle}\n\nReplay {index + 1}/{frameCount}",
                    $"{baseDetails}\n\n{EmojiUiFormatter.BuildStatusChip("Decisive moment")}\n{moment.Caption}");
                yield return new WaitForSeconds(1.2f);
            }

            replayRoutine = null;
            SetActionButton(true, "Return Home", ReturnHome);
        }

        private readonly struct DetailSection
        {
            public DetailSection(string title, string body)
            {
                Title = title;
                Body = body;
            }

            public string Title { get; }
            public string Body { get; }
        }

        private void StopReplay()
        {
            if (replayRoutine == null)
            {
                return;
            }

            StopCoroutine(replayRoutine);
            replayRoutine = null;
        }

        private static ReplayMoment[] BuildReplayMoments(BattleEventDto[] events, string[] whyChain)
        {
            var list = new List<ReplayMoment>();
            if (events != null)
            {
                foreach (var evt in events)
                {
                    if (evt == null || string.IsNullOrWhiteSpace(evt.caption))
                    {
                        continue;
                    }

                    list.Add(new ReplayMoment
                    {
                        Caption = evt.caption,
                        ReasonCode = evt.reasonCode ?? string.Empty
                    });
                }
            }

            if (list.Count == 0 && whyChain != null)
            {
                foreach (var entry in whyChain)
                {
                    if (string.IsNullOrWhiteSpace(entry))
                    {
                        continue;
                    }

                    list.Add(new ReplayMoment
                    {
                        Caption = entry,
                        ReasonCode = string.Empty
                    });
                }
            }

            return list.ToArray();
        }

        private void RefreshDecisiveMomentStrip(IEnumerable<string> moments)
        {
            if (decisiveMomentsStrip == null)
            {
                return;
            }

            for (var index = decisiveMomentsStrip.childCount - 1; index >= 0; index--)
            {
                Destroy(decisiveMomentsStrip.GetChild(index).gameObject);
            }

            var compactMoments = moments?
                .Where(entry => !string.IsNullOrWhiteSpace(entry))
                .Take(4)
                .ToArray() ?? Array.Empty<string>();

            if (compactMoments.Length == 0)
            {
                decisiveMomentsStrip.gameObject.SetActive(false);
                return;
            }

            decisiveMomentsStrip.gameObject.SetActive(true);
            for (var index = 0; index < compactMoments.Length; index++)
            {
                CreateDecisiveMomentChip(compactMoments[index], index);
            }
        }

        private void CreateDecisiveMomentChip(string caption, int index)
        {
            var chipObject = new GameObject($"MomentChip{index + 1}", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(DecisiveMomentChip));
            chipObject.transform.SetParent(decisiveMomentsStrip, false);

            var layoutElement = chipObject.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 210f;
            layoutElement.preferredHeight = 44f;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(chipObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 4f);
            labelRect.offsetMax = new Vector2(-6f, -4f);

            var label = labelObject.GetComponent<Text>();
            label.font = scoreLabel != null ? scoreLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;

            var chip = chipObject.GetComponent<DecisiveMomentChip>();
            chip.Bind(caption, index);
        }

        private string BuildTimeoutLine(QueueOrJoinMatchResponseDto snapshot, string prefix)
        {
            if (snapshot == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.phaseDeadlineAt) &&
                DateTime.TryParse(snapshot.phaseDeadlineAt, out var parsedDeadline))
            {
                var deadlineUtc = parsedDeadline.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(parsedDeadline, DateTimeKind.Utc)
                    : parsedDeadline.ToUniversalTime();
                var remaining = (int)Math.Ceiling((deadlineUtc - DateTime.UtcNow).TotalSeconds);
                if (remaining > 0)
                {
                    return $"{prefix}: {remaining}s";
                }
            }

            if (localPhaseDeadlineUtc.HasValue)
            {
                var remaining = (int)Math.Ceiling((localPhaseDeadlineUtc.Value - DateTime.UtcNow).TotalSeconds);
                if (remaining > 0)
                {
                    return $"{prefix}: {remaining}s";
                }
            }

            if (snapshot.phaseTimeoutSecondsRemaining <= 0)
            {
                return string.Empty;
            }

            return $"{prefix}: {snapshot.phaseTimeoutSecondsRemaining}s";
        }

        private bool HasVisibleTimeout(QueueOrJoinMatchResponseDto snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(snapshot.phaseDeadlineAt))
            {
                return true;
            }

            if (localPhaseDeadlineUtc.HasValue && localPhaseDeadlineUtc.Value > DateTime.UtcNow)
            {
                return true;
            }

            return snapshot.phaseTimeoutSecondsRemaining > 0;
        }

        private static string BuildEmojiSummary(string[] emojiIds)
        {
            if (emojiIds == null || emojiIds.Length == 0)
            {
                return "No squad data.";
            }

            return string.Join(" • ", emojiIds.Select(HumanizeEmojiId));
        }

        private static string BuildEmojiGlyphSummary(string[] emojiIds)
        {
            if (emojiIds == null || emojiIds.Length == 0)
            {
                return "No squad";
            }

            return string.Join(" ", emojiIds.Select(emojiId =>
                EmojiIdUtility.TryFromApiId(emojiId, out var parsed)
                    ? EmojiIdUtility.ToEmojiGlyph(parsed)
                    : "•"));
        }

        private static string HumanizeEmojiId(string emojiId)
        {
            return EmojiIdUtility.TryFromApiId(emojiId, out var parsedEmojiId)
                ? EmojiIdUtility.ToDisplayName(parsedEmojiId)
                : string.IsNullOrWhiteSpace(emojiId) ? "Unknown" : emojiId;
        }

        private static string HumanizeEmojiIdOrPending(string emojiId)
        {
            return string.IsNullOrWhiteSpace(emojiId) ? "Pending" : HumanizeEmojiId(emojiId);
        }

        private static bool TryResolveEntryDeck(AppBootstrap bootstrap, bool isBotMode, out string[] selectedDeck, out string deckId, out string error)
        {
            selectedDeck = Array.Empty<string>();
            deckId = string.Empty;
            error = string.Empty;

            if (bootstrap == null)
            {
                error = "App bootstrap is unavailable.";
                return false;
            }

            bootstrap.ActiveDeckService.EnsureInitialized(bootstrap.SessionState.UserId);

            var pendingSquad = LaunchSelections.GetPendingSquad();
            if (pendingSquad.Count > 0)
            {
                if (pendingSquad.Distinct().Count() != pendingSquad.Count)
                {
                    error = "Selected squad contains duplicate emojis.";
                    return false;
                }

                if (!isBotMode && pendingSquad.Count != 6)
                {
                    error = "Ranked requires exactly 6 selected emojis before blind ban.";
                    return false;
                }

                if (isBotMode && pendingSquad.Count != 5)
                {
                    error = "Bot battle requires exactly 5 selected emojis.";
                    return false;
                }

                selectedDeck = EmojiIdUtility.ToApiIds(pendingSquad);
                deckId = bootstrap.ActiveDeckService.DeckId;
                return true;
            }

            if (bootstrap.ActiveDeckService.HasActiveDeck)
            {
                if (!isBotMode && bootstrap.ActiveDeckService.ActiveDeckEmojiIds.Count != 6)
                {
                    error = "Ranked requires a 6-emoji squad.";
                    return false;
                }

                selectedDeck = EmojiIdUtility.ToApiIds(bootstrap.ActiveDeckService.ActiveDeckEmojiIds);
                deckId = bootstrap.ActiveDeckService.DeckId;
                return true;
            }

            error = isBotMode
                ? "Choose 5 emojis before starting Battle Bot."
                : "Choose 6 emojis before entering ranked.";
            return false;
        }
    }
}
