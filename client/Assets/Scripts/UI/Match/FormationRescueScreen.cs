using System;
using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Content;
using EmojiWar.Client.Core;
using EmojiWar.Client.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Match
{
    /// <summary>
    /// Formation-only rescue screen. The hierarchy is built once and refreshed in place
    /// so slot selection, tray updates, and polling changes do not recreate the screen.
    /// </summary>
    public sealed class FormationRescueScreen : MonoBehaviour
    {
        private static readonly string[] SlotIds =
        {
            "front_left",
            "front_center",
            "front_right",
            "back_left",
            "back_right"
        };

        private static readonly string[] SlotTitles =
        {
            "Front Left",
            "Front Center",
            "Front Right",
            "Back Left",
            "Back Right"
        };

        private static readonly string[] SlotRowLabels =
        {
            "FRONT",
            "FRONT",
            "FRONT",
            "BACK",
            "BACK"
        };

        private static readonly Vector2 TrayFighterSize = new(90f, 94f);
        private static readonly Vector2 SlotStickerSize = new(118f, 104f);

        private readonly List<RectTransform> enterTargets = new();
        private readonly List<FighterView> fighterViews = new();
        private readonly List<SlotView> slotViews = new();

        private RectTransform root;
        private RectTransform contentRoot;
        private RectTransform fighterTrayRoot;
        private HorizontalLayoutGroup fighterTrayLayout;

        private TMP_Text stepChipLabel;
        private TMP_Text progressChipLabel;
        private TMP_Text titleLabel;
        private TMP_Text subtitleLabel;
        private TMP_Text opponentProgressLabel;
        private TMP_Text lockButtonLabel;
        private TMP_Text resetButtonLabel;

        private Button lockButton;
        private Button resetButton;

        private bool introPlayed;
        private bool lockPulseActive;
        private string fighterSignature = string.Empty;

        private IReadOnlyList<UnitView> availableUnits = Array.Empty<UnitView>();
        private string[] recommendedAssignments = EmptyAssignments();
        private string[] slotAssignments = EmptyAssignments();
        private string selectedUnitId = string.Empty;
        private int selectedSlotIndex = -1;
        private string yourBanResultId = string.Empty;
        private string opponentBanResultId = string.Empty;
        private string blockedUnitId = string.Empty;
        private bool isLocked;
        private int opponentPlacedCount;
        private ScreenConfig screenConfig = ScreenConfig.RankedDefault;
        private Action<string[]> onFormationChanged;
        private Action<string[]> onLockFormation;

        public readonly struct ScreenConfig
        {
            public ScreenConfig(
                string stepText,
                string titleText,
                string idleSubtitle,
                string lockedSubtitle,
                string opponentWaitingText,
                string opponentLockedText,
                bool showBanResults,
                string yourBanFallbackText,
                string opponentBanFallbackText)
            {
                StepText = string.IsNullOrWhiteSpace(stepText) ? "STEP 3 OF 4" : stepText;
                TitleText = string.IsNullOrWhiteSpace(titleText) ? "Set Formation" : titleText;
                IdleSubtitle = string.IsNullOrWhiteSpace(idleSubtitle) ? "Tap a fighter, then tap a slot." : idleSubtitle;
                LockedSubtitle = string.IsNullOrWhiteSpace(lockedSubtitle) ? "Formation locked. Waiting for opponent..." : lockedSubtitle;
                OpponentWaitingText = string.IsNullOrWhiteSpace(opponentWaitingText) ? "Opponent placing..." : opponentWaitingText;
                OpponentLockedText = string.IsNullOrWhiteSpace(opponentLockedText) ? "Opponent locked" : opponentLockedText;
                ShowBanResults = showBanResults;
                YourBanFallbackText = string.IsNullOrWhiteSpace(yourBanFallbackText) ? "Waiting for ban result..." : yourBanFallbackText;
                OpponentBanFallbackText = string.IsNullOrWhiteSpace(opponentBanFallbackText) ? "Waiting for ban result..." : opponentBanFallbackText;
            }

            public string StepText { get; }
            public string TitleText { get; }
            public string IdleSubtitle { get; }
            public string LockedSubtitle { get; }
            public string OpponentWaitingText { get; }
            public string OpponentLockedText { get; }
            public bool ShowBanResults { get; }
            public string YourBanFallbackText { get; }
            public string OpponentBanFallbackText { get; }

            public static ScreenConfig RankedDefault => new(
                "STEP 3 OF 4",
                "Set Formation",
                "Recommended formation is ready. Tap a fighter, then tap a slot.",
                "Formation locked. Waiting for opponent...",
                "Opponent placing...",
                "Opponent locked",
                true,
                "Waiting for ban result...",
                "Waiting for ban result...");

            public static ScreenConfig BotDefault => new(
                "STEP 2 OF 3",
                "Set Formation",
                "Recommended formation is ready. Tap a fighter, then tap a slot.",
                "Formation locked. Battle starting...",
                "Bot placing...",
                "Bot locked",
                false,
                "No ban phase",
                "No ban phase");

            public static ScreenConfig BattlePracticeDefault => new(
                "STEP 3 OF 4",
                "Set Formation",
                "Bans are revealed. Place your final 5 for the practice battle.",
                "Formation locked. Practice battle starting...",
                "Practice Bot ready",
                "Practice Bot ready",
                true,
                "Your ban is revealed",
                "Practice Bot ban is revealed");
        }

        public readonly struct UnitView
        {
            public UnitView(string id, string name, string role, Color cardColor, Color auraColor)
            {
                Id = id ?? string.Empty;
                Name = string.IsNullOrWhiteSpace(name) ? "Unit" : name;
                Role = string.IsNullOrWhiteSpace(role) ? "UNIT" : role;
                CardColor = cardColor;
                AuraColor = auraColor;
            }

            public string Id { get; }
            public string Name { get; }
            public string Role { get; }
            public Color CardColor { get; }
            public Color AuraColor { get; }

            public static UnitView FromApiId(string apiId, bool enemyTone)
            {
                var key = UnitIconLibrary.NormalizeUnitKey(apiId);
                var displayName = ToDisplayName(apiId, key);
                var role = ToRole(apiId);
                var primary = UnitIconLibrary.GetPrimaryColor(key);
                var secondary = UnitIconLibrary.GetSecondaryColor(key);
                var baseColor = enemyTone
                    ? Color.Lerp(primary, RescueStickerFactory.Palette.Coral, 0.34f)
                    : Color.Lerp(primary, RescueStickerFactory.Palette.InkPurple, 0.24f);
                var aura = Color.Lerp(primary, secondary, 0.22f);
                return new UnitView(key, displayName, role, baseColor, aura);
            }
        }

        public void Bind(
            IReadOnlyList<UnitView> availableFighters,
            string yourBanId,
            string opponentBanId,
            string[] orderedAssignments,
            bool locked,
            int opponentPlaced,
            Action<string[]> formationChanged,
            Action<string[]> lockFormation,
            ScreenConfig? config = null)
        {
            availableUnits = availableFighters ?? Array.Empty<UnitView>();
            yourBanResultId = NormalizeOptionalKey(yourBanId);
            opponentBanResultId = NormalizeOptionalKey(opponentBanId);
            blockedUnitId = opponentBanResultId;
            recommendedAssignments = BuildRecommendedAssignments(availableUnits, blockedUnitId);
            slotAssignments = SanitizeAssignments(CopyAssignments(orderedAssignments));
            if (!locked && slotAssignments.All(string.IsNullOrWhiteSpace))
            {
                slotAssignments = CloneAssignments(recommendedAssignments);
            }

            isLocked = locked;
            opponentPlacedCount = Mathf.Clamp(opponentPlaced, 0, SlotIds.Length);
            screenConfig = config ?? ScreenConfig.RankedDefault;
            onFormationChanged = formationChanged;
            onLockFormation = lockFormation;

            if (isLocked)
            {
                selectedUnitId = string.Empty;
                selectedSlotIndex = -1;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(selectedUnitId) && !availableUnits.Any(unit => unit.Id == selectedUnitId))
                {
                    selectedUnitId = string.Empty;
                }

                if (selectedSlotIndex < -1 || selectedSlotIndex >= SlotIds.Length)
                {
                    selectedSlotIndex = -1;
                }
            }

            BuildOnce();
            EnsureFighterTray();
            RefreshHeader();
            RefreshBanResults();
            RefreshSlots();
            RefreshFighterTray();
            RefreshFooter();
            PlayIntroOnce();
        }

        public void Hide()
        {
            fighterViews.Clear();
            slotViews.Clear();
            enterTargets.Clear();
            fighterSignature = string.Empty;
            introPlayed = false;
            lockPulseActive = false;

            if (root != null)
            {
                Destroy(root.gameObject);
            }

            root = null;
            contentRoot = null;
            fighterTrayRoot = null;
            fighterTrayLayout = null;
            progressChipLabel = null;
            titleLabel = null;
            subtitleLabel = null;
            opponentProgressLabel = null;
            lockButtonLabel = null;
            resetButtonLabel = null;
            lockButton = null;
            resetButton = null;
            availableUnits = Array.Empty<UnitView>();
            recommendedAssignments = EmptyAssignments();
            slotAssignments = EmptyAssignments();
            selectedUnitId = string.Empty;
            selectedSlotIndex = -1;
            yourBanResultId = string.Empty;
            opponentBanResultId = string.Empty;
            blockedUnitId = string.Empty;
            isLocked = false;
            opponentPlacedCount = 0;
            onFormationChanged = null;
            onLockFormation = null;
        }

        private void OnDisable()
        {
            Hide();
        }

        private bool CanLockFormation => !isLocked && slotAssignments.All(value => !string.IsNullOrWhiteSpace(value));

        private void BuildOnce()
        {
            if (root != null)
            {
                return;
            }

            var rootObject = RescueStickerFactory.CreateScreenRoot(transform, "FormationRescueRoot");
            root = rootObject.GetComponent<RectTransform>();
            root.SetAsLastSibling();

            RescueStickerFactory.CreateGradientLikeBackground(
                root,
                "FormationStickerPopGradient",
                Color.Lerp(RescueStickerFactory.Palette.ElectricPurple, RescueStickerFactory.Palette.Aqua, 0.20f),
                RescueStickerFactory.Palette.Mint);

            contentRoot = CreateRect("FormationContent", root);
            Stretch(contentRoot);

            CreateHeader();
            CreateBanResultsBand();
            CreateBoard();
            CreateFighterTray();
            CreateFooter();
        }

        private void CreateHeader()
        {
            var header = CreateRect("FormationHeader", contentRoot);
            SetAnchors(header, new Vector2(0.06f, 0.855f), new Vector2(0.94f, 0.968f));
            AddEnterTarget(header);

            var stepChip = RescueStickerFactory.CreateStatusChip(
                header,
                "STEP 3 OF 4",
                RescueStickerFactory.Palette.SunnyYellow,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(stepChip.GetComponent<RectTransform>(), new Vector2(0f, 0.68f), new Vector2(0.32f, 1f));

            var progressChip = RescueStickerFactory.CreateStatusChip(
                header,
                "0/5 PLACED",
                RescueStickerFactory.Palette.Mint,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(progressChip.GetComponent<RectTransform>(), new Vector2(0.67f, 0.68f), new Vector2(1f, 1f));
            progressChipLabel = progressChip.GetComponentInChildren<TMP_Text>(true);
            stepChipLabel = stepChip.GetComponentInChildren<TMP_Text>(true);

            titleLabel = RescueStickerFactory.CreateLabel(
                header,
                "Title",
                "Set Formation",
                34f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.22f),
                new Vector2(0.72f, 0.70f));

            subtitleLabel = RescueStickerFactory.CreateLabel(
                header,
                "Subtitle",
                "Pick a fighter, then tap a slot.",
                16f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0f),
                new Vector2(0.62f, 0.28f));

            var opponentChip = RescueStickerFactory.CreateStatusChip(
                header,
                "Opponent placing...",
                new Color(0.29f, 0.25f, 0.64f, 0.92f),
                RescueStickerFactory.Palette.SoftWhite);
            SetAnchors(opponentChip.GetComponent<RectTransform>(), new Vector2(0.63f, 0.02f), new Vector2(1f, 0.42f));
            opponentProgressLabel = opponentChip.GetComponentInChildren<TMP_Text>(true);
        }

        private void CreateBanResultsBand()
        {
            var band = RescueStickerFactory.CreateArenaSurface(
                contentRoot,
                "FormationBanResultsBand",
                new Color(1f, 1f, 1f, 0.22f),
                RescueStickerFactory.Palette.Aqua,
                Vector2.zero);
            var bandRect = band.GetComponent<RectTransform>();
            SetAnchors(bandRect, new Vector2(0.055f, 0.762f), new Vector2(0.945f, 0.84f));
            AddEnterTarget(bandRect);

            CreateBanResultCard(
                bandRect,
                "YourBanCard",
                "You banned",
                new Vector2(0.02f, 0.10f),
                new Vector2(0.49f, 0.90f),
                out _,
                out _);

            CreateBanResultCard(
                bandRect,
                "OpponentBanCard",
                "Opponent banned",
                new Vector2(0.51f, 0.10f),
                new Vector2(0.98f, 0.90f),
                out _,
                out _);
        }

        private void CreateBoard()
        {
            var board = RescueStickerFactory.CreateArenaSurface(
                contentRoot,
                "FormationBoardSurface",
                new Color(1f, 1f, 1f, 0.22f),
                RescueStickerFactory.Palette.Aqua,
                Vector2.zero);
            var boardRect = board.GetComponent<RectTransform>();
            SetAnchors(boardRect, new Vector2(0.055f, 0.395f), new Vector2(0.945f, 0.74f));
            AddEnterTarget(boardRect);

            RescueStickerFactory.CreateBlob(
                boardRect,
                "FormationBlobA",
                RescueStickerFactory.Palette.HotPink,
                new Vector2(-114f, 20f),
                new Vector2(162f, 162f),
                0.10f);
            RescueStickerFactory.CreateBlob(
                boardRect,
                "FormationBlobB",
                RescueStickerFactory.Palette.SunnyYellow,
                new Vector2(128f, -52f),
                new Vector2(148f, 148f),
                0.08f);

            RescueStickerFactory.CreateLabel(
                board.transform,
                "BoardTitle",
                "Snap fighters into battle slots",
                21f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.91f),
                new Vector2(0.74f, 0.99f));

            RescueStickerFactory.CreateLabel(
                board.transform,
                "BoardGuide",
                "Frontline takes pressure first. Backline is safer for setup and support.",
                13f,
                FontStyles.Bold,
                new Color(1f, 1f, 1f, 0.88f),
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.82f),
                new Vector2(0.96f, 0.90f));

            var frontlineChip = RescueStickerFactory.CreateStatusChip(
                board.transform,
                "FRONTLINE",
                RescueStickerFactory.Palette.SunnyYellow,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(frontlineChip.GetComponent<RectTransform>(), new Vector2(0.04f, 0.72f), new Vector2(0.30f, 0.82f));

            RescueStickerFactory.CreateLabel(
                board.transform,
                "FrontRowHint",
                "Takes pressure first",
                12f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.31f, 0.73f),
                new Vector2(0.60f, 0.81f));

            var backlineChip = RescueStickerFactory.CreateStatusChip(
                board.transform,
                "BACKLINE",
                RescueStickerFactory.Palette.Aqua,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(backlineChip.GetComponent<RectTransform>(), new Vector2(0.04f, 0.29f), new Vector2(0.28f, 0.39f));

            RescueStickerFactory.CreateLabel(
                board.transform,
                "BackRowHint",
                "Safer row for setup",
                12f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.29f, 0.30f),
                new Vector2(0.58f, 0.38f));

            slotViews.Add(CreateSlotView(boardRect, 0, new Vector2(0.05f, 0.45f), new Vector2(0.31f, 0.80f)));
            slotViews.Add(CreateSlotView(boardRect, 1, new Vector2(0.37f, 0.45f), new Vector2(0.63f, 0.80f)));
            slotViews.Add(CreateSlotView(boardRect, 2, new Vector2(0.69f, 0.45f), new Vector2(0.95f, 0.80f)));
            slotViews.Add(CreateSlotView(boardRect, 3, new Vector2(0.20f, 0.07f), new Vector2(0.46f, 0.37f)));
            slotViews.Add(CreateSlotView(boardRect, 4, new Vector2(0.54f, 0.07f), new Vector2(0.80f, 0.37f)));
        }

        private SlotView CreateSlotView(RectTransform parent, int slotIndex, Vector2 anchorMin, Vector2 anchorMax)
        {
            var slot = RescueStickerFactory.CreateArenaSurface(
                parent,
                $"FormationSlot{slotIndex}",
                new Color(0.15f, 0.18f, 0.42f, 0.60f),
                RescueStickerFactory.Palette.SoftWhite,
                Vector2.zero);
            var rect = slot.GetComponent<RectTransform>();
            SetAnchors(rect, anchorMin, anchorMax);

            var button = slot.AddComponent<Button>();
            button.transition = Selectable.Transition.None;

            var body = slot.GetComponent<Image>();
            var outline = slot.GetComponent<Outline>();
            var canvasGroup = slot.GetComponent<CanvasGroup>() ?? slot.AddComponent<CanvasGroup>();

            var slotTitle = RescueStickerFactory.CreateLabel(
                slot.transform,
                "SlotTitle",
                SlotTitles[slotIndex],
                13f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.06f, 0.82f),
                new Vector2(0.94f, 0.96f));
            slotTitle.textWrappingMode = TextWrappingModes.NoWrap;

            var contentRoot = CreateRect("SlotContent", rect);
            SetAnchors(contentRoot, new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.78f));

            var placeholderRoot = CreateRect("PlaceholderRoot", contentRoot);
            Stretch(placeholderRoot);
            var placeholderGlyph = RescueStickerFactory.CreateLabel(
                placeholderRoot,
                "PlaceholderGlyph",
                "+",
                44f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.Aqua,
                TextAlignmentOptions.Center,
                new Vector2(0.18f, 0.28f),
                new Vector2(0.82f, 0.80f));
            placeholderGlyph.textWrappingMode = TextWrappingModes.NoWrap;
            var placeholderHint = RescueStickerFactory.CreateLabel(
                placeholderRoot,
                "PlaceholderHint",
                "Tap to place",
                12f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.10f, 0.02f),
                new Vector2(0.90f, 0.22f));
            placeholderHint.textWrappingMode = TextWrappingModes.NoWrap;

            var view = new SlotView(slotIndex, rect, body, outline, canvasGroup, button, contentRoot, placeholderRoot.gameObject);
            button.onClick.AddListener(() => HandleSlotPressed(view.Index));
            return view;
        }

        private void CreateFighterTray()
        {
            var tray = RescueStickerFactory.CreateArenaSurface(
                contentRoot,
                "FormationTraySurface",
                new Color(1f, 1f, 1f, 0.22f),
                RescueStickerFactory.Palette.ElectricPurple,
                Vector2.zero);
            var trayRect = tray.GetComponent<RectTransform>();
            SetAnchors(trayRect, new Vector2(0.055f, 0.175f), new Vector2(0.945f, 0.355f));
            AddEnterTarget(trayRect);

            RescueStickerFactory.CreateLabel(
                tray.transform,
                "TrayTitle",
                "Available Fighters",
                21f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.04f, 0.74f),
                new Vector2(0.70f, 0.96f));

            fighterTrayRoot = CreateRect("FighterTrayRow", trayRect);
            SetAnchors(fighterTrayRoot, new Vector2(0.02f, 0.08f), new Vector2(0.98f, 0.70f));
            fighterTrayLayout = fighterTrayRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            fighterTrayLayout.spacing = 6f;
            fighterTrayLayout.childAlignment = TextAnchor.MiddleCenter;
            fighterTrayLayout.childControlWidth = false;
            fighterTrayLayout.childControlHeight = false;
            fighterTrayLayout.childForceExpandWidth = false;
            fighterTrayLayout.childForceExpandHeight = false;
        }

        private void CreateFooter()
        {
            var footer = CreateRect("FormationFooter", contentRoot);
            SetAnchors(footer, new Vector2(0.06f, 0.06f), new Vector2(0.94f, 0.15f));
            AddEnterTarget(footer);

            resetButton = RescueStickerFactory.CreateToyButton(
                footer,
                "Reset",
                new Color(0.47f, 0.41f, 0.72f, 0.94f),
                RescueStickerFactory.Palette.SoftWhite,
                new Vector2(180f, 72f),
                primary: false);
            SetAnchors(resetButton.transform as RectTransform, new Vector2(0f, 0f), new Vector2(0.32f, 1f));
            resetButtonLabel = resetButton.GetComponentInChildren<TMP_Text>(true);
            resetButton.onClick.RemoveAllListeners();
            resetButton.onClick.AddListener(HandleResetPressed);

            lockButton = RescueStickerFactory.CreateToyButton(
                footer,
                "Lock Formation",
                new Color(0.50f, 0.43f, 0.72f, 0.92f),
                RescueStickerFactory.Palette.SoftWhite,
                new Vector2(360f, 72f),
                primary: true);
            SetAnchors(lockButton.transform as RectTransform, new Vector2(0.36f, 0f), new Vector2(1f, 1f));
            lockButtonLabel = lockButton.GetComponentInChildren<TMP_Text>(true);
            lockButton.onClick.RemoveAllListeners();
            lockButton.onClick.AddListener(HandleLockPressed);
        }

        private void CreateBanResultCard(
            RectTransform parent,
            string name,
            string prefix,
            Vector2 anchorMin,
            Vector2 anchorMax,
            out TMP_Text label,
            out RectTransform iconRoot)
        {
            var card = RescueStickerFactory.CreateArenaSurface(
                parent,
                name,
                new Color(0.19f, 0.24f, 0.51f, 0.72f),
                RescueStickerFactory.Palette.SoftWhite,
                Vector2.zero);
            var rect = card.GetComponent<RectTransform>();
            SetAnchors(rect, anchorMin, anchorMax);

            iconRoot = CreateRect("IconRoot", rect);
            iconRoot.anchorMin = new Vector2(0.11f, 0.50f);
            iconRoot.anchorMax = new Vector2(0.11f, 0.50f);
            iconRoot.sizeDelta = new Vector2(46f, 46f);
            iconRoot.anchoredPosition = Vector2.zero;

            label = RescueStickerFactory.CreateLabel(
                card.transform,
                "ResultLabel",
                $"{prefix}: Waiting for ban result…",
                14f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.24f, 0.15f),
                new Vector2(0.92f, 0.85f));
            label.textWrappingMode = TextWrappingModes.NoWrap;

            var banStamp = RescueStickerFactory.CreateStatusChip(
                card.transform,
                "BAN",
                RescueStickerFactory.Palette.Coral,
                RescueStickerFactory.Palette.SoftWhite);
            banStamp.name = "BanStamp";
            var stampRect = banStamp.GetComponent<RectTransform>();
            SetAnchors(stampRect, new Vector2(0.76f, 0.58f), new Vector2(0.98f, 0.92f));
            banStamp.SetActive(false);
        }

        private void EnsureFighterTray()
        {
            if (fighterTrayRoot == null)
            {
                return;
            }

            var signature = string.Join("|", availableUnits.Select(unit => unit.Id));
            if (string.Equals(signature, fighterSignature, StringComparison.Ordinal) &&
                fighterViews.Count == availableUnits.Count)
            {
                return;
            }

            fighterSignature = signature;
            fighterViews.Clear();

            for (var index = fighterTrayRoot.childCount - 1; index >= 0; index--)
            {
                Destroy(fighterTrayRoot.GetChild(index).gameObject);
            }

            for (var index = 0; index < availableUnits.Count; index++)
            {
                var unit = availableUnits[index];
                var tile = RescueStickerFactory.CreateCompactUnitStickerTile(
                    fighterTrayRoot,
                    unit.Name,
                    unit.Id,
                    unit.Role,
                    unit.CardColor,
                    unit.AuraColor,
                    false,
                    false,
                    TrayFighterSize,
                    0);

                var rect = tile.GetComponent<RectTransform>();
                AddFixedLayout(tile, TrayFighterSize.x, TrayFighterSize.y);

                var button = tile.GetComponent<Button>() ?? tile.AddComponent<Button>();
                button.transition = Selectable.Transition.None;

                var view = new FighterView(
                    unit,
                    rect,
                    tile.GetComponent<Image>(),
                    tile.GetComponent<CanvasGroup>() ?? tile.AddComponent<CanvasGroup>(),
                    tile.GetComponent<Outline>(),
                    button,
                    rect.Find("EmojiAvatar") as RectTransform);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => HandleFighterPressed(view.Unit.Id));

                if (view.Avatar != null)
                {
                    NativeMotionKit.IdleBob(this, view.Avatar, 4f, 1.18f, true);
                    NativeMotionKit.BreatheScale(this, view.Avatar, 0.022f, 1.32f, true);
                }

                fighterViews.Add(view);
            }
        }

        private void RefreshHeader()
        {
            if (stepChipLabel != null)
            {
                stepChipLabel.text = screenConfig.StepText;
            }

            if (titleLabel != null)
            {
                titleLabel.text = screenConfig.TitleText;
            }

            if (subtitleLabel != null)
            {
                subtitleLabel.text = isLocked
                    ? screenConfig.LockedSubtitle
                    : !string.IsNullOrWhiteSpace(selectedUnitId)
                        ? "Tap a slot to place the selected fighter."
                        : selectedSlotIndex >= 0
                            ? "Pick a fighter for the highlighted slot."
                            : screenConfig.IdleSubtitle;
            }

            if (progressChipLabel != null)
            {
                var placedCount = slotAssignments.Count(value => !string.IsNullOrWhiteSpace(value));
                progressChipLabel.text = isLocked
                    ? "FORMATION LOCKED"
                    : CanLockFormation
                        ? "READY TO LOCK"
                        : $"{placedCount}/5 PLACED";
            }

            if (opponentProgressLabel != null)
            {
                opponentProgressLabel.text = opponentPlacedCount >= SlotIds.Length
                    ? screenConfig.OpponentLockedText
                    : opponentPlacedCount > 0
                        ? $"Opponent {opponentPlacedCount}/5"
                        : screenConfig.OpponentWaitingText;
            }
        }

        private void RefreshBanResults()
        {
            var band = contentRoot != null ? contentRoot.Find("FormationBanResultsBand") as RectTransform : null;
            if (band != null)
            {
                band.gameObject.SetActive(screenConfig.ShowBanResults);
            }

            if (!screenConfig.ShowBanResults)
            {
                return;
            }

            RefreshBanResultCard("YourBanCard", "You banned", yourBanResultId, screenConfig.YourBanFallbackText, enemyTone: true);
            RefreshBanResultCard("OpponentBanCard", "Opponent banned", opponentBanResultId, screenConfig.OpponentBanFallbackText, enemyTone: false);
        }

        private void RefreshBanResultCard(string cardName, string prefix, string unitId, string fallbackText, bool enemyTone)
        {
            if (root == null)
            {
                return;
            }

            var card = root.Find(cardName) as RectTransform ?? contentRoot.Find($"FormationBanResultsBand/{cardName}") as RectTransform;
            if (card == null)
            {
                return;
            }

            var label = card.Find("ResultLabel")?.GetComponent<TMP_Text>();
            var iconRoot = card.Find("IconRoot") as RectTransform;
            if (label == null || iconRoot == null)
            {
                return;
            }

            var normalized = NormalizeOptionalKey(unitId);
            var banStamp = card.Find("BanStamp") as RectTransform;
            if (banStamp != null)
            {
                var showStamp = !string.IsNullOrWhiteSpace(normalized);
                var wasActive = banStamp.gameObject.activeSelf;
                banStamp.gameObject.SetActive(showStamp);
                if (showStamp && !wasActive)
                {
                    NativeMotionKit.StampSlam(this, banStamp, 1.18f, 0.20f);
                }
            }

            if (string.IsNullOrWhiteSpace(normalized))
            {
                label.text = $"{prefix}: {fallbackText}";
            }
            else
            {
                var unit = UnitView.FromApiId(normalized, enemyTone);
                label.text = $"{prefix}: {unit.Name}";
            }

            var current = iconRoot.Find("Avatar");
            var desiredKey = string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized;
            if (current != null && string.Equals(current.name, $"Avatar_{desiredKey}", StringComparison.Ordinal))
            {
                return;
            }

            for (var index = iconRoot.childCount - 1; index >= 0; index--)
            {
                Destroy(iconRoot.GetChild(index).gameObject);
            }

            if (string.IsNullOrWhiteSpace(normalized))
            {
                var waiting = RescueStickerFactory.CreateStatusChip(
                    iconRoot,
                    "?",
                    new Color(0.45f, 0.39f, 0.72f, 0.92f),
                    RescueStickerFactory.Palette.SoftWhite);
                waiting.name = "Avatar_";
                Stretch(waiting.GetComponent<RectTransform>());
                return;
            }

            var view = UnitView.FromApiId(normalized, enemyTone);
            var avatar = RescueStickerFactory.CreateEmojiAvatar(iconRoot, view.Id, view.Name, view.AuraColor, new Vector2(46f, 46f));
            avatar.name = $"Avatar_{desiredKey}";
            var avatarRect = avatar.GetComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0.5f, 0.5f);
            avatarRect.anchorMax = new Vector2(0.5f, 0.5f);
            avatarRect.anchoredPosition = Vector2.zero;
            NativeMotionKit.PopIn(this, avatarRect, null, 0.18f, 0.86f);
        }

        private void RefreshSlots()
        {
            for (var index = 0; index < slotViews.Count; index++)
            {
                RefreshSlot(slotViews[index], index);
            }
        }

        private void RefreshSlot(SlotView view, int slotIndex)
        {
            var assignedId = slotAssignments[slotIndex];
            var isSelectedSlot = !isLocked && selectedSlotIndex == slotIndex;
            var hasUnit = !string.IsNullOrWhiteSpace(assignedId);

            view.PlaceholderRoot.SetActive(!hasUnit);

            if (!string.Equals(view.CurrentUnitId, assignedId, StringComparison.Ordinal))
            {
                view.CurrentUnitId = assignedId;
                if (view.ContentSticker != null)
                {
                    Destroy(view.ContentSticker.gameObject);
                    view.ContentSticker = null;
                }

                if (hasUnit)
                {
                    var unit = availableUnits.FirstOrDefault(candidate => candidate.Id == assignedId);
                    if (!string.IsNullOrWhiteSpace(unit.Id))
                    {
                        var sticker = RescueStickerFactory.CreateMiniSquadSticker(
                            view.ContentRoot,
                            unit.Name,
                            unit.Id,
                            unit.AuraColor,
                            SlotStickerSize);
                        var stickerRect = sticker.GetComponent<RectTransform>();
                        stickerRect.anchorMin = new Vector2(0.5f, 0.5f);
                        stickerRect.anchorMax = new Vector2(0.5f, 0.5f);
                        stickerRect.anchoredPosition = Vector2.zero;
                        view.ContentSticker = stickerRect;
                        NativeMotionKit.DropIntoSlot(this, stickerRect, new Vector2(0f, 34f), 0.26f);
                        NativeMotionKit.PunchScale(this, view.Rect, 0.08f, 0.16f);
                    }
                }
            }

            if (view.Body != null)
            {
                view.Body.color = isLocked
                    ? new Color(0.29f, 0.42f, 0.34f, 0.78f)
                    : isSelectedSlot
                        ? new Color(0.29f, 0.45f, 0.76f, 0.84f)
                        : hasUnit
                            ? new Color(0.22f, 0.28f, 0.56f, 0.78f)
                            : new Color(0.15f, 0.18f, 0.42f, 0.60f);
            }

            if (view.Outline != null)
            {
                view.Outline.effectColor = isLocked
                    ? RescueStickerFactory.Palette.Mint
                    : isSelectedSlot
                        ? RescueStickerFactory.Palette.SunnyYellow
                        : hasUnit
                            ? RescueStickerFactory.Palette.Aqua
                            : RescueStickerFactory.Palette.SoftWhite;
                var outlineSize = isSelectedSlot ? 4f : hasUnit ? 3.4f : 2.4f;
                view.Outline.effectDistance = new Vector2(outlineSize, outlineSize);
            }

            if (view.Button != null)
            {
                view.Button.interactable = !isLocked;
            }

            if (view.CanvasGroup != null)
            {
                view.CanvasGroup.alpha = 1f;
            }
        }

        private void RefreshFighterTray()
        {
            foreach (var view in fighterViews)
            {
                var isSelected = !isLocked && string.Equals(selectedUnitId, view.Unit.Id, StringComparison.Ordinal);
                var isUsed = slotAssignments.Any(value => string.Equals(value, view.Unit.Id, StringComparison.Ordinal));
                var isBlocked = string.Equals(blockedUnitId, view.Unit.Id, StringComparison.Ordinal);

                if (view.CanvasGroup != null)
                {
                    view.CanvasGroup.alpha = isLocked
                        ? 0.64f
                        : isBlocked
                            ? 0.42f
                            : isUsed && !isSelected
                                ? 0.56f
                                : 1f;
                }

                if (view.Body != null)
                {
                    view.Body.color = isBlocked
                        ? Color.Lerp(view.Unit.CardColor, RescueStickerFactory.Palette.Coral, 0.50f)
                        : isSelected
                            ? Color.Lerp(view.Unit.CardColor, view.Unit.AuraColor, 0.34f)
                            : isUsed
                                ? Color.Lerp(view.Unit.CardColor, RescueStickerFactory.Palette.InkPurple, 0.42f)
                                : Color.Lerp(view.Unit.CardColor, RescueStickerFactory.Palette.InkPurple, 0.10f);
                }

                if (view.Outline != null)
                {
                    view.Outline.effectColor = isBlocked
                        ? RescueStickerFactory.Palette.Coral
                        : isSelected
                            ? RescueStickerFactory.Palette.SunnyYellow
                            : isUsed
                                ? RescueStickerFactory.Palette.Mint
                                : new Color(
                                    RescueStickerFactory.Palette.SoftWhite.r,
                                    RescueStickerFactory.Palette.SoftWhite.g,
                                    RescueStickerFactory.Palette.SoftWhite.b,
                                    0.86f);
                    var outlineSize = isBlocked ? 3.4f : isSelected ? 4f : isUsed ? 3f : 2f;
                    view.Outline.effectDistance = new Vector2(outlineSize, outlineSize);
                }

                if (view.Button != null)
                {
                    view.Button.interactable = !isLocked && !isBlocked;
                }

                if (isBlocked)
                {
                    SetStateBadge(view.Rect, "BANNED", RescueStickerFactory.Palette.Coral, RescueStickerFactory.Palette.SoftWhite);
                }
                else if (isUsed)
                {
                    SetStateBadge(view.Rect, "USED", RescueStickerFactory.Palette.Mint, RescueStickerFactory.Palette.InkPurple);
                }
                else
                {
                    ClearStateBadge(view.Rect);
                }
            }
        }

        private void RefreshFooter()
        {
            var isDefaultLayout = AssignmentsMatch(slotAssignments, recommendedAssignments);
            if (resetButton != null)
            {
                resetButton.interactable = !isLocked && !isDefaultLayout;
                var body = resetButton.GetComponent<Image>();
                if (body != null)
                {
                    body.color = resetButton.interactable
                        ? new Color(0.47f, 0.41f, 0.72f, 0.94f)
                        : new Color(0.37f, 0.33f, 0.56f, 0.80f);
                }
            }

            if (resetButtonLabel != null)
            {
                resetButtonLabel.text = recommendedAssignments.Any(value => !string.IsNullOrWhiteSpace(value))
                    ? "Reset Layout"
                    : "Reset";
            }

            if (lockButton == null)
            {
                return;
            }

            lockButton.interactable = CanLockFormation;
            var label = isLocked ? "Formation Locked" : CanLockFormation ? "Lock Formation" : "Fill All 5 Slots";
            var bodyColor = isLocked
                ? RescueStickerFactory.Palette.Mint
                : CanLockFormation
                    ? RescueStickerFactory.Palette.HotPink
                    : new Color(0.50f, 0.43f, 0.72f, 0.92f);
            var textColor = isLocked
                ? RescueStickerFactory.Palette.InkPurple
                : RescueStickerFactory.Palette.SoftWhite;

            var lockBody = lockButton.GetComponent<Image>();
            if (lockBody != null)
            {
                lockBody.color = bodyColor;
            }

            if (lockButtonLabel != null)
            {
                lockButtonLabel.text = label;
                lockButtonLabel.color = textColor;
            }

            var rect = lockButton.transform as RectTransform;
            if (CanLockFormation && !lockPulseActive)
            {
                NativeMotionKit.BreatheScale(this, rect, 0.022f, 1.08f, true);
                lockPulseActive = true;
            }
            else if (!CanLockFormation && lockPulseActive)
            {
                NativeMotionKit.BreatheScale(this, rect, 0f, 1.08f, false);
                lockPulseActive = false;
            }

            if (!CanLockFormation && rect != null)
            {
                rect.localScale = Vector3.one;
            }
        }

        private void HandleFighterPressed(string unitId)
        {
            if (isLocked || string.IsNullOrWhiteSpace(unitId))
            {
                return;
            }

            if (string.Equals(blockedUnitId, unitId, StringComparison.Ordinal))
            {
                return;
            }

            selectedUnitId = string.Equals(selectedUnitId, unitId, StringComparison.Ordinal) ? string.Empty : unitId;
            if (!string.IsNullOrWhiteSpace(selectedUnitId) && selectedSlotIndex >= 0)
            {
                AssignUnitToSlot(selectedSlotIndex, selectedUnitId);
                return;
            }

            var view = fighterViews.FirstOrDefault(item => item.Unit.Id == unitId);
            if (view.Avatar != null)
            {
                NativeMotionKit.PunchScale(this, view.Avatar, 0.12f, 0.16f);
            }

            RefreshHeader();
            RefreshFighterTray();
            RefreshSlots();
        }

        private void HandleSlotPressed(int slotIndex)
        {
            if (isLocked || slotIndex < 0 || slotIndex >= SlotIds.Length)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(selectedUnitId))
            {
                AssignUnitToSlot(slotIndex, selectedUnitId);
                return;
            }

            var assigned = slotAssignments[slotIndex];
            if (!string.IsNullOrWhiteSpace(assigned))
            {
                selectedUnitId = assigned;
                slotAssignments[slotIndex] = string.Empty;
                selectedSlotIndex = slotIndex;
                NotifyFormationChanged();
            }
            else
            {
                selectedSlotIndex = selectedSlotIndex == slotIndex ? -1 : slotIndex;
            }

            RefreshHeader();
            RefreshSlots();
            RefreshFighterTray();
            RefreshFooter();
        }

        private void AssignUnitToSlot(int slotIndex, string unitId)
        {
            if (slotIndex < 0 ||
                slotIndex >= SlotIds.Length ||
                string.IsNullOrWhiteSpace(unitId) ||
                string.Equals(blockedUnitId, unitId, StringComparison.Ordinal))
            {
                return;
            }

            for (var index = 0; index < slotAssignments.Length; index++)
            {
                if (string.Equals(slotAssignments[index], unitId, StringComparison.Ordinal))
                {
                    slotAssignments[index] = string.Empty;
                }
            }

            slotAssignments[slotIndex] = unitId;
            selectedUnitId = string.Empty;
            selectedSlotIndex = -1;
            NotifyFormationChanged();
            RefreshHeader();
            RefreshSlots();
            RefreshFighterTray();
            RefreshFooter();
        }

        private void HandleResetPressed()
        {
            if (isLocked)
            {
                return;
            }

            if (AssignmentsMatch(slotAssignments, recommendedAssignments))
            {
                return;
            }

            slotAssignments = CloneAssignments(recommendedAssignments);
            selectedUnitId = string.Empty;
            selectedSlotIndex = -1;
            NotifyFormationChanged();
            RefreshHeader();
            RefreshSlots();
            RefreshFighterTray();
            RefreshFooter();
        }

        private void HandleLockPressed()
        {
            if (!CanLockFormation)
            {
                return;
            }

            var rect = lockButton.transform as RectTransform;
            NativeMotionKit.PunchScale(this, rect, 0.08f, 0.15f);
            onLockFormation?.Invoke(CloneAssignments(slotAssignments));
        }

        private void NotifyFormationChanged()
        {
            onFormationChanged?.Invoke(CloneAssignments(slotAssignments));
        }

        private void ClearStateBadge(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            var existing = rect.Find("StateBadge");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }
        }

        private void SetStateBadge(RectTransform rect, string text, Color background, Color foreground)
        {
            if (rect == null)
            {
                return;
            }

            var existing = rect.Find("StateBadge");
            if (existing != null)
            {
                var existingLabel = existing.GetComponentInChildren<TMP_Text>(true);
                var existingImage = existing.GetComponent<Image>();
                if (existingLabel != null)
                {
                    existingLabel.text = text;
                    existingLabel.color = foreground;
                }

                if (existingImage != null)
                {
                    existingImage.color = background;
                }

                return;
            }

            var badge = RescueStickerFactory.CreateStatusChip(
                rect,
                text,
                background,
                foreground);
            badge.name = "StateBadge";
            SetAnchors(badge.GetComponent<RectTransform>(), new Vector2(0.60f, 0.82f), new Vector2(0.97f, 0.98f));
            NativeMotionKit.StampSlam(this, badge.GetComponent<RectTransform>(), 1.14f, 0.18f);
        }

        private void PlayIntroOnce()
        {
            if (introPlayed)
            {
                return;
            }

            introPlayed = true;
            for (var index = 0; index < enterTargets.Count; index++)
            {
                var target = enterTargets[index];
                if (target == null)
                {
                    continue;
                }

                var group = target.GetComponent<CanvasGroup>() ?? target.gameObject.AddComponent<CanvasGroup>();
                NativeMotionKit.SlideFadeIn(this, target, group, new Vector2(0f, -18f), 0.26f + index * 0.025f);
            }
        }

        private void AddEnterTarget(RectTransform target)
        {
            if (target != null)
            {
                enterTargets.Add(target);
            }
        }

        private static string[] EmptyAssignments()
        {
            return new string[SlotIds.Length];
        }

        private static string[] CopyAssignments(string[] assignments)
        {
            var copy = EmptyAssignments();
            if (assignments == null)
            {
                return copy;
            }

            for (var index = 0; index < Mathf.Min(copy.Length, assignments.Length); index++)
            {
                copy[index] = NormalizeOptionalKey(assignments[index]);
            }

            return copy;
        }

        private static string[] CloneAssignments(string[] assignments)
        {
            return assignments == null ? EmptyAssignments() : (string[])assignments.Clone();
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.localScale = Vector3.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one);
        }

        private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddFixedLayout(GameObject target, float width, float height)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            layout.minWidth = width;
            layout.minHeight = height;
        }

        private static string NormalizeOptionalKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : UnitIconLibrary.NormalizeUnitKey(value);
        }

        private static string ToDisplayName(string apiId, string normalizedKey)
        {
            if (EmojiIdUtility.TryFromApiId(UnitIconLibrary.NormalizeUnitKey(apiId), out var emojiId))
            {
                return EmojiIdUtility.ToDisplayName(emojiId);
            }

            var source = string.IsNullOrWhiteSpace(normalizedKey) ? "unit" : normalizedKey;
            return string.Join(" ", source.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Length == 0 ? part : char.ToUpperInvariant(part[0]) + part.Substring(1)));
        }

        private static string ToRole(string apiId)
        {
            return EmojiIdUtility.TryFromApiId(UnitIconLibrary.NormalizeUnitKey(apiId), out var emojiId)
                ? EmojiUiFormatter.BuildRoleTag(emojiId)
                : "UNIT";
        }

        private string[] SanitizeAssignments(string[] assignments)
        {
            var copy = EmptyAssignments();
            if (assignments == null || availableUnits.Count == 0)
            {
                return copy;
            }

            var allowed = new HashSet<string>(
                availableUnits
                    .Select(unit => unit.Id)
                    .Where(id => !string.IsNullOrWhiteSpace(id) && !string.Equals(id, blockedUnitId, StringComparison.Ordinal)),
                StringComparer.Ordinal);
            var used = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < Mathf.Min(copy.Length, assignments.Length); index++)
            {
                var normalized = NormalizeOptionalKey(assignments[index]);
                if (string.IsNullOrWhiteSpace(normalized) || !allowed.Contains(normalized) || !used.Add(normalized))
                {
                    continue;
                }

                copy[index] = normalized;
            }

            return copy;
        }

        private string[] BuildRecommendedAssignments(IReadOnlyList<UnitView> units, string blockedId)
        {
            var assignments = EmptyAssignments();
            if (units == null || units.Count == 0)
            {
                return assignments;
            }

            var candidates = units
                .Where(unit => !string.IsNullOrWhiteSpace(unit.Id) && !string.Equals(unit.Id, blockedId, StringComparison.Ordinal))
                .Select(BuildPlacementCandidate)
                .OrderBy(candidate => candidate.Unit.Id, StringComparer.Ordinal)
                .ToList();

            if (candidates.Count == 0)
            {
                return assignments;
            }

            var frontUnits = candidates
                .OrderByDescending(candidate => candidate.FrontScore)
                .ThenByDescending(candidate => candidate.CenterScore)
                .ThenBy(candidate => candidate.Unit.Name, StringComparer.Ordinal)
                .Take(3)
                .ToList();

            var backUnits = candidates
                .Where(candidate => !frontUnits.Any(front => front.Unit.Id == candidate.Unit.Id))
                .OrderByDescending(candidate => candidate.BackScore)
                .ThenBy(candidate => candidate.Unit.Name, StringComparer.Ordinal)
                .Take(2)
                .ToList();

            if (frontUnits.Count < 3)
            {
                frontUnits = candidates.Take(Mathf.Min(3, candidates.Count)).ToList();
                backUnits = candidates
                    .Where(candidate => !frontUnits.Any(front => front.Unit.Id == candidate.Unit.Id))
                    .Take(2)
                    .ToList();
            }

            if (frontUnits.Count > 0)
            {
                var center = frontUnits
                    .OrderByDescending(candidate => candidate.CenterScore)
                    .ThenBy(candidate => candidate.Unit.Name, StringComparer.Ordinal)
                    .First();
                assignments[1] = center.Unit.Id;

                var flanks = frontUnits
                    .Where(candidate => candidate.Unit.Id != center.Unit.Id)
                    .OrderByDescending(candidate => candidate.FlankScore)
                    .ThenBy(candidate => candidate.Unit.Name, StringComparer.Ordinal)
                    .ToList();
                if (flanks.Count > 0)
                {
                    assignments[0] = flanks[0].Unit.Id;
                }

                if (flanks.Count > 1)
                {
                    assignments[2] = flanks[1].Unit.Id;
                }
            }

            var orderedBack = backUnits
                .OrderByDescending(candidate => candidate.BackScore)
                .ThenBy(candidate => candidate.Unit.Name, StringComparer.Ordinal)
                .ToList();
            if (orderedBack.Count > 0)
            {
                assignments[3] = orderedBack[0].Unit.Id;
            }

            if (orderedBack.Count > 1)
            {
                assignments[4] = orderedBack[1].Unit.Id;
            }

            return assignments;
        }

        private PlacementCandidate BuildPlacementCandidate(UnitView unit)
        {
            EmojiDefinition definition = null;
            if (EmojiIdUtility.TryFromApiId(unit.Id, out var emojiId))
            {
                definition = AppBootstrap.Instance?.EmojiCatalog?.Find(emojiId);
            }

            var stats = definition != null ? definition.BattleStats : default;
            var role = definition != null ? definition.Role : EmojiRole.Element;
            var preferredRow = definition != null ? definition.BattleStats.preferredRow : PreferredRow.Flex;

            var frontScore = preferredRow switch
            {
                PreferredRow.Front => 30,
                PreferredRow.Flex => 12,
                _ => -18
            };
            frontScore += stats.hp * 4 + stats.attack * 2 + stats.speed;
            frontScore += role switch
            {
                EmojiRole.GuardSupport => 8,
                EmojiRole.Element => 6,
                EmojiRole.Hazard => 4,
                EmojiRole.Trick => 1,
                EmojiRole.StatusRamp => -2,
                _ => 0
            };

            var backScore = preferredRow switch
            {
                PreferredRow.Back => 30,
                PreferredRow.Flex => 12,
                _ => -12
            };
            backScore += stats.speed * 4 + stats.attack * 3 + stats.hp;
            backScore += role switch
            {
                EmojiRole.Trick => 8,
                EmojiRole.StatusRamp => 8,
                EmojiRole.Hazard => 6,
                EmojiRole.GuardSupport => 5,
                EmojiRole.Element => 2,
                _ => 0
            };

            var centerScore = frontScore + stats.hp * 2 + stats.attack;
            var flankScore = frontScore + stats.speed * 2 + stats.attack;
            return new PlacementCandidate(unit, frontScore, backScore, centerScore, flankScore);
        }

        private static bool AssignmentsMatch(string[] left, string[] right)
        {
            var length = Mathf.Max(left?.Length ?? 0, right?.Length ?? 0);
            for (var index = 0; index < length; index++)
            {
                var leftValue = left != null && index < left.Length ? NormalizeOptionalKey(left[index]) : string.Empty;
                var rightValue = right != null && index < right.Length ? NormalizeOptionalKey(right[index]) : string.Empty;
                if (!string.Equals(leftValue, rightValue, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class SlotView
        {
            public SlotView(
                int index,
                RectTransform rect,
                Image body,
                Outline outline,
                CanvasGroup canvasGroup,
                Button button,
                RectTransform contentRoot,
                GameObject placeholderRoot)
            {
                Index = index;
                Rect = rect;
                Body = body;
                Outline = outline;
                CanvasGroup = canvasGroup;
                Button = button;
                ContentRoot = contentRoot;
                PlaceholderRoot = placeholderRoot;
            }

            public int Index { get; }
            public RectTransform Rect { get; }
            public Image Body { get; }
            public Outline Outline { get; }
            public CanvasGroup CanvasGroup { get; }
            public Button Button { get; }
            public RectTransform ContentRoot { get; }
            public GameObject PlaceholderRoot { get; }
            public string CurrentUnitId { get; set; } = string.Empty;
            public RectTransform ContentSticker { get; set; }
        }

        private readonly struct PlacementCandidate
        {
            public PlacementCandidate(UnitView unit, int frontScore, int backScore, int centerScore, int flankScore)
            {
                Unit = unit;
                FrontScore = frontScore;
                BackScore = backScore;
                CenterScore = centerScore;
                FlankScore = flankScore;
            }

            public UnitView Unit { get; }
            public int FrontScore { get; }
            public int BackScore { get; }
            public int CenterScore { get; }
            public int FlankScore { get; }
        }

        private sealed class FighterView
        {
            public FighterView(
                UnitView unit,
                RectTransform rect,
                Image body,
                CanvasGroup canvasGroup,
                Outline outline,
                Button button,
                RectTransform avatar)
            {
                Unit = unit;
                Rect = rect;
                Body = body;
                CanvasGroup = canvasGroup;
                Outline = outline;
                Button = button;
                Avatar = avatar;
            }

            public UnitView Unit { get; }
            public RectTransform Rect { get; }
            public Image Body { get; }
            public CanvasGroup CanvasGroup { get; }
            public Outline Outline { get; }
            public Button Button { get; }
            public RectTransform Avatar { get; }
        }
    }
}
