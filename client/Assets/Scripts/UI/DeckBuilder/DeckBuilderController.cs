using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Content;
using EmojiWar.Client.Core;
using EmojiWar.Client.Core.Decks;
using EmojiWar.Client.UI.Common;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.DeckBuilder
{
    public sealed class DeckBuilderController : MonoBehaviour
    {
        private const int DeckBuilderSlideIndex = 2;
        private const bool UseSlideBackground = false;
        private const bool UsePrefabFirstV2Layout = true;
        private const bool UseRescueSquadBuilder = true;

        [SerializeField] private Text deckSummaryLabel;
        [SerializeField] private Text selectedTrayLabel;
        [SerializeField] private Text statusLabel;
        [SerializeField] private Image deckBuilderPanelBackground;
        [SerializeField] private RectTransform selectedTrayContainer;
        [SerializeField] private PhaseBar phaseStepper;
        [SerializeField] private StatusChip statusChip;
        [SerializeField] private StickyFooterAction stickyFooterAction;
        [SerializeField] private StickyPrimaryAction stickyPrimaryAction;
        [SerializeField] private Button[] emojiButtons;
        [SerializeField] private Text[] emojiButtonLabels;
        [SerializeField] private Button saveButton;
        [SerializeField] private Text saveButtonLabel;
        [SerializeField] private Button backButton;

        private readonly List<EmojiId> workingSelection = new();
        private EmojiId[] availableEmojiIds = Array.Empty<EmojiId>();
        private bool isSaving;
        private bool isRankedEntryFlow;
        private bool isBotEntryFlow;
        private int requiredSelectionCount = 6;
        private string entryHintLine = "Step 1 of 4 • Squad";
        private bool runtimeTrayInitialized;
        private bool v2FoundationReady = true;
        private string v2FoundationMessage = string.Empty;
        private bool prefabWiringHealthy = true;
        private string prefabWiringMessage = string.Empty;
        private readonly HashSet<int> stickerVisualInitialized = new();
        private SquadBuilderRescueScreen rescueScreen;
        // V2 recovery milestone: DeckBuilder is prefab-first only.
        private static bool ShouldUseRuntimeLayout => !UsePrefabFirstV2Layout;

        private void Awake()
        {
            AutoWireSceneReferences();
            BindEmojiButtons();

            if (saveButton != null)
            {
                saveButton.onClick.RemoveAllListeners();
                saveButton.onClick.AddListener(SaveDeck);
            }

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(ReturnHome);
            }
        }

        private void Start()
        {
            v2FoundationReady = V2BootstrapGuard.EnsureReady(out v2FoundationMessage, requireSlides: true);
            isRankedEntryFlow = LaunchSelections.IsRankedEntryDeckBuilderFlow();
            isBotEntryFlow = LaunchSelections.IsBotEntryDeckBuilderFlow();
            requiredSelectionCount = LaunchSelections.GetDeckBuilderFlow() == LaunchSelections.DeckBuilderFlowBotSmartEntry ? 5 : 6;
            availableEmojiIds = EmojiIdUtility.LaunchRoster.ToArray();
            entryHintLine = BuildEntryHint();
            if (ShouldUseRuntimeLayout)
            {
                EnsureRuntimeDeckBuilderPanel();
                EnsureSelectedTrayContainer();
                ConfigureSelectedTrayContainer();
            }
            EnsureV2MetaWidgets();
            ApplyBuilderSurfaceStyling();
            EnsureEmojiButtonsReady();
            BindEmojiButtons();
            if (ShouldUseRuntimeLayout)
            {
                NormalizeMetaLayout();
                HideLegacyOverlayText();
            }
            else
            {
                EnsurePrefabBuilderZones();
                ValidatePrefabWiring();
            }
            InitializeSelectionFromFlow();
            DisableRuntimeGridArtifactsWhenPrefabMode();
            if (phaseStepper != null)
            {
                phaseStepper.SetStep(PhaseStep.Squad);
            }

            if (UseRescueSquadBuilder && MountRescueSquadBuilder())
            {
                return;
            }

            UpdateView();
            StickerPopArenaFlow.AttachDeckBuilder(this);
        }

        private bool MountRescueSquadBuilder()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>(true);
            }

            var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRect == null)
            {
                return false;
            }

            var existing = canvasRect.Find("SquadBuilderRescueScreen");
            GameObject screenObject;
            if (existing != null)
            {
                screenObject = existing.gameObject;
            }
            else
            {
                screenObject = RescueStickerFactory.CreateScreenRoot(canvasRect, "SquadBuilderRescueScreen");
            }

            screenObject.transform.SetAsLastSibling();
            rescueScreen = screenObject.GetComponent<SquadBuilderRescueScreen>();
            if (rescueScreen == null)
            {
                rescueScreen = screenObject.AddComponent<SquadBuilderRescueScreen>();
            }

            HideLegacyDeckBuilderUiForRescue(screenObject.transform);
            rescueScreen.Initialize(this);
            rescueScreen.Show();
            return true;
        }

        private void HideLegacyDeckBuilderUiForRescue(Transform rescueRoot)
        {
            SetInactiveIfNotRescue(deckBuilderPanelBackground != null ? deckBuilderPanelBackground.gameObject : null, rescueRoot);
            SetInactiveIfNotRescue(deckSummaryLabel != null ? deckSummaryLabel.gameObject : null, rescueRoot);
            SetInactiveIfNotRescue(selectedTrayLabel != null ? selectedTrayLabel.gameObject : null, rescueRoot);
            SetInactiveIfNotRescue(statusLabel != null ? statusLabel.gameObject : null, rescueRoot);
            SetInactiveIfNotRescue(selectedTrayContainer != null ? selectedTrayContainer.gameObject : null, rescueRoot);
            SetInactiveIfNotRescue(phaseStepper != null ? phaseStepper.gameObject : null, rescueRoot);
            SetInactiveIfNotRescue(statusChip != null ? statusChip.gameObject : null, rescueRoot);
            SetInactiveIfNotRescue(stickyFooterAction != null ? stickyFooterAction.gameObject : null, rescueRoot);
            SetInactiveIfNotRescue(stickyPrimaryAction != null ? stickyPrimaryAction.gameObject : null, rescueRoot);
            SetInactiveIfNotRescue(saveButton != null ? saveButton.gameObject : null, rescueRoot);
            SetInactiveIfNotRescue(backButton != null ? backButton.gameObject : null, rescueRoot);

            if (emojiButtons != null)
            {
                foreach (var button in emojiButtons)
                {
                    SetInactiveIfNotRescue(button != null ? button.gameObject : null, rescueRoot);
                }
            }

            var oldOverlay = GameObject.Find("StickerPopArenaOverlay");
            SetInactiveIfNotRescue(oldOverlay, rescueRoot);
        }

        private static void SetInactiveIfNotRescue(GameObject target, Transform rescueRoot)
        {
            if (target == null || rescueRoot == null)
            {
                return;
            }

            if (target.transform == rescueRoot || target.transform.IsChildOf(rescueRoot))
            {
                return;
            }

            target.SetActive(false);
        }

        private void EnsureRuntimeDeckBuilderPanel()
        {
            if (deckBuilderPanelBackground != null &&
                string.Equals(deckBuilderPanelBackground.gameObject.name, "RuntimeDeckBuilderPanel", StringComparison.Ordinal))
            {
                return;
            }

            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            var canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            if (deckBuilderPanelBackground != null)
            {
                deckBuilderPanelBackground.gameObject.SetActive(false);
            }

            var legacyPanelObject = GameObject.Find("DeckBuilderPanel");
            if (legacyPanelObject != null)
            {
                legacyPanelObject.SetActive(false);
            }

            var existing = canvasRect.Find("RuntimeDeckBuilderPanel");
            if (existing != null)
            {
                deckBuilderPanelBackground = existing.GetComponent<Image>();
                if (deckBuilderPanelBackground != null)
                {
                    return;
                }
            }

            var panelObject = new GameObject("RuntimeDeckBuilderPanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasRect, false);
            panelObject.transform.SetSiblingIndex(0);

            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.04f, 0.04f);
            panelRect.anchorMax = new Vector2(0.96f, 0.95f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            deckBuilderPanelBackground = panelObject.GetComponent<Image>();
            deckBuilderPanelBackground.color = UiThemeRuntime.Theme.SurfaceColor;
        }

        public IReadOnlyList<EmojiId> SelectedEmojis => workingSelection;

        public IReadOnlyList<EmojiId> AvailableEmojiIds => availableEmojiIds;

        public int RequiredSelectionCount => requiredSelectionCount;

        public bool CanContinueFromRescue => v2FoundationReady && prefabWiringHealthy && !isSaving && ValidateWorkingSelection(out _);

        public string RescueContinueLabel => isRankedEntryFlow || isBotEntryFlow ? "Continue" : "Save Squad";

        public string RescueTitle => isRankedEntryFlow
            ? "Build Your Ranked Squad"
            : isBotEntryFlow
                ? LaunchSelections.GetDeckBuilderFlow() == LaunchSelections.DeckBuilderFlowBotSmartEntry
                    ? "Build Your Smart Bot Squad"
                    : "Build Your Practice Ban Squad"
                : "Build Your Squad";

        public string RescueSubtitle => isRankedEntryFlow
            ? "Build your ranked squad first"
            : isBotEntryFlow
                ? LaunchSelections.GetDeckBuilderFlow() == LaunchSelections.DeckBuilderFlowBotSmartEntry
                    ? $"Pick {requiredSelectionCount} sticker fighters"
                    : $"Pick {requiredSelectionCount} fighters for blind ban practice"
                : $"Pick {requiredSelectionCount} sticker fighters";

        public bool TryAddEmoji(EmojiId emojiId)
        {
            if (workingSelection.Contains(emojiId))
            {
                return false;
            }

            if (workingSelection.Count >= requiredSelectionCount)
            {
                return false;
            }

            workingSelection.Add(emojiId);
            return true;
        }

        public void RemoveEmoji(EmojiId emojiId)
        {
            workingSelection.Remove(emojiId);
        }

        public bool ToggleEmojiForRescue(EmojiId emojiId)
        {
            if (workingSelection.Contains(emojiId))
            {
                workingSelection.Remove(emojiId);
                return true;
            }

            if (workingSelection.Count >= requiredSelectionCount)
            {
                return false;
            }

            workingSelection.Add(emojiId);
            return true;
        }

        public void ContinueFromRescue()
        {
            SaveDeck();
            rescueScreen?.RefreshFromController();
        }

        private void LoadActiveDeck()
        {
            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null)
            {
                workingSelection.Clear();
                workingSelection.AddRange(EmojiIdUtility.LaunchRoster.Take(requiredSelectionCount));
                return;
            }

            bootstrap.ActiveDeckService.EnsureInitialized(bootstrap.SessionState.UserId);
            workingSelection.Clear();
            workingSelection.AddRange(bootstrap.ActiveDeckService.ActiveDeckEmojiIds);
        }

        private void InitializeSelectionFromFlow()
        {
            var bootstrap = AppBootstrap.Instance;
            var pending = LaunchSelections.GetPendingSquad();

            workingSelection.Clear();
            if (pending.Count > 0)
            {
                foreach (var emojiId in pending)
                {
                    if (workingSelection.Contains(emojiId))
                    {
                        continue;
                    }

                    workingSelection.Add(emojiId);
                    if (workingSelection.Count >= requiredSelectionCount)
                    {
                        break;
                    }
                }
            }

            if (workingSelection.Count == requiredSelectionCount)
            {
                return;
            }

            if (bootstrap != null)
            {
                bootstrap.ActiveDeckService.EnsureInitialized(bootstrap.SessionState.UserId);
                foreach (var emojiId in bootstrap.ActiveDeckService.ActiveDeckEmojiIds)
                {
                    if (workingSelection.Contains(emojiId))
                    {
                        continue;
                    }

                    workingSelection.Add(emojiId);
                    if (workingSelection.Count >= requiredSelectionCount)
                    {
                        break;
                    }
                }
            }

            if (workingSelection.Count > requiredSelectionCount)
            {
                workingSelection.RemoveRange(requiredSelectionCount, workingSelection.Count - requiredSelectionCount);
            }
        }

        private string BuildEntryHint()
        {
            if (isRankedEntryFlow)
            {
                return "Step 1 of 4 • Squad";
            }

            if (isBotEntryFlow)
            {
                return "Step 1 of 3 • Squad";
            }

            return "Active Squad";
        }

        private void BindEmojiButtons()
        {
            if (emojiButtons == null)
            {
                return;
            }

            for (var index = 0; index < emojiButtons.Length; index++)
            {
                if (emojiButtons[index] == null)
                {
                    continue;
                }

                var capturedIndex = index;
                emojiButtons[index].onClick.RemoveAllListeners();
                emojiButtons[index].onClick.AddListener(() => ToggleEmoji(capturedIndex));
            }
        }

        private void ToggleEmoji(int emojiIndex)
        {
            if (emojiIndex < 0 || emojiIndex >= availableEmojiIds.Length)
            {
                return;
            }

            var emojiId = availableEmojiIds[emojiIndex];
            var targetButton = emojiButtons != null && emojiIndex < emojiButtons.Length ? emojiButtons[emojiIndex] : null;
            var targetMotion = targetButton != null ? targetButton.GetComponent<UiMotionController>() : null;
            if (targetMotion == null && targetButton != null)
            {
                targetMotion = targetButton.gameObject.AddComponent<UiMotionController>();
            }

            if (workingSelection.Contains(emojiId))
            {
                workingSelection.Remove(emojiId);
                targetMotion?.PlayStickerSlam();
                UpdateView();
                return;
            }

            if (workingSelection.Count >= requiredSelectionCount)
            {
                UpdateView();
                return;
            }

            workingSelection.Add(emojiId);
            targetMotion?.PlayJumpSelect();
            UpdateView();
        }

        private void SaveDeck()
        {
            if (!v2FoundationReady)
            {
                UpdateView();
                return;
            }

            if (!prefabWiringHealthy)
            {
                UpdateView();
                return;
            }

            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null)
            {
                UpdateView();
                return;
            }

            if (!ValidateWorkingSelection(out _))
            {
                UpdateView();
                return;
            }

            if (isSaving)
            {
                return;
            }

            StartCoroutine(SaveDeckRoutine(bootstrap));
        }

        private IEnumerator SaveDeckRoutine(AppBootstrap bootstrap)
        {
            isSaving = true;
            UpdateView();

            if (isRankedEntryFlow || isBotEntryFlow)
            {
                LaunchSelections.SetPendingSquad(workingSelection);
                bootstrap.ActiveDeckService.MarkStarterPromptSeen();
                isSaving = false;
                SceneManager.LoadScene(SceneNames.Match);
                yield break;
            }

            yield return bootstrap.SaveActiveDeck(workingSelection, null);

            bootstrap.ActiveDeckService.MarkStarterPromptSeen();
            isSaving = false;

            UpdateView();
        }

        private void ReturnHome()
        {
            LaunchSelections.BeginDeckEdit();
            SceneManager.LoadScene(SceneNames.Home);
        }

        private void UpdateView()
        {
            if (ShouldUseRuntimeLayout)
            {
                HideLegacyOverlayText();
            }
            else
            {
                EnsurePrefabBuilderZones();
                ValidatePrefabWiring();
            }

            if (deckSummaryLabel != null)
            {
                deckSummaryLabel.text = $"Selected {workingSelection.Count}/{requiredSelectionCount}";

                deckSummaryLabel.fontSize = Mathf.Max(24, UiThemeRuntime.Theme.HeadingFontSize + 2);
                deckSummaryLabel.alignment = TextAnchor.MiddleCenter;
                deckSummaryLabel.resizeTextForBestFit = false;
            }

            if (selectedTrayLabel != null)
            {
                selectedTrayLabel.text = string.Empty;
                selectedTrayLabel.gameObject.SetActive(false);
            }
            RenderSelectedTrayCards();

            var validationStatus = BuildValidationStatus();
            if (statusLabel != null)
            {
                statusLabel.text = string.Empty;
                statusLabel.gameObject.SetActive(false);
            }

            if (statusChip != null)
            {
                statusChip.SetStatus(validationStatus);
                statusChip.SetPanelState(MatchUiPanelState.Queue);
            }

            UpdateEmojiButtons();

            if (saveButton != null)
            {
                saveButton.interactable = v2FoundationReady && prefabWiringHealthy && !isSaving && ValidateWorkingSelection(out _);
                var saveImage = saveButton.GetComponent<Image>();
                if (saveImage != null)
                {
                    saveImage.color = saveButton.interactable
                        ? UiThemeRuntime.Theme.PrimaryCtaColor
                        : UiThemeRuntime.Theme.SecondaryCtaColor * new Color(1f, 1f, 1f, 0.65f);
                }
            }

            if (saveButtonLabel != null)
            {
                if (isRankedEntryFlow)
                {
                    saveButtonLabel.text = "Continue";
                }
                else if (isBotEntryFlow)
                {
                    saveButtonLabel.text = "Continue";
                }
                else
                {
                    saveButtonLabel.text = "Save Active Squad";
                }

                saveButtonLabel.fontSize = Mathf.Max(UiThemeRuntime.Theme.BodyFontSize, 20);
            }

            if (backButton != null)
            {
                var backImage = backButton.GetComponent<Image>();
                if (backImage != null)
                {
                    backImage.color = UiThemeRuntime.Theme.SecondaryCtaColor;
                }
            }

            var isSaveEnabled = v2FoundationReady && prefabWiringHealthy && !isSaving && ValidateWorkingSelection(out _);
            var ctaText = saveButtonLabel != null ? saveButtonLabel.text : "Continue";
            if (stickyPrimaryAction != null)
            {
                stickyPrimaryAction.Bind(ctaText, isSaveEnabled, SaveDeck, emphasize: isSaveEnabled);
            }
            else if (stickyFooterAction != null)
            {
                stickyFooterAction.Bind(ctaText, isSaveEnabled, SaveDeck);
            }
        }

        private void AutoWireSceneReferences()
        {
            var allowGlobalFallback = ShouldUseRuntimeLayout;
            var panelScope = ResolvePanelRoot();

            if (deckSummaryLabel == null)
            {
                deckSummaryLabel = FindTextByObjectName("DeckSummary", panelScope, allowGlobalFallback);
            }

            if (selectedTrayLabel == null)
            {
                selectedTrayLabel = FindTextByObjectName("SelectedTray", panelScope, allowGlobalFallback);
            }

            if (statusLabel == null)
            {
                statusLabel = FindTextByObjectName("DeckStatus", panelScope, allowGlobalFallback);
            }

            if (deckBuilderPanelBackground == null)
            {
                deckBuilderPanelBackground = FindImageByObjectName("DeckBuilderPanel", panelScope, allowGlobalFallback);
                if (deckBuilderPanelBackground == null)
                {
                    deckBuilderPanelBackground = FindObjectsOfType<Image>(true)
                        .Where(image => image != null && image.GetComponent<Button>() == null)
                        .OrderByDescending(image =>
                        {
                            var rect = image.rectTransform.rect;
                            return Mathf.Abs(rect.width * rect.height);
                        })
                        .FirstOrDefault();
                }
            }

            if (phaseStepper == null)
            {
                phaseStepper = FindObjectOfType<PhaseBar>(true);
            }

            if (statusChip == null)
            {
                statusChip = FindObjectOfType<StatusChip>(true);
            }

            if (saveButton == null)
            {
                var buttonObject = FindChildByName("Save Active DeckButton", panelScope, allowGlobalFallback) ??
                                   FindChildByName("ContinueButton", panelScope, allowGlobalFallback);
                if (buttonObject != null)
                {
                    saveButton = buttonObject.GetComponent<Button>();
                }
            }

            if (backButton == null)
            {
                var buttonObject = FindChildByName("Back HomeButton", panelScope, allowGlobalFallback);
                if (buttonObject != null)
                {
                    backButton = buttonObject.GetComponent<Button>();
                }
            }

            if (saveButtonLabel == null && saveButton != null)
            {
                saveButtonLabel = saveButton.GetComponentInChildren<Text>(true);
            }

            if (selectedTrayContainer == null)
            {
                selectedTrayContainer =
                    FindChildByName("SelectedTrayContainer", panelScope, allowGlobalFallback)?.GetComponent<RectTransform>() ??
                    FindChildByName("SelectedTrayCards", panelScope, allowGlobalFallback)?.GetComponent<RectTransform>();
            }

            if (IsUnsafeSelectedTrayContainer(selectedTrayContainer))
            {
                selectedTrayContainer = null;
            }

            var panelRoot = panelScope ?? ResolvePanelRoot();
            if (emojiButtons == null || emojiButtons.Length == 0 || !HasSufficientEmojiButtons(emojiButtons))
            {
                if (!TryDiscoverEmojiButtonsFromGrid(panelRoot) &&
                    !TryDiscoverEmojiButtonsBySceneIndex(panelRoot) &&
                    !TryDiscoverEmojiButtonsByName(panelRoot) &&
                    !TryDiscoverEmojiButtonsByLabel(panelRoot))
                {
                    if (ShouldUseRuntimeLayout)
                    {
                        emojiButtons = FindObjectsOfType<Button>(true)
                            .Where(button =>
                            {
                                var name = button.name;
                                return name.StartsWith("Emoji ", StringComparison.OrdinalIgnoreCase) ||
                                       name.StartsWith("Choice ", StringComparison.OrdinalIgnoreCase);
                            })
                            .OrderBy(button => button.name, StringComparer.Ordinal)
                            .ToArray();
                    }
                }
            }

            if ((emojiButtonLabels == null || emojiButtonLabels.Length == 0) && emojiButtons != null)
            {
                emojiButtonLabels = emojiButtons
                    .Select(button => button != null ? button.GetComponentInChildren<Text>(true) : null)
                    .ToArray();
            }

            if (stickyPrimaryAction == null && saveButton != null)
            {
                stickyPrimaryAction = saveButton.GetComponent<StickyPrimaryAction>();
                if (stickyPrimaryAction == null)
                {
                    stickyPrimaryAction = saveButton.gameObject.AddComponent<StickyPrimaryAction>();
                }
            }
        }

        private void EnsureEmojiButtonsReady()
        {
            if (HasSufficientEmojiButtons(emojiButtons))
            {
                return;
            }

            var panelRoot = ResolvePanelRoot();
            if (TryDiscoverEmojiButtonsFromGrid(panelRoot) ||
                TryDiscoverEmojiButtonsBySceneIndex(panelRoot) ||
                TryDiscoverEmojiButtonsByName(panelRoot) ||
                TryDiscoverEmojiButtonsByLabel(panelRoot))
            {
                return;
            }

            if (ShouldUseRuntimeLayout)
            {
                CreateRuntimeEmojiButtonGrid();
                DisableLegacyEmojiButtons();
            }
        }

        private void ValidatePrefabWiring()
        {
            if (ShouldUseRuntimeLayout)
            {
                prefabWiringHealthy = true;
                prefabWiringMessage = string.Empty;
                return;
            }

            var issues = new List<string>();
            if (!HasSufficientEmojiButtons(emojiButtons))
            {
                var panelRoot = ResolvePanelRoot();
                TryDiscoverEmojiButtonsFromGrid(panelRoot);
            }

            if (!HasSufficientEmojiButtons(emojiButtons))
            {
                issues.Add($"expected {EmojiIdUtility.LaunchRoster.Length} emoji buttons");
            }

            if (deckSummaryLabel == null)
            {
                issues.Add("DeckSummary label missing");
            }

            if (saveButton == null)
            {
                issues.Add("Continue button missing");
            }

            if (backButton == null)
            {
                issues.Add("Back Home button missing");
            }

            if (selectedTrayContainer == null || IsUnsafeSelectedTrayContainer(selectedTrayContainer))
            {
                issues.Add("SelectedTrayContainer missing");
            }

            if (issues.Count == 0)
            {
                prefabWiringHealthy = true;
                prefabWiringMessage = string.Empty;
                return;
            }

            prefabWiringHealthy = false;
            prefabWiringMessage = $"DeckBuilder prefab wiring incomplete: {string.Join("; ", issues)}.";
            Debug.LogError(prefabWiringMessage);
        }

        private static bool HasSufficientEmojiButtons(Button[] buttons)
        {
            if (buttons == null)
            {
                return false;
            }

            return buttons.Count(button => button != null) >= EmojiIdUtility.LaunchRoster.Length;
        }

        private bool TryDiscoverEmojiButtonsFromGrid(RectTransform scopeRoot)
        {
            if (scopeRoot == null)
            {
                return false;
            }

            var gridRoot = FindChildByName("EmojiGrid", scopeRoot, allowGlobalFallback: false);
            if (gridRoot == null)
            {
                return false;
            }

            var mapped = gridRoot.GetComponentsInChildren<Button>(true)
                .Where(button => button != null && button != saveButton && button != backButton)
                .OrderBy(button =>
                {
                    var sibling = button.transform.GetSiblingIndex();
                    return sibling < 0 ? int.MaxValue : sibling;
                })
                .Take(EmojiIdUtility.LaunchRoster.Length)
                .ToArray();
            if (!HasSufficientEmojiButtons(mapped))
            {
                return false;
            }

            emojiButtons = mapped;
            emojiButtonLabels = emojiButtons
                .Select(button => button != null ? button.GetComponentInChildren<Text>(true) : null)
                .ToArray();
            return true;
        }

        private bool TryDiscoverEmojiButtonsByName(RectTransform scopeRoot)
        {
            if (scopeRoot == null)
            {
                return false;
            }

            var allButtons = scopeRoot.GetComponentsInChildren<Button>(true)
                .Where(button => button != null && button != saveButton && button != backButton)
                .ToArray();
            if (allButtons.Length == 0)
            {
                return false;
            }

            var mapped = new List<Button>(EmojiIdUtility.LaunchRoster.Length);
            foreach (var emojiId in EmojiIdUtility.LaunchRoster)
            {
                var expectedName = $"Emoji {EmojiIdUtility.ToDisplayName(emojiId)}Button";
                var match = allButtons.FirstOrDefault(button =>
                    string.Equals(button.name, expectedName, StringComparison.OrdinalIgnoreCase));
                if (match == null || mapped.Contains(match))
                {
                    return false;
                }

                mapped.Add(match);
            }

            emojiButtons = mapped.ToArray();
            emojiButtonLabels = emojiButtons
                .Select(button => button != null ? button.GetComponentInChildren<Text>(true) : null)
                .ToArray();
            return true;
        }

        private bool TryDiscoverEmojiButtonsBySceneIndex(RectTransform scopeRoot)
        {
            var mapped = new List<Button>(EmojiIdUtility.LaunchRoster.Length);
            var mappedLabels = new List<Text>(EmojiIdUtility.LaunchRoster.Length);

            for (var slot = 1; slot <= EmojiIdUtility.LaunchRoster.Length; slot++)
            {
                var expectedName = $"Emoji {slot}Button";
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
                mappedLabels.Add(button.GetComponentInChildren<Text>(true));
            }

            emojiButtons = mapped.ToArray();
            emojiButtonLabels = mappedLabels.ToArray();
            return HasSufficientEmojiButtons(emojiButtons);
        }

        private bool TryDiscoverEmojiButtonsByLabel(RectTransform scopeRoot = null)
        {
            scopeRoot ??= ResolvePanelRoot();
            var allButtons = (scopeRoot != null
                    ? scopeRoot.GetComponentsInChildren<Button>(true)
                    : ShouldUseRuntimeLayout
                        ? FindObjectsOfType<Button>(true)
                        : Array.Empty<Button>())
                .Where(button => button != null && button != saveButton && button != backButton)
                .ToArray();
            if (allButtons.Length == 0)
            {
                return false;
            }

            var mapped = new List<Button>(EmojiIdUtility.LaunchRoster.Length);
            foreach (var emojiId in EmojiIdUtility.LaunchRoster)
            {
                var expected = EmojiIdUtility.ToDisplayName(emojiId);
                var match = allButtons.FirstOrDefault(button =>
                {
                    if (button == null)
                    {
                        return false;
                    }

                    if (HasAncestorNamed(button.transform, "RuntimeBottomNav"))
                    {
                        return false;
                    }

                    var label = button.GetComponentInChildren<Text>(true);
                    if (label != null && DoesLabelMatchEmoji(label.text, expected))
                    {
                        return true;
                    }

                    var labels = button.GetComponentsInChildren<Text>(true);
                    return labels.Any(candidate => candidate != null && DoesLabelMatchEmoji(candidate.text, expected));
                });

                if (match == null || mapped.Contains(match))
                {
                    return false;
                }

                mapped.Add(match);
            }

            emojiButtons = mapped.ToArray();
            emojiButtonLabels = emojiButtons
                .Select(button => button != null ? button.GetComponentInChildren<Text>(true) : null)
                .ToArray();
            return true;
        }

        private void DisableRuntimeGridArtifactsWhenPrefabMode()
        {
            if (ShouldUseRuntimeLayout)
            {
                return;
            }

            var runtimeGrid = GameObject.Find("RuntimeEmojiGrid");
            if (runtimeGrid != null)
            {
                runtimeGrid.SetActive(false);
            }
        }

        private void CreateRuntimeEmojiButtonGrid()
        {
            var root = ResolvePanelRoot();
            if (root == null)
            {
                return;
            }

            var existingRuntimeGrid = root.Find("RuntimeEmojiGrid");
            if (existingRuntimeGrid != null)
            {
                Destroy(existingRuntimeGrid.gameObject);
            }

            var gridObject = new GameObject("RuntimeEmojiGrid", typeof(RectTransform), typeof(GridLayoutGroup));
            gridObject.transform.SetParent(root, false);
            var gridRect = gridObject.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.08f, 0.24f);
            gridRect.anchorMax = new Vector2(0.92f, 0.69f);
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = Vector2.zero;

            var grid = gridObject.GetComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.cellSize = new Vector2(250f, 78f);

            var buttons = new List<Button>(availableEmojiIds.Length);
            var labels = new List<Text>(availableEmojiIds.Length);
            foreach (var emojiId in availableEmojiIds)
            {
                var buttonObject = new GameObject(
                    $"Emoji {EmojiIdUtility.ToDisplayName(emojiId)}Button",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button));
                buttonObject.transform.SetParent(gridObject.transform, false);

                var image = buttonObject.GetComponent<Image>();
                image.color = UiThemeRuntime.Theme.CardColors.Default;

                var button = buttonObject.GetComponent<Button>();
                buttons.Add(button);

                var labelObject = new GameObject("Title", typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(buttonObject.transform, false);
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(10f, 8f);
                labelRect.offsetMax = new Vector2(-10f, -8f);

                var label = labelObject.GetComponent<Text>();
                label.text = EmojiIdUtility.ToDisplayName(emojiId);
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
                label.font = deckSummaryLabel != null
                    ? deckSummaryLabel.font
                    : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.fontSize = Mathf.Max(UiThemeRuntime.Theme.BodyFontSize - 2, 18);
                labels.Add(label);
            }

            emojiButtons = buttons.ToArray();
            emojiButtonLabels = labels.ToArray();
        }

        private void DisableLegacyEmojiButtons()
        {
            var buttons = FindObjectsOfType<Button>(true);
            foreach (var button in buttons)
            {
                if (button == null || button == saveButton || button == backButton)
                {
                    continue;
                }

                if (HasAncestorNamed(button.transform, "RuntimeEmojiGrid") ||
                    HasAncestorNamed(button.transform, "RuntimeBottomNav"))
                {
                    continue;
                }

                var lowerName = button.name.ToLowerInvariant();
                var label = button.GetComponentInChildren<Text>(true);
                var lowerText = (label != null ? label.text : string.Empty).ToLowerInvariant();
                var looksLikeEmojiCard = lowerName.Contains("emoji ") ||
                                         lowerName.Contains("choice ") ||
                                         EmojiIdUtility.LaunchRoster
                                             .Select(EmojiIdUtility.ToDisplayName)
                                             .Any(display => string.Equals(display.ToLowerInvariant(), lowerText));
                if (looksLikeEmojiCard)
                {
                    button.gameObject.SetActive(false);
                }
            }
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
            if (ShouldUseRuntimeLayout)
            {
                var runtimePanel = GameObject.Find("RuntimeDeckBuilderPanel");
                if (runtimePanel != null && runtimePanel.transform is RectTransform runtimeRect)
                {
                    var runtimeImage = runtimePanel.GetComponent<Image>();
                    if (runtimeImage != null)
                    {
                        deckBuilderPanelBackground = runtimeImage;
                    }

                    return runtimeRect;
                }
            }

            if (deckBuilderPanelBackground != null)
            {
                return deckBuilderPanelBackground.rectTransform;
            }

            var panel = GameObject.Find("DeckBuilderPanel") ?? GameObject.Find("RuntimeDeckBuilderPanel");
            if (panel != null && panel.transform is RectTransform panelRect)
            {
                return panelRect;
            }

            var canvas = FindObjectOfType<Canvas>();
            return canvas != null ? canvas.transform as RectTransform : null;
        }

        private void ApplyBuilderSurfaceStyling()
        {
            if (deckBuilderPanelBackground == null)
            {
                return;
            }

            if (UseSlideBackground && UiThemeRuntime.TryGetSlideSprite(DeckBuilderSlideIndex, out var slide))
            {
                deckBuilderPanelBackground.sprite = slide;
                deckBuilderPanelBackground.type = Image.Type.Simple;
                deckBuilderPanelBackground.preserveAspect = false;
                deckBuilderPanelBackground.color = Color.white;
                return;
            }

            deckBuilderPanelBackground.sprite = null;
            deckBuilderPanelBackground.color = UiThemeRuntime.Theme.SurfaceColor;
        }

        private PhaseBar CreateRuntimePhaseBar(RectTransform root)
        {
            var phaseObject = new GameObject("RuntimePhaseBar", typeof(RectTransform), typeof(Image), typeof(PhaseBar));
            phaseObject.transform.SetParent(root, false);
            var phaseRect = phaseObject.GetComponent<RectTransform>();
            phaseRect.anchorMin = new Vector2(0.10f, 0.93f);
            phaseRect.anchorMax = new Vector2(0.90f, 0.97f);
            phaseRect.offsetMin = Vector2.zero;
            phaseRect.offsetMax = Vector2.zero;

            var phaseBackground = phaseObject.GetComponent<Image>();
            phaseBackground.color = UiThemeRuntime.Theme.SurfaceColor * new Color(1f, 1f, 1f, 0.88f);

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
            phaseLabel.font = deckSummaryLabel != null
                ? deckSummaryLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return phaseObject.GetComponent<PhaseBar>();
        }

        private StatusChip CreateRuntimeStatusChip(RectTransform root)
        {
            var chipObject = new GameObject("RuntimeStatusChip", typeof(RectTransform), typeof(Image), typeof(StatusChip));
            chipObject.transform.SetParent(root, false);
            var chipRect = chipObject.GetComponent<RectTransform>();
            chipRect.anchorMin = new Vector2(0.16f, 0.765f);
            chipRect.anchorMax = new Vector2(0.84f, 0.805f);
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
            chipLabel.font = deckSummaryLabel != null
                ? deckSummaryLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return chipObject.GetComponent<StatusChip>();
        }

        private void EnsureSelectedTrayContainer()
        {
            if (!ShouldUseRuntimeLayout)
            {
                return;
            }

            if (runtimeTrayInitialized)
            {
                return;
            }

            runtimeTrayInitialized = true;

            if (selectedTrayContainer != null)
            {
                return;
            }

            var parent = ResolvePanelRoot();
            if (parent == null && selectedTrayLabel != null)
            {
                parent = selectedTrayLabel.transform.parent as RectTransform;
            }
            if (parent == null)
            {
                return;
            }
            var trayRoot = new GameObject("SelectedTrayCards", typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            trayRoot.transform.SetParent(parent, false);
            if (selectedTrayLabel != null)
            {
                trayRoot.transform.SetSiblingIndex(selectedTrayLabel.transform.GetSiblingIndex() + 1);
            }
            selectedTrayContainer = trayRoot.GetComponent<RectTransform>();
            ConfigureSelectedTrayContainer();
        }

        private void ConfigureSelectedTrayContainer()
        {
            if (!EnsureSafeSelectedTrayContainer())
            {
                return;
            }

            selectedTrayContainer.anchorMin = new Vector2(0.08f, 0.18f);
            selectedTrayContainer.anchorMax = new Vector2(0.92f, 0.25f);
            selectedTrayContainer.pivot = new Vector2(0.5f, 0.5f);
            selectedTrayContainer.offsetMin = Vector2.zero;
            selectedTrayContainer.offsetMax = Vector2.zero;

            var layoutElement = selectedTrayContainer.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = selectedTrayContainer.gameObject.AddComponent<LayoutElement>();
            }

            layoutElement.preferredHeight = 72f;
            layoutElement.minHeight = 72f;

            var layout = selectedTrayContainer.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
            {
                layout = selectedTrayContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            layout.spacing = 8f;
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private void NormalizeMetaLayout()
        {
            if (deckSummaryLabel != null)
            {
                var summaryRect = deckSummaryLabel.rectTransform;
                summaryRect.anchorMin = new Vector2(0.08f, 0.82f);
                summaryRect.anchorMax = new Vector2(0.92f, 0.885f);
                summaryRect.offsetMin = Vector2.zero;
                summaryRect.offsetMax = Vector2.zero;
            }

            if (phaseStepper != null)
            {
                var phaseRect = phaseStepper.transform as RectTransform;
                if (phaseRect != null)
                {
                    phaseRect.anchorMin = new Vector2(0.10f, 0.93f);
                    phaseRect.anchorMax = new Vector2(0.90f, 0.97f);
                    phaseRect.offsetMin = Vector2.zero;
                    phaseRect.offsetMax = Vector2.zero;
                }
            }

            if (statusChip != null)
            {
                var chipRect = statusChip.transform as RectTransform;
                if (chipRect != null)
                {
                    chipRect.anchorMin = new Vector2(0.16f, 0.765f);
                    chipRect.anchorMax = new Vector2(0.84f, 0.805f);
                    chipRect.offsetMin = Vector2.zero;
                    chipRect.offsetMax = Vector2.zero;
                }
            }

            if (selectedTrayContainer != null)
            {
                selectedTrayContainer.anchorMin = new Vector2(0.08f, 0.69f);
                selectedTrayContainer.anchorMax = new Vector2(0.92f, 0.75f);
                selectedTrayContainer.offsetMin = Vector2.zero;
                selectedTrayContainer.offsetMax = Vector2.zero;
            }
        }

        private void EnsurePrefabBuilderZones()
        {
            if (ShouldUseRuntimeLayout)
            {
                return;
            }

            var root = ResolvePanelRoot();
            if (root == null)
            {
                return;
            }

            EnsurePrefabMetaScaffold(root);
            EnsurePrefabSelectedTrayScaffold(root);
            EnsurePrefabButtonGridScaffold(root);
            EnsurePrefabFooterScaffold(root);
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        }

        private void EnsurePrefabMetaScaffold(RectTransform root)
        {
            if (phaseStepper != null && phaseStepper.transform is RectTransform phaseRect)
            {
                phaseRect.anchorMin = new Vector2(0.10f, 0.93f);
                phaseRect.anchorMax = new Vector2(0.90f, 0.97f);
                phaseRect.offsetMin = Vector2.zero;
                phaseRect.offsetMax = Vector2.zero;
                phaseRect.SetAsFirstSibling();
            }

            if (deckSummaryLabel != null)
            {
                var summaryRect = deckSummaryLabel.rectTransform;
                summaryRect.anchorMin = new Vector2(0.22f, 0.84f);
                summaryRect.anchorMax = new Vector2(0.78f, 0.89f);
                summaryRect.offsetMin = Vector2.zero;
                summaryRect.offsetMax = Vector2.zero;
                deckSummaryLabel.alignment = TextAnchor.MiddleCenter;
                deckSummaryLabel.fontSize = Mathf.Max(22, UiThemeRuntime.Theme.HeadingFontSize);
                deckSummaryLabel.resizeTextForBestFit = false;
            }

            if (statusChip != null && statusChip.transform is RectTransform chipRect)
            {
                chipRect.anchorMin = new Vector2(0.18f, 0.78f);
                chipRect.anchorMax = new Vector2(0.82f, 0.82f);
                chipRect.offsetMin = Vector2.zero;
                chipRect.offsetMax = Vector2.zero;
            }

            if (statusLabel != null)
            {
                statusLabel.gameObject.SetActive(false);
            }
        }

        private void EnsurePrefabSelectedTrayScaffold(RectTransform root)
        {
            if (selectedTrayContainer == null || IsUnsafeSelectedTrayContainer(selectedTrayContainer))
            {
                var existing = FindChildByName("SelectedTrayContainer", root, allowGlobalFallback: false);
                if (existing != null)
                {
                    selectedTrayContainer = existing.GetComponent<RectTransform>();
                }
            }

            if (selectedTrayContainer == null || IsUnsafeSelectedTrayContainer(selectedTrayContainer))
            {
                var containerObject = new GameObject(
                    "SelectedTrayContainer",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(HorizontalLayoutGroup),
                    typeof(LayoutElement));
                containerObject.transform.SetParent(root, false);
                selectedTrayContainer = containerObject.GetComponent<RectTransform>();
            }

            if (selectedTrayContainer == null || IsUnsafeSelectedTrayContainer(selectedTrayContainer))
            {
                return;
            }

            if (selectedTrayContainer.transform.parent != root)
            {
                selectedTrayContainer.transform.SetParent(root, false);
            }

            selectedTrayContainer.anchorMin = new Vector2(0.12f, 0.68f);
            selectedTrayContainer.anchorMax = new Vector2(0.88f, 0.75f);
            selectedTrayContainer.offsetMin = Vector2.zero;
            selectedTrayContainer.offsetMax = Vector2.zero;
            selectedTrayContainer.gameObject.SetActive(true);

            var trayImage = selectedTrayContainer.GetComponent<Image>();
            if (trayImage == null)
            {
                trayImage = selectedTrayContainer.gameObject.AddComponent<Image>();
            }

            trayImage.color = UiThemeRuntime.Theme.SecondaryCtaColor * new Color(1f, 1f, 1f, 0.28f);

            var trayLayout = selectedTrayContainer.GetComponent<HorizontalLayoutGroup>();
            if (trayLayout == null)
            {
                trayLayout = selectedTrayContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            trayLayout.spacing = 6f;
            trayLayout.padding = new RectOffset(8, 8, 8, 8);
            trayLayout.childAlignment = TextAnchor.MiddleCenter;
            trayLayout.childControlWidth = false;
            trayLayout.childControlHeight = true;
            trayLayout.childForceExpandWidth = false;
            trayLayout.childForceExpandHeight = false;

            var trayLayoutElement = selectedTrayContainer.GetComponent<LayoutElement>();
            if (trayLayoutElement == null)
            {
                trayLayoutElement = selectedTrayContainer.gameObject.AddComponent<LayoutElement>();
            }

            trayLayoutElement.preferredHeight = 64f;
            trayLayoutElement.minHeight = 64f;

            if (selectedTrayLabel != null)
            {
                selectedTrayLabel.gameObject.SetActive(false);
            }
        }

        private void EnsurePrefabButtonGridScaffold(RectTransform root)
        {
            if (!HasSufficientEmojiButtons(emojiButtons))
            {
                EnsureEmojiButtonsReady();
                BindEmojiButtons();
            }

            if (!HasSufficientEmojiButtons(emojiButtons))
            {
                TryDiscoverEmojiButtonsFromGrid(root);
                TryDiscoverEmojiButtonsBySceneIndex(root);
                TryDiscoverEmojiButtonsByName(root);
                TryDiscoverEmojiButtonsByLabel(root);
                BindEmojiButtons();
            }

            var existingGridObject = FindChildByName("EmojiGrid", root, allowGlobalFallback: false);
            if (existingGridObject == null)
            {
                existingGridObject = new GameObject("EmojiGrid", typeof(RectTransform), typeof(GridLayoutGroup));
                existingGridObject.transform.SetParent(root, false);
            }

            var emojiButtonRoot = existingGridObject.GetComponent<RectTransform>();
            if (emojiButtonRoot == null)
            {
                return;
            }

            if (emojiButtonRoot.parent != root)
            {
                emojiButtonRoot.SetParent(root, false);
            }

            emojiButtonRoot.anchorMin = new Vector2(0.10f, 0.22f);
            emojiButtonRoot.anchorMax = new Vector2(0.90f, 0.66f);
            emojiButtonRoot.offsetMin = Vector2.zero;
            emojiButtonRoot.offsetMax = Vector2.zero;

            var grid = emojiButtonRoot.GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                grid = emojiButtonRoot.gameObject.AddComponent<GridLayoutGroup>();
            }

            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.spacing = new Vector2(8f, 8f);
            grid.padding = new RectOffset(4, 4, 4, 4);
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.childAlignment = TextAnchor.UpperCenter;

            var availableWidth = Mathf.Max(320f, root.rect.width * 0.80f);
            var cardWidth = Mathf.Floor((availableWidth - grid.padding.left - grid.padding.right - grid.spacing.x) / 2f);
            grid.cellSize = new Vector2(cardWidth, 84f);

            var existingButtons = emojiButtonRoot.GetComponentsInChildren<Button>(true)
                .Where(button => button != null && button != saveButton && button != backButton)
                .ToList();
            var mappedButtons = new List<Button>(EmojiIdUtility.LaunchRoster.Length);
            foreach (var emojiId in EmojiIdUtility.LaunchRoster)
            {
                var expectedName = $"Emoji {EmojiIdUtility.ToDisplayName(emojiId)}Button";
                var button = existingButtons.FirstOrDefault(candidate =>
                    string.Equals(candidate.name, expectedName, StringComparison.OrdinalIgnoreCase));
                if (button == null)
                {
                    button = CreatePrefabEmojiButton(emojiButtonRoot, emojiId);
                    existingButtons.Add(button);
                }

                mappedButtons.Add(button);
            }

            emojiButtons = mappedButtons.ToArray();
            emojiButtonLabels = emojiButtons
                .Select(button => button != null ? button.GetComponentInChildren<Text>(true) : null)
                .ToArray();
            BindEmojiButtons();
        }

        private Button CreatePrefabEmojiButton(RectTransform parent, EmojiId emojiId)
        {
            var buttonName = $"Emoji {EmojiIdUtility.ToDisplayName(emojiId)}Button";
            var buttonObject = new GameObject(
                buttonName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            buttonObject.transform.SetParent(parent, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = UiThemeRuntime.Theme.SecondaryCtaColor;

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 84f;
            layout.minHeight = 80f;
            layout.flexibleHeight = 0f;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);

            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 6f);
            labelRect.offsetMax = new Vector2(-8f, -6f);

            var label = labelObject.GetComponent<Text>();
            label.text = EmojiIdUtility.ToDisplayName(emojiId);
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.fontSize = Mathf.Max(UiThemeRuntime.Theme.BodyFontSize - 1, 16);
            label.resizeTextForBestFit = false;
            label.font = deckSummaryLabel != null
                ? deckSummaryLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return buttonObject.GetComponent<Button>();
        }

        private void EnsurePrefabFooterScaffold(RectTransform root)
        {
            if (saveButton != null && saveButton.transform is RectTransform saveRect)
            {
                saveRect.anchorMin = new Vector2(0.10f, 0.11f);
                saveRect.anchorMax = new Vector2(0.90f, 0.17f);
                saveRect.offsetMin = Vector2.zero;
                saveRect.offsetMax = Vector2.zero;
            }

            if (backButton != null && backButton.transform is RectTransform backRect)
            {
                backRect.anchorMin = new Vector2(0.10f, 0.04f);
                backRect.anchorMax = new Vector2(0.90f, 0.10f);
                backRect.offsetMin = Vector2.zero;
                backRect.offsetMax = Vector2.zero;
            }

            if (stickyFooterAction != null)
            {
                stickyFooterAction.Bind("Continue", false, SaveDeck);
            }
        }

        private void RenderSelectedTrayCards()
        {
            if (!EnsureSafeSelectedTrayContainer())
            {
                return;
            }

            for (var index = selectedTrayContainer.childCount - 1; index >= 0; index--)
            {
                var child = selectedTrayContainer.GetChild(index);
                if (child == null)
                {
                    continue;
                }

                if (child.name.StartsWith("Tray-", StringComparison.OrdinalIgnoreCase))
                {
                    Destroy(child.gameObject);
                }
                else if (child.name.StartsWith("TrayPlaceholder", StringComparison.OrdinalIgnoreCase))
                {
                    Destroy(child.gameObject);
                }
            }

            selectedTrayContainer.gameObject.SetActive(true);
            if (workingSelection.Count == 0)
            {
                CreateTrayPlaceholderCard();
                return;
            }

            foreach (var emojiId in workingSelection)
            {
                CreateSelectedTrayCard(emojiId);
            }
        }

        private void CreateTrayPlaceholderCard()
        {
            var placeholderObject = new GameObject("TrayPlaceholder", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
            placeholderObject.transform.SetParent(selectedTrayContainer, false);

            var layout = placeholderObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 240f;
            layout.preferredHeight = 48f;
            layout.minWidth = 200f;

            var image = placeholderObject.GetComponent<Image>();
            image.color = UiThemeRuntime.Theme.SecondaryCtaColor * new Color(1f, 1f, 1f, 0.24f);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(placeholderObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 6f);
            labelRect.offsetMax = new Vector2(-8f, -6f);

            var label = labelObject.GetComponent<Text>();
            label.text = "Tap cards to add to squad";
            label.color = Color.white * new Color(1f, 1f, 1f, 0.88f);
            label.font = deckSummaryLabel != null ? deckSummaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = Mathf.Max(UiThemeRuntime.Theme.BodyFontSize - 2, 15);
            label.alignment = TextAnchor.MiddleCenter;
            label.resizeTextForBestFit = false;
        }

        private void CreateSelectedTrayCard(EmojiId emojiId)
        {
            var cardObject = new GameObject($"Tray-{emojiId}", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
            cardObject.transform.SetParent(selectedTrayContainer, false);

            var layout = cardObject.GetComponent<LayoutElement>();
            layout.preferredWidth = 84f;
            layout.preferredHeight = 54f;
            layout.minWidth = 84f;

            var image = cardObject.GetComponent<Image>();
            image.color = UiThemeRuntime.ResolveRoleAccent(emojiId) * new Color(1f, 1f, 1f, 0.58f);

            var button = cardObject.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                RemoveEmoji(emojiId);
                UpdateView();
            });

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(cardObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 6f);
            labelRect.offsetMax = new Vector2(-8f, -6f);

            var label = labelObject.GetComponent<Text>();
            label.text = EmojiIdUtility.ToEmojiGlyph(emojiId);
            label.color = Color.white;
            label.font = deckSummaryLabel != null ? deckSummaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = Mathf.Max(UiThemeRuntime.Theme.HeadingFontSize - 4, 18);
            label.alignment = TextAnchor.MiddleCenter;
        }

        private bool EnsureSafeSelectedTrayContainer()
        {
            if (selectedTrayContainer == null)
            {
                return false;
            }

            if (!IsUnsafeSelectedTrayContainer(selectedTrayContainer))
            {
                return true;
            }

            Debug.LogWarning("DeckBuilder: selected tray container points to a non-tray layout. Tray rendering is disabled to protect the emoji grid.");
            selectedTrayContainer = null;
            return false;
        }

        private static bool IsUnsafeSelectedTrayContainer(RectTransform candidate)
        {
            if (candidate == null)
            {
                return false;
            }

            if (candidate.GetComponent<Button>() != null)
            {
                return true;
            }

            var lowerName = candidate.name.ToLowerInvariant();
            if (lowerName.Contains("grid") || lowerName.Contains("choices"))
            {
                return true;
            }

            if (candidate.GetComponent<HorizontalLayoutGroup>() == null)
            {
                return true;
            }

            var buttons = candidate.GetComponentsInChildren<Button>(true);
            var emojiLikeButtons = buttons.Count(button =>
                button != null &&
                (button.name.StartsWith("Emoji ", StringComparison.OrdinalIgnoreCase) ||
                 button.name.StartsWith("Choice ", StringComparison.OrdinalIgnoreCase)));

            return emojiLikeButtons >= 4;
        }

        private string BuildSelectedTraySummary()
        {
            if (workingSelection.Count == 0)
            {
                return $"Selected Tray ({workingSelection.Count}/{requiredSelectionCount})";
            }

            return $"Selected Tray ({workingSelection.Count}/{requiredSelectionCount}) • Tap a card to remove";
        }

        private bool ValidateWorkingSelection(out string validationError)
        {
            if (workingSelection.Count < requiredSelectionCount)
            {
                validationError = $"Choose {requiredSelectionCount - workingSelection.Count} more emoji{(requiredSelectionCount - workingSelection.Count == 1 ? string.Empty : "s")} to continue.";
                return false;
            }

            if (workingSelection.Count > requiredSelectionCount)
            {
                validationError = $"This entry uses exactly {requiredSelectionCount} emojis.";
                return false;
            }

            if (workingSelection.Distinct().Count() != workingSelection.Count)
            {
                validationError = "Each selected slot must be a different emoji.";
                return false;
            }

            validationError = string.Empty;
            return true;
        }

        private void UpdateEmojiButtons()
        {
            if (emojiButtons == null)
            {
                return;
            }

            for (var index = 0; index < emojiButtons.Length; index++)
            {
                if (emojiButtons[index] == null)
                {
                    continue;
                }

                var isVisible = index < availableEmojiIds.Length;
                emojiButtons[index].gameObject.SetActive(isVisible);
                emojiButtons[index].interactable = isVisible && v2FoundationReady;

                if (!isVisible)
                {
                    continue;
                }

                var emojiId = availableEmojiIds[index];
                var isSelected = workingSelection.Contains(emojiId);
                var isSelectionLocked = workingSelection.Count >= requiredSelectionCount && !isSelected;
                var cardState = UnitCardState.Default;
                if (isSelected)
                {
                    cardState |= UnitCardState.Selected;
                }

                if (isSelectionLocked)
                {
                    cardState |= UnitCardState.Disabled;
                }

                var image = emojiButtons[index].GetComponent<Image>();
                if (image != null)
                {
                    image.color = UiThemeRuntime.ResolveCardColor(cardState);
                }

                if (emojiButtonLabels != null && index < emojiButtonLabels.Length && emojiButtonLabels[index] != null)
                {
                    emojiButtonLabels[index].text = string.Empty;
                    emojiButtonLabels[index].fontSize = UiThemeRuntime.Theme.BodyFontSize;
                }

                ApplyStickerCardVisual(emojiButtons[index], emojiId, cardState);

                var motion = emojiButtons[index].GetComponent<UiMotionController>();
                if (motion == null)
                {
                    motion = emojiButtons[index].gameObject.AddComponent<UiMotionController>();
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

            card.SetCompactMode(true);
            card.Bind(emojiId, cardState);
        }

        private void EnsureStickerCardLayout(Button button)
        {
            var buttonId = button.GetInstanceID();
            if (stickerVisualInitialized.Contains(buttonId))
            {
                return;
            }

            stickerVisualInitialized.Add(buttonId);
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
                existingRect.offsetMin = new Vector2(14f, 8f);
                existingRect.offsetMax = new Vector2(-14f, -8f);
                existingText.alignment = TextAnchor.MiddleCenter;
                existingText.font = deckSummaryLabel != null ? deckSummaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                existingText.fontSize = Mathf.Max(UiThemeRuntime.Theme.BodyFontSize - 2, 18);
                existingText.color = Color.white;
            }

            EnsureCardLabel(button.transform, "Glyph", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(26f, 20f), TextAnchor.UpperLeft, Mathf.Max(UiThemeRuntime.Theme.ChipFontSize - 1, 14));
            EnsureCardLabel(button.transform, "RolePill", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-8f, -8f), new Vector2(60f, 20f), TextAnchor.UpperRight, Mathf.Max(UiThemeRuntime.Theme.ChipFontSize - 3, 12));
            EnsureCardLabel(button.transform, "StateBadge", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 6f), new Vector2(62f, 18f), TextAnchor.LowerRight, Mathf.Max(UiThemeRuntime.Theme.ChipFontSize - 3, 11));

            if (button.transform.Find("Aura") == null)
            {
                var auraObject = new GameObject("Aura", typeof(RectTransform), typeof(Image));
                auraObject.transform.SetParent(button.transform, false);
                auraObject.transform.SetSiblingIndex(0);
                var auraRect = auraObject.GetComponent<RectTransform>();
                auraRect.anchorMin = new Vector2(0.015f, 0.06f);
                auraRect.anchorMax = new Vector2(0.985f, 0.94f);
                auraRect.offsetMin = Vector2.zero;
                auraRect.offsetMax = Vector2.zero;

                var auraImage = auraObject.GetComponent<Image>();
                auraImage.color = UiThemeRuntime.Theme.ControlAccent * new Color(1f, 1f, 1f, 0.12f);
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
            text.font = deckSummaryLabel != null ? deckSummaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
        }

        private string BuildValidationStatus()
        {
            if (!v2FoundationReady)
            {
                return string.IsNullOrWhiteSpace(v2FoundationMessage)
                    ? "V2 setup incomplete. Run EmojiWar > V2 > Create Default Theme Assets."
                    : $"V2 setup incomplete. {v2FoundationMessage}";
            }

            if (!prefabWiringHealthy)
            {
                return string.IsNullOrWhiteSpace(prefabWiringMessage)
                    ? "DeckBuilder prefab wiring is incomplete."
                    : prefabWiringMessage;
            }

            if (ValidateWorkingSelection(out _))
            {
                return isRankedEntryFlow
                    ? "Ready"
                    : isBotEntryFlow
                        ? "Ready"
                        : "Ready";
            }

            var missing = requiredSelectionCount - workingSelection.Count;
            if (missing > 0)
            {
                return $"{missing} more unit{(missing == 1 ? string.Empty : "s")} required";
            }

            return $"Use exactly {requiredSelectionCount}";
        }

        private static bool DoesLabelMatchEmoji(string labelValue, string expectedDisplayName)
        {
            if (string.IsNullOrWhiteSpace(expectedDisplayName) || string.IsNullOrWhiteSpace(labelValue))
            {
                return false;
            }

            var normalized = labelValue
                .Replace("✓", string.Empty)
                .Replace("✔", string.Empty)
                .Replace("•", " ")
                .Trim();

            if (string.Equals(normalized, expectedDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return normalized.StartsWith(expectedDisplayName + " ", StringComparison.OrdinalIgnoreCase);
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

        private void HideLegacyOverlayText()
        {
            if (selectedTrayLabel != null)
            {
                selectedTrayLabel.gameObject.SetActive(false);
            }

            if (statusLabel != null)
            {
                statusLabel.gameObject.SetActive(false);
            }

            var labels = FindObjectsOfType<Text>(true);
            foreach (var label in labels)
            {
                if (label == null)
                {
                    continue;
                }

                if (label == deckSummaryLabel ||
                    label == selectedTrayLabel ||
                    label == statusLabel)
                {
                    continue;
                }

                if (label.GetComponentInParent<Button>(true) != null)
                {
                    continue;
                }

                if (HasAncestorNamed(label.transform, "SelectedTrayCards"))
                {
                    continue;
                }

                var lowerName = label.name.ToLowerInvariant();
                var lowerText = (label.text ?? string.Empty).ToLowerInvariant();
                if (lowerName.Contains("selectedtray") ||
                    lowerName.Contains("deckstatus") ||
                    lowerName.Contains("currentsquad") ||
                    lowerName.Contains("activedeck") ||
                    lowerName.Contains("summary") ||
                    lowerText.Contains("selected tray") ||
                    lowerText.Contains("tap a card to remove") ||
                    lowerText.Contains("current squad") ||
                    lowerText.Contains("active squad"))
                {
                    label.gameObject.SetActive(false);
                }
            }
        }

        private static bool HasAncestorNamed(Transform node, string ancestorName)
        {
            if (node == null || string.IsNullOrWhiteSpace(ancestorName))
            {
                return false;
            }

            var current = node.parent;
            while (current != null)
            {
                if (string.Equals(current.name, ancestorName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }
    }
}
