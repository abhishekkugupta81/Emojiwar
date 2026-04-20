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
    /// It is intentionally self-contained so Formation and Result can keep their current paths.
    /// </summary>
    public sealed class BlindBanRescueScreen : MonoBehaviour
    {
        private static readonly Vector2 EnemyTileSize = new(112f, 116f);
        private static readonly Vector2 PlayerMiniSize = new(90f, 86f);

        private readonly List<RectTransform> enterTargets = new();
        private RectTransform root;
        private RectTransform contentRoot;
        private IReadOnlyList<UnitView> enemyUnits = Array.Empty<UnitView>();
        private IReadOnlyList<UnitView> playerUnits = Array.Empty<UnitView>();
        private string selectedEnemyId = string.Empty;
        private string lockedEnemyId = string.Empty;
        private string opponentBanId = string.Empty;
        private Action<string> onTargetChanged;
        private Action<string> onBanLocked;
        private int remainingSeconds;

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
            string selectedEnemy,
            string lockedEnemy,
            string opponentBan,
            Action<string> targetChanged,
            Action<string> banLockedCallback,
            int secondsRemaining)
        {
            enemyUnits = enemies ?? Array.Empty<UnitView>();
            playerUnits = players ?? Array.Empty<UnitView>();
            selectedEnemyId = NormalizeOptionalKey(selectedEnemy);
            lockedEnemyId = NormalizeOptionalKey(lockedEnemy);
            opponentBanId = NormalizeOptionalKey(opponentBan);
            onTargetChanged = targetChanged;
            onBanLocked = banLockedCallback;
            remainingSeconds = Mathf.Max(0, secondsRemaining);

            BuildLayout();
            PlayEnterMotion();
        }

        public void Hide()
        {
            if (root != null)
            {
                Destroy(root.gameObject);
                root = null;
            }
        }

        private void OnDisable()
        {
            Hide();
        }

        private void BuildLayout()
        {
            Hide();
            enterTargets.Clear();

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
                IsLocked ? "LOCKED" : $"{Mathf.Max(1, remainingSeconds)}s",
                IsLocked ? RescueStickerFactory.Palette.Mint : RescueStickerFactory.Palette.Coral,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(timerChip.GetComponent<RectTransform>(), new Vector2(0.66f, 0.68f), new Vector2(1f, 0.98f));

            RescueStickerFactory.CreateLabel(
                header,
                "Title",
                IsLocked ? "Ban Locked" : "Choose a Ban",
                38f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.18f),
                new Vector2(0.72f, 0.68f));

            RescueStickerFactory.CreateLabel(
                header,
                "StatusCopy",
                IsLocked ? "Waiting for opponent..." : "Pick 1 enemy sticker",
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
            SetAnchors(panelRect, new Vector2(0.055f, 0.455f), new Vector2(0.945f, 0.785f));
            AddEnterTarget(panelRect);

            RescueStickerFactory.CreateLabel(
                panel.transform,
                "EnemyTitle",
                "Enemy Squad",
                22f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.045f, 0.84f),
                new Vector2(0.55f, 0.97f));

            RescueStickerFactory.CreateLabel(
                panel.transform,
                "EnemyHint",
                IsLocked ? "Ban mark is locked in" : "Tap a target",
                15f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SunnyYellow,
                TextAlignmentOptions.Right,
                new Vector2(0.50f, 0.84f),
                new Vector2(0.955f, 0.97f));

            var gridRect = CreateRect("EnemyGrid", panelRect);
            SetAnchors(gridRect, new Vector2(0.055f, 0.075f), new Vector2(0.945f, 0.81f));
            var grid = gridRect.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.cellSize = EnemyTileSize;
            grid.spacing = new Vector2(10f, 8f);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.padding = new RectOffset(0, 0, 0, 0);

            foreach (var unit in enemyUnits.Take(6))
            {
                CreateEnemyCard(gridRect, unit);
            }
        }

        private void CreateEnemyCard(RectTransform parent, UnitView unit)
        {
            var isSelected = string.Equals(selectedEnemyId, unit.Id, StringComparison.Ordinal);
            var isLocked = string.Equals(lockedEnemyId, unit.Id, StringComparison.Ordinal);
            var card = RescueStickerFactory.CreateCompactUnitStickerTile(
                parent,
                unit.Name,
                unit.Id,
                unit.Role,
                unit.CardColor,
                isLocked ? RescueStickerFactory.Palette.Coral : unit.AuraColor,
                isSelected || isLocked,
                IsLocked && !isLocked,
                EnemyTileSize,
                isSelected || isLocked ? 1 : 0);
            var rect = card.GetComponent<RectTransform>();
            var layout = card.AddComponent<LayoutElement>();
            layout.preferredWidth = EnemyTileSize.x;
            layout.preferredHeight = EnemyTileSize.y;

            var avatar = rect.Find("EmojiAvatar") as RectTransform;
            if (avatar != null)
            {
                NativeMotionKit.IdleBob(this, avatar, isLocked ? 4f : 6f, 1.18f, true);
                NativeMotionKit.BreatheScale(this, avatar, isLocked ? 0.018f : 0.028f, 1.36f, true);
            }

            if (!IsLocked)
            {
                var button = card.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() =>
                {
                    selectedEnemyId = unit.Id;
                    NativeMotionKit.PunchScale(this, rect, 0.12f, 0.16f);
                    NativeMotionKit.PunchScale(this, avatar, 0.14f, 0.16f);
                    onTargetChanged?.Invoke(unit.Id);
                });
            }

            if (isLocked)
            {
                CreateBanStamp(rect);
                var image = card.GetComponent<Image>();
                if (image != null)
                {
                    NativeMotionKit.PulseGraphic(
                        this,
                        image,
                        image.color,
                        Color.Lerp(image.color, RescueStickerFactory.Palette.Coral, 0.28f),
                        0.95f);
                }
            }
        }

        private void CreateBanStamp(RectTransform card)
        {
            var stamp = RescueStickerFactory.CreateStatusChip(
                card,
                "BANNED",
                RescueStickerFactory.Palette.Coral,
                RescueStickerFactory.Palette.SoftWhite);
            var rect = stamp.GetComponent<RectTransform>();
            SetAnchors(rect, new Vector2(0.11f, 0.42f), new Vector2(0.89f, 0.63f));
            rect.localRotation = Quaternion.Euler(0f, 0f, -11f);
            rect.SetAsLastSibling();
            NativeMotionKit.StampSlam(this, rect, 1.20f, 0.22f);
            NativeMotionKit.Shake(this, card, 5f, 0.18f);
        }

        private void CreateVsMoment()
        {
            var vsRoot = CreateRect("BlindBanVsMoment", contentRoot);
            SetAnchors(vsRoot, new Vector2(0.08f, 0.365f), new Vector2(0.92f, 0.455f));
            AddEnterTarget(vsRoot);

            var glow = RescueStickerFactory.CreateBlob(
                vsRoot,
                "VsGlow",
                RescueStickerFactory.Palette.SunnyYellow,
                Vector2.zero,
                new Vector2(158f, 158f),
                0.20f);
            glow.transform.SetAsFirstSibling();

            var vsChip = RescueStickerFactory.CreateStatusChip(
                vsRoot,
                "VS",
                RescueStickerFactory.Palette.HotPink,
                RescueStickerFactory.Palette.SoftWhite);
            SetAnchors(vsChip.GetComponent<RectTransform>(), new Vector2(0.38f, 0.16f), new Vector2(0.62f, 0.86f));
            NativeMotionKit.BreatheScale(this, vsChip.transform as RectTransform, 0.035f, 1.2f, true);

            RescueStickerFactory.CreateLabel(
                vsRoot,
                "Instruction",
                IsLocked ? "Ban locked. Both squads stay visible." : "Tap one enemy sticker to ban",
                18f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0f, 0f),
                new Vector2(1f, 0.25f));
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
                $"{Mathf.Min(6, playerUnits.Count)}/6 READY",
                RescueStickerFactory.Palette.Mint,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(chip.GetComponent<RectTransform>(), new Vector2(0.70f, 0.70f), new Vector2(0.955f, 0.95f));

            var row = CreateRect("YourSquadRow", trayRect);
            SetAnchors(row, new Vector2(0.025f, 0.035f), new Vector2(0.975f, 0.70f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 7f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (var index = 0; index < playerUnits.Take(6).Count(); index++)
            {
                var unit = playerUnits[index];
                var mini = RescueStickerFactory.CreateMiniSquadSticker(
                    row,
                    unit.Name,
                    unit.Id,
                    unit.AuraColor,
                    PlayerMiniSize);
                AddFixedLayout(mini, PlayerMiniSize.x, PlayerMiniSize.y);
                if (string.Equals(opponentBanId, unit.Id, StringComparison.Ordinal))
                {
                    AddOpponentBanMark(mini.transform as RectTransform);
                }

                NativeMotionKit.PopIn(this, mini.transform as RectTransform, null, 0.20f + index * 0.015f, 0.78f);
            }
        }

        private void AddOpponentBanMark(RectTransform mini)
        {
            if (mini == null)
            {
                return;
            }

            var mark = RescueStickerFactory.CreateStatusChip(
                mini,
                "X",
                RescueStickerFactory.Palette.Coral,
                RescueStickerFactory.Palette.SoftWhite);
            SetAnchors(mark.GetComponent<RectTransform>(), new Vector2(0.62f, 0.56f), new Vector2(0.96f, 0.94f));
        }

        private void CreateLockButton()
        {
            var selectedReady = !string.IsNullOrWhiteSpace(selectedEnemyId);
            var label = IsLocked ? "Ban Locked" : selectedReady ? "Lock Ban" : "Choose Enemy";
            var color = IsLocked
                ? RescueStickerFactory.Palette.Mint
                : selectedReady
                    ? RescueStickerFactory.Palette.HotPink
                    : new Color(0.50f, 0.43f, 0.72f, 0.92f);

            var button = RescueStickerFactory.CreateToyButton(
                contentRoot,
                label,
                color,
                IsLocked ? RescueStickerFactory.Palette.InkPurple : RescueStickerFactory.Palette.SoftWhite,
                new Vector2(560f, 76f),
                primary: selectedReady && !IsLocked);
            var rect = button.transform as RectTransform;
            SetAnchors(rect, new Vector2(0.08f, 0.075f), new Vector2(0.92f, 0.155f));
            AddEnterTarget(rect);
            PolishButton(button, IsLocked ? 24f : 28f);

            button.interactable = selectedReady && !IsLocked;
            if (button.interactable)
            {
                button.onClick.AddListener(() =>
                {
                    NativeMotionKit.PunchScale(this, rect, 0.08f, 0.15f);
                    onBanLocked?.Invoke(selectedEnemyId);
                });
                NativeMotionKit.BreatheScale(this, rect, 0.025f, 1.1f, true);
            }
        }

        private bool IsLocked => !string.IsNullOrWhiteSpace(lockedEnemyId);

        private void PlayEnterMotion()
        {
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
                SetAnchors(highlight as RectTransform, new Vector2(0.10f, 0.68f), new Vector2(0.90f, 0.88f));
                var image = highlight.GetComponent<Image>();
                if (image != null)
                {
                    image.color = new Color(1f, 1f, 1f, 0.16f);
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
    }
}
