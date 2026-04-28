using System;
using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Content;
using EmojiWar.Client.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Match
{
    /// <summary>
    /// Ban-only rescue screen using the locked sticker-pop visual language.
    /// The hierarchy is built once, then data updates are applied in-place so
    /// countdown refreshes and selection changes do not replay full-screen motion.
    /// </summary>
    public sealed class BlindBanRescueScreen : MonoBehaviour
    {
        private static readonly Vector2 EnemyTileSize = new(136f, 134f);
        private static readonly Vector2 PlayerMiniSize = new(96f, 90f);
        private static readonly Color MysteryCardColor = new(0.36f, 0.28f, 0.64f, 0.98f);
        private static readonly Color MysteryAuraColor = new(0.33f, 0.83f, 1f, 1f);

        public enum BlindBanVisibilityMode
        {
            TestingRevealOpponentCards,
            ProductionHiddenOpponentCards
        }

        [SerializeField]
        private BlindBanVisibilityMode visibilityMode = BlindBanVisibilityMode.TestingRevealOpponentCards;

        private readonly List<RectTransform> enterTargets = new();
        private readonly List<EnemyCardView> enemyCards = new();
        private readonly List<PlayerMiniView> playerMiniViews = new();

        private RectTransform root;
        private RectTransform contentRoot;
        private RectTransform enemyGridRoot;
        private RectTransform playerRowRoot;

        private GridLayoutGroup enemyGridLayout;
        private HorizontalLayoutGroup playerRowLayout;

        private TMP_Text timerChipLabel;
        private TMP_Text titleLabel;
        private TMP_Text statusCopyLabel;
        private TMP_Text enemyTitleLabel;
        private TMP_Text enemyHintLabel;
        private TMP_Text playerReadyLabel;
        private TMP_Text vsInstructionLabel;
        private TMP_Text lockButtonLabel;
        private TMP_Text resultsTitleLabel;
        private TMP_Text yourBanResultLabel;
        private TMP_Text opponentBanResultLabel;

        private Button lockButton;
        private GameObject testingRevealChip;
        private GameObject vsChip;
        private GameObject banResultsRoot;

        private bool introPlayed;
        private bool lockButtonPulseActive;
        private string enemyLayoutSignature = string.Empty;
        private string playerLayoutSignature = string.Empty;

        private IReadOnlyList<UnitView> enemyUnits = Array.Empty<UnitView>();
        private IReadOnlyList<UnitView> playerUnits = Array.Empty<UnitView>();
        private string selectedEnemyId = string.Empty;
        private string lockedEnemyId = string.Empty;
        private string opponentBanId = string.Empty;
        private Action<string> onTargetChanged;
        private Action<string> onBanLocked;
        private int remainingSeconds;
        private BlindBanVisibilityMode resolvedVisibilityMode = BlindBanVisibilityMode.ProductionHiddenOpponentCards;

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

            public static UnitView FromEmojiId(EmojiId emojiId, bool enemyTone)
            {
                return FromApiId(EmojiIdUtility.ToApiId(emojiId), enemyTone);
            }

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
            IReadOnlyList<UnitView> enemies,
            IReadOnlyList<UnitView> players,
            BlindBanVisibilityMode? requestedVisibilityMode,
            string selectedEnemy,
            string lockedEnemy,
            string opponentBan,
            Action<string> targetChanged,
            Action<string> banLockedCallback,
            int secondsRemaining)
        {
            resolvedVisibilityMode = ResolveVisibilityMode(requestedVisibilityMode);
            enemyUnits = enemies ?? Array.Empty<UnitView>();
            playerUnits = players ?? Array.Empty<UnitView>();
            selectedEnemyId = NormalizeOptionalKey(selectedEnemy);
            lockedEnemyId = NormalizeOptionalKey(lockedEnemy);
            opponentBanId = NormalizeOptionalKey(opponentBan);
            onTargetChanged = targetChanged;
            onBanLocked = banLockedCallback;
            remainingSeconds = Mathf.Max(0, secondsRemaining);

            BuildOnce();
            EnsureEnemyCards();
            EnsurePlayerMinis();
            RefreshHeader();
            RefreshEnemyStates();
            RefreshVsState();
            RefreshPlayerState();
            RefreshLockState();
            PlayIntroOnce();
        }

        public void RefreshTimerOnly(int secondsRemaining)
        {
            remainingSeconds = Mathf.Max(0, secondsRemaining);
            RefreshHeader();
        }

        public void Hide()
        {
            enemyCards.Clear();
            playerMiniViews.Clear();
            enterTargets.Clear();
            enemyLayoutSignature = string.Empty;
            playerLayoutSignature = string.Empty;
            introPlayed = false;
            lockButtonPulseActive = false;

            if (root != null)
            {
                Destroy(root.gameObject);
            }

            root = null;
            contentRoot = null;
            enemyGridRoot = null;
            playerRowRoot = null;
            enemyGridLayout = null;
            playerRowLayout = null;
            timerChipLabel = null;
            titleLabel = null;
            statusCopyLabel = null;
            enemyTitleLabel = null;
            enemyHintLabel = null;
            playerReadyLabel = null;
            vsInstructionLabel = null;
            lockButtonLabel = null;
            resultsTitleLabel = null;
            yourBanResultLabel = null;
            opponentBanResultLabel = null;
            lockButton = null;
            testingRevealChip = null;
            vsChip = null;
            banResultsRoot = null;
            resolvedVisibilityMode = BlindBanVisibilityMode.ProductionHiddenOpponentCards;
        }

        private void OnDisable()
        {
            Hide();
        }

        private bool IsLocked => !string.IsNullOrWhiteSpace(lockedEnemyId);
        private bool HasBanResults => IsLocked && !string.IsNullOrWhiteSpace(opponentBanId);
        // TODO: production backend should send blinded ban slot ids/tokens instead of full opponent identities.
        private BlindBanVisibilityMode ActiveVisibilityMode => resolvedVisibilityMode;
        private bool IsTestingRevealMode => ActiveVisibilityMode == BlindBanVisibilityMode.TestingRevealOpponentCards;
        private bool IsProductionHiddenMode => ActiveVisibilityMode == BlindBanVisibilityMode.ProductionHiddenOpponentCards;
        private bool ShouldRevealLockedEnemyIdentity => IsProductionHiddenMode && HasBanResults;

        private void BuildOnce()
        {
            if (root != null)
            {
                return;
            }

            var rootObject = RescueStickerFactory.CreateScreenRoot(transform, "BlindBanRescueRoot");
            root = rootObject.GetComponent<RectTransform>();
            root.SetAsLastSibling();

            RescueStickerFactory.CreateGradientLikeBackground(
                root,
                "BlindBanStickerPopGradient",
                Color.Lerp(RescueStickerFactory.Palette.ElectricPurple, RescueStickerFactory.Palette.Coral, 0.12f),
                RescueStickerFactory.Palette.Mint);

            contentRoot = CreateRect("BlindBanContent", root);
            Stretch(contentRoot);

            CreateHeader();
            CreateEnemySquad();
            CreateVsMoment();
            CreatePlayerSquad();
            CreateLockButton();
        }

        private void CreateHeader()
        {
            var header = CreateRect("BlindBanHeader", contentRoot);
            SetAnchors(header, new Vector2(0.06f, 0.815f), new Vector2(0.94f, 0.965f));
            AddEnterTarget(header);

            var stepChip = RescueStickerFactory.CreateStatusChip(
                header,
                "STEP 2 OF 4",
                RescueStickerFactory.Palette.SunnyYellow,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(stepChip.GetComponent<RectTransform>(), new Vector2(0f, 0.68f), new Vector2(0.34f, 0.98f));

            var timerChip = RescueStickerFactory.CreateStatusChip(
                header,
                "27s",
                RescueStickerFactory.Palette.Coral,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(timerChip.GetComponent<RectTransform>(), new Vector2(0.66f, 0.68f), new Vector2(1f, 0.98f));
            timerChipLabel = timerChip.GetComponentInChildren<TMP_Text>(true);

            titleLabel = RescueStickerFactory.CreateLabel(
                header,
                "Title",
                "Blind Ban",
                38f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.18f),
                new Vector2(0.72f, 0.68f));

            statusCopyLabel = RescueStickerFactory.CreateLabel(
                header,
                "StatusCopy",
                "Pick 1 enemy sticker",
                18f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Right,
                new Vector2(0.46f, 0.18f),
                new Vector2(1f, 0.62f));
        }

        private void CreateEnemySquad()
        {
            var panel = RescueStickerFactory.CreateArenaSurface(
                contentRoot,
                "EnemySquadStickerBoard",
                new Color(1f, 1f, 1f, 0.21f),
                RescueStickerFactory.Palette.Coral,
                Vector2.zero);
            var panelRect = panel.GetComponent<RectTransform>();
            SetAnchors(panelRect, new Vector2(0.055f, 0.44f), new Vector2(0.945f, 0.78f));
            AddEnterTarget(panelRect);

            RescueStickerFactory.CreateBlob(
                panelRect,
                "EnemyBlobWarm",
                RescueStickerFactory.Palette.Coral,
                new Vector2(-96f, -4f),
                new Vector2(176f, 176f),
                0.11f);
            RescueStickerFactory.CreateBlob(
                panelRect,
                "EnemyBlobGold",
                RescueStickerFactory.Palette.SunnyYellow,
                new Vector2(112f, 26f),
                new Vector2(150f, 150f),
                0.08f);

            enemyTitleLabel = RescueStickerFactory.CreateLabel(
                panel.transform,
                "EnemyTitle",
                "Enemy Squad",
                22f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.045f, 0.84f),
                new Vector2(0.55f, 0.97f));

            testingRevealChip = RescueStickerFactory.CreateStatusChip(
                panel.transform,
                "TEST REVEAL",
                RescueStickerFactory.Palette.SunnyYellow,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(testingRevealChip.GetComponent<RectTransform>(), new Vector2(0.57f, 0.855f), new Vector2(0.79f, 0.965f));

            enemyHintLabel = RescueStickerFactory.CreateLabel(
                panel.transform,
                "EnemyHint",
                "Tap a target",
                15f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SunnyYellow,
                TextAlignmentOptions.Right,
                new Vector2(0.50f, 0.84f),
                new Vector2(0.955f, 0.97f));

            enemyGridRoot = CreateRect("EnemyGrid", panelRect);
            SetAnchors(enemyGridRoot, new Vector2(0.065f, 0.09f), new Vector2(0.935f, 0.80f));
            enemyGridLayout = enemyGridRoot.gameObject.AddComponent<GridLayoutGroup>();
            enemyGridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            enemyGridLayout.constraintCount = 3;
            enemyGridLayout.cellSize = EnemyTileSize;
            enemyGridLayout.spacing = new Vector2(14f, 12f);
            enemyGridLayout.childAlignment = TextAnchor.MiddleCenter;
            enemyGridLayout.padding = new RectOffset(4, 4, 2, 2);
        }

        private void CreateVsMoment()
        {
            var vsRoot = CreateRect("BlindBanVsMoment", contentRoot);
            SetAnchors(vsRoot, new Vector2(0.16f, 0.345f), new Vector2(0.84f, 0.415f));
            AddEnterTarget(vsRoot);

            RescueStickerFactory.CreateBlob(
                vsRoot,
                "VsGlow",
                RescueStickerFactory.Palette.SunnyYellow,
                Vector2.zero,
                new Vector2(116f, 116f),
                0.16f);

            vsChip = RescueStickerFactory.CreateStatusChip(
                vsRoot,
                "VS",
                RescueStickerFactory.Palette.HotPink,
                RescueStickerFactory.Palette.SoftWhite).gameObject;
            SetAnchors(vsChip.GetComponent<RectTransform>(), new Vector2(0.40f, 0.22f), new Vector2(0.60f, 0.92f));
            NativeMotionKit.BreatheScale(this, vsChip.transform as RectTransform, 0.018f, 1.2f, true);

            vsInstructionLabel = RescueStickerFactory.CreateLabel(
                vsRoot,
                "Instruction",
                "Tap one enemy sticker to ban",
                18f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.24f));

            banResultsRoot = new GameObject("BanResults", typeof(RectTransform)).gameObject;
            banResultsRoot.transform.SetParent(vsRoot, false);
            var resultsRect = banResultsRoot.GetComponent<RectTransform>();
            Stretch(resultsRect);

            resultsTitleLabel = RescueStickerFactory.CreateLabel(
                banResultsRoot.transform,
                "ResultsTitle",
                "BAN RESULTS",
                18f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SunnyYellow,
                TextAlignmentOptions.Center,
                new Vector2(0f, 0.64f),
                new Vector2(1f, 1f));

            yourBanResultLabel = RescueStickerFactory.CreateLabel(
                banResultsRoot.transform,
                "YourBanResult",
                "You banned: Pending",
                16f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.04f, 0.32f),
                new Vector2(0.96f, 0.66f));

            opponentBanResultLabel = RescueStickerFactory.CreateLabel(
                banResultsRoot.transform,
                "OpponentBanResult",
                "Opponent banned: Pending",
                16f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.04f, 0f),
                new Vector2(0.96f, 0.34f));

            banResultsRoot.SetActive(false);
        }

        private void CreatePlayerSquad()
        {
            var tray = RescueStickerFactory.CreateArenaSurface(
                contentRoot,
                "YourSquadBanTray",
                new Color(1f, 1f, 1f, 0.23f),
                RescueStickerFactory.Palette.Aqua,
                Vector2.zero);
            var trayRect = tray.GetComponent<RectTransform>();
            SetAnchors(trayRect, new Vector2(0.055f, 0.205f), new Vector2(0.945f, 0.355f));
            AddEnterTarget(trayRect);

            RescueStickerFactory.CreateLabel(
                tray.transform,
                "Title",
                "Your Squad",
                22f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.045f, 0.70f),
                new Vector2(0.52f, 0.95f));

            var chip = RescueStickerFactory.CreateStatusChip(
                tray.transform,
                "6/6 READY",
                RescueStickerFactory.Palette.Mint,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(chip.GetComponent<RectTransform>(), new Vector2(0.70f, 0.70f), new Vector2(0.955f, 0.95f));
            playerReadyLabel = chip.GetComponentInChildren<TMP_Text>(true);

            playerRowRoot = CreateRect("YourSquadRow", trayRect);
            SetAnchors(playerRowRoot, new Vector2(0.02f, 0.035f), new Vector2(0.98f, 0.70f));
            playerRowLayout = playerRowRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            playerRowLayout.spacing = 8f;
            playerRowLayout.childAlignment = TextAnchor.MiddleCenter;
            playerRowLayout.childControlWidth = false;
            playerRowLayout.childControlHeight = false;
            playerRowLayout.childForceExpandWidth = false;
            playerRowLayout.childForceExpandHeight = false;
        }

        private void CreateLockButton()
        {
            lockButton = RescueStickerFactory.CreateToyButton(
                contentRoot,
                "Choose Enemy",
                new Color(0.50f, 0.43f, 0.72f, 0.92f),
                RescueStickerFactory.Palette.SoftWhite,
                new Vector2(560f, 76f),
                primary: false);
            var rect = lockButton.transform as RectTransform;
            SetAnchors(rect, new Vector2(0.08f, 0.075f), new Vector2(0.92f, 0.155f));
            AddEnterTarget(rect);
            lockButtonLabel = lockButton.GetComponentInChildren<TMP_Text>(true);
            PolishButton(lockButton, 28f);

            lockButton.onClick.RemoveAllListeners();
            lockButton.onClick.AddListener(HandleLockPressed);
        }

        private void EnsureEnemyCards()
        {
            if (enemyGridRoot == null)
            {
                return;
            }

            var signature = $"{(int)ActiveVisibilityMode}|{(ShouldRevealLockedEnemyIdentity ? lockedEnemyId : string.Empty)}|" +
                            string.Join("|", enemyUnits.Take(6).Select(unit => unit.Id));
            if (string.Equals(signature, enemyLayoutSignature, StringComparison.Ordinal) &&
                enemyCards.Count == enemyUnits.Take(6).Count())
            {
                return;
            }

            enemyLayoutSignature = signature;
            enemyCards.Clear();

            for (var index = enemyGridRoot.childCount - 1; index >= 0; index--)
            {
                Destroy(enemyGridRoot.GetChild(index).gameObject);
            }

            foreach (var unit in enemyUnits.Take(6))
            {
                enemyCards.Add(CreateEnemyCardView(unit, enemyCards.Count));
            }
        }

        private EnemyCardView CreateEnemyCardView(UnitView unit, int slotIndex)
        {
            if (ShouldShowEnemyIdentity(unit))
            {
                return CreateVisibleEnemyCardView(unit);
            }

            return CreateMysteryEnemyCardView(unit, slotIndex);
        }

        private EnemyCardView CreateVisibleEnemyCardView(UnitView unit)
        {
            var card = RescueStickerFactory.CreateCompactUnitStickerTile(
                enemyGridRoot,
                unit.Name,
                unit.Id,
                unit.Role,
                unit.CardColor,
                unit.AuraColor,
                false,
                false,
                EnemyTileSize,
                0);
            var rect = card.GetComponent<RectTransform>();

            var layout = card.GetComponent<LayoutElement>() ?? card.AddComponent<LayoutElement>();
            layout.preferredWidth = EnemyTileSize.x;
            layout.preferredHeight = EnemyTileSize.y;
            layout.minWidth = EnemyTileSize.x;
            layout.minHeight = EnemyTileSize.y;

            var body = card.GetComponent<Image>();
            var group = card.GetComponent<CanvasGroup>() ?? card.AddComponent<CanvasGroup>();
            var outline = card.GetComponent<Outline>();
            var button = card.GetComponent<Button>() ?? card.AddComponent<Button>();
            button.transition = Selectable.Transition.None;

            var avatar = rect.Find("EmojiAvatar") as RectTransform;
            if (avatar != null)
            {
                NativeMotionKit.IdleBob(this, avatar, 5f, 1.16f, true);
                NativeMotionKit.BreatheScale(this, avatar, 0.024f, 1.34f, true);
            }

            var view = new EnemyCardView(unit, rect, body, group, outline, button, avatar);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleEnemyPressed(view));
            return view;
        }

        private EnemyCardView CreateMysteryEnemyCardView(UnitView unit, int slotIndex)
        {
            var card = new GameObject($"MysterySlot{slotIndex + 1}", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
            card.transform.SetParent(enemyGridRoot, false);

            var rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = EnemyTileSize;

            var layout = card.AddComponent<LayoutElement>();
            layout.preferredWidth = EnemyTileSize.x;
            layout.preferredHeight = EnemyTileSize.y;
            layout.minWidth = EnemyTileSize.x;
            layout.minHeight = EnemyTileSize.y;

            var surfaceSeed = RescueStickerFactory.CreateArenaSurface(card.transform, "MysteryTileVisualSeed", Color.clear, Color.clear, Vector2.zero);
            var roundedSprite = surfaceSeed.GetComponent<Image>().sprite;
            if (Application.isPlaying)
            {
                Destroy(surfaceSeed);
            }
            else
            {
                DestroyImmediate(surfaceSeed);
            }

            var group = card.GetComponent<CanvasGroup>();
            var body = card.GetComponent<Image>();
            body.sprite = roundedSprite;
            body.type = Image.Type.Sliced;
            body.color = Color.Lerp(MysteryCardColor, RescueStickerFactory.Palette.InkPurple, 0.10f);
            var outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(
                RescueStickerFactory.Palette.SoftWhite.r,
                RescueStickerFactory.Palette.SoftWhite.g,
                RescueStickerFactory.Palette.SoftWhite.b,
                0.86f);
            outline.effectDistance = new Vector2(2f, 2f);
            var shadow = card.AddComponent<Shadow>();
            shadow.effectColor = new Color(
                RescueStickerFactory.Palette.InkPurple.r,
                RescueStickerFactory.Palette.InkPurple.g,
                RescueStickerFactory.Palette.InkPurple.b,
                0.42f);
            shadow.effectDistance = new Vector2(0f, -3f);

            var avatar = CreateRect("MysteryAvatar", rect);
            avatar.sizeDelta = new Vector2(Mathf.Min(EnemyTileSize.x * 0.68f, EnemyTileSize.y * 0.72f), Mathf.Min(EnemyTileSize.x * 0.68f, EnemyTileSize.y * 0.72f));
            avatar.anchorMin = new Vector2(0.5f, 0.68f);
            avatar.anchorMax = new Vector2(0.5f, 0.68f);
            avatar.anchoredPosition = Vector2.zero;

            var glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
            glow.transform.SetParent(avatar, false);
            var glowRect = glow.GetComponent<RectTransform>();
            Stretch(glowRect);
            var glowImage = glow.GetComponent<Image>();
            glowImage.sprite = roundedSprite;
            glowImage.type = Image.Type.Sliced;
            glowImage.color = new Color(MysteryAuraColor.r, MysteryAuraColor.g, MysteryAuraColor.b, 0.28f);

            var sticker = new GameObject("StickerBase", typeof(RectTransform), typeof(Image));
            sticker.transform.SetParent(avatar, false);
            var stickerRect = sticker.GetComponent<RectTransform>();
            SetAnchors(stickerRect, new Vector2(0.09f, 0.09f), new Vector2(0.91f, 0.91f));
            var stickerImage = sticker.GetComponent<Image>();
            stickerImage.sprite = roundedSprite;
            stickerImage.type = Image.Type.Sliced;
            stickerImage.color = new Color(1f, 1f, 1f, 0.22f);

            var mysteryLabel = RescueStickerFactory.CreateLabel(
                avatar,
                "MysteryGlyph",
                "?",
                54f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.20f, 0.18f),
                new Vector2(0.80f, 0.82f));
            mysteryLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var hiddenLabel = RescueStickerFactory.CreateLabel(
                rect,
                "UnitName",
                "Hidden",
                14f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.145f),
                new Vector2(0.95f, 0.295f));
            hiddenLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var slotPill = new GameObject("RolePill", typeof(RectTransform), typeof(Image));
            slotPill.transform.SetParent(rect, false);
            var slotRect = slotPill.GetComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(EnemyTileSize.x * 0.52f, Mathf.Max(16f, EnemyTileSize.y * 0.135f));
            slotRect.anchorMin = new Vector2(0.5f, 0.082f);
            slotRect.anchorMax = new Vector2(0.5f, 0.082f);
            slotRect.anchoredPosition = Vector2.zero;
            var slotImage = slotPill.GetComponent<Image>();
            slotImage.sprite = roundedSprite;
            slotImage.type = Image.Type.Sliced;
            slotImage.color = Color.Lerp(MysteryAuraColor, RescueStickerFactory.Palette.SoftWhite, 0.18f);
            RescueStickerFactory.CreateLabel(
                slotPill.transform,
                "Role",
                $"Slot {slotIndex + 1}",
                9f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.InkPurple,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one);

            var button = card.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            NativeMotionKit.IdleBob(this, avatar, 5f, 1.16f, true);
            NativeMotionKit.BreatheScale(this, avatar, 0.024f, 1.34f, true);

            var view = new EnemyCardView(unit, rect, body, group, outline, button, avatar);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => HandleEnemyPressed(view));
            return view;
        }

        private void EnsurePlayerMinis()
        {
            if (playerRowRoot == null)
            {
                return;
            }

            var signature = string.Join("|", playerUnits.Take(6).Select(unit => unit.Id));
            if (string.Equals(signature, playerLayoutSignature, StringComparison.Ordinal) &&
                playerMiniViews.Count == playerUnits.Take(6).Count())
            {
                return;
            }

            playerLayoutSignature = signature;
            playerMiniViews.Clear();

            for (var index = playerRowRoot.childCount - 1; index >= 0; index--)
            {
                Destroy(playerRowRoot.GetChild(index).gameObject);
            }

            var visiblePlayers = playerUnits.Take(6).ToArray();
            for (var index = 0; index < visiblePlayers.Length; index++)
            {
                var unit = visiblePlayers[index];
                var mini = RescueStickerFactory.CreateMiniSquadSticker(
                    playerRowRoot,
                    unit.Name,
                    unit.Id,
                    unit.AuraColor,
                    PlayerMiniSize);
                AddFixedLayout(mini, PlayerMiniSize.x, PlayerMiniSize.y);

                var rect = mini.GetComponent<RectTransform>();
                var avatar = rect.Find("EmojiAvatar") as RectTransform;
                if (avatar != null)
                {
                    avatar.sizeDelta = new Vector2(PlayerMiniSize.x * 1.02f, PlayerMiniSize.y * 0.88f);
                }

                NativeMotionKit.PopIn(this, rect, null, 0.20f + index * 0.015f, 0.78f);
                playerMiniViews.Add(new PlayerMiniView(unit, rect, mini.GetComponent<Image>(), avatar));
            }
        }

        private void RefreshHeader()
        {
            if (titleLabel != null)
            {
                titleLabel.text = "Blind Ban";
            }

            if (statusCopyLabel != null)
            {
                statusCopyLabel.text = HasBanResults
                    ? "Bans revealed"
                    : IsLocked
                    ? IsProductionHiddenMode
                        ? BuildLockedRevealCopy()
                        : "Testing reveal: waiting for opponent..."
                    : IsProductionHiddenMode
                        ? "Enemy squad hidden"
                        : "Testing reveal: opponent shown";
            }

            if (timerChipLabel != null)
            {
                timerChipLabel.text = HasBanResults ? "REVEALED" : IsLocked ? "LOCKED" : $"{Mathf.Max(1, remainingSeconds)}s";
            }
        }

        private void RefreshEnemyStates()
        {
            foreach (var view in enemyCards)
            {
                RefreshEnemyCardState(view);
            }

            if (enemyTitleLabel != null)
            {
                enemyTitleLabel.text = IsProductionHiddenMode
                    ? "Hidden Enemy Squad"
                    : "Enemy Squad · Test Reveal";
            }

            if (enemyHintLabel != null)
            {
                enemyHintLabel.text = HasBanResults
                    ? BuildBanResultsHint()
                    : IsLocked
                    ? IsProductionHiddenMode
                        ? BuildLockedRevealHint()
                        : "Testing reveal active"
                    : IsProductionHiddenMode
                        ? "Tap a mystery slot"
                        : "Tap a target";
            }

            if (testingRevealChip != null)
            {
                testingRevealChip.SetActive(IsTestingRevealMode);
            }
        }

        private void RefreshEnemyCardState(EnemyCardView view)
        {
            var isSelected = !IsLocked && string.Equals(selectedEnemyId, view.Unit.Id, StringComparison.Ordinal);
            var isLockedTarget = string.Equals(lockedEnemyId, view.Unit.Id, StringComparison.Ordinal);
            var isDisabled = IsLocked && !isLockedTarget;

            if (view.CanvasGroup != null)
            {
                view.CanvasGroup.alpha = isDisabled ? 0.48f : 1f;
            }

            if (view.Body != null)
            {
                view.Body.color = isLockedTarget
                    ? Color.Lerp(view.Unit.CardColor, RescueStickerFactory.Palette.Coral, 0.40f)
                    : isSelected
                        ? Color.Lerp(view.Unit.CardColor, view.Unit.AuraColor, 0.30f)
                        : Color.Lerp(view.Unit.CardColor, RescueStickerFactory.Palette.InkPurple, 0.10f);
            }

            if (view.Outline != null)
            {
                view.Outline.effectColor = isLockedTarget
                    ? RescueStickerFactory.Palette.Coral
                    : isSelected
                        ? RescueStickerFactory.Palette.SunnyYellow
                        : new Color(
                            RescueStickerFactory.Palette.SoftWhite.r,
                            RescueStickerFactory.Palette.SoftWhite.g,
                            RescueStickerFactory.Palette.SoftWhite.b,
                            0.86f);
                var outlineSize = isLockedTarget ? 4.75f : isSelected ? 4.5f : 2f;
                view.Outline.effectDistance = new Vector2(outlineSize, outlineSize);
            }

            if (view.Button != null)
            {
                view.Button.interactable = !IsLocked;
            }

            SetSelectedBadge(view.Rect, isSelected);
            SetDisabledOverlay(view.Rect, isDisabled);
            SetBanStamp(view, isLockedTarget);
        }

        private void RefreshVsState()
        {
            if (vsChip != null)
            {
                vsChip.SetActive(!HasBanResults);
            }

            if (banResultsRoot != null)
            {
                banResultsRoot.SetActive(HasBanResults);
            }

            if (vsInstructionLabel != null)
            {
                vsInstructionLabel.gameObject.SetActive(!HasBanResults);
            }

            if (resultsTitleLabel != null)
            {
                resultsTitleLabel.text = "BAN RESULTS";
            }

            if (yourBanResultLabel != null)
            {
                yourBanResultLabel.text = BuildYourBanResultText();
            }

            if (opponentBanResultLabel != null)
            {
                opponentBanResultLabel.text = BuildOpponentBanResultText();
            }

            if (vsInstructionLabel != null)
            {
                vsInstructionLabel.text = IsLocked
                    ? IsProductionHiddenMode
                        ? BuildLockedRevealInstruction()
                        : "Ban locked. Both squads stay visible."
                    : IsProductionHiddenMode
                        ? "Pick 1 mystery slot. Reveals after both bans lock."
                        : "Tap one enemy sticker to ban";
            }
        }

        private void RefreshPlayerState()
        {
            if (playerReadyLabel != null)
            {
                playerReadyLabel.text = $"{Mathf.Min(6, playerUnits.Count)}/6 READY";
            }

            foreach (var mini in playerMiniViews)
            {
                SetOpponentBanMark(mini.Rect, string.Equals(opponentBanId, mini.Unit.Id, StringComparison.Ordinal));
            }
        }

        private void RefreshLockState()
        {
            if (lockButton == null)
            {
                return;
            }

            var selectedReady = !string.IsNullOrWhiteSpace(selectedEnemyId);
            var actionable = selectedReady && !IsLocked;
            var label = IsLocked ? "Ban Locked" : actionable ? "Lock Ban" : IsProductionHiddenMode ? "Choose Slot" : "Choose Enemy";
            var bodyColor = IsLocked
                ? RescueStickerFactory.Palette.Mint
                : actionable
                    ? RescueStickerFactory.Palette.HotPink
                    : new Color(0.50f, 0.43f, 0.72f, 0.92f);
            var textColor = IsLocked
                ? RescueStickerFactory.Palette.InkPurple
                : RescueStickerFactory.Palette.SoftWhite;

            lockButton.interactable = actionable;

            var body = lockButton.GetComponent<Image>();
            if (body != null)
            {
                body.color = bodyColor;
            }

            if (lockButtonLabel != null)
            {
                lockButtonLabel.text = label;
                lockButtonLabel.color = textColor;
            }

            var highlight = lockButton.transform.Find("Highlight");
            if (highlight != null)
            {
                var image = highlight.GetComponent<Image>();
                if (image != null)
                {
                    image.color = actionable
                        ? new Color(1f, 1f, 1f, 0.18f)
                        : new Color(1f, 1f, 1f, 0.10f);
                }
            }

            var outline = lockButton.GetComponent<Outline>();
            if (outline != null)
            {
                outline.effectColor = actionable
                    ? RescueStickerFactory.Palette.SunnyYellow
                    : RescueStickerFactory.Palette.SoftWhite;
                var outlineSize = actionable ? 4f : 2.5f;
                outline.effectDistance = new Vector2(outlineSize, outlineSize);
            }

            var rect = lockButton.transform as RectTransform;
            if (actionable && !lockButtonPulseActive)
            {
                NativeMotionKit.BreatheScale(this, rect, 0.022f, 1.08f, true);
                lockButtonPulseActive = true;
            }
            else if (!actionable && lockButtonPulseActive)
            {
                NativeMotionKit.BreatheScale(this, rect, 0f, 1.08f, false);
                lockButtonPulseActive = false;
            }

            if (!actionable && rect != null)
            {
                rect.localScale = Vector3.one;
            }
        }

        private void HandleEnemyPressed(EnemyCardView view)
        {
            if (IsLocked || view.Rect == null)
            {
                return;
            }

            selectedEnemyId = view.Unit.Id;
            if (view.Avatar != null)
            {
                NativeMotionKit.PunchScale(this, view.Avatar, 0.14f, 0.16f);
            }

            RefreshEnemyStates();
            RefreshLockState();
            onTargetChanged?.Invoke(view.Unit.Id);
        }

        private void HandleLockPressed()
        {
            if (IsLocked || string.IsNullOrWhiteSpace(selectedEnemyId))
            {
                return;
            }

            var rect = lockButton.transform as RectTransform;
            NativeMotionKit.PunchScale(this, rect, 0.08f, 0.15f);
            onBanLocked?.Invoke(selectedEnemyId);
        }

        private bool ShouldShowEnemyIdentity(UnitView unit)
        {
            return IsTestingRevealMode ||
                   (ShouldRevealLockedEnemyIdentity &&
                    string.Equals(unit.Id, lockedEnemyId, StringComparison.Ordinal));
        }

        private string BuildLockedRevealCopy()
        {
            return "Ban locked. Enemy stays hidden until reveal.";
        }

        private string BuildLockedRevealHint()
        {
            return "Reveals after both bans lock";
        }

        private string BuildLockedRevealInstruction()
        {
            return "Mystery slot locked. Waiting for opponent...";
        }

        private string BuildBanResultsHint()
        {
            return "Both bans resolved";
        }

        private string BuildYourBanResultText()
        {
            return TryGetLockedEnemy(out var lockedUnit)
                ? $"You banned: {lockedUnit.Name}"
                : "You banned: Pending";
        }

        private string BuildOpponentBanResultText()
        {
            return TryGetPlayerBannedUnit(out var bannedUnit)
                ? $"Opponent banned: {bannedUnit.Name}"
                : "Opponent banned: Pending";
        }

        private bool TryGetLockedEnemy(out UnitView lockedUnit)
        {
            foreach (var unit in enemyUnits)
            {
                if (string.Equals(unit.Id, lockedEnemyId, StringComparison.Ordinal))
                {
                    lockedUnit = unit;
                    return true;
                }
            }

            lockedUnit = default;
            return false;
        }

        private bool TryGetPlayerBannedUnit(out UnitView bannedUnit)
        {
            foreach (var unit in playerUnits)
            {
                if (string.Equals(unit.Id, opponentBanId, StringComparison.Ordinal))
                {
                    bannedUnit = unit;
                    return true;
                }
            }

            bannedUnit = default;
            return false;
        }

        private void SetSelectedBadge(RectTransform card, bool isSelected)
        {
            if (card == null)
            {
                return;
            }

            var existing = card.Find("SelectedOrderBadge");
            if (!isSelected)
            {
                if (existing != null)
                {
                    Destroy(existing.gameObject);
                }

                return;
            }

            if (existing != null)
            {
                return;
            }

            var badge = RescueStickerFactory.CreateStatusChip(
                card,
                "1",
                RescueStickerFactory.Palette.SunnyYellow,
                RescueStickerFactory.Palette.InkPurple);
            badge.name = "SelectedOrderBadge";
            var badgeRect = badge.GetComponent<RectTransform>();
            SetAnchors(badgeRect, new Vector2(0.80f, 0.82f), new Vector2(0.97f, 0.98f));
        }

        private void SetDisabledOverlay(RectTransform card, bool isDisabled)
        {
            if (card == null)
            {
                return;
            }

            var existing = card.Find("DisabledOverlay");
            if (!isDisabled)
            {
                if (existing != null)
                {
                    Destroy(existing.gameObject);
                }

                return;
            }

            if (existing != null)
            {
                return;
            }

            var overlayObject = new GameObject("DisabledOverlay", typeof(RectTransform), typeof(Image));
            overlayObject.transform.SetParent(card, false);
            overlayObject.transform.SetAsLastSibling();
            var overlayRect = overlayObject.GetComponent<RectTransform>();
            Stretch(overlayRect);
            var overlayImage = overlayObject.GetComponent<Image>();
            overlayImage.sprite = card.GetComponent<Image>() != null ? card.GetComponent<Image>().sprite : null;
            overlayImage.type = Image.Type.Sliced;
            overlayImage.color = new Color(
                RescueStickerFactory.Palette.InkPurple.r,
                RescueStickerFactory.Palette.InkPurple.g,
                RescueStickerFactory.Palette.InkPurple.b,
                0.38f);
            overlayImage.raycastTarget = false;
        }

        private void SetBanStamp(EnemyCardView view, bool isLockedTarget)
        {
            if (view.Rect == null)
            {
                return;
            }

            var existing = view.Rect.Find("BanStamp");
            if (!isLockedTarget)
            {
                if (existing != null)
                {
                    Destroy(existing.gameObject);
                }

                view.LockVisualApplied = false;
                return;
            }

            if (existing == null)
            {
                var stamp = RescueStickerFactory.CreateStatusChip(
                    view.Rect,
                    "BANNED",
                    RescueStickerFactory.Palette.Coral,
                    RescueStickerFactory.Palette.SoftWhite);
                stamp.name = "BanStamp";
                var stampRect = stamp.GetComponent<RectTransform>();
                SetAnchors(stampRect, new Vector2(0.13f, 0.40f), new Vector2(0.87f, 0.60f));
                stampRect.localRotation = Quaternion.Euler(0f, 0f, -11f);
                stampRect.SetAsLastSibling();

                if (!view.LockVisualApplied)
                {
                    NativeMotionKit.StampSlam(this, stampRect, 1.20f, 0.22f);
                    NativeMotionKit.Shake(this, stampRect, 4f, 0.18f);
                    if (view.Avatar != null)
                    {
                        NativeMotionKit.PunchScale(this, view.Avatar, 0.10f, 0.18f);
                    }

                    if (view.Body != null)
                    {
                        NativeMotionKit.PulseGraphic(
                            this,
                            view.Body,
                            view.Body.color,
                            Color.Lerp(view.Body.color, RescueStickerFactory.Palette.Coral, 0.24f),
                            0.95f);
                    }

                    view.LockVisualApplied = true;
                }

                return;
            }

            if (!view.LockVisualApplied)
            {
                var stampRect = existing as RectTransform;
                if (stampRect != null)
                {
                    NativeMotionKit.StampSlam(this, stampRect, 1.20f, 0.22f);
                }

                view.LockVisualApplied = true;
            }
        }

        private void SetOpponentBanMark(RectTransform mini, bool show)
        {
            if (mini == null)
            {
                return;
            }

            var existing = mini.Find("OpponentBanMark");
            if (!show)
            {
                if (existing != null)
                {
                    Destroy(existing.gameObject);
                }

                return;
            }

            if (existing != null)
            {
                var text = existing.GetComponentInChildren<TMP_Text>(true);
                if (text != null)
                {
                    text.text = "BAN";
                }
                return;
            }

            var mark = RescueStickerFactory.CreateStatusChip(
                mini,
                "BAN",
                RescueStickerFactory.Palette.Coral,
                RescueStickerFactory.Palette.SoftWhite);
            mark.name = "OpponentBanMark";
            SetAnchors(mark.GetComponent<RectTransform>(), new Vector2(0.62f, 0.56f), new Vector2(0.96f, 0.94f));
        }

        private BlindBanVisibilityMode ResolveVisibilityMode(BlindBanVisibilityMode? requestedVisibilityMode)
        {
            if (requestedVisibilityMode.HasValue)
            {
                return requestedVisibilityMode.Value;
            }

            return Application.isEditor
                ? visibilityMode
                : BlindBanVisibilityMode.ProductionHiddenOpponentCards;
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
                NativeMotionKit.SlideFadeIn(this, target, group, new Vector2(0f, -22f), 0.28f + index * 0.025f);
            }
        }

        private void AddEnterTarget(RectTransform target)
        {
            if (target != null)
            {
                enterTargets.Add(target);
            }
        }

        private static void PolishButton(Button button, float labelSize)
        {
            if (button == null)
            {
                return;
            }

            var shadow = button.transform.Find("Shadow");
            if (shadow != null)
            {
                shadow.gameObject.SetActive(false);
            }

            var highlight = button.transform.Find("Highlight");
            if (highlight != null)
            {
                SetAnchors(highlight as RectTransform, new Vector2(0.10f, 0.70f), new Vector2(0.90f, 0.86f));
                var image = highlight.GetComponent<Image>();
                if (image != null)
                {
                    image.color = new Color(1f, 1f, 1f, 0.14f);
                }
            }

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSize = labelSize;
                label.alignment = TextAlignmentOptions.Center;
            }
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

        private static string NormalizeOptionalKey(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : UnitIconLibrary.NormalizeUnitKey(value);
        }

        private sealed class EnemyCardView
        {
            public EnemyCardView(
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
            public bool LockVisualApplied { get; set; }
        }

        private sealed class PlayerMiniView
        {
            public PlayerMiniView(UnitView unit, RectTransform rect, Image body, RectTransform avatar)
            {
                Unit = unit;
                Rect = rect;
                Body = body;
                Avatar = avatar;
            }

            public UnitView Unit { get; }
            public RectTransform Rect { get; }
            public Image Body { get; }
            public RectTransform Avatar { get; }
        }
    }
}
