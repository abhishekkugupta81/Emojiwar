using System;
using System.Collections;
using System.Linq;
using EmojiWar.Client.Content;
using EmojiWar.Client.Core;
using EmojiWar.Client.Gameplay.Contracts;
using EmojiWar.Client.UI.Common;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Home
{
    public sealed class HomeScreenController : MonoBehaviour
    {
        private const int HomeSlideIndex = 1;
        private const bool UseSlideBackground = false;
        private const bool ShowRuntimeHeroCluster = false;
        private const bool UsePrefabFirstV2Layout = true;
        private static readonly Vector2 SafeAreaAnchorMin = new(0.03f, 0.03f);
        private static readonly Vector2 SafeAreaAnchorMax = new(0.97f, 0.97f);
        private static readonly Vector2 HeroHeaderAnchorMin = new(0.08f, 0.80f);
        private static readonly Vector2 HeroHeaderAnchorMax = new(0.92f, 0.96f);
        private static readonly Vector2 HeroDecorAnchorMin = new(0.08f, 0.66f);
        private static readonly Vector2 HeroDecorAnchorMax = new(0.92f, 0.79f);
        private static readonly Vector2 SquadStripAnchorMin = new(0.08f, 0.52f);
        private static readonly Vector2 SquadStripAnchorMax = new(0.92f, 0.64f);
        private static readonly Vector2 ActionStackAnchorMin = new(0.10f, 0.19f);
        private static readonly Vector2 ActionStackAnchorMax = new(0.90f, 0.51f);

        [SerializeField] private Text profileSummaryLabel;
        [SerializeField] private Text deckSummaryLabel;
        [SerializeField] private RectTransform sceneSquadChipsContainer;
        [SerializeField] private GameObject resumeRankedRoot;
        [SerializeField] private Text resumeRankedLabel;
        [SerializeField] private GameObject starterPromptRoot;
        [SerializeField] private Text starterPromptLabel;
        [SerializeField] private Button rankedButton;
        [SerializeField] private Button practiceButton;
        [SerializeField] private Button codexButton;
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Button editDeckButton;
        [SerializeField] private Image homePanelBackground;
        [SerializeField] private bool useRescueHome = true;

        private Coroutine rankSummaryRoutine;
        private string cachedRankSummary = "Ranked summary pending";
        private HomeRescueScreen rescueHomeScreen;
        private GameObject runtimeBottomNav;
        private GameObject runtimeProfilePanel;
        private RectTransform runtimeSquadStripRoot;
        private Text runtimeProfilePanelText;
        private RectTransform runtimeSquadCardsContainer;
        private RectTransform runtimeActionStackRoot;
        private GameObject runtimeHeroCluster;
        private RectTransform runtimeHeroHeaderRoot;
        private Text runtimeHeroTitleLabel;
        private Text runtimeHeroSubtitleLabel;
        private readonly Image[] runtimeHeroBadgeImages = new Image[3];
        private readonly Text[] runtimeHeroBadgeLabels = new Text[3];
        private RectTransform runtimeHomeRoot;
        private bool runtimeUiInitialized;
        private bool v2FoundationReady = true;
        private string v2FoundationMessage = string.Empty;
        private RectTransform sceneBottomNavRoot;
        private GameObject sceneProfilePanel;
        private Text sceneProfilePanelText;
        private readonly System.Collections.Generic.List<GameObject> sceneSquadChipObjects = new();
        private Coroutine sceneProfilePanelAnimationRoutine;
        private RectTransform sceneSafeAreaRoot;
        private static Sprite cachedCircleSprite;

        // V2 recovery milestone: Home is prefab-first only.
        private static bool ShouldUseRuntimeLayout => !UsePrefabFirstV2Layout;

        private void Awake()
        {
            AutoWireSceneReferences();
            if (useRescueHome)
            {
                HideLegacyPromptPanels();
                HideLegacySummaryLabels();
                HideRuntimeGeneratedUi();
                return;
            }

            v2FoundationReady = V2BootstrapGuard.EnsureReady(out v2FoundationMessage, requireSlides: true);
            EnsurePrefabHomeZones();
            HideLegacyPromptPanels();
            RewordPrimaryButtons();
            StylePrimaryButtons();
            ApplyHomeSurfaceStyling();
        }

        private void OnEnable()
        {
            if (useRescueHome)
            {
                ShowRescueHome();
                return;
            }

            RefreshView();
            StickerPopArenaFlow.AttachHome(this);
            if (rankSummaryRoutine != null)
            {
                StopCoroutine(rankSummaryRoutine);
            }

            rankSummaryRoutine = StartCoroutine(RefreshRankSummary());
        }

        private void OnDisable()
        {
            if (rescueHomeScreen != null)
            {
                rescueHomeScreen.Hide();
            }

            if (rankSummaryRoutine != null)
            {
                StopCoroutine(rankSummaryRoutine);
                rankSummaryRoutine = null;
            }
        }

        private void ShowRescueHome()
        {
            DestroyOldStickerPopHomeOverlay();
            SetLegacyHomeUiVisible(false);

            var canvas = GetComponentInParent<Canvas>() ?? FindObjectOfType<Canvas>();
            var parent = canvas != null ? canvas.transform : transform;
            if (rescueHomeScreen == null)
            {
                rescueHomeScreen = parent.GetComponentsInChildren<HomeRescueScreen>(true)
                    .FirstOrDefault(screen => screen != null);
            }

            if (rescueHomeScreen == null)
            {
                var screenObject = new GameObject("HomeRescueScreen", typeof(RectTransform), typeof(HomeRescueScreen));
                screenObject.transform.SetParent(parent, false);
                var rect = screenObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rescueHomeScreen = screenObject.GetComponent<HomeRescueScreen>();
            }

            rescueHomeScreen.Initialize(this);
            rescueHomeScreen.gameObject.SetActive(true);
            rescueHomeScreen.transform.SetAsLastSibling();
            rescueHomeScreen.Show();
        }

        private void SetLegacyHomeUiVisible(bool isVisible)
        {
            SetObjectActive(profileSummaryLabel != null ? profileSummaryLabel.gameObject : null, isVisible);
            SetObjectActive(deckSummaryLabel != null ? deckSummaryLabel.gameObject : null, isVisible);
            SetObjectActive(sceneSquadChipsContainer != null ? sceneSquadChipsContainer.gameObject : null, isVisible);
            SetObjectActive(resumeRankedRoot, isVisible);
            SetObjectActive(starterPromptRoot, isVisible);
            SetObjectActive(rankedButton != null ? rankedButton.gameObject : null, isVisible);
            SetObjectActive(practiceButton != null ? practiceButton.gameObject : null, isVisible);
            SetObjectActive(codexButton != null ? codexButton.gameObject : null, isVisible);
            SetObjectActive(leaderboardButton != null ? leaderboardButton.gameObject : null, isVisible);
            SetObjectActive(editDeckButton != null ? editDeckButton.gameObject : null, isVisible);
            SetObjectActive(runtimeBottomNav, isVisible);
            SetObjectActive(runtimeProfilePanel, isVisible);
            SetObjectActive(runtimeSquadStripRoot != null ? runtimeSquadStripRoot.gameObject : null, isVisible);
            SetObjectActive(runtimeActionStackRoot != null ? runtimeActionStackRoot.gameObject : null, isVisible);
            SetObjectActive(runtimeHeroCluster, isVisible);
            SetObjectActive(runtimeHeroHeaderRoot != null ? runtimeHeroHeaderRoot.gameObject : null, isVisible);
            SetObjectActive(sceneBottomNavRoot != null ? sceneBottomNavRoot.gameObject : null, isVisible);
        }

        private static void SetObjectActive(GameObject target, bool isActive)
        {
            if (target != null)
            {
                target.SetActive(isActive);
            }
        }

        private static void DestroyOldStickerPopHomeOverlay()
        {
            var overlays = FindObjectsOfType<RectTransform>(true)
                .Where(rect => rect != null && string.Equals(rect.name, "StickerPopArenaOverlay", StringComparison.Ordinal));
            foreach (var overlay in overlays)
            {
                if (overlay != null)
                {
                    Destroy(overlay.gameObject);
                }
            }
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (sceneSquadChipsContainer == null)
            {
                return;
            }

            var chipsLayout = sceneSquadChipsContainer.GetComponent<GridLayoutGroup>();
            ApplySceneSquadChipGridSizing(chipsLayout);
        }

        private void AutoWireSceneReferences()
        {
            var allowGlobalFallback = ShouldUseRuntimeLayout;
            if (homePanelBackground == null)
            {
                homePanelBackground = FindObjectsOfType<Image>(true)
                    .Where(image => image != null && string.Equals(image.gameObject.name, "HomePanel", StringComparison.Ordinal))
                    .OrderByDescending(image => image.gameObject.activeInHierarchy)
                    .ThenByDescending(image =>
                    {
                        var rect = image.rectTransform.rect;
                        return Mathf.Abs(rect.width * rect.height);
                    })
                    .FirstOrDefault();
            }

            var homeScope = homePanelBackground != null
                ? homePanelBackground.transform
                : FindObjectOfType<Canvas>()?.transform;

            if (profileSummaryLabel == null)
            {
                profileSummaryLabel = FindTextByObjectName("ProfileSummary", homeScope, allowGlobalFallback);
            }

            if (deckSummaryLabel == null)
            {
                deckSummaryLabel = FindTextByObjectName("DeckSummary", homeScope, allowGlobalFallback);
            }

            if (sceneSquadChipsContainer == null)
            {
                sceneSquadChipsContainer = FindChildByName("ActiveSquadChips", homeScope, allowGlobalFallback)
                    ?.GetComponent<RectTransform>();
            }

            if (resumeRankedRoot == null)
            {
                resumeRankedRoot = FindChildByName("Resume Ranked MatchButton", homeScope, allowGlobalFallback);
            }

            if (resumeRankedLabel == null && resumeRankedRoot != null)
            {
                resumeRankedLabel = resumeRankedRoot.GetComponentInChildren<Text>(true);
            }

            if (starterPromptRoot == null)
            {
                starterPromptRoot = FindChildByName("StarterPromptPanel", homeScope, allowGlobalFallback);
            }

            if (starterPromptLabel == null && starterPromptRoot != null)
            {
                starterPromptLabel = starterPromptRoot.GetComponentInChildren<Text>(true);
            }

            rankedButton = rankedButton != null
                ? rankedButton
                : FindButtonByObjectName("Battle PlayersButton", homeScope, allowGlobalFallback)
                  ?? (allowGlobalFallback
                      ? FindButtonByLabel("Battle Players") ?? FindButtonByLabel("Play Ranked")
                      : null);
            practiceButton = practiceButton != null
                ? practiceButton
                : FindButtonByObjectName("Battle BotButton", homeScope, allowGlobalFallback)
                  ?? (allowGlobalFallback
                      ? FindButtonByLabel("Battle Bot") ?? FindButtonByLabel("Practice")
                      : null);
            codexButton = codexButton != null
                ? codexButton
                : FindButtonByObjectName("CodexButton", homeScope, allowGlobalFallback)
                  ?? (allowGlobalFallback ? FindButtonByLabel("Codex") : null);
            leaderboardButton = leaderboardButton != null
                ? leaderboardButton
                : FindButtonByObjectName("LeaderboardButton", homeScope, allowGlobalFallback)
                  ?? (allowGlobalFallback ? FindButtonByLabel("Leaderboard") : null);
            editDeckButton = editDeckButton != null
                ? editDeckButton
                : FindButtonByObjectName("Edit DeckButton", homeScope, allowGlobalFallback)
                  ?? (allowGlobalFallback
                      ? FindButtonByLabel("Edit Deck") ?? FindButtonByLabel("Edit Squad")
                      : null);

            if (homePanelBackground == null)
            {
                homePanelBackground = FindChildByName("HomePanel", homeScope, allowGlobalFallback)?.GetComponent<Image>();
                if (homePanelBackground == null && allowGlobalFallback)
                {
                    homePanelBackground = FindObjectsOfType<Image>(true)
                        .Where(image => image != null && image.GetComponent<Button>() == null)
                        .OrderByDescending(image =>
                        {
                            var rect = image.rectTransform.rect;
                            return Mathf.Abs(rect.width * rect.height);
                        })
                        .FirstOrDefault();
                }
            }
        }

        public void OpenBattlePlayers()
        {
            if (LaunchSelections.HasRankedResume())
            {
                OpenResumeRankedMatch();
                return;
            }

            LaunchSelections.BeginRankedMatchSelection();
            TryPrefillPendingSquad(6);
            SceneManager.LoadScene(SceneNames.DeckBuilder);
        }

        public void OpenResumeRankedMatch()
        {
            LaunchSelections.BeginRankedResume();
            SceneManager.LoadScene(SceneNames.Match);
        }

        public void OpenBattleBot()
        {
            LaunchSelections.BeginBotMatchSelection(LaunchSelections.BotPractice);
            TryPrefillPendingSquad(5);
            SceneManager.LoadScene(SceneNames.DeckBuilder);
        }

        public void OpenSmartBot()
        {
            LaunchSelections.BeginBotMatchSelection(LaunchSelections.BotSmart);
            TryPrefillPendingSquad(5);
            SceneManager.LoadScene(SceneNames.DeckBuilder);
        }

        public void OpenDeckBuilder()
        {
            LaunchSelections.BeginDeckEdit();
            SceneManager.LoadScene(SceneNames.DeckBuilder);
        }

        public void OpenStarterDeckBuilder()
        {
            AppBootstrap.Instance?.ActiveDeckService.MarkStarterPromptSeen();
            OpenDeckBuilder();
        }

        public void OpenStarterBotMatch()
        {
            AppBootstrap.Instance?.ActiveDeckService.MarkStarterPromptSeen();
            OpenBattleBot();
        }

        public void OpenCodex()
        {
            SceneManager.LoadScene(SceneNames.Codex);
        }

        public void OpenLeaderboard()
        {
            SceneManager.LoadScene(SceneNames.Leaderboard);
        }

        public void OpenProfilePlaceholder()
        {
            EnsureProfilePanelFallback();
            if (sceneProfilePanel == null)
            {
                return;
            }

            if (sceneProfilePanelText != null)
            {
                sceneProfilePanelText.text = BuildProfilePanelCopy();
            }

            AnimateSceneProfilePanel(!sceneProfilePanel.activeSelf);
        }

        private void RefreshView()
        {
            var bootstrap = AppBootstrap.Instance;
            HideLegacyPromptPanels();
            HideLegacySummaryLabels();
            HideRuntimeGeneratedUi();
            HidePrefabModeArtifacts();
            EnsurePrefabHomeZones();
            DisableLegacyImageArtifacts();

            if (bootstrap == null)
            {
                SetPrimaryButtonsInteractable(false);
                if (deckSummaryLabel != null)
                {
                    deckSummaryLabel.text = "Current Squad";
                    deckSummaryLabel.gameObject.SetActive(true);
                }
                RebuildSceneOwnedSquadChips(Array.Empty<EmojiId>());

                if (profileSummaryLabel != null)
                {
                    profileSummaryLabel.text = "Player Profile\nUnavailable";
                }

                EnsurePrefabHomeZones();
                return;
            }

            bootstrap.ActiveDeckService.EnsureInitialized(bootstrap.SessionState.UserId);
            if (!v2FoundationReady)
            {
                ShowV2SetupBlockedState();
                return;
            }

            SetPrimaryButtonsInteractable(true);
            EnsurePrefabHomeZones();
            var squadForHome = bootstrap.ActiveDeckService.ActiveDeckEmojiIds;
            if (squadForHome == null || squadForHome.Count == 0)
            {
                var pendingSquad = LaunchSelections.GetPendingSquad();
                if (pendingSquad != null && pendingSquad.Count > 0)
                {
                    squadForHome = pendingSquad;
                }
            }

            RefreshSceneOwnedSquadSummary(squadForHome);
            RefreshSceneHeroHeader();
            ApplyHomeSurfaceStyling();
            EnsurePrefabLayoutConsistency();

            if (resumeRankedLabel != null)
            {
                resumeRankedLabel.text = string.Empty;
            }

            if (starterPromptLabel != null)
            {
                starterPromptLabel.text = string.Empty;
            }
        }

        private void EnsurePrefabHomeZones()
        {
            if (ShouldUseRuntimeLayout)
            {
                return;
            }

            EnsureSceneSafeAreaRoot();
            EnsurePrefabZoneHierarchy();
            EnsureSceneSquadStripScaffold();
            EnsureSceneActionStack();
            EnsureSceneBottomNavFallback();
            EnsureProfilePanelFallback();
            EnsurePrefabLayoutConsistency();
            RefreshSceneHeroHeader();
        }

        private void EnsureSceneSafeAreaRoot()
        {
            if (homePanelBackground == null)
            {
                sceneSafeAreaRoot = null;
                return;
            }

            var homeRect = homePanelBackground.rectTransform;
            var safeArea = FindChildByName("SafeAreaRoot", homeRect, allowGlobalFallback: false);
            if (safeArea == null)
            {
                safeArea = new GameObject("SafeAreaRoot", typeof(RectTransform), typeof(Image));
                safeArea.transform.SetParent(homeRect, false);
            }

            sceneSafeAreaRoot = safeArea.GetComponent<RectTransform>();
            if (sceneSafeAreaRoot == null)
            {
                var replacement = new GameObject("SafeAreaRoot", typeof(RectTransform), typeof(Image));
                replacement.transform.SetParent(homeRect, false);
                sceneSafeAreaRoot = replacement.GetComponent<RectTransform>();
                safeArea = replacement;
            }

            sceneSafeAreaRoot.anchorMin = SafeAreaAnchorMin;
            sceneSafeAreaRoot.anchorMax = SafeAreaAnchorMax;
            sceneSafeAreaRoot.offsetMin = Vector2.zero;
            sceneSafeAreaRoot.offsetMax = Vector2.zero;
            sceneSafeAreaRoot.pivot = new Vector2(0.5f, 0.5f);
            sceneSafeAreaRoot.localScale = Vector3.one;

            var image = safeArea.GetComponent<Image>();
            if (image != null)
            {
                image.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        private void EnsurePrefabZoneHierarchy()
        {
            if (homePanelBackground == null)
            {
                return;
            }

            var zoneRoot = sceneSafeAreaRoot != null
                ? sceneSafeAreaRoot
                : homePanelBackground.rectTransform;
            var heroHeaderObject = FindChildByName("HeroHeader", zoneRoot, allowGlobalFallback: false);
            var heroDecorObject = FindChildByName("HeroDecor", zoneRoot, allowGlobalFallback: false);
            var squadStripObject = FindChildByName("SquadStripPanel", zoneRoot, allowGlobalFallback: false);
            var actionStackObject = FindChildByName("ActionStack", zoneRoot, allowGlobalFallback: false);

            if (heroHeaderObject == null)
            {
                heroHeaderObject = new GameObject("HeroHeader", typeof(RectTransform), typeof(Image));
                heroHeaderObject.transform.SetParent(zoneRoot, false);
            }

            if (heroDecorObject == null)
            {
                heroDecorObject = new GameObject("HeroDecor", typeof(RectTransform));
                heroDecorObject.transform.SetParent(zoneRoot, false);
            }

            if (squadStripObject == null)
            {
                squadStripObject = new GameObject("SquadStripPanel", typeof(RectTransform), typeof(Image));
                squadStripObject.transform.SetParent(zoneRoot, false);
            }

            if (actionStackObject == null)
            {
                actionStackObject = new GameObject("ActionStack", typeof(RectTransform), typeof(VerticalLayoutGroup));
                actionStackObject.transform.SetParent(zoneRoot, false);
            }

            var heroHeader = heroHeaderObject.GetComponent<RectTransform>();
            var heroDecor = heroDecorObject.GetComponent<RectTransform>();
            var squadStrip = squadStripObject.GetComponent<RectTransform>();
            var actionStack = actionStackObject.GetComponent<RectTransform>();
            if (heroHeader == null || heroDecor == null || squadStrip == null || actionStack == null)
            {
                return;
            }

            if (heroHeaderObject.GetComponent<Image>() == null)
            {
                heroHeaderObject.AddComponent<Image>();
            }

            if (squadStripObject.GetComponent<Image>() == null)
            {
                squadStripObject.AddComponent<Image>();
            }

            if (actionStackObject.GetComponent<VerticalLayoutGroup>() == null)
            {
                actionStackObject.AddComponent<VerticalLayoutGroup>();
            }

            heroHeader.anchorMin = HeroHeaderAnchorMin;
            heroHeader.anchorMax = HeroHeaderAnchorMax;
            heroHeader.offsetMin = Vector2.zero;
            heroHeader.offsetMax = Vector2.zero;

            heroDecor.anchorMin = HeroDecorAnchorMin;
            heroDecor.anchorMax = HeroDecorAnchorMax;
            heroDecor.offsetMin = Vector2.zero;
            heroDecor.offsetMax = Vector2.zero;

            squadStrip.anchorMin = SquadStripAnchorMin;
            squadStrip.anchorMax = SquadStripAnchorMax;
            squadStrip.offsetMin = Vector2.zero;
            squadStrip.offsetMax = Vector2.zero;

            actionStack.anchorMin = ActionStackAnchorMin;
            actionStack.anchorMax = ActionStackAnchorMax;
            actionStack.offsetMin = Vector2.zero;
            actionStack.offsetMax = Vector2.zero;

            heroHeader.SetSiblingIndex(0);
            heroDecor.SetSiblingIndex(Mathf.Min(1, zoneRoot.childCount - 1));
            squadStrip.SetSiblingIndex(Mathf.Min(2, zoneRoot.childCount - 1));
            actionStack.SetSiblingIndex(Mathf.Min(3, zoneRoot.childCount - 1));

            EnsureHeroHeaderLabels(heroHeader);
            EnsureHeroDecorPrefabOnly(heroDecor);
        }

        private void EnsureHeroHeaderLabels(RectTransform heroHeader)
        {
            if (heroHeader == null)
            {
                return;
            }

            var title = FindTextByObjectName("HeroTitle", heroHeader, allowGlobalFallback: false);
            if (title == null)
            {
                return;
            }

            var titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0f, 0.52f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = new Vector2(8f, 0f);
            titleRect.offsetMax = new Vector2(-8f, -2f);
            title.alignment = TextAnchor.MiddleCenter;
            title.font = profileSummaryLabel != null
                ? profileSummaryLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            title.fontSize = Mathf.Max(UiThemeRuntime.Theme.HeroFontSize + 2, 68);
            title.color = Color.white;
            title.text = "EmojiWar";

            var subtitle = FindTextByObjectName("HeroSubtitle", heroHeader, allowGlobalFallback: false);
            if (subtitle == null)
            {
                return;
            }

            var subtitleRect = subtitle.rectTransform;
            subtitleRect.anchorMin = new Vector2(0f, 0.18f);
            subtitleRect.anchorMax = new Vector2(1f, 0.52f);
            subtitleRect.offsetMin = new Vector2(10f, 2f);
            subtitleRect.offsetMax = new Vector2(-10f, -2f);
            subtitle.alignment = TextAnchor.MiddleCenter;
            subtitle.font = title.font;
            subtitle.fontSize = Mathf.Max(UiThemeRuntime.Theme.BodyFontSize - 4, 18);
            subtitle.color = new Color(0.92f, 0.95f, 1f, 0.92f);
            subtitle.text = "Build a squad. Survive the blind ban. Win the auto-battle.";

            var metadata = FindTextByObjectName("HeroMeta", heroHeader, allowGlobalFallback: false);
            if (metadata != null)
            {
                metadata.gameObject.SetActive(false);
            }
        }

        private void RefreshSceneHeroHeader()
        {
            if (homePanelBackground == null)
            {
                return;
            }

            var heroHeader = FindChildByName("HeroHeader", homePanelBackground.rectTransform, allowGlobalFallback: false)
                ?.GetComponent<RectTransform>();
            if (heroHeader == null)
            {
                return;
            }

            EnsureHeroHeaderLabels(heroHeader);
        }

        private static void EnsureHeroDecorPrefabOnly(RectTransform heroDecor)
        {
            if (heroDecor == null)
            {
                return;
            }

            heroDecor.gameObject.SetActive(true);
            // Prefab-first V2 mode: force deterministic hero deco ownership here.
            for (var index = heroDecor.childCount - 1; index >= 0; index--)
            {
                var child = heroDecor.GetChild(index);
                if (child == null)
                {
                    continue;
                }

                var childName = child.name;
                var keep =
                    childName.StartsWith("V2HeroBlob_", StringComparison.Ordinal) ||
                    childName.StartsWith("V2HeroSticker_", StringComparison.Ordinal);
                if (!keep)
                {
                    Destroy(child.gameObject);
                }
            }

            var panelImage = heroDecor.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0f, 0f, 0f, 0f);
            }

            // Decorative blobs.
            EnsureHeroDecorBlob(heroDecor, "V2HeroBlob_Left", new Vector2(-92f, 6f), new Vector2(96f, 96f), new Color(1f, 0.55f, 0.24f, 0.24f));
            EnsureHeroDecorBlob(heroDecor, "V2HeroBlob_Center", new Vector2(0f, 0f), new Vector2(108f, 108f), new Color(0.56f, 0.36f, 1f, 0.24f));
            EnsureHeroDecorBlob(heroDecor, "V2HeroBlob_Right", new Vector2(94f, 8f), new Vector2(94f, 94f), new Color(0.20f, 0.85f, 1f, 0.24f));

            // Featured emoji stickers.
            EnsureHeroDecorSticker(heroDecor, "V2HeroSticker_Fire", new Vector2(-112f, 6f), 74f, new Color(1f, 0.58f, 0.27f, 0.94f), -7f);
            EnsureHeroDecorSticker(heroDecor, "V2HeroSticker_Lightning", new Vector2(0f, 0f), 82f, new Color(1f, 0.82f, 0.35f, 0.95f), 0f);
            EnsureHeroDecorSticker(heroDecor, "V2HeroSticker_Heart", new Vector2(112f, 6f), 74f, new Color(0.95f, 0.43f, 0.94f, 0.94f), 7f);
        }

        private static void EnsureHeroDecorBlob(
            RectTransform root,
            string nodeName,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            if (root == null)
            {
                return;
            }

            var node = FindChildByName(nodeName, root, allowGlobalFallback: false);
            if (node == null)
            {
                node = new GameObject(nodeName, typeof(RectTransform), typeof(Image), typeof(UiMotionController));
                node.transform.SetParent(root, false);
            }

            var rect = node.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            var image = node.GetComponent<Image>();
            image.sprite = GetOrCreateCircleSprite();
            image.color = color;
            image.raycastTarget = false;

            var motion = node.GetComponent<UiMotionController>();
            motion.Configure(enableIdle: true, enableCtaBreathe: false, enableTilt: true);
        }

        private static void EnsureHeroDecorSticker(
            RectTransform root,
            string nodeName,
            Vector2 position,
            float size,
            Color color,
            float tiltDegrees)
        {
            if (root == null)
            {
                return;
            }

            var node = FindChildByName(nodeName, root, allowGlobalFallback: false);
            if (node == null)
            {
                node = new GameObject(nodeName, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Shadow), typeof(UiMotionController));
                node.transform.SetParent(root, false);
            }

            var rect = node.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = position;
            rect.localRotation = Quaternion.Euler(0f, 0f, tiltDegrees);

            var image = node.GetComponent<Image>();
            image.sprite = GetOrCreateCircleSprite();
            image.color = color;
            image.raycastTarget = false;

            var outline = node.GetComponent<Outline>();
            if (outline == null)
            {
                outline = node.AddComponent<Outline>();
            }

            outline.effectColor = new Color(1f, 1f, 1f, 0.56f);
            outline.effectDistance = new Vector2(1.8f, -1.8f);
            outline.useGraphicAlpha = true;

            var shadow = node.GetComponent<Shadow>();
            if (shadow == null)
            {
                shadow = node.AddComponent<Shadow>();
            }

            shadow.effectColor = new Color(0.08f, 0.08f, 0.16f, 0.48f);
            shadow.effectDistance = new Vector2(0f, -4f);
            shadow.useGraphicAlpha = true;
            EnsureStickerHighlight(rect, size);
            var legacyEmojiLabel = FindChildByName("Emoji", rect, allowGlobalFallback: false);
            if (legacyEmojiLabel != null)
            {
                Destroy(legacyEmojiLabel.gameObject);
            }

            EnsureHeroStickerGlyph(nodeName, rect, size);

            var motion = node.GetComponent<UiMotionController>();
            motion.Configure(enableIdle: true, enableCtaBreathe: false, enableTilt: true);
        }

        private static void EnsureHeroStickerGlyph(string nodeName, RectTransform stickerRect, float size)
        {
            if (stickerRect == null)
            {
                return;
            }

            var glyphNode = FindChildByName("Glyph", stickerRect, allowGlobalFallback: false);
            if (glyphNode == null)
            {
                glyphNode = new GameObject("Glyph", typeof(RectTransform), typeof(Text), typeof(Outline));
                glyphNode.transform.SetParent(stickerRect, false);
            }

            var glyphRect = glyphNode.GetComponent<RectTransform>();
            glyphRect.anchorMin = Vector2.zero;
            glyphRect.anchorMax = Vector2.one;
            glyphRect.offsetMin = Vector2.zero;
            glyphRect.offsetMax = Vector2.zero;

            var glyphLabel = glyphNode.GetComponent<Text>();
            glyphLabel.text = ResolveHeroDecorGlyph(nodeName);
            glyphLabel.alignment = TextAnchor.MiddleCenter;
            glyphLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            glyphLabel.fontSize = Mathf.Max(20, Mathf.RoundToInt(size * 0.52f));
            glyphLabel.color = new Color(1f, 1f, 1f, 0.96f);
            glyphLabel.raycastTarget = false;

            var outline = glyphNode.GetComponent<Outline>();
            outline.effectColor = new Color(0.09f, 0.08f, 0.18f, 0.6f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);
            outline.useGraphicAlpha = true;
        }

        private static string ResolveHeroDecorGlyph(string nodeName)
        {
            if (string.IsNullOrWhiteSpace(nodeName))
            {
                return "✨";
            }

            if (nodeName.IndexOf("Fire", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "🔥";
            }

            if (nodeName.IndexOf("Lightning", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "⚡";
            }

            if (nodeName.IndexOf("Heart", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "❤️";
            }

            return "✨";
        }

        private static void EnsureStickerHighlight(RectTransform stickerRect, float size)
        {
            if (stickerRect == null)
            {
                return;
            }

            var highlight = FindChildByName("Highlight", stickerRect, allowGlobalFallback: false);
            if (highlight == null)
            {
                highlight = new GameObject("Highlight", typeof(RectTransform), typeof(Image));
                highlight.transform.SetParent(stickerRect, false);
            }

            var highlightRect = highlight.GetComponent<RectTransform>();
            highlightRect.anchorMin = new Vector2(0.5f, 0.5f);
            highlightRect.anchorMax = new Vector2(0.5f, 0.5f);
            highlightRect.pivot = new Vector2(0.5f, 0.5f);
            highlightRect.sizeDelta = new Vector2(size * 0.38f, size * 0.38f);
            highlightRect.anchoredPosition = new Vector2(-size * 0.16f, size * 0.16f);

            var highlightImage = highlight.GetComponent<Image>();
            highlightImage.sprite = GetOrCreateCircleSprite();
            highlightImage.color = new Color(1f, 1f, 1f, 0.28f);
            highlightImage.raycastTarget = false;
        }

        private static Sprite GetOrCreateCircleSprite()
        {
            if (cachedCircleSprite != null)
            {
                return cachedCircleSprite;
            }

            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "EmojiWar_V2_CircleSprite",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var center = (size - 1f) * 0.5f;
            var radius = center - 1.5f;
            var feather = 4.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = x - center;
                    var dy = y - center;
                    var distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                    var alpha = Mathf.Clamp01((radius - distance) / feather);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            cachedCircleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size);
            cachedCircleSprite.name = "EmojiWar_V2_CircleSprite";
            return cachedCircleSprite;
        }

        private static void EnsureHeroAccentNode(
            RectTransform clusterRect,
            string name,
            Vector2 position,
            Color tint,
            float size)
        {
            if (clusterRect == null)
            {
                return;
            }

            var existing = FindChildByName(name, clusterRect, allowGlobalFallback: false);
            if (existing == null)
            {
                existing = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(UiMotionController));
                existing.transform.SetParent(clusterRect, false);
            }

            var rect = existing.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = position;

            var image = existing.GetComponent<Image>();
            image.color = tint;

            var motion = existing.GetComponent<UiMotionController>();
            motion.Configure(enableIdle: true, enableCtaBreathe: false, enableTilt: false);
        }

        private void EnsureHeroAccentGlyph(RectTransform clusterRect)
        {
            if (clusterRect == null)
            {
                return;
            }

            var glyphNode = FindChildByName("HeroAccentGlyph", clusterRect, allowGlobalFallback: false);
            if (glyphNode == null)
            {
                glyphNode = new GameObject("HeroAccentGlyph", typeof(RectTransform), typeof(Text), typeof(UiMotionController));
                glyphNode.transform.SetParent(clusterRect, false);
            }

            var glyphRect = glyphNode.GetComponent<RectTransform>();
            glyphRect.anchorMin = new Vector2(0.5f, 0.5f);
            glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
            glyphRect.pivot = new Vector2(0.5f, 0.5f);
            glyphRect.sizeDelta = new Vector2(54f, 54f);
            glyphRect.anchoredPosition = new Vector2(0f, 0f);

            var glyphLabel = glyphNode.GetComponent<Text>();
            glyphLabel.text = "⚡";
            glyphLabel.alignment = TextAnchor.MiddleCenter;
            glyphLabel.color = new Color(1f, 0.78f, 0.35f, 0.94f);
            glyphLabel.font = profileSummaryLabel != null
                ? profileSummaryLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            glyphLabel.fontSize = 42;
            glyphLabel.resizeTextForBestFit = false;

            var glyphMotion = glyphNode.GetComponent<UiMotionController>();
            glyphMotion.Configure(enableIdle: true, enableCtaBreathe: false, enableTilt: false);
        }

        private void EnsureSceneActionStack()
        {
            if (homePanelBackground == null)
            {
                return;
            }

            var zoneRoot = sceneSafeAreaRoot != null
                ? sceneSafeAreaRoot
                : homePanelBackground.rectTransform;
            var stack = FindChildByName("ActionStack", zoneRoot, allowGlobalFallback: false)
                        ?? FindChildByName("ActionStack", FindObjectOfType<Canvas>()?.transform, allowGlobalFallback: false);
            if (stack == null)
            {
                return;
            }

            var stackRect = stack.GetComponent<RectTransform>();
            stackRect.anchorMin = ActionStackAnchorMin;
            stackRect.anchorMax = ActionStackAnchorMax;
            stackRect.offsetMin = Vector2.zero;
            stackRect.offsetMax = Vector2.zero;

            var stackLayout = stack.GetComponent<VerticalLayoutGroup>();
            if (stackLayout == null)
            {
                return;
            }

            stackLayout.spacing = 11f;
            stackLayout.padding = new RectOffset(0, 0, 2, 2);
            stackLayout.childAlignment = TextAnchor.UpperCenter;
            stackLayout.childControlWidth = true;
            stackLayout.childControlHeight = false;
            stackLayout.childForceExpandWidth = true;
            stackLayout.childForceExpandHeight = false;

            var buttons = new[] { rankedButton, practiceButton, codexButton, leaderboardButton, editDeckButton }
                .Where(button => button != null)
                .ToArray();
            foreach (var button in buttons)
            {
                var buttonRect = button.transform as RectTransform;
                if (buttonRect == null)
                {
                    continue;
                }

                var element = button.GetComponent<LayoutElement>();
                if (element == null)
                {
                    continue;
                }

                element.minHeight = 56f;
                element.preferredHeight = 64f;
                element.flexibleHeight = 0f;
            }

            if (stackRect.parent != zoneRoot)
            {
                stackRect.SetParent(zoneRoot, false);
            }
        }

        private void EnsureSceneSquadStripScaffold()
        {
            var zoneRoot = sceneSafeAreaRoot != null
                ? sceneSafeAreaRoot
                : homePanelBackground != null
                    ? homePanelBackground.rectTransform
                    : null;
            if (zoneRoot == null)
            {
                return;
            }

            var stripPanel = FindChildByName("SquadStripPanel", zoneRoot, allowGlobalFallback: false);
            if (stripPanel == null)
            {
                stripPanel = new GameObject("SquadStripPanel", typeof(RectTransform), typeof(Image));
                stripPanel.transform.SetParent(zoneRoot, false);
            }

            var stripRect = stripPanel.GetComponent<RectTransform>();
            if (stripRect == null)
            {
                return;
            }

            if (stripRect.parent != zoneRoot)
            {
                stripRect.SetParent(zoneRoot, false);
            }

            stripRect.anchorMin = SquadStripAnchorMin;
            stripRect.anchorMax = SquadStripAnchorMax;
            stripRect.offsetMin = Vector2.zero;
            stripRect.offsetMax = Vector2.zero;
            stripRect.SetSiblingIndex(Mathf.Min(2, Mathf.Max(0, zoneRoot.childCount - 1)));

            var stripImage = stripPanel.GetComponent<Image>();
            if (stripImage != null)
            {
                stripImage.color = Color.Lerp(UiThemeRuntime.Theme.SecondaryCtaColor, UiThemeRuntime.Theme.SurfaceColor, 0.36f) * new Color(1f, 1f, 1f, 0.96f);
            }

            if (deckSummaryLabel == null)
            {
                deckSummaryLabel = FindTextByObjectName("DeckSummary", zoneRoot, allowGlobalFallback: false);
            }

            if (deckSummaryLabel == null)
            {
                var summaryNode = new GameObject("DeckSummary", typeof(RectTransform), typeof(Text));
                summaryNode.transform.SetParent(stripRect, false);
                deckSummaryLabel = summaryNode.GetComponent<Text>();
            }

            var summaryRect = deckSummaryLabel.rectTransform;
            if (summaryRect.parent != stripRect)
            {
                summaryRect.SetParent(stripRect, false);
            }

            summaryRect.anchorMin = new Vector2(0f, 0.76f);
            summaryRect.anchorMax = new Vector2(1f, 1f);
            summaryRect.offsetMin = new Vector2(12f, -2f);
            summaryRect.offsetMax = new Vector2(-12f, -2f);
            deckSummaryLabel.alignment = TextAnchor.MiddleCenter;
            deckSummaryLabel.fontSize = Mathf.Max(15, UiThemeRuntime.Theme.ChipFontSize + 1);
            deckSummaryLabel.resizeTextForBestFit = false;
            deckSummaryLabel.color = Color.white;
            deckSummaryLabel.font = profileSummaryLabel != null
                ? profileSummaryLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            deckSummaryLabel.text = "Current Squad";
            deckSummaryLabel.gameObject.SetActive(true);

            if (sceneSquadChipsContainer == null)
            {
                sceneSquadChipsContainer = FindChildByName("ActiveSquadChips", stripRect, allowGlobalFallback: false)
                    ?.GetComponent<RectTransform>();
            }

            if (sceneSquadChipsContainer == null)
            {
                var chipsNode = new GameObject("ActiveSquadChips", typeof(RectTransform), typeof(GridLayoutGroup));
                chipsNode.transform.SetParent(stripRect, false);
                sceneSquadChipsContainer = chipsNode.GetComponent<RectTransform>();
            }

            if (sceneSquadChipsContainer.parent != stripRect)
            {
                sceneSquadChipsContainer.SetParent(stripRect, false);
            }

            sceneSquadChipsContainer.anchorMin = new Vector2(0.03f, 0.06f);
            sceneSquadChipsContainer.anchorMax = new Vector2(0.97f, 0.76f);
            sceneSquadChipsContainer.offsetMin = Vector2.zero;
            sceneSquadChipsContainer.offsetMax = Vector2.zero;
            sceneSquadChipsContainer.gameObject.SetActive(true);

            var chipsLayout = sceneSquadChipsContainer.GetComponent<GridLayoutGroup>();
            if (chipsLayout == null)
            {
                chipsLayout = sceneSquadChipsContainer.gameObject.AddComponent<GridLayoutGroup>();
            }

            chipsLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            chipsLayout.constraintCount = 3;
            chipsLayout.spacing = new Vector2(10f, 8f);
            chipsLayout.padding = new RectOffset(8, 8, 4, 4);
            chipsLayout.cellSize = new Vector2(128f, 40f);
            chipsLayout.childAlignment = TextAnchor.MiddleCenter;
            chipsLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            chipsLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            LayoutRebuilder.ForceRebuildLayoutImmediate(stripRect);
            ApplySceneSquadChipGridSizing(chipsLayout);
        }

        private void ApplySceneSquadChipGridSizing(GridLayoutGroup chipsLayout)
        {
            if (chipsLayout == null || sceneSquadChipsContainer == null)
            {
                return;
            }

            var containerWidth = sceneSquadChipsContainer.rect.width;
            if (containerWidth <= 2f)
            {
                var parentRect = sceneSquadChipsContainer.parent as RectTransform;
                if (parentRect != null)
                {
                    containerWidth = parentRect.rect.width * (sceneSquadChipsContainer.anchorMax.x - sceneSquadChipsContainer.anchorMin.x);
                }
            }

            var columns = Mathf.Max(1, chipsLayout.constraintCount);
            var horizontalPadding = chipsLayout.padding.left + chipsLayout.padding.right;
            var horizontalSpacing = chipsLayout.spacing.x * Mathf.Max(0, columns - 1);
            var availableWidth = Mathf.Max(160f, containerWidth - horizontalPadding - horizontalSpacing);
            var cellWidth = Mathf.Clamp(availableWidth / columns, 96f, 220f);
            var cellHeight = Mathf.Clamp(cellWidth * 0.48f, 42f, 62f);
            chipsLayout.cellSize = new Vector2(cellWidth, cellHeight);
        }

        private void EnsureSceneBottomNavFallback()
        {
            if (ShouldUseRuntimeLayout)
            {
                return;
            }

            var canvasRect = FindObjectOfType<Canvas>()?.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            var navParent = sceneSafeAreaRoot != null ? sceneSafeAreaRoot : canvasRect;
            var navRoot = FindChildByName("BottomNav", navParent, allowGlobalFallback: false);
            if (navRoot == null)
            {
                navRoot = FindChildByName("BottomNav", canvasRect, allowGlobalFallback: false);
            }

            if (navRoot == null)
            {
                navRoot = new GameObject("BottomNav", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
                navRoot.transform.SetParent(navParent, false);
            }

            var navRect = navRoot.GetComponent<RectTransform>();
            if (navRect == null)
            {
                sceneBottomNavRoot = null;
                return;
            }

            navRect.anchorMin = new Vector2(0f, 0f);
            navRect.anchorMax = new Vector2(1f, 0f);
            navRect.pivot = new Vector2(0.5f, 0f);
            navRect.sizeDelta = new Vector2(0f, 74f);
            navRect.anchoredPosition = Vector2.zero;
            navRect.SetAsLastSibling();

            var navImage = navRoot.GetComponent<Image>();
            if (navImage != null)
            {
                navImage.color = new Color(0.09f, 0.15f, 0.24f, 0.96f);
            }

            var navLayout = navRoot.GetComponent<HorizontalLayoutGroup>();
            if (navLayout != null)
            {
                navLayout.spacing = 6f;
                navLayout.padding = new RectOffset(8, 8, 8, 8);
                navLayout.childForceExpandWidth = true;
                navLayout.childForceExpandHeight = true;
                navLayout.childAlignment = TextAnchor.MiddleCenter;
            }

            EnsureNavButton(navRoot.transform, "Home", RefreshView);
            EnsureNavButton(navRoot.transform, "Squad", OpenDeckBuilder);
            EnsureNavButton(navRoot.transform, "Codex", OpenCodex);
            EnsureNavButton(navRoot.transform, "Profile", OpenProfilePlaceholder);

            sceneBottomNavRoot = navRoot != null ? navRoot.GetComponent<RectTransform>() : null;
            if (sceneBottomNavRoot != null)
            {
                sceneBottomNavRoot.SetAsLastSibling();
            }
        }

        private static void BindExistingNavButton(Transform navRoot, string label, UnityEngine.Events.UnityAction action)
        {
            if (navRoot == null)
            {
                return;
            }

            var existing = navRoot.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => HasButtonLabel(button, label));
            if (existing == null)
            {
                return;
            }

            existing.onClick.RemoveAllListeners();
            existing.onClick.AddListener(action);
        }

        private void EnsureNavButton(Transform navRoot, string label, UnityEngine.Events.UnityAction action)
        {
            if (navRoot == null)
            {
                return;
            }

            var existing = navRoot.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => HasButtonLabel(button, label));
            if (existing == null)
            {
                var buttonObject = new GameObject($"Nav{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                buttonObject.transform.SetParent(navRoot, false);
                var layout = buttonObject.GetComponent<LayoutElement>();
                layout.minHeight = 48f;
                layout.preferredHeight = 52f;

                var image = buttonObject.GetComponent<Image>();
                image.color = new Color(0.2f, 0.33f, 0.49f, 1f);

                existing = buttonObject.GetComponent<Button>();

                var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                textObject.transform.SetParent(buttonObject.transform, false);
                var textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                var text = textObject.GetComponent<Text>();
                text.text = label;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.fontSize = Mathf.Max(UiThemeRuntime.Theme.ChipFontSize, 14);
                text.font = profileSummaryLabel != null
                    ? profileSummaryLabel.font
                    : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            existing.onClick.RemoveAllListeners();
            existing.onClick.AddListener(action);
        }

        private void EnsureProfilePanelFallback()
        {
            if (ShouldUseRuntimeLayout)
            {
                return;
            }

            var canvasRect = FindObjectOfType<Canvas>()?.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            if (sceneProfilePanel == null)
            {
                sceneProfilePanel =
                    FindChildByName("ProfilePlaceholderPanel", canvasRect, allowGlobalFallback: false) ??
                    FindChildByName("ProfilePanel", canvasRect, allowGlobalFallback: false);
            }

            if (sceneProfilePanel == null)
            {
                sceneProfilePanelText = null;
                return;
            }

            var panelRect = sceneProfilePanel.GetComponent<RectTransform>();
            panelRect.SetAsLastSibling();

            var backdrop = sceneProfilePanel.GetComponent<Image>();
            if (backdrop != null)
            {
                backdrop.color = new Color(0f, 0f, 0f, 0.62f);
            }

            var backdropCanvas = sceneProfilePanel.GetComponent<CanvasGroup>();
            if (backdropCanvas == null)
            {
                sceneProfilePanelText = null;
                return;
            }

            var closeOnBackdrop = sceneProfilePanel.GetComponent<Button>();
            if (closeOnBackdrop != null)
            {
                closeOnBackdrop.transition = Selectable.Transition.None;
                closeOnBackdrop.onClick.RemoveAllListeners();
                closeOnBackdrop.onClick.AddListener(() => AnimateSceneProfilePanel(false));
            }

            var card = FindChildByName("ProfileCard", panelRect, allowGlobalFallback: false);
            if (card == null)
            {
                sceneProfilePanelText = null;
                return;
            }

            var cardRect = card.GetComponent<RectTransform>();
            var cardImage = card.GetComponent<Image>();
            if (cardImage != null)
            {
                cardImage.color = new Color(0.10f, 0.18f, 0.30f, 0.98f);
            }

            var cardButton = card.GetComponent<Button>();
            if (cardButton != null)
            {
                cardButton.transition = Selectable.Transition.None;
                cardButton.onClick.RemoveAllListeners();
                cardButton.onClick.AddListener(() => { });
            }

            var textObject = FindChildByName("ProfileText", cardRect, allowGlobalFallback: false);
            if (textObject == null)
            {
                sceneProfilePanelText = null;
                return;
            }

            sceneProfilePanelText = textObject.GetComponent<Text>();
            var textRect = sceneProfilePanelText.rectTransform;
            sceneProfilePanelText.alignment = TextAnchor.MiddleLeft;
            sceneProfilePanelText.color = Color.white;
            sceneProfilePanelText.fontSize = Mathf.Max(UiThemeRuntime.Theme.BodyFontSize - 3, 15);
            sceneProfilePanelText.font = profileSummaryLabel != null
                ? profileSummaryLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var closeButton = FindChildByName("CloseButton", cardRect, allowGlobalFallback: false);
            if (closeButton == null)
            {
                sceneProfilePanelText = null;
                return;
            }

            var closeRect = closeButton.GetComponent<RectTransform>();
            var closeImage = closeButton.GetComponent<Image>();
            if (closeImage != null)
            {
                closeImage.color = UiThemeRuntime.Theme.SecondaryCtaColor;
            }

            var closeBtn = closeButton.GetComponent<Button>();
            closeBtn.onClick.RemoveAllListeners();
            closeBtn.onClick.AddListener(() => AnimateSceneProfilePanel(false));

            var closeLabelObject = FindChildByName("Label", closeRect, allowGlobalFallback: false);
            if (closeLabelObject == null)
            {
                sceneProfilePanelText = null;
                return;
            }

            var closeLabel = closeLabelObject.GetComponent<Text>();
            var closeLabelRect = closeLabel.rectTransform;
            closeLabelRect.anchorMin = Vector2.zero;
            closeLabelRect.anchorMax = Vector2.one;
            closeLabelRect.offsetMin = Vector2.zero;
            closeLabelRect.offsetMax = Vector2.zero;
            closeLabel.text = "Close";
            closeLabel.alignment = TextAnchor.MiddleCenter;
            closeLabel.color = Color.white;
            closeLabel.fontSize = Mathf.Max(UiThemeRuntime.Theme.BodyFontSize - 1, 16);
            closeLabel.font = sceneProfilePanelText.font;

            sceneProfilePanel.SetActive(false);
            backdropCanvas.alpha = 0f;
            backdropCanvas.blocksRaycasts = false;
            backdropCanvas.interactable = false;
        }

        private void RefreshSceneOwnedSquadSummary(System.Collections.Generic.IReadOnlyList<EmojiId> emojiIds)
        {
            if (sceneSquadChipsContainer == null || deckSummaryLabel == null)
            {
                EnsureSceneSquadStripScaffold();
            }

            if (emojiIds == null || emojiIds.Count == 0)
            {
                if (deckSummaryLabel != null)
                {
                    deckSummaryLabel.text = "Current Squad";
                    deckSummaryLabel.gameObject.SetActive(true);
                }

                RebuildSceneOwnedSquadChips(Array.Empty<EmojiId>());
                return;
            }

            if (deckSummaryLabel != null)
            {
                deckSummaryLabel.text = "Current Squad";
                deckSummaryLabel.alignment = TextAnchor.MiddleCenter;
                deckSummaryLabel.resizeTextForBestFit = false;
                deckSummaryLabel.fontSize = Mathf.Max(17, UiThemeRuntime.Theme.BodyFontSize - 1);
                deckSummaryLabel.gameObject.SetActive(true);
            }

            RebuildSceneOwnedSquadChips(emojiIds);
        }

        private void RebuildSceneOwnedSquadChips(System.Collections.Generic.IReadOnlyList<EmojiId> emojiIds)
        {
            if (sceneSquadChipsContainer == null)
            {
                EnsureSceneSquadStripScaffold();
            }

            if (sceneSquadChipsContainer == null)
            {
                return;
            }

            sceneSquadChipsContainer.gameObject.SetActive(true);
            var chipsLayout = sceneSquadChipsContainer.GetComponent<GridLayoutGroup>();
            ApplySceneSquadChipGridSizing(chipsLayout);

            for (var index = sceneSquadChipsContainer.childCount - 1; index >= 0; index--)
            {
                var child = sceneSquadChipsContainer.GetChild(index);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }

            for (var index = 0; index < sceneSquadChipObjects.Count; index++)
            {
                var existing = sceneSquadChipObjects[index];
                if (existing != null)
                {
                    Destroy(existing);
                }
            }

            sceneSquadChipObjects.Clear();

            if (emojiIds == null || emojiIds.Count == 0)
            {
                CreatePlaceholderChip("No squad yet");
                CreatePlaceholderChip("Tap Edit Squad");
                CreatePlaceholderChip("Pick 6 units");
                LayoutRebuilder.ForceRebuildLayoutImmediate(sceneSquadChipsContainer);
                return;
            }

            var settleDelay = 0f;
            foreach (var emojiId in emojiIds.Take(6))
            {
                var cardObject = new GameObject($"SquadChip_{emojiId}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                cardObject.transform.SetParent(sceneSquadChipsContainer, false);
                var image = cardObject.GetComponent<Image>();
                image.color = Color.Lerp(UiThemeRuntime.ResolveRoleAccent(emojiId), UiThemeRuntime.Theme.CardColors.Default, 0.32f);

                var layout = cardObject.GetComponent<LayoutElement>();
                var preferredWidth = chipsLayout != null ? chipsLayout.cellSize.x : 146f;
                var preferredHeight = chipsLayout != null ? chipsLayout.cellSize.y : 46f;
                layout.preferredHeight = preferredHeight;
                layout.preferredWidth = preferredWidth;
                layout.minHeight = Mathf.Max(32f, preferredHeight - 2f);
                layout.minWidth = Mathf.Max(92f, preferredWidth - 6f);
                layout.flexibleWidth = 0f;

                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(cardObject.transform, false);
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(8f, 4f);
                labelRect.offsetMax = new Vector2(-8f, -4f);

                var label = labelObject.GetComponent<Text>();
                label.font = profileSummaryLabel != null
                    ? profileSummaryLabel.font
                    : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.alignment = TextAnchor.MiddleCenter;
                label.color = Color.white;
                label.fontSize = Mathf.Max(UiThemeRuntime.Theme.ChipFontSize + 1, 16);
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 13;
                label.resizeTextMaxSize = Mathf.Max(UiThemeRuntime.Theme.ChipFontSize + 2, 17);
                label.text = EmojiIdUtility.ToDisplayName(emojiId);

                var motion = cardObject.AddComponent<UiMotionController>();
                motion.Configure(enableIdle: false, enableCtaBreathe: false, enableTilt: false);
                StartCoroutine(PlayChipSettle(motion, settleDelay));
                settleDelay += 0.03f;

                sceneSquadChipObjects.Add(cardObject);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(sceneSquadChipsContainer);
        }

        private void CreatePlaceholderChip(string text)
        {
            if (sceneSquadChipsContainer == null)
            {
                return;
            }

            var cardObject = new GameObject($"SquadChip_{text.Replace(" ", string.Empty)}", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            cardObject.transform.SetParent(sceneSquadChipsContainer, false);
            var image = cardObject.GetComponent<Image>();
            image.color = Color.Lerp(UiThemeRuntime.Theme.SecondaryCtaColor, UiThemeRuntime.Theme.PrimaryCtaColor, 0.22f) * new Color(1f, 1f, 1f, 0.92f);

            var layout = cardObject.GetComponent<LayoutElement>();
            var chipsLayout = sceneSquadChipsContainer.GetComponent<GridLayoutGroup>();
            var preferredWidth = chipsLayout != null ? chipsLayout.cellSize.x : 146f;
            var preferredHeight = chipsLayout != null ? chipsLayout.cellSize.y : 46f;
            layout.preferredHeight = preferredHeight;
            layout.preferredWidth = preferredWidth;
            layout.minHeight = Mathf.Max(32f, preferredHeight - 2f);
            layout.minWidth = Mathf.Max(92f, preferredWidth - 6f);
            layout.flexibleWidth = 0f;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(cardObject.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(8f, 4f);
            labelRect.offsetMax = new Vector2(-8f, -4f);

            var label = labelObject.GetComponent<Text>();
            label.font = profileSummaryLabel != null
                ? profileSummaryLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 1f, 1f, 0.88f);
            label.fontSize = Mathf.Max(UiThemeRuntime.Theme.ChipFontSize + 1, 15);
            label.resizeTextForBestFit = false;
            label.text = text;

            var outline = cardObject.GetComponent<Outline>();
            if (outline == null)
            {
                outline = cardObject.AddComponent<Outline>();
            }

            outline.effectColor = new Color(0.88f, 0.94f, 1f, 0.72f);
            outline.effectDistance = new Vector2(1f, -1f);

            sceneSquadChipObjects.Add(cardObject);
        }

        private IEnumerator PlayChipSettle(UiMotionController motion, float delaySeconds)
        {
            if (motion == null)
            {
                yield break;
            }

            if (delaySeconds > 0f)
            {
                yield return new WaitForSeconds(delaySeconds);
            }

            if (motion != null && motion.isActiveAndEnabled)
            {
                motion.PlayJumpSelect();
            }
        }

        private static string BuildDisplayName(EmojiWar.Client.Core.Session.GuestSessionState sessionState)
        {
            return sessionState == null || string.IsNullOrWhiteSpace(sessionState.DisplayName)
                ? "Player"
                : sessionState.DisplayName;
        }

        private static string BuildUserSuffix(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return "No session";
            }

            var sanitized = userId.Replace("-", string.Empty);
            if (sanitized.Length > 8)
            {
                sanitized = sanitized.Substring(0, 8);
            }

            return $"ID {sanitized}";
        }

        private IEnumerator RefreshRankSummary()
        {
            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null || bootstrap.SupabaseConfig == null || !bootstrap.SupabaseConfig.IsConfigured || !bootstrap.SessionState.HasSession)
            {
                cachedRankSummary = "S1 • Ranked summary unavailable";
                RefreshView();
                yield break;
            }

            var payload = JsonUtility.ToJson(new GetLeaderboardRequestDto
            {
                limit = 5,
            });

            using var request = bootstrap.FunctionClient.BuildJsonRequest("get_leaderboard", payload, bootstrap.SessionState.AccessToken);
            request.timeout = 8;
            if (!TryBeginWebRequest(request, out var operation, out _))
            {
                cachedRankSummary = "S1 • Ranked summary unavailable";
                RefreshView();
                yield break;
            }

            yield return operation;

            if (request.result != UnityWebRequest.Result.Success)
            {
                cachedRankSummary = "S1 • Ranked summary unavailable";
                RefreshView();
                yield break;
            }

            var response = JsonUtility.FromJson<GetLeaderboardResponseDto>(request.downloadHandler.text);
            if (response?.myEntry != null)
            {
                var wins = response.myEntry.wins;
                var losses = response.myEntry.losses;
                cachedRankSummary = $"S1 • #{response.myEntry.rank} • {wins}-{losses}";
            }
            else
            {
                cachedRankSummary = "S1 • Unranked";
            }

            RefreshView();
        }

        private void RewordPrimaryButtons()
        {
            TrySetButtonText(rankedButton, "Play Ranked");
            TrySetButtonText(practiceButton, "Practice");
            TrySetButtonText(codexButton, "Codex");
            TrySetButtonText(leaderboardButton, "Leaderboard");
            TrySetButtonText(editDeckButton, "Edit Squad");
        }

        private void StylePrimaryButtons()
        {
            StyleButton(rankedButton, UiThemeRuntime.Theme.PrimaryCtaColor, true);
            StyleButton(practiceButton, UiThemeRuntime.Theme.SecondaryCtaColor, false);
            StyleButton(codexButton, UiThemeRuntime.Theme.SecondaryCtaColor, false);
            StyleButton(leaderboardButton, UiThemeRuntime.Theme.SecondaryCtaColor, false);
            StyleButton(editDeckButton, UiThemeRuntime.Theme.SecondaryCtaColor, false);
            NormalizePrimaryButtonLabels();
        }

        private void TrySetButtonText(Button button, string text)
        {
            if (button == null)
            {
                return;
            }

            var label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = text;
                label.fontSize = Mathf.Max(UiThemeRuntime.Theme.BodyFontSize + 3, 24);
            }
        }

        private static void StyleButton(Button button, Color tint, bool breathe)
        {
            if (button == null)
            {
                return;
            }

            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = tint;
            }

            var motion = button.GetComponent<UiMotionController>();
            if (breathe)
            {
                if (motion == null)
                {
                    motion = button.gameObject.AddComponent<UiMotionController>();
                }

                motion.Configure(enableIdle: true, enableCtaBreathe: true);
            }
            else if (motion != null)
            {
                motion.Configure(enableIdle: false, enableCtaBreathe: false);
            }
        }

        private void NormalizePrimaryButtonLabels()
        {
            var buttons = new[] { rankedButton, practiceButton, codexButton, leaderboardButton, editDeckButton };
            foreach (var button in buttons)
            {
                NormalizeButtonLabelHierarchy(button);
            }
        }

        private static void NormalizeButtonLabelHierarchy(Button button)
        {
            if (button == null)
            {
                return;
            }

            var labels = button.GetComponentsInChildren<Text>(true);
            if (labels == null || labels.Length <= 1)
            {
                return;
            }

            Text primary = null;
            var bestArea = -1f;
            foreach (var candidate in labels)
            {
                if (candidate == null)
                {
                    continue;
                }

                var rect = candidate.rectTransform.rect;
                var area = Mathf.Abs(rect.width * rect.height);
                if (area > bestArea)
                {
                    bestArea = area;
                    primary = candidate;
                }
            }

            foreach (var label in labels)
            {
                if (label == null)
                {
                    continue;
                }

                if (label == primary)
                {
                    label.gameObject.SetActive(true);
                    label.alignment = TextAnchor.MiddleCenter;
                    label.resizeTextForBestFit = false;
                    continue;
                }

                label.gameObject.SetActive(false);
            }
        }

        private void EnsureRuntimeUi()
        {
            if (runtimeUiInitialized)
            {
                return;
            }

            runtimeUiInitialized = true;
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                return;
            }

            if (!ShouldUseRuntimeLayout)
            {
                return;
            }

            EnsureRuntimeHomePanel(canvas.transform as RectTransform);

            var root = ResolveHomeRoot(canvas);
            runtimeHomeRoot = root;
            if (root == null)
            {
                return;
            }

            if (ShowRuntimeHeroCluster)
            {
                CreateHeroCluster(root);
            }
            CreateRuntimeHeroHeader(root);
            CreateSquadStrip(root);
            CreateRuntimeActionButtons(root);
            CreateBottomNav(canvas.transform as RectTransform);
            CreateProfilePanel(canvas.transform as RectTransform);
        }

        private void EnsureRuntimeHomePanel(RectTransform canvasTransform)
        {
            if (canvasTransform == null)
            {
                return;
            }

            if (homePanelBackground != null &&
                string.Equals(homePanelBackground.gameObject.name, "RuntimeHomePanel", StringComparison.Ordinal))
            {
                return;
            }

            if (homePanelBackground != null)
            {
                // Disable legacy panel ownership so V2 runtime panel is the single source of layout.
                homePanelBackground.gameObject.SetActive(false);
            }

            var existing = canvasTransform.Find("RuntimeHomePanel");
            if (existing != null)
            {
                homePanelBackground = existing.GetComponent<Image>();
                if (homePanelBackground != null)
                {
                    return;
                }
            }

            var panelObject = new GameObject("RuntimeHomePanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasTransform, false);
            panelObject.transform.SetSiblingIndex(0);

            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.04f, 0.045f);
            panelRect.anchorMax = new Vector2(0.96f, 0.94f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            homePanelBackground = panelObject.GetComponent<Image>();
            homePanelBackground.color = UiThemeRuntime.Theme.SurfaceColor;
        }

        private RectTransform ResolveHomeRoot(Canvas canvas)
        {
            if (homePanelBackground != null)
            {
                return homePanelBackground.rectTransform;
            }

            var canvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (canvasRect != null)
            {
                var existing = canvasRect.Find("RuntimeHomePanel") as RectTransform;
                if (existing != null)
                {
                    return existing;
                }
            }

            return canvasRect;
        }

        private void TryPrefillPendingSquad(int requiredCount)
        {
            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null)
            {
                return;
            }

            bootstrap.ActiveDeckService.EnsureInitialized(bootstrap.SessionState.UserId);
            if (!bootstrap.ActiveDeckService.HasActiveDeck)
            {
                LaunchSelections.ClearPendingSquad();
                return;
            }

            var prefill = bootstrap.ActiveDeckService.ActiveDeckEmojiIds
                .Take(Mathf.Clamp(requiredCount, 1, 6))
                .ToList();

            LaunchSelections.SetPendingSquad(prefill);
        }

        private void ApplyHomeSurfaceStyling()
        {
            if (homePanelBackground != null)
            {
                if (UseSlideBackground && UiThemeRuntime.TryGetSlideSprite(HomeSlideIndex, out var slide))
                {
                    homePanelBackground.sprite = slide;
                    homePanelBackground.type = Image.Type.Simple;
                    homePanelBackground.preserveAspect = false;
                    homePanelBackground.color = Color.white;
                    EnsureHomeSurfaceDecor(showDecor: false);
                }
                else
                {
                    homePanelBackground.sprite = null;
                    homePanelBackground.color = Color.Lerp(UiThemeRuntime.Theme.HomeGradient.Top, UiThemeRuntime.Theme.HomeGradient.Bottom, 0.50f);
                    EnsureHomeSurfaceDecor(showDecor: true);
                }
            }

            if (runtimeHeroCluster != null)
            {
                runtimeHeroCluster.SetActive(ShowRuntimeHeroCluster);
            }

            if (!ShouldUseRuntimeLayout)
            {
                RefreshSceneHeroHeader();
            }
        }

        private void EnsureHomeSurfaceDecor(bool showDecor)
        {
            var surfaceRoot = sceneSafeAreaRoot != null
                ? sceneSafeAreaRoot
                : homePanelBackground != null
                    ? homePanelBackground.rectTransform
                    : null;
            if (surfaceRoot == null)
            {
                return;
            }

            var decorNode = FindChildByName("HomeSurfaceDecor", surfaceRoot, allowGlobalFallback: false);
            if (decorNode == null)
            {
                decorNode = new GameObject("HomeSurfaceDecor", typeof(RectTransform));
                decorNode.transform.SetParent(surfaceRoot, false);
            }

            var decorRect = decorNode.GetComponent<RectTransform>();
            decorRect.anchorMin = Vector2.zero;
            decorRect.anchorMax = Vector2.one;
            decorRect.offsetMin = Vector2.zero;
            decorRect.offsetMax = Vector2.zero;
            decorRect.SetSiblingIndex(0);

            EnsureSurfaceBlob(
                decorRect,
                "HomeDecorBlobTopLeft",
                new Vector2(0.08f, 0.92f),
                0.18f,
                UiThemeRuntime.Theme.HomeGradient.Top * new Color(1f, 1f, 1f, 0.22f));
            EnsureSurfaceBlob(
                decorRect,
                "HomeDecorBlobTopRight",
                new Vector2(0.92f, 0.88f),
                0.15f,
                UiThemeRuntime.Theme.PrimaryCtaColor * new Color(1f, 1f, 1f, 0.20f));
            EnsureSurfaceBlob(
                decorRect,
                "HomeDecorBlobBottomLeft",
                new Vector2(0.14f, 0.08f),
                0.22f,
                UiThemeRuntime.Theme.HomeGradient.Bottom * new Color(1f, 1f, 1f, 0.20f));

            decorNode.SetActive(showDecor);
        }

        private static void EnsureSurfaceBlob(
            RectTransform parent,
            string nodeName,
            Vector2 anchorPosition,
            float normalizedSize,
            Color color)
        {
            if (parent == null)
            {
                return;
            }

            var node = FindChildByName(nodeName, parent, allowGlobalFallback: false);
            if (node == null)
            {
                node = new GameObject(nodeName, typeof(RectTransform), typeof(Image));
                node.transform.SetParent(parent, false);
            }

            var rect = node.GetComponent<RectTransform>();
            rect.anchorMin = anchorPosition;
            rect.anchorMax = anchorPosition;
            rect.pivot = new Vector2(0.5f, 0.5f);
            var baseSize = Mathf.Min(parent.rect.width, parent.rect.height);
            var size = Mathf.Max(60f, baseSize * normalizedSize);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;

            var image = node.GetComponent<Image>();
            image.sprite = GetOrCreateCircleSprite();
            image.color = color;
            image.raycastTarget = false;
        }

        private Button FindButtonByLabel(string label)
        {
            var allButtons = FindObjectsOfType<Button>(true);
            foreach (var button in allButtons)
            {
                var text = button.GetComponentInChildren<Text>(true);
                if (text == null)
                {
                    continue;
                }

                if (string.Equals(text.text, label, System.StringComparison.OrdinalIgnoreCase))
                {
                    return button;
                }
            }

            return null;
        }

        private static Button FindButtonByObjectName(string objectName, Transform scopeRoot = null, bool allowGlobalFallback = true)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            var buttonRoot = FindChildByName(objectName, scopeRoot, allowGlobalFallback);
            return buttonRoot != null ? buttonRoot.GetComponent<Button>() : null;
        }

        private static Text FindTextByObjectName(string objectName, Transform scopeRoot = null, bool allowGlobalFallback = true)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            return FindChildByName(objectName, scopeRoot, allowGlobalFallback)?.GetComponent<Text>();
        }

        private static GameObject FindChildByName(string objectName, Transform scopeRoot = null, bool allowGlobalFallback = true)
        {
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

        private void CreateBottomNav(RectTransform canvasTransform)
        {
            if (canvasTransform == null || runtimeBottomNav != null)
            {
                return;
            }

            var existingButtons = FindObjectsOfType<Button>(true);
            var hasSceneBottomNav =
                existingButtons.Any(button => HasButtonLabel(button, "Home")) &&
                existingButtons.Any(button => HasButtonLabel(button, "Squad")) &&
                existingButtons.Any(button => HasButtonLabel(button, "Codex")) &&
                existingButtons.Any(button => HasButtonLabel(button, "Profile"));
            if (hasSceneBottomNav)
            {
                return;
            }

            runtimeBottomNav = new GameObject("RuntimeBottomNav", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            runtimeBottomNav.transform.SetParent(canvasTransform, false);

            var rect = runtimeBottomNav.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 92f);
            rect.anchoredPosition = new Vector2(0f, 0f);

            var background = runtimeBottomNav.GetComponent<Image>();
            background.color = new Color(0.09f, 0.15f, 0.24f, 0.96f);

            var layout = runtimeBottomNav.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            CreateNavButton("Home", () => RefreshView());
            CreateNavButton("Squad", OpenDeckBuilder);
            CreateNavButton("Codex", OpenCodex);
            CreateNavButton("Profile", OpenProfilePlaceholder);
        }

        private static bool HasButtonLabel(Button button, string expectedLabel)
        {
            if (button == null)
            {
                return false;
            }

            var text = button.GetComponentInChildren<Text>(true);
            return text != null &&
                   string.Equals(text.text?.Trim(), expectedLabel, StringComparison.OrdinalIgnoreCase);
        }

        private void CreateNavButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            if (runtimeBottomNav == null)
            {
                return;
            }

            var buttonObject = new GameObject($"Nav{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(runtimeBottomNav.transform, false);

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.2f, 0.33f, 0.49f, 1f);

            var button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(onClick);

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);

            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObject.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = Mathf.Max(UiThemeRuntime.Theme.ChipFontSize + 1, 18);
            text.font = profileSummaryLabel != null ? profileSummaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void CreateProfilePanel(RectTransform canvasTransform)
        {
            if (canvasTransform == null || runtimeProfilePanel != null)
            {
                return;
            }

            runtimeProfilePanel = new GameObject("RuntimeProfilePanel", typeof(RectTransform), typeof(Image));
            runtimeProfilePanel.transform.SetParent(canvasTransform, false);
            runtimeProfilePanel.SetActive(false);

            var rect = runtimeProfilePanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(600f, 360f);

            var panelImage = runtimeProfilePanel.GetComponent<Image>();
            panelImage.color = new Color(0.12f, 0.2f, 0.3f, 0.98f);

            var textObject = new GameObject("ProfileText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(runtimeProfilePanel.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(24f, 24f);
            textRect.offsetMax = new Vector2(-24f, -80f);

            runtimeProfilePanelText = textObject.GetComponent<Text>();
            runtimeProfilePanelText.alignment = TextAnchor.UpperLeft;
            runtimeProfilePanelText.color = Color.white;
            runtimeProfilePanelText.fontSize = 24;
            runtimeProfilePanelText.font = profileSummaryLabel != null ? profileSummaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var closeButton = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
            closeButton.transform.SetParent(runtimeProfilePanel.transform, false);
            var closeRect = closeButton.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 16f);
            closeRect.sizeDelta = new Vector2(260f, 52f);
            closeButton.GetComponent<Image>().color = new Color(0.2f, 0.33f, 0.49f, 1f);
            closeButton.GetComponent<Button>().onClick.AddListener(() => runtimeProfilePanel.SetActive(false));

            var closeTextObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            closeTextObject.transform.SetParent(closeButton.transform, false);
            var closeTextRect = closeTextObject.GetComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero;
            closeTextRect.offsetMax = Vector2.zero;

            var closeText = closeTextObject.GetComponent<Text>();
            closeText.text = "Close";
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.color = Color.white;
            closeText.fontSize = 22;
            closeText.font = runtimeProfilePanelText.font;
        }

        private void RefreshRuntimeProfilePanel()
        {
            if (runtimeProfilePanelText == null)
            {
                return;
            }

            runtimeProfilePanelText.text = BuildProfilePanelCopy();
        }

        private string BuildProfilePanelCopy()
        {
            var bootstrap = AppBootstrap.Instance;
            if (bootstrap == null)
            {
                return "Display Name: Player\nUser: No session\nRanked: Unavailable";
            }

            return
                $"Display Name: {BuildDisplayName(bootstrap.SessionState)}\n" +
                $"User: {BuildUserSuffix(bootstrap.SessionState.UserId)}\n" +
                $"Ranked: {cachedRankSummary}";
        }

        private void AnimateSceneProfilePanel(bool show)
        {
            if (sceneProfilePanel == null)
            {
                return;
            }

            if (sceneProfilePanelAnimationRoutine != null)
            {
                StopCoroutine(sceneProfilePanelAnimationRoutine);
                sceneProfilePanelAnimationRoutine = null;
            }

            sceneProfilePanelAnimationRoutine = StartCoroutine(AnimateSceneProfilePanelRoutine(show));
        }

        private IEnumerator AnimateSceneProfilePanelRoutine(bool show)
        {
            if (sceneProfilePanel == null)
            {
                yield break;
            }

            var canvasGroup = sceneProfilePanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = sceneProfilePanel.AddComponent<CanvasGroup>();
            }

            var card = FindChildByName("ProfileCard", sceneProfilePanel.transform, allowGlobalFallback: false);
            var cardRect = card != null ? card.GetComponent<RectTransform>() : null;
            var startScale = show ? 0.96f : 1f;
            var endScale = show ? 1f : 0.96f;
            var startAlpha = show ? 0f : canvasGroup.alpha;
            var endAlpha = show ? 1f : 0f;

            if (show)
            {
                sceneProfilePanel.SetActive(true);
                canvasGroup.blocksRaycasts = true;
                canvasGroup.interactable = true;
            }

            if (cardRect != null)
            {
                cardRect.localScale = Vector3.one * startScale;
            }

            canvasGroup.alpha = startAlpha;
            var duration = 0.16f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(elapsed / duration);
                var eased = 1f - Mathf.Pow(1f - normalized, 3f);
                canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
                if (cardRect != null)
                {
                    var scale = Mathf.Lerp(startScale, endScale, eased);
                    cardRect.localScale = Vector3.one * scale;
                }

                yield return null;
            }

            canvasGroup.alpha = endAlpha;
            if (cardRect != null)
            {
                cardRect.localScale = Vector3.one * endScale;
            }

            if (!show)
            {
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
                sceneProfilePanel.SetActive(false);
            }

            sceneProfilePanelAnimationRoutine = null;
        }

        private void CreateSquadStrip(RectTransform canvasTransform)
        {
            if (canvasTransform == null || runtimeSquadCardsContainer != null)
            {
                return;
            }

            var stripRoot = new GameObject("RuntimeSquadStrip", typeof(RectTransform), typeof(Image));
            stripRoot.transform.SetParent(canvasTransform, false);
            var rect = stripRoot.GetComponent<RectTransform>();
            runtimeSquadStripRoot = rect;
            rect.anchorMin = new Vector2(0.10f, 0.57f);
            rect.anchorMax = new Vector2(0.90f, 0.67f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = stripRoot.GetComponent<Image>();
            bg.color = UiThemeRuntime.Theme.SecondaryCtaColor * new Color(1f, 1f, 1f, 0.46f);

            var titleObject = new GameObject("Title", typeof(RectTransform), typeof(Text));
            titleObject.transform.SetParent(stripRoot.transform, false);
            var titleRect = titleObject.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 30f);
            titleRect.anchoredPosition = new Vector2(0f, -4f);

            var title = titleObject.GetComponent<Text>();
            title.text = "Current Squad";
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            title.fontSize = Mathf.Max(20, UiThemeRuntime.Theme.ChipFontSize + 4);
            title.font = profileSummaryLabel != null ? profileSummaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var cardsObject = new GameObject("Cards", typeof(RectTransform), typeof(GridLayoutGroup));
            cardsObject.transform.SetParent(stripRoot.transform, false);
            runtimeSquadCardsContainer = cardsObject.GetComponent<RectTransform>();
            runtimeSquadCardsContainer.anchorMin = new Vector2(0f, 0f);
            runtimeSquadCardsContainer.anchorMax = new Vector2(1f, 1f);
            runtimeSquadCardsContainer.offsetMin = new Vector2(10f, 8f);
            runtimeSquadCardsContainer.offsetMax = new Vector2(-10f, -24f);

            var cardLayout = cardsObject.GetComponent<GridLayoutGroup>();
            cardLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            cardLayout.constraintCount = 3;
            cardLayout.spacing = new Vector2(10f, 8f);
            cardLayout.padding = new RectOffset(2, 2, 2, 2);
            cardLayout.cellSize = new Vector2(176f, 44f);
            cardLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            cardLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            cardLayout.childAlignment = TextAnchor.UpperCenter;
        }

        private void CreateRuntimeActionButtons(RectTransform homeRoot)
        {
            if (homeRoot == null || runtimeActionStackRoot != null)
            {
                return;
            }

            var legacyRanked = rankedButton;
            var legacyPractice = practiceButton;
            var legacyCodex = codexButton;
            var legacyLeaderboard = leaderboardButton;
            var legacyEditDeck = editDeckButton;

            var stackObject = new GameObject(
                "RuntimeActionStack",
                typeof(RectTransform),
                typeof(VerticalLayoutGroup));
            stackObject.transform.SetParent(homeRoot, false);
            runtimeActionStackRoot = stackObject.GetComponent<RectTransform>();
            runtimeActionStackRoot.anchorMin = new Vector2(0.10f, 0.22f);
            runtimeActionStackRoot.anchorMax = new Vector2(0.90f, 0.55f);
            runtimeActionStackRoot.offsetMin = Vector2.zero;
            runtimeActionStackRoot.offsetMax = Vector2.zero;

            var layout = stackObject.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            rankedButton = CreateRuntimeActionButton("Play Ranked", OpenBattlePlayers, true);
            practiceButton = CreateRuntimeActionButton("Practice", OpenBattleBot, false);
            codexButton = CreateRuntimeActionButton("Codex", OpenCodex, false);
            leaderboardButton = CreateRuntimeActionButton("Leaderboard", OpenLeaderboard, false);
            editDeckButton = CreateRuntimeActionButton("Edit Squad", OpenDeckBuilder, false);

            DisableLegacyPrimaryButton(legacyRanked);
            DisableLegacyPrimaryButton(legacyPractice);
            DisableLegacyPrimaryButton(legacyCodex);
            DisableLegacyPrimaryButton(legacyLeaderboard);
            DisableLegacyPrimaryButton(legacyEditDeck);
        }

        private Button CreateRuntimeActionButton(string label, UnityEngine.Events.UnityAction onClick, bool isPrimary)
        {
            if (runtimeActionStackRoot == null)
            {
                return null;
            }

            var buttonObject = new GameObject(
                $"Action-{label.Replace(" ", string.Empty)}",
                typeof(RectTransform),
                typeof(LayoutElement),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(runtimeActionStackRoot, false);

            var layout = buttonObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 78f;
            layout.minHeight = 72f;

            var image = buttonObject.GetComponent<Image>();
            image.color = isPrimary ? UiThemeRuntime.Theme.PrimaryCtaColor : UiThemeRuntime.Theme.SecondaryCtaColor;

            var button = buttonObject.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 4f);
            textRect.offsetMax = new Vector2(-8f, -4f);

            var text = textObject.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontSize = Mathf.Max(UiThemeRuntime.Theme.BodyFontSize + 2, 26);
            text.font = profileSummaryLabel != null
                ? profileSummaryLabel.font
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return button;
        }

        private void DisableLegacyPrimaryButton(Button legacyButton)
        {
            if (legacyButton == null)
            {
                return;
            }

            if (runtimeActionStackRoot != null && HasAncestorNamed(legacyButton.transform, runtimeActionStackRoot.name))
            {
                return;
            }

            legacyButton.gameObject.SetActive(false);
        }

        private void CreateRuntimeHeroHeader(RectTransform homeRoot)
        {
            if (homeRoot == null || runtimeHeroHeaderRoot != null)
            {
                return;
            }

            var headerRoot = new GameObject("RuntimeHeroHeader", typeof(RectTransform), typeof(Image));
            headerRoot.transform.SetParent(homeRoot, false);
            runtimeHeroHeaderRoot = headerRoot.GetComponent<RectTransform>();
            runtimeHeroHeaderRoot.anchorMin = new Vector2(0.08f, 0.83f);
            runtimeHeroHeaderRoot.anchorMax = new Vector2(0.92f, 0.94f);
            runtimeHeroHeaderRoot.offsetMin = Vector2.zero;
            runtimeHeroHeaderRoot.offsetMax = Vector2.zero;
            runtimeHeroHeaderRoot.SetAsLastSibling();

            var image = headerRoot.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.04f);

            runtimeHeroTitleLabel = CreateHeaderText(
                headerRoot.transform,
                "Title",
                "EmojiWar",
                UiThemeRuntime.Theme.HeroFontSize,
                new Vector2(0f, 0.50f),
                new Vector2(1f, 1f));

            runtimeHeroSubtitleLabel = CreateHeaderText(
                headerRoot.transform,
                "Subtitle",
                "Build a squad. Survive blind ban. Win the auto-battle.",
                Mathf.Max(18, UiThemeRuntime.Theme.BodyFontSize - 2),
                new Vector2(0f, 0f),
                new Vector2(1f, 0.50f));
        }

        private Text CreateHeaderText(
            Transform parent,
            string name,
            string text,
            int fontSize,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = new Vector2(10f, 4f);
            rect.offsetMax = new Vector2(-10f, -4f);

            var label = textObject.GetComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = profileSummaryLabel != null ? profileSummaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = fontSize;
            label.resizeTextForBestFit = false;
            return label;
        }

        private void CreateHeroCluster(RectTransform canvasTransform)
        {
            if (canvasTransform == null || runtimeHeroCluster != null)
            {
                return;
            }

            runtimeHeroCluster = new GameObject("RuntimeHeroCluster", typeof(RectTransform));
            runtimeHeroCluster.transform.SetParent(canvasTransform, false);

            var rootRect = runtimeHeroCluster.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0.75f);
            rootRect.anchorMax = new Vector2(0.5f, 0.75f);
            rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(360f, 108f);
            rootRect.anchoredPosition = new Vector2(0f, -4f);

            CreateHeroBadge(rootRect, 0, new Vector2(-110f, -4f), false);
            CreateHeroBadge(rootRect, 1, new Vector2(0f, 4f), true);
            CreateHeroBadge(rootRect, 2, new Vector2(110f, -4f), false);
        }

        private void CreateHeroBadge(RectTransform parent, int index, Vector2 anchoredPosition, bool emphasize)
        {
            var badge = new GameObject($"HeroBadge-{index}", typeof(RectTransform), typeof(Image), typeof(UiMotionController));
            badge.transform.SetParent(parent, false);
            var badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = anchoredPosition;
            badgeRect.sizeDelta = emphasize ? new Vector2(92f, 92f) : new Vector2(76f, 76f);

            var image = badge.GetComponent<Image>();
            image.color = new Color(0.20f, 0.33f, 0.50f, 0.95f);
            runtimeHeroBadgeImages[Mathf.Clamp(index, 0, runtimeHeroBadgeImages.Length - 1)] = image;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(badge.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            var label = labelObject.GetComponent<Text>();
            label.text = string.Empty;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = profileSummaryLabel != null ? profileSummaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = emphasize ? 46 : 38;
            label.resizeTextForBestFit = false;
            runtimeHeroBadgeLabels[Mathf.Clamp(index, 0, runtimeHeroBadgeLabels.Length - 1)] = label;

            var motion = badge.GetComponent<UiMotionController>();
            motion.Configure(enableIdle: true, enableCtaBreathe: false, enableTilt: true);
        }

        private void RefreshRuntimeHeroCluster(System.Collections.Generic.IReadOnlyList<EmojiId> emojiIds)
        {
            if (runtimeHeroCluster == null)
            {
                return;
            }

            runtimeHeroCluster.SetActive(ShowRuntimeHeroCluster);

            var picks = emojiIds == null || emojiIds.Count == 0
                ? EmojiIdUtility.LaunchRoster.Take(3).ToArray()
                : emojiIds.Take(3).ToArray();

            for (var index = 0; index < runtimeHeroBadgeLabels.Length; index++)
            {
                var label = runtimeHeroBadgeLabels[index];
                var image = runtimeHeroBadgeImages[index];
                if (label == null || image == null)
                {
                    continue;
                }

                var emojiId = index < picks.Length ? picks[index] : EmojiId.Fire;
                label.text = BuildHeroBadgeGlyph(emojiId);
                label.color = Color.white;
                image.color = UiThemeRuntime.ResolveRoleAccent(emojiId) * new Color(1f, 1f, 1f, 0.86f);
            }
        }

        private static string BuildHeroBadgeGlyph(EmojiId emojiId)
        {
            var name = EmojiIdUtility.ToDisplayName(emojiId);
            if (string.IsNullOrWhiteSpace(name))
            {
                return "?";
            }

            var pieces = name
                .Split(new[] { ' ', '-', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(token => token.Length > 0)
                .Take(2)
                .Select(token => char.ToUpperInvariant(token[0]).ToString())
                .ToArray();
            if (pieces.Length > 0)
            {
                return string.Join(string.Empty, pieces);
            }

            var first = name[0];
            return char.IsLetterOrDigit(first) ? char.ToUpperInvariant(first).ToString() : "?";
        }

        private void RefreshRuntimeSquadStrip(System.Collections.Generic.IReadOnlyList<EmojiId> emojiIds)
        {
            if (runtimeSquadCardsContainer == null)
            {
                return;
            }

            for (var index = runtimeSquadCardsContainer.childCount - 1; index >= 0; index--)
            {
                Destroy(runtimeSquadCardsContainer.GetChild(index).gameObject);
            }

            if (emojiIds == null || emojiIds.Count == 0)
            {
                if (runtimeSquadStripRoot != null)
                {
                    runtimeSquadStripRoot.gameObject.SetActive(false);
                }
                return;
            }

            if (runtimeSquadStripRoot != null)
            {
                runtimeSquadStripRoot.gameObject.SetActive(true);
            }

            foreach (var emojiId in emojiIds)
            {
                var roleColor = UiThemeRuntime.ResolveRoleAccent(emojiId);
                CreateRuntimeSquadCard(
                    BuildSquadChipLabel(emojiId),
                    roleColor * new Color(1f, 1f, 1f, 0.65f));
            }
        }

        private static string BuildSquadChipLabel(EmojiId emojiId)
        {
            var name = EmojiIdUtility.ToDisplayName(emojiId);
            if (string.IsNullOrWhiteSpace(name))
            {
                return "?";
            }

            return name.Length <= 9 ? name : name.Substring(0, 9);
        }

        private void CreateRuntimeSquadCard(string text, Color cardColor)
        {
            if (runtimeSquadCardsContainer == null)
            {
                return;
            }

            var card = new GameObject("SquadCard", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            card.transform.SetParent(runtimeSquadCardsContainer, false);
            var cardRect = card.GetComponent<RectTransform>();
            cardRect.sizeDelta = new Vector2(144f, 32f);

            var layout = card.GetComponent<LayoutElement>();
            layout.preferredWidth = 144f;
            layout.preferredHeight = 32f;
            layout.minWidth = 130f;

            var image = card.GetComponent<Image>();
            image.color = cardColor;

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(card.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 4f);
            labelRect.offsetMax = new Vector2(-6f, -4f);

            var label = labelObject.GetComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.font = profileSummaryLabel != null ? profileSummaryLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = Mathf.Max(UiThemeRuntime.Theme.ChipFontSize + 1, 16);
            label.color = Color.white;
        }

        private void RebuildHomeLayout()
        {
            var panelRect = runtimeHomeRoot != null
                ? runtimeHomeRoot
                : homePanelBackground != null
                    ? homePanelBackground.rectTransform
                    : null;
            if (panelRect == null)
            {
                return;
            }

            NormalizeDeckSummary(panelRect);
            NormalizeSquadStrip(panelRect);
            NormalizeButtonStack(panelRect);
            NormalizeRuntimeHeroHeader(panelRect);
            if (runtimeSquadCardsContainer != null)
            {
                runtimeSquadCardsContainer.gameObject.SetActive(true);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

            if (!v2FoundationReady && deckSummaryLabel != null)
            {
                deckSummaryLabel.text = "V2 setup incomplete.\n" +
                                        "Run: EmojiWar > V2 > Create Default Theme Assets";
            }

            RefreshRuntimeHeroHeader();
        }

        private void HideLegacyPromptPanels()
        {
            // V2 uses the hero/status layout instead of stacked legacy prompt panels.
            if (resumeRankedRoot != null)
            {
                resumeRankedRoot.SetActive(false);
            }
            else if (ShouldUseRuntimeLayout)
            {
                var fallbackResumeRoot = GameObject.Find("Resume Ranked MatchButton");
                if (fallbackResumeRoot != null)
                {
                    fallbackResumeRoot.SetActive(false);
                }
            }

            if (starterPromptRoot != null)
            {
                starterPromptRoot.SetActive(false);
            }
            else if (ShouldUseRuntimeLayout)
            {
                var fallbackStarterRoot = GameObject.Find("StarterPromptPanel");
                if (fallbackStarterRoot != null)
                {
                    fallbackStarterRoot.SetActive(false);
                }
            }
        }

        private void HideLegacySummaryLabels()
        {
            if (profileSummaryLabel != null)
            {
                profileSummaryLabel.gameObject.SetActive(false);
            }

            if (resumeRankedLabel != null)
            {
                resumeRankedLabel.gameObject.SetActive(false);
            }

            if (starterPromptLabel != null)
            {
                starterPromptLabel.gameObject.SetActive(false);
            }
        }

        private void HideRuntimeGeneratedUi()
        {
            if (runtimeBottomNav != null)
            {
                runtimeBottomNav.SetActive(false);
            }

            if (runtimeProfilePanel != null)
            {
                runtimeProfilePanel.SetActive(false);
            }

            if (runtimeSquadStripRoot != null)
            {
                runtimeSquadStripRoot.gameObject.SetActive(false);
            }

            if (runtimeActionStackRoot != null)
            {
                runtimeActionStackRoot.gameObject.SetActive(false);
            }

            if (runtimeHeroCluster != null)
            {
                runtimeHeroCluster.SetActive(false);
            }

            if (runtimeHeroHeaderRoot != null)
            {
                runtimeHeroHeaderRoot.gameObject.SetActive(false);
            }

            DisableRuntimeRootByName("RuntimeBottomNav");
            DisableRuntimeRootByName("RuntimeProfilePanel");
            DisableRuntimeRootByName("RuntimeSquadStrip");
            DisableRuntimeRootByName("RuntimeActionStack");
            DisableRuntimeRootByName("RuntimeHeroCluster");
            DisableRuntimeRootByName("RuntimeHeroHeader");
        }

        private static void DisableRuntimeRootByName(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
            {
                return;
            }

            var root = GameObject.Find(objectName);
            if (root != null)
            {
                root.SetActive(false);
            }
        }

        private void HidePrefabModeArtifacts()
        {
            DisableRuntimeRootByName("RuntimeBottomNav");
            DisableRuntimeRootByName("RuntimeProfilePanel");
            DisableRuntimeRootByName("RuntimeSquadStrip");
            DisableRuntimeRootByName("RuntimeActionStack");
            DisableRuntimeRootByName("RuntimeHeroCluster");
            DisableRuntimeRootByName("RuntimeHeroHeader");
        }

        private void EnsurePrefabLayoutConsistency()
        {
            if (ShouldUseRuntimeLayout || homePanelBackground == null)
            {
                return;
            }

            if (deckSummaryLabel != null)
            {
                deckSummaryLabel.alignment = TextAnchor.MiddleCenter;
                deckSummaryLabel.resizeTextForBestFit = false;
                deckSummaryLabel.fontSize = Mathf.Max(15, UiThemeRuntime.Theme.ChipFontSize + 1);
                deckSummaryLabel.gameObject.SetActive(true);
            }

            if (sceneSquadChipsContainer != null)
            {
                sceneSquadChipsContainer.gameObject.SetActive(true);
            }

            if (sceneBottomNavRoot != null)
            {
                sceneBottomNavRoot.SetAsLastSibling();
            }

            if (sceneProfilePanel != null)
            {
                sceneProfilePanel.transform.SetAsLastSibling();
            }

            // Keep scene-authored Home panel sizing; avoid forcing a second normalization pass.
        }

        private void HideLegacyOverlayText()
        {
            var labels = FindObjectsOfType<Text>(true);
            foreach (var label in labels)
            {
                if (label == null)
                {
                    continue;
                }

                if (label == profileSummaryLabel ||
                    label == deckSummaryLabel ||
                    label == resumeRankedLabel ||
                    label == starterPromptLabel)
                {
                    continue;
                }

                if (label.GetComponentInParent<Button>(true) != null)
                {
                    continue;
                }

                if (HasAncestorNamed(label.transform, "StickerPopArenaOverlay") ||
                    HasAncestorNamed(label.transform, "RuntimeSquadStrip") ||
                    HasAncestorNamed(label.transform, "RuntimeBottomNav") ||
                    HasAncestorNamed(label.transform, "RuntimeProfilePanel") ||
                    HasAncestorNamed(label.transform, "RuntimeHeroHeader"))
                {
                    continue;
                }

                var lowerName = label.name.ToLowerInvariant();
                var lowerText = (label.text ?? string.Empty).ToLowerInvariant();
                var isCoreHeroText =
                    lowerText == "emojiwar" ||
                    lowerText.Contains("build a squad") ||
                    lowerText.Contains("survive the blind ban") ||
                    lowerText.Contains("win the auto-battle");
                if (isCoreHeroText)
                {
                    continue;
                }
                if (lowerName.Contains("profile") ||
                    lowerName.Contains("decksummary") ||
                    lowerName.Contains("starterprompt") ||
                    lowerName.Contains("resumeranked") ||
                    lowerName.Contains("currentsquad") ||
                    lowerName.Contains("activedeck") ||
                    lowerName.Contains("ranksummary") ||
                    lowerName.Contains("session") ||
                    lowerText.Contains("current squad") ||
                    lowerText.Contains("active squad") ||
                    lowerText.Contains("ranked summary") ||
                    lowerText.Contains("currentsquad") ||
                    lowerText.Contains("current squad") ||
                    lowerText.Contains("player-") ||
                    lowerText.Contains("id "))
                {
                    label.gameObject.SetActive(false);
                    continue;
                }

                // Home V2 owns hero + squad strip + button labels only.
                label.gameObject.SetActive(false);
            }
        }

        private void DisableLegacySquadStripRoots()
        {
            var rects = FindObjectsOfType<RectTransform>(true);
            foreach (var rect in rects)
            {
                if (rect == null || rect.gameObject == null)
                {
                    continue;
                }

                if (HasAncestorNamed(rect, "StickerPopArenaOverlay") ||
                    HasAncestorNamed(rect, "RuntimeSquadStrip") ||
                    HasAncestorNamed(rect, "RuntimeBottomNav") ||
                    HasAncestorNamed(rect, "RuntimeProfilePanel"))
                {
                    continue;
                }

                var lowerName = rect.name.ToLowerInvariant();
                if (lowerName.Contains("currentsquad") ||
                    lowerName.Contains("activesquad") ||
                    lowerName.Contains("decksummary") ||
                    lowerName.Contains("profilesummary") ||
                    lowerName.Contains("starterprompt") ||
                    lowerName.Contains("resumeranked"))
                {
                    rect.gameObject.SetActive(false);
                }
            }
        }

        private void DisableLegacyImageArtifacts()
        {
            var images = FindObjectsOfType<Image>(true);
            foreach (var image in images)
            {
                if (image == null || image.gameObject == null)
                {
                    continue;
                }

                if (image == homePanelBackground)
                {
                    continue;
                }

                if (image.GetComponentInParent<Button>(true) != null)
                {
                    continue;
                }

                if (HasAncestorNamed(image.transform, "StickerPopArenaOverlay") ||
                    HasAncestorNamed(image.transform, "RuntimeSquadStrip") ||
                    HasAncestorNamed(image.transform, "RuntimeBottomNav") ||
                    HasAncestorNamed(image.transform, "RuntimeProfilePanel") ||
                    HasAncestorNamed(image.transform, "RuntimeHeroHeader") ||
                    HasAncestorNamed(image.transform, "RuntimeHeroCluster"))
                {
                    continue;
                }

                var lowerName = image.name.ToLowerInvariant();
                if ((lowerName.Contains("active") && lowerName.Contains("squad")) ||
                    (lowerName.Contains("current") && lowerName.Contains("squad")) ||
                    lowerName.Contains("decksummary") ||
                    lowerName.Contains("profilesummary") ||
                    lowerName.Contains("runtimehero") ||
                    lowerName.Contains("heroaccent") ||
                    lowerName.Contains("herobadge"))
                {
                    image.gameObject.SetActive(false);
                }
            }
        }

        private void DisableLegacyActionPanels()
        {
            var images = FindObjectsOfType<Image>(true);
            foreach (var image in images)
            {
                if (image == null || image.gameObject == null)
                {
                    continue;
                }

                if (image == homePanelBackground)
                {
                    continue;
                }

                if (HasAncestorNamed(image.transform, "StickerPopArenaOverlay") ||
                    HasAncestorNamed(image.transform, "RuntimeActionStack") ||
                    HasAncestorNamed(image.transform, "RuntimeSquadStrip") ||
                    HasAncestorNamed(image.transform, "RuntimeBottomNav") ||
                    HasAncestorNamed(image.transform, "RuntimeProfilePanel") ||
                    HasAncestorNamed(image.transform, "RuntimeHeroHeader") ||
                    HasAncestorNamed(image.transform, "RuntimeHeroCluster"))
                {
                    continue;
                }

                var texts = image.GetComponentsInChildren<Text>(true);
                if (texts == null || texts.Length == 0)
                {
                    continue;
                }

                var containsPrimaryActionLabel = texts.Any(label =>
                {
                    var value = (label.text ?? string.Empty).Trim();
                    return string.Equals(value, "Play Ranked", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "Practice", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "Codex", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "Leaderboard", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "Edit Squad", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "Edit Deck", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "Battle Players", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(value, "Battle Bot", StringComparison.OrdinalIgnoreCase);
                });

                if (containsPrimaryActionLabel)
                {
                    image.gameObject.SetActive(false);
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

        private void NormalizeDeckSummary(RectTransform homeRect)
        {
            if (deckSummaryLabel == null || homeRect == null)
            {
                return;
            }

            var rect = deckSummaryLabel.rectTransform;
            rect.SetParent(homeRect, true);
            rect.anchorMin = new Vector2(0.18f, 0.63f);
            rect.anchorMax = new Vector2(0.82f, 0.66f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void NormalizeSceneOwnedSquadStrip(RectTransform homeRect)
        {
            if (sceneSquadChipsContainer == null || homeRect == null)
            {
                return;
            }

            sceneSquadChipsContainer.SetParent(homeRect, true);
            sceneSquadChipsContainer.anchorMin = new Vector2(0.14f, 0.56f);
            sceneSquadChipsContainer.anchorMax = new Vector2(0.86f, 0.62f);
            sceneSquadChipsContainer.offsetMin = Vector2.zero;
            sceneSquadChipsContainer.offsetMax = Vector2.zero;

            var layout = sceneSquadChipsContainer.GetComponent<GridLayoutGroup>();
            if (layout == null)
            {
                layout = sceneSquadChipsContainer.gameObject.AddComponent<GridLayoutGroup>();
            }

            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.spacing = new Vector2(4f, 4f);
            layout.padding = new RectOffset(4, 4, 4, 4);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.cellSize = new Vector2(74f, 24f);
        }

        private void NormalizeSquadStrip(RectTransform homeRect)
        {
            if (runtimeSquadStripRoot == null || homeRect == null)
            {
                return;
            }

            runtimeSquadStripRoot.SetParent(homeRect, true);
                runtimeSquadStripRoot.anchorMin = new Vector2(0.10f, 0.57f);
                runtimeSquadStripRoot.anchorMax = new Vector2(0.90f, 0.67f);
            runtimeSquadStripRoot.offsetMin = Vector2.zero;
            runtimeSquadStripRoot.offsetMax = Vector2.zero;
        }

        private void NormalizeButtonStack(RectTransform homeRect)
        {
            if (homeRect == null)
            {
                return;
            }

            if (runtimeActionStackRoot != null)
            {
                runtimeActionStackRoot.SetParent(homeRect, true);
                runtimeActionStackRoot.anchorMin = new Vector2(0.10f, 0.22f);
                runtimeActionStackRoot.anchorMax = new Vector2(0.90f, 0.55f);
                runtimeActionStackRoot.offsetMin = Vector2.zero;
                runtimeActionStackRoot.offsetMax = Vector2.zero;
                return;
            }

            var orderedButtons = new[] { rankedButton, practiceButton, codexButton, leaderboardButton, editDeckButton }
                .Where(button => button != null)
                .ToArray();
            if (orderedButtons.Length == 0)
            {
                return;
            }

            const float topStart = 0.58f;
            const float spacing = 0.086f;
            for (var index = 0; index < orderedButtons.Length; index++)
            {
                var button = orderedButtons[index];
                var rect = button.transform as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                rect.SetParent(homeRect, true);
                rect.anchorMin = new Vector2(0.12f, topStart - (index + 1) * spacing);
                rect.anchorMax = new Vector2(0.88f, topStart - index * spacing);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                NormalizeButtonLabelHierarchy(button);
            }
        }

        private void NormalizeRuntimeHeroHeader(RectTransform homeRect)
        {
            if (homeRect == null)
            {
                return;
            }

            if (runtimeHeroHeaderRoot != null)
            {
                runtimeHeroHeaderRoot.SetParent(homeRect, true);
                runtimeHeroHeaderRoot.anchorMin = new Vector2(0.08f, 0.83f);
                runtimeHeroHeaderRoot.anchorMax = new Vector2(0.92f, 0.95f);
                runtimeHeroHeaderRoot.offsetMin = Vector2.zero;
                runtimeHeroHeaderRoot.offsetMax = Vector2.zero;
                runtimeHeroHeaderRoot.gameObject.SetActive(true);
                runtimeHeroHeaderRoot.SetAsLastSibling();
            }
        }

        private void RefreshRuntimeHeroHeader()
        {
            if (runtimeHeroHeaderRoot != null)
            {
                runtimeHeroHeaderRoot.gameObject.SetActive(true);
            }

            if (runtimeHeroTitleLabel != null)
            {
                runtimeHeroTitleLabel.text = "EmojiWar";
                runtimeHeroTitleLabel.fontSize = Mathf.Max(UiThemeRuntime.Theme.HeroFontSize + 2, 58);
                runtimeHeroTitleLabel.gameObject.SetActive(true);
            }

            if (runtimeHeroSubtitleLabel != null)
            {
                runtimeHeroSubtitleLabel.text = "Build a squad. Survive blind ban. Win the auto-battle.";
                runtimeHeroSubtitleLabel.fontSize = Mathf.Max(UiThemeRuntime.Theme.BodyFontSize - 1, 20);
                runtimeHeroSubtitleLabel.color = new Color(0.92f, 0.95f, 1f, 0.92f);
                runtimeHeroSubtitleLabel.gameObject.SetActive(true);
            }
        }

        private void ShowV2SetupBlockedState()
        {
            SetPrimaryButtonsInteractable(false);
            if (runtimeSquadCardsContainer != null)
            {
                runtimeSquadCardsContainer.gameObject.SetActive(false);
            }
            if (deckSummaryLabel != null)
            {
                var detail = string.IsNullOrWhiteSpace(v2FoundationMessage)
                    ? string.Empty
                    : $"\n\n{v2FoundationMessage}";
                deckSummaryLabel.text = "V2 setup incomplete.\n" +
                                        "Run: EmojiWar > V2 > Create Default Theme Assets\n" +
                                        "Then: EmojiWar > V2 > Import Sticker Pop Arena PPT Assets" +
                                        detail;
                deckSummaryLabel.alignment = TextAnchor.MiddleCenter;
                deckSummaryLabel.fontSize = Mathf.Max(14, UiThemeRuntime.Theme.BodyFontSize - 2);
                deckSummaryLabel.gameObject.SetActive(true);
            }
        }

        private void DisableDuplicatePrimaryButtons()
        {
            var allButtons = FindObjectsOfType<Button>(true);
            HideDuplicateButtonCopies(allButtons, rankedButton, "Play Ranked", "Battle Players");
            HideDuplicateButtonCopies(allButtons, practiceButton, "Practice", "Battle Bot");
            HideDuplicateButtonCopies(allButtons, codexButton, "Codex");
            HideDuplicateButtonCopies(allButtons, leaderboardButton, "Leaderboard");
            HideDuplicateButtonCopies(allButtons, editDeckButton, "Edit Squad", "Edit Deck");
        }

        private static void HideDuplicateButtonCopies(Button[] allButtons, Button activeButton, params string[] labels)
        {
            if (allButtons == null || labels == null || labels.Length == 0)
            {
                return;
            }

            foreach (var button in allButtons)
            {
                if (button == null || button == activeButton)
                {
                    continue;
                }

                var label = button.GetComponentInChildren<Text>(true);
                if (label == null)
                {
                    continue;
                }

                var matches = labels.Any(candidate =>
                    string.Equals(label.text?.Trim(), candidate, StringComparison.OrdinalIgnoreCase));
                if (!matches)
                {
                    continue;
                }

                if (button.transform.parent == null)
                {
                    continue;
                }

                if (button.transform.parent.name.IndexOf("Nav", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                button.gameObject.SetActive(false);
            }
        }

        private void SetPrimaryButtonsInteractable(bool isInteractable)
        {
            var buttons = new[] { rankedButton, practiceButton, codexButton, leaderboardButton, editDeckButton };
            foreach (var button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                button.interactable = isInteractable;
                var image = button.GetComponent<Image>();
                if (image != null)
                {
                    var baseColor = button == rankedButton
                        ? UiThemeRuntime.Theme.PrimaryCtaColor
                        : UiThemeRuntime.Theme.SecondaryCtaColor;
                    image.color = baseColor * new Color(1f, 1f, 1f, isInteractable ? 1f : 0.55f);
                }
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
            catch
            {
                return false;
            }
        }
    }
}
