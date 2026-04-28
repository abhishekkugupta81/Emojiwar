using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Content;
using EmojiWar.Client.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.DeckBuilder
{
    /// <summary>
    /// Golden rescue implementation for the Squad Builder only.
    /// It mounts over the legacy DeckBuilder scene UI and delegates actual
    /// selection/save flow back to DeckBuilderController.
    /// </summary>
    public sealed class SquadBuilderRescueScreen : MonoBehaviour
    {
        private enum SquadFilter
        {
            All,
            Burst,
            Control,
            Support
        }

        private const float TapRefreshDelay = 0.08f;
        private static readonly Vector2 CompactTileMinSize = new(124f, 96f);
        private static readonly Vector2 CompactTileMaxSize = new(242f, 156f);
        private static readonly Vector2 MiniSlotSize = new(124f, 116f);

        private readonly List<UnitView> roster = new();
        private readonly Dictionary<SquadFilter, Button> filterButtons = new();

        private DeckBuilderController controller;
        private RectTransform root;
        private RectTransform contentRoot;
        private RectTransform headerRoot;
        private RectTransform filterRow;
        private RectTransform rosterViewport;
        private RectTransform rosterContent;
        private GridLayoutGroup rosterGrid;
        private RectTransform selectedTrayRow;
        private Image progressChipImage;
        private TMP_Text progressChipLabel;
        private TMP_Text trayCountLabel;
        private Button continueButton;
        private TMP_Text continueLabel;
        private SquadFilter activeFilter = SquadFilter.All;
        private Coroutine delayedRosterRefresh;

        public void Initialize(DeckBuilderController owner)
        {
            controller = owner;
            root = transform as RectTransform;
            if (root == null)
            {
                root = gameObject.AddComponent<RectTransform>();
            }

            RescueStickerFactory.Stretch(root);
            BuildRosterAdapter();
            BuildLayout();
            RefreshAll();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            RefreshAll();
        }

        public void Hide()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
        }

        public void RefreshFromController()
        {
            RefreshAll();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        private void BuildLayout()
        {
            ClearChildren(root);
            filterButtons.Clear();

            RescueStickerFactory.CreateGradientLikeBackground(
                root,
                "SquadToyBoxGradient",
                RescueStickerFactory.Palette.ElectricPurple,
                RescueStickerFactory.Palette.Mint);

            contentRoot = CreateRect("SquadBuilderRescueContent", root);
            RescueStickerFactory.Stretch(contentRoot);

            CreateRescueHeader();

            CreateFilterRow();
            CreateRosterArena();
            CreateSelectedTray();
            CreateContinueButton();
        }

        private void CreateRescueHeader()
        {
            headerRoot = CreateRect("SquadBuilderHeader", contentRoot);
            headerRoot.anchorMin = new Vector2(0.06f, 0.855f);
            headerRoot.anchorMax = new Vector2(0.94f, 0.978f);
            headerRoot.offsetMin = Vector2.zero;
            headerRoot.offsetMax = Vector2.zero;

            var stepChip = RescueStickerFactory.CreateStatusChip(
                headerRoot,
                "STEP 1 OF 4",
                RescueStickerFactory.Palette.SunnyYellow,
                RescueStickerFactory.Palette.InkPurple);
            var stepRect = stepChip.GetComponent<RectTransform>();
            stepRect.anchorMin = new Vector2(0f, 0.72f);
            stepRect.anchorMax = new Vector2(0.33f, 1f);
            stepRect.offsetMin = Vector2.zero;
            stepRect.offsetMax = Vector2.zero;

            RescueStickerFactory.CreateLabel(
                headerRoot,
                "Title",
                controller != null ? controller.RescueTitle : "Build Your Squad",
                36f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.24f),
                new Vector2(0.76f, 0.74f));

            RescueStickerFactory.CreateLabel(
                headerRoot,
                "Subtitle",
                controller != null ? controller.RescueSubtitle : "Pick 6 sticker fighters",
                17f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.02f),
                new Vector2(0.72f, 0.27f));

            var progressChip = RescueStickerFactory.CreateStatusChip(
                headerRoot,
                ProgressText(),
                RescueStickerFactory.Palette.InkPurple,
                RescueStickerFactory.Palette.Mint);
            var progressRect = progressChip.GetComponent<RectTransform>();
            progressRect.anchorMin = new Vector2(0.60f, 0.62f);
            progressRect.anchorMax = new Vector2(1f, 0.98f);
            progressRect.offsetMin = Vector2.zero;
            progressRect.offsetMax = Vector2.zero;
            progressChipImage = progressChip.GetComponent<Image>();
            progressChipLabel = progressChip.GetComponentInChildren<TMP_Text>(true);
        }

        private void CreateFilterRow()
        {
            filterRow = CreateRect("FilterRow", contentRoot);
            filterRow.anchorMin = new Vector2(0.055f, 0.786f);
            filterRow.anchorMax = new Vector2(0.945f, 0.845f);
            filterRow.offsetMin = Vector2.zero;
            filterRow.offsetMax = Vector2.zero;

            var layout = filterRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            CreateFilterButton(SquadFilter.All, "All", 96f);
            CreateFilterButton(SquadFilter.Burst, "Burst", 120f);
            CreateFilterButton(SquadFilter.Control, "Control", 142f);
            CreateFilterButton(SquadFilter.Support, "Support", 146f);
        }

        private void CreateFilterButton(SquadFilter filter, string label, float width)
        {
            var active = filter == activeFilter;
            var button = RescueStickerFactory.CreateToyButton(
                filterRow,
                label,
                active ? RescueStickerFactory.Palette.SunnyYellow : RescueStickerFactory.Palette.ElectricPurple,
                active ? RescueStickerFactory.Palette.InkPurple : RescueStickerFactory.Palette.SoftWhite,
                new Vector2(width, 48f),
                primary: active);

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (activeFilter == filter)
                {
                    NativeMotionKit.PunchScale(this, button.transform as RectTransform, 0.08f, 0.16f);
                    return;
                }

                activeFilter = filter;
                RefreshFilterButtons();
                RefreshRoster();
            });

            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = 48f;
            layout.minWidth = width;
            layout.minHeight = 48f;
            filterButtons[filter] = button;
        }

        private void CreateRosterArena()
        {
            var arena = RescueStickerFactory.CreateArenaSurface(
                contentRoot,
                "RosterToyBoxArena",
                new Color(1f, 1f, 1f, 0.11f),
                RescueStickerFactory.Palette.Aqua,
                Vector2.zero);
            var arenaRect = arena.GetComponent<RectTransform>();
            arenaRect.anchorMin = new Vector2(0.020f, 0.300f);
            arenaRect.anchorMax = new Vector2(0.980f, 0.782f);
            arenaRect.offsetMin = Vector2.zero;
            arenaRect.offsetMax = Vector2.zero;

            RescueStickerFactory.CreateBlob(
                arena.transform,
                "RosterPinkStickerBlob",
                RescueStickerFactory.Palette.HotPink,
                new Vector2(-245f, 120f),
                new Vector2(145f, 145f),
                0.13f);
            RescueStickerFactory.CreateBlob(
                arena.transform,
                "RosterMintStickerBlob",
                RescueStickerFactory.Palette.Mint,
                new Vector2(250f, -120f),
                new Vector2(175f, 175f),
                0.12f);

            var title = RescueStickerFactory.CreateLabel(
                arena.transform,
                "RosterTitle",
                "Sticker board",
                21f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.045f, 0.900f),
                new Vector2(0.55f, 0.985f));
            title.textWrappingMode = TextWrappingModes.NoWrap;

            var hint = RescueStickerFactory.CreateLabel(
                arena.transform,
                "RosterHint",
                "Collect 6",
                16f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SunnyYellow,
                TextAlignmentOptions.Right,
                new Vector2(0.44f, 0.900f),
                new Vector2(0.955f, 0.985f));
            hint.textWrappingMode = TextWrappingModes.NoWrap;

            rosterViewport = CreateRect("RosterViewport", arenaRect);
            rosterViewport.anchorMin = new Vector2(0.008f, 0.018f);
            rosterViewport.anchorMax = new Vector2(0.992f, 0.895f);
            rosterViewport.offsetMin = Vector2.zero;
            rosterViewport.offsetMax = Vector2.zero;

            rosterContent = CreateRect("RosterGridContent", rosterViewport);
            rosterContent.anchorMin = Vector2.zero;
            rosterContent.anchorMax = Vector2.one;
            rosterContent.pivot = new Vector2(0.5f, 0.5f);
            rosterContent.offsetMin = Vector2.zero;
            rosterContent.offsetMax = Vector2.zero;

            rosterGrid = rosterContent.gameObject.AddComponent<GridLayoutGroup>();
            rosterGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            rosterGrid.constraintCount = 3;
            rosterGrid.cellSize = CompactTileMaxSize;
            rosterGrid.spacing = new Vector2(5f, 4f);
            rosterGrid.childAlignment = TextAnchor.MiddleCenter;
            rosterGrid.padding = new RectOffset(2, 2, 2, 2);
        }

        private void CreateSelectedTray()
        {
            var tray = RescueStickerFactory.CreateArenaSurface(
                contentRoot,
                "SelectedSquadStickerTray",
                new Color(0.98f, 0.97f, 1f, 0.24f),
                RescueStickerFactory.Palette.HotPink,
                Vector2.zero);
            var trayRect = tray.GetComponent<RectTransform>();
            trayRect.anchorMin = new Vector2(0.030f, 0.132f);
            trayRect.anchorMax = new Vector2(0.970f, 0.297f);
            trayRect.offsetMin = Vector2.zero;
            trayRect.offsetMax = Vector2.zero;

            RescueStickerFactory.CreateLabel(
                tray.transform,
                "TrayTitle",
                "Selected Squad",
                19f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.045f, 0.70f),
                new Vector2(0.55f, 0.96f));

            trayCountLabel = RescueStickerFactory.CreateLabel(
                tray.transform,
                "TrayCount",
                ProgressText(),
                16f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SunnyYellow,
                TextAlignmentOptions.Right,
                new Vector2(0.60f, 0.70f),
                new Vector2(0.955f, 0.96f));

            selectedTrayRow = CreateRect("SelectedSquadSlots", trayRect);
            selectedTrayRow.anchorMin = new Vector2(0.006f, 0.04f);
            selectedTrayRow.anchorMax = new Vector2(0.994f, 0.72f);
            selectedTrayRow.offsetMin = Vector2.zero;
            selectedTrayRow.offsetMax = Vector2.zero;

            var layout = selectedTrayRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private void CreateContinueButton()
        {
            continueButton = RescueStickerFactory.CreateToyButton(
                contentRoot,
                "Continue",
                RescueStickerFactory.Palette.HotPink,
                RescueStickerFactory.Palette.SoftWhite,
                Vector2.zero,
                primary: true);

            var rect = continueButton.transform as RectTransform;
            rect.anchorMin = new Vector2(0.08f, 0.035f);
            rect.anchorMax = new Vector2(0.92f, 0.112f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            continueLabel = continueButton.GetComponentInChildren<TMP_Text>(true);
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(HandleContinue);
        }

        private void RefreshAll()
        {
            if (controller == null)
            {
                return;
            }

            RefreshHeader();
            RefreshFilterButtons();
            RefreshRoster();
            RefreshSelectedTray();
            RefreshContinueState();
            NativeMotionKit.StaggerChildrenPopIn(this, contentRoot, 0.035f);
        }

        private void RefreshHeader()
        {
            if (progressChipLabel != null)
            {
                progressChipLabel.text = ProgressText();
                progressChipLabel.color = IsComplete
                    ? RescueStickerFactory.Palette.InkPurple
                    : RescueStickerFactory.Palette.SunnyYellow;
            }

            if (progressChipImage != null)
            {
                progressChipImage.color = IsComplete
                    ? RescueStickerFactory.Palette.Mint
                    : RescueStickerFactory.Palette.InkPurple;
            }

            if (trayCountLabel != null)
            {
                trayCountLabel.text = ProgressText();
                trayCountLabel.color = IsComplete
                    ? RescueStickerFactory.Palette.Mint
                    : RescueStickerFactory.Palette.SunnyYellow;
            }
        }

        private void RefreshFilterButtons()
        {
            foreach (var pair in filterButtons)
            {
                var active = pair.Key == activeFilter;
                var image = pair.Value != null ? pair.Value.targetGraphic as Image : null;
                if (image != null)
                {
                    image.color = active
                        ? RescueStickerFactory.Palette.SunnyYellow
                        : RescueStickerFactory.Palette.ElectricPurple;
                }

                var label = pair.Value != null ? pair.Value.GetComponentInChildren<TMP_Text>(true) : null;
                if (label != null)
                {
                    label.color = active
                        ? RescueStickerFactory.Palette.InkPurple
                        : RescueStickerFactory.Palette.SoftWhite;
                }
            }
        }

        private void RefreshRoster()
        {
            if (rosterContent == null)
            {
                return;
            }

            ClearChildren(rosterContent);

            var selected = SelectedSet;
            var selectedList = controller.SelectedEmojis.ToList();
            var selectionFull = selected.Count >= controller.RequiredSelectionCount;
            var visibleUnits = roster.Where(UnitMatchesFilter).ToList();
            var tileSize = ConfigureRosterGrid(visibleUnits.Count);
            foreach (var unit in visibleUnits)
            {
                var isSelected = selected.Contains(unit.Id);
                var disabled = selectionFull && !isSelected;
                var selectedOrder = isSelected ? selectedList.IndexOf(unit.Id) + 1 : 0;
                var card = RescueStickerFactory.CreateCompactUnitStickerTile(
                    rosterContent,
                    unit.Name,
                    unit.Emoji,
                    unit.Role,
                    unit.CardColor,
                    unit.AuraColor,
                    isSelected,
                    disabled,
                    tileSize,
                    selectedOrder);

                var rect = card.GetComponent<RectTransform>();
                var layout = card.AddComponent<LayoutElement>();
                layout.preferredWidth = tileSize.x;
                layout.preferredHeight = tileSize.y;
                layout.minWidth = tileSize.x;
                layout.minHeight = tileSize.y;

                var button = card.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.interactable = !disabled || isSelected;
                button.onClick.AddListener(() => ToggleUnitSelection(unit, rect));

                var body = card.GetComponent<Image>();
                if (isSelected && body != null)
                {
                    NativeMotionKit.PulseGraphic(
                        this,
                        body,
                        body.color,
                        Color.Lerp(body.color, RescueStickerFactory.Palette.SunnyYellow, 0.28f),
                        0.72f);
                }

                var avatar = card.transform.Find("EmojiAvatar") as RectTransform;
                if (avatar != null)
                {
                    NativeMotionKit.IdleBob(this, avatar, 5f, 1.05f, true);
                    NativeMotionKit.BreatheScale(this, avatar, 0.034f, 1.22f, true);
                }
            }

            NativeMotionKit.StaggerChildrenPopIn(this, rosterContent, 0.030f);
        }

        private Vector2 ConfigureRosterGrid(int visibleCount)
        {
            if (rosterGrid == null)
            {
                return CompactTileMinSize;
            }

            var count = Mathf.Max(1, visibleCount);
            var columns = count <= 4 ? count : count <= 12 ? 3 : count <= 16 ? 4 : 5;
            var rows = Mathf.CeilToInt(count / (float)columns);
            var spacingX = columns >= 4 ? 4f : 7f;
            var spacingY = rows >= 4 ? 3f : 6f;
            var paddingX = columns >= 4 ? 2 : 4;
            var paddingY = rows >= 4 ? 2 : 4;

            var viewportWidth = rosterViewport != null ? rosterViewport.rect.width : 0f;
            var viewportHeight = rosterViewport != null ? rosterViewport.rect.height : 0f;
            var screenWidth = root != null && root.rect.width > 100f ? root.rect.width : 720f;
            var screenHeight = root != null && root.rect.height > 100f ? root.rect.height : 1280f;
            if (viewportWidth < 100f)
            {
                viewportWidth = screenWidth * 0.96f * 0.984f;
            }

            if (viewportHeight < 100f)
            {
                viewportHeight = screenHeight * 0.482f * 0.877f;
            }

            var tileWidth = (viewportWidth - paddingX * 2f - spacingX * (columns - 1)) / columns;
            var tileHeight = (viewportHeight - paddingY * 2f - spacingY * (rows - 1)) / rows;
            var tileSize = new Vector2(
                Mathf.Floor(Mathf.Clamp(tileWidth, CompactTileMinSize.x, CompactTileMaxSize.x)),
                Mathf.Floor(Mathf.Clamp(tileHeight, CompactTileMinSize.y, CompactTileMaxSize.y)));

            rosterGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            rosterGrid.constraintCount = columns;
            rosterGrid.cellSize = tileSize;
            rosterGrid.spacing = new Vector2(spacingX, spacingY);
            rosterGrid.padding = new RectOffset(paddingX, paddingX, paddingY, paddingY);
            rosterGrid.childAlignment = TextAnchor.MiddleCenter;
            return tileSize;
        }

        private void RefreshSelectedTray()
        {
            if (selectedTrayRow == null || controller == null)
            {
                return;
            }

            ClearChildren(selectedTrayRow);
            var selected = controller.SelectedEmojis.ToList();
            for (var index = 0; index < controller.RequiredSelectionCount; index++)
            {
                if (index < selected.Count)
                {
                    var emojiId = selected[index];
                    var color = ResolveAuraColor(EmojiUiFormatter.BuildRoleTag(emojiId));
                    var mini = RescueStickerFactory.CreateMiniSquadSticker(
                        selectedTrayRow,
                        EmojiIdUtility.ToDisplayName(emojiId),
                        EmojiIdUtility.ToDisplayName(emojiId),
                        color,
                        MiniSlotSize);
                    AddFixedLayout(mini, MiniSlotSize.x, MiniSlotSize.y);

                    var button = mini.AddComponent<Button>();
                    button.transition = Selectable.Transition.None;
                    button.onClick.AddListener(() =>
                    {
                        controller.ToggleEmojiForRescue(emojiId);
                        RefreshAll();
                    });

                    NativeMotionKit.PopIn(this, mini.transform as RectTransform, mini.GetComponent<CanvasGroup>(), 0.24f, 0.76f);
                    continue;
                }

                CreateEmptySquadSlot(index + 1);
            }
        }

        private void RefreshContinueState()
        {
            if (continueButton == null || controller == null)
            {
                return;
            }

            var enabled = controller.CanContinueFromRescue;
            continueButton.interactable = enabled;

            var image = continueButton.targetGraphic as Image;
            if (image != null)
            {
                image.color = enabled
                    ? Color.Lerp(RescueStickerFactory.Palette.HotPink, RescueStickerFactory.Palette.Coral, 0.12f)
                    : new Color(0.50f, 0.43f, 0.72f, 0.92f);
            }

            if (continueLabel != null)
            {
                continueLabel.text = controller.RescueContinueLabel;
                continueLabel.color = enabled
                    ? RescueStickerFactory.Palette.SoftWhite
                    : new Color(1f, 1f, 1f, 0.58f);
            }

            var rect = continueButton.transform as RectTransform;
            if (enabled)
            {
                NativeMotionKit.BreatheScale(this, rect, 0.028f, 1.05f, true);
            }

        }

        private void ToggleUnitSelection(UnitView unit, RectTransform cardRect)
        {
            if (controller == null)
            {
                return;
            }

            var changed = controller.ToggleEmojiForRescue(unit.Id);
            if (!changed)
            {
                NativeMotionKit.Shake(this, cardRect, 12f, 0.20f);
                NativeMotionKit.Shake(this, selectedTrayRow, 10f, 0.20f);
                return;
            }

            NativeMotionKit.PunchScale(this, cardRect, 0.18f, 0.22f);
            var avatar = cardRect != null ? cardRect.Find("EmojiAvatar") as RectTransform : null;
            if (avatar != null)
            {
                NativeMotionKit.PunchScale(this, avatar, 0.22f, 0.20f);
            }

            RefreshHeader();
            RefreshSelectedTray();
            RefreshContinueState();

            if (delayedRosterRefresh != null)
            {
                StopCoroutine(delayedRosterRefresh);
            }

            delayedRosterRefresh = StartCoroutine(RefreshRosterAfterTap());
        }

        private IEnumerator RefreshRosterAfterTap()
        {
            yield return new WaitForSecondsRealtime(TapRefreshDelay);
            RefreshRoster();
            delayedRosterRefresh = null;
        }

        private void HandleContinue()
        {
            if (controller == null)
            {
                return;
            }

            if (!controller.CanContinueFromRescue)
            {
                NativeMotionKit.Shake(this, continueButton.transform as RectTransform, 9f, 0.18f);
                return;
            }

            NativeMotionKit.PunchScale(this, continueButton.transform as RectTransform, 0.08f, 0.16f);
            controller.ContinueFromRescue();
        }

        private void CreateEmptySquadSlot(int slotNumber)
        {
            var slot = RescueStickerFactory.CreateArenaSurface(
                selectedTrayRow,
                $"EmptySquadSlot{slotNumber}",
                new Color(1f, 1f, 1f, 0.14f),
                RescueStickerFactory.Palette.SoftWhite,
                MiniSlotSize);
            AddFixedLayout(slot, MiniSlotSize.x, MiniSlotSize.y);

            RescueStickerFactory.CreateLabel(
                slot.transform,
                "Plus",
                "+",
                36f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0f, 0.28f),
                new Vector2(1f, 0.92f));
            RescueStickerFactory.CreateLabel(
                slot.transform,
                "Slot",
                slotNumber.ToString(),
                13f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SunnyYellow,
                TextAlignmentOptions.Center,
                new Vector2(0f, 0.04f),
                new Vector2(1f, 0.28f));
        }

        private void BuildRosterAdapter()
        {
            roster.Clear();
            var ids = controller?.AvailableEmojiIds;
            if (ids == null || ids.Count == 0)
            {
                // TODO: Remove fallback once all scenes expose DeckBuilderController.AvailableEmojiIds.
                ids = EmojiIdUtility.LaunchRoster;
            }

            foreach (var emojiId in ids)
            {
                var role = EmojiUiFormatter.BuildRoleTag(emojiId);
                var name = EmojiIdUtility.ToDisplayName(emojiId);
                roster.Add(new UnitView(
                    emojiId,
                    name,
                    name,
                    role,
                    ResolveFilter(role),
                    ResolveCardColor(role),
                    ResolveAuraColor(role)));
            }
        }

        private bool UnitMatchesFilter(UnitView unit)
        {
            return activeFilter == SquadFilter.All || unit.Filter == activeFilter;
        }

        private bool IsComplete => controller != null && controller.SelectedEmojis.Count == controller.RequiredSelectionCount;

        private HashSet<EmojiId> SelectedSet => controller == null
            ? new HashSet<EmojiId>()
            : new HashSet<EmojiId>(controller.SelectedEmojis);

        private string ProgressText()
        {
            if (controller == null)
            {
                return "0/6 PICKED";
            }

            var selectedCount = controller.SelectedEmojis.Count;
            var required = controller.RequiredSelectionCount;
            return selectedCount == required
                ? $"{selectedCount}/{required} READY"
                : $"{selectedCount}/{required} PICKED";
        }

        private static SquadFilter ResolveFilter(string role)
        {
            return role switch
            {
                "BURST" => SquadFilter.Burst,
                "SUP" => SquadFilter.Support,
                "CTL" => SquadFilter.Control,
                "RAMP" => SquadFilter.Control,
                _ => SquadFilter.All
            };
        }

        private static Color ResolveCardColor(string role)
        {
            return role switch
            {
                "BURST" => new Color32(0xFF, 0x76, 0x8A, 0xFF),
                "SUP" => new Color32(0x2F, 0xC7, 0x9E, 0xFF),
                "CTL" => new Color32(0x26, 0x98, 0xDE, 0xFF),
                "RAMP" => new Color32(0x4D, 0xBC, 0x70, 0xFF),
                _ => new Color32(0xB3, 0x72, 0xFF, 0xFF)
            };
        }

        private static Color ResolveAuraColor(string role)
        {
            return role switch
            {
                "BURST" => RescueStickerFactory.Palette.Coral,
                "SUP" => RescueStickerFactory.Palette.Mint,
                "CTL" => RescueStickerFactory.Palette.Aqua,
                "RAMP" => new Color32(0x8F, 0xFF, 0x63, 0xFF),
                _ => RescueStickerFactory.Palette.SunnyYellow
            };
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

        private static void ClearChildren(Transform parent)
        {
            if (parent == null)
            {
                return;
            }

            for (var index = parent.childCount - 1; index >= 0; index--)
            {
                Destroy(parent.GetChild(index).gameObject);
            }
        }

        private static void AddFixedLayout(GameObject target, float width, float height)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.preferredHeight = height;
            layout.minWidth = width;
            layout.minHeight = height;
        }

        private readonly struct UnitView
        {
            public readonly EmojiId Id;
            public readonly string Name;
            public readonly string Emoji;
            public readonly string Role;
            public readonly SquadFilter Filter;
            public readonly Color CardColor;
            public readonly Color AuraColor;

            public UnitView(
                EmojiId id,
                string name,
                string emoji,
                string role,
                SquadFilter filter,
                Color cardColor,
                Color auraColor)
            {
                Id = id;
                Name = name;
                Emoji = emoji;
                Role = role;
                Filter = filter;
                CardColor = cardColor;
                AuraColor = auraColor;
            }
        }
    }
}
