using System;
using System.Collections.Generic;
using EmojiWar.Client.Content;
using EmojiWar.Client.Core;
using EmojiWar.Client.UI.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Home
{
    /// <summary>
    /// Home-only rescue screen using the locked Squad Builder sticker-pop style.
    /// Navigation is delegated back to HomeScreenController so gameplay flow stays unchanged.
    /// </summary>
    public sealed class HomeRescueScreen : MonoBehaviour
    {
        private static readonly Vector2 SquadMiniSize = EmojiWarVisualStyle.Layout.SquadStripTile;
        private static readonly EmojiId[] StarterSquad =
        {
            EmojiId.Fire,
            EmojiId.Water,
            EmojiId.Lightning,
            EmojiId.Shield,
            EmojiId.Heart,
            EmojiId.Wind
        };

        private HomeScreenController controller;
        private RectTransform root;
        private RectTransform contentRoot;
        private readonly List<RectTransform> enterTargets = new();

        public void Initialize(HomeScreenController owner)
        {
            controller = owner;
        }

        public void Show()
        {
            if (root != null)
            {
                Destroy(root.gameObject);
                root = null;
            }

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
            enterTargets.Clear();

            var rootObject = RescueStickerFactory.CreateScreenRoot(transform, "HomeRescueRoot");
            root = rootObject.GetComponent<RectTransform>();
            root.SetAsLastSibling();

            RescueStickerFactory.CreateGradientLikeBackground(
                root,
                "HomeStickerPopGradient",
                EmojiWarVisualStyle.Colors.BgTop,
                EmojiWarVisualStyle.Colors.BgBottom);

            contentRoot = CreateRect("HomeRescueContent", root);
            Stretch(contentRoot);

            CreateHeader();
            CreateHeroStage();
            CreateCurrentSquad();
            CreateModeCtas();
            CreateSecondaryActions();
            CreateBottomNav();
        }

        private void CreateHeader()
        {
            var header = CreateRect("HomeHeader", contentRoot);
            SetAnchors(header, new Vector2(0.06f, 0.905f), new Vector2(0.94f, 0.972f));
            AddEnterTarget(header);

            RescueStickerFactory.CreateLabel(
                header,
                "Logo",
                "Emoji War",
                20f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SunnyYellow,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.34f),
                new Vector2(0.42f, 0.94f));

            var seasonChip = RescueStickerFactory.CreateStatusChip(
                header,
                "Season 1",
                RescueStickerFactory.Palette.SunnyYellow,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(seasonChip.GetComponent<RectTransform>(), new Vector2(0.56f, 0.38f), new Vector2(0.80f, 0.94f));

            var rankedChip = RescueStickerFactory.CreateStatusChip(
                header,
                "Ranked",
                RescueStickerFactory.Palette.Mint,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(rankedChip.GetComponent<RectTransform>(), new Vector2(0.82f, 0.38f), new Vector2(1f, 0.94f));
        }

        private void CreateHeroStage()
        {
            var heroRect = RescueStickerFactory.CreateOpenHeroStage(contentRoot, "HomeHeroStage");
            SetAnchors(heroRect, new Vector2(0.035f, EmojiWarVisualStyle.Layout.HeroStageBottom), new Vector2(0.965f, EmojiWarVisualStyle.Layout.HeroStageTop));
            AddEnterTarget(heroRect);

            var titleStack = CreateRect("HeroTitleStack", heroRect);
            SetAnchors(titleStack, new Vector2(0.18f, 0.21f), new Vector2(0.82f, 0.76f));

            var modeBadge = RescueStickerFactory.CreateStatusChip(
                titleStack,
                "Ranked PvP",
                new Color(0.22f, 0.16f, 0.52f, 0.82f),
                EmojiWarVisualStyle.Colors.GoldLight);
            SetAnchors(modeBadge.GetComponent<RectTransform>(), new Vector2(0.32f, 0.78f), new Vector2(0.68f, 0.92f));

            RescueStickerFactory.CreateLabel(
                titleStack,
                "HeroTitleTopShadow",
                "Emoji",
                80f,
                FontStyles.Bold,
                new Color(0.16f, 0.10f, 0.34f, 0.88f),
                TextAlignmentOptions.Center,
                new Vector2(0.03f, 0.44f),
                new Vector2(0.99f, 0.74f));
            RescueStickerFactory.CreateLabel(
                titleStack,
                "HeroTitleTop",
                "Emoji",
                80f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.02f, 0.46f),
                new Vector2(0.98f, 0.76f));

            RescueStickerFactory.CreateLabel(
                titleStack,
                "HeroTitleBottomShadow",
                "Clash",
                98f,
                FontStyles.Bold,
                new Color(0.20f, 0.11f, 0.34f, 0.92f),
                TextAlignmentOptions.Center,
                new Vector2(0.03f, 0.14f),
                new Vector2(0.99f, 0.50f));
            RescueStickerFactory.CreateLabel(
                titleStack,
                "HeroTitleBottom",
                "Clash",
                98f,
                FontStyles.Bold,
                EmojiWarVisualStyle.Colors.GoldLight,
                TextAlignmentOptions.Center,
                new Vector2(0.02f, 0.18f),
                new Vector2(0.98f, 0.52f));

            RescueStickerFactory.CreateLabel(
                titleStack,
                "HeroCopy",
                "Build a squad. Ban one. Clash fast.",
                17f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.14f, 0.01f),
                new Vector2(0.86f, 0.14f));

            var centerGlow = RescueStickerFactory.CreateBlob(
                heroRect,
                "HeroCenterGlow",
                RescueStickerFactory.Palette.SunnyYellow,
                new Vector2(0f, -14f),
                new Vector2(340f, 250f),
                0.05f);

            RescueStickerFactory.CreateBlob(
                heroRect,
                "HeroFloorGlow",
                RescueStickerFactory.Palette.Aqua,
                new Vector2(0f, -172f),
                new Vector2(600f, 170f),
                0.16f);

            var stageRing = RescueStickerFactory.CreateBlob(
                heroRect,
                "HeroStageRing",
                RescueStickerFactory.Palette.SoftWhite,
                new Vector2(0f, -150f),
                new Vector2(470f, 116f),
                0.05f);
            stageRing.transform.SetAsFirstSibling();

            RescueStickerFactory.CreateBlob(
                heroRect,
                "HeroAuraLeft",
                RescueStickerFactory.Palette.HotPink,
                new Vector2(-176f, 34f),
                new Vector2(220f, 248f),
                0.06f);
            RescueStickerFactory.CreateBlob(
                heroRect,
                "HeroAuraRight",
                RescueStickerFactory.Palette.Aqua,
                new Vector2(202f, 36f),
                new Vector2(220f, 248f),
                0.06f);

            var squad = ResolveCurrentSquad();
            var picks = new[]
            {
                squad[0],
                squad[Mathf.Min(1, squad.Count - 1)],
                squad[Mathf.Min(2, squad.Count - 1)],
                squad[Mathf.Min(3, squad.Count - 1)],
                squad[Mathf.Min(4, squad.Count - 1)],
                squad[Mathf.Min(5, squad.Count - 1)]
            };
            var positions = new[]
            {
                new Vector2(0.15f, 0.76f),
                new Vector2(0.86f, 0.73f),
                new Vector2(0.06f, 0.46f),
                new Vector2(0.93f, 0.43f),
                new Vector2(0.29f, 0.12f),
                new Vector2(0.74f, 0.11f)
            };
            var heroSize = EmojiWarVisualStyle.Layout.LargeHeroAvatar.x;
            var sizes = new[]
            {
                heroSize + 12f,
                heroSize + 4f,
                heroSize - 14f,
                heroSize - 24f,
                heroSize - 10f,
                heroSize - 20f
            };
            var tilts = new[] { -10f, 6f, -14f, 10f, -5f, 9f };

            for (var index = 0; index < picks.Length; index++)
            {
                var unit = picks[index];
                var key = EmojiIdUtility.ToApiId(unit);
                var name = EmojiIdUtility.ToDisplayName(unit);
                var color = UnitIconLibrary.GetPrimaryColor(key);
                var avatar = RescueStickerFactory.CreateHeroFighter(
                    heroRect,
                    key,
                    name,
                    color,
                    new Vector2(sizes[index], sizes[index]));
                var rect = avatar.GetComponent<RectTransform>();
                rect.anchorMin = positions[index];
                rect.anchorMax = positions[index];
                rect.anchoredPosition = Vector2.zero;
                rect.localRotation = Quaternion.Euler(0f, 0f, tilts[index]);
                NativeMotionKit.IdleBob(this, rect, 7f + index, 1.18f + index * 0.10f, true);
                NativeMotionKit.BreatheScale(this, rect, 0.030f, 1.36f + index * 0.08f, true);
            }
        }

        private void CreateCurrentSquad()
        {
            var tray = RescueStickerFactory.CreateGlassPanel(
                contentRoot,
                "HomeCurrentSquadTray",
                Vector2.zero,
                strong: false);
            var trayRect = tray.GetComponent<RectTransform>();
            SetAnchors(trayRect, new Vector2(0.045f, EmojiWarVisualStyle.Layout.SquadStripBottom), new Vector2(0.955f, EmojiWarVisualStyle.Layout.SquadStripTop));
            AddEnterTarget(trayRect);

            RescueStickerFactory.CreateLabel(
                tray.transform,
                "Title",
                "Sticker Squad",
                24f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.045f, 0.75f),
                new Vector2(0.52f, 0.96f));

            var readyChip = RescueStickerFactory.CreateStatusChip(
                tray.transform,
                controller != null ? controller.GetRankedSquadStatusLabel() : "6/6 READY",
                controller != null && controller.HasReadyRankedSquad()
                    ? RescueStickerFactory.Palette.Mint
                    : RescueStickerFactory.Palette.SunnyYellow,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(readyChip.GetComponent<RectTransform>(), new Vector2(0.72f, 0.74f), new Vector2(0.955f, 0.96f));

            RescueStickerFactory.CreateBlob(
                tray.transform,
                "CurrentSquadRowGlow",
                RescueStickerFactory.Palette.Aqua,
                new Vector2(0f, -18f),
                new Vector2(640f, 112f),
                0.07f);

            var row = CreateRect("CurrentSquadRow", trayRect);
            SetAnchors(row, new Vector2(0.018f, 0.04f), new Vector2(0.982f, 0.82f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var squad = ResolveCurrentSquad();
            for (var index = 0; index < squad.Count; index++)
            {
                var unit = squad[index];
                var key = EmojiIdUtility.ToApiId(unit);
                var name = EmojiIdUtility.ToDisplayName(unit);
                var mini = RescueStickerFactory.CreateFloatingMiniSquadSticker(
                    row,
                    name,
                    key,
                    UnitIconLibrary.GetPrimaryColor(key),
                    SquadMiniSize);
                AddFixedLayout(mini, SquadMiniSize.x, SquadMiniSize.y);
                NativeMotionKit.PopIn(this, mini.transform as RectTransform, null, 0.22f + index * 0.015f, 0.74f);
                var visual = mini.transform.Find("FloatingStickerVisual") as RectTransform;
                if (visual != null)
                {
                    var tilt = index switch
                    {
                        0 => -7f,
                        1 => 4f,
                        2 => -5f,
                        3 => 6f,
                        4 => -3f,
                        _ => 5f
                    };
                    visual.localRotation = Quaternion.Euler(0f, 0f, tilt);
                    NativeMotionKit.IdleBob(this, visual, 3f + index * 0.55f, 1.18f + index * 0.06f, true);
                    NativeMotionKit.BreatheScale(this, visual, 0.020f, 1.26f + index * 0.05f, true);
                }
            }
        }

        private void CreateModeCtas()
        {
            var rankedButton = RescueStickerFactory.CreatePrimaryGoldButton(
                contentRoot,
                "Play Ranked",
                new Vector2(560f, 90f));
            var rankedRect = rankedButton.transform as RectTransform;
            SetAnchors(rankedRect, new Vector2(0.05f, EmojiWarVisualStyle.Layout.PrimaryCtaBottom), new Vector2(0.95f, EmojiWarVisualStyle.Layout.PrimaryCtaTop));
            PolishHomeButton(rankedButton, 34f, keepHighlight: true);
            AddEnterTarget(rankedRect);
            NativeMotionKit.BreatheScale(this, rankedRect, 0.025f, 1.05f, true);
            WireButton(rankedButton, () => controller?.OpenBattlePlayers(), 0.07f);
        }

        private void CreateSecondaryActions()
        {
            var rootRect = CreateRect("HomeSecondaryActions", contentRoot);
            SetAnchors(rootRect, new Vector2(0.05f, EmojiWarVisualStyle.Layout.SecondaryActionsBottom), new Vector2(0.95f, EmojiWarVisualStyle.Layout.SecondaryActionsTop));
            AddEnterTarget(rootRect);

            CreateSecondaryButton(rootRect, "Quick Clash", new Vector2(0f, 0f), new Vector2(0.188f, 1f), () => controller?.OpenEmojiClash());
            CreateSecondaryButton(rootRect, "Edit Squad", new Vector2(0.204f, 0f), new Vector2(0.392f, 1f), () => controller?.OpenDeckBuilder());
            CreateSecondaryButton(rootRect, "Practice", new Vector2(0.408f, 0f), new Vector2(0.596f, 1f), () => controller?.OpenBattleBot());
            CreateSecondaryButton(rootRect, "Codex", new Vector2(0.612f, 0f), new Vector2(0.80f, 1f), () => controller?.OpenCodex());
            CreateSecondaryButton(rootRect, "Ranks", new Vector2(0.816f, 0f), new Vector2(1f, 1f), () => controller?.OpenLeaderboard());
        }

        private void CreateSecondaryButton(RectTransform parent, string label, Vector2 min, Vector2 max, Action action)
        {
            var button = RescueStickerFactory.CreateSecondaryActionButton(
                parent,
                label,
                new Vector2(134f, 58f));
            SetAnchors(button.transform as RectTransform, min, max);
            PolishHomeButton(button, 18f, keepHighlight: false);
            WireButton(button, action, 0.06f);
        }

        private void CreateBottomNav()
        {
            var nav = RescueStickerFactory.CreateLightBottomNavPlate(
                contentRoot,
                "HomeBottomNav",
                Vector2.zero);
            var navRect = nav.GetComponent<RectTransform>();
            SetAnchors(navRect, new Vector2(0.05f, EmojiWarVisualStyle.Layout.BottomNavBottom), new Vector2(0.95f, EmojiWarVisualStyle.Layout.BottomNavTop));
            AddEnterTarget(navRect);

            var row = CreateRect("BottomNavRow", navRect);
            SetAnchors(row, new Vector2(0.035f, 0.16f), new Vector2(0.965f, 0.84f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateNavButton(row, "Home", EmojiWarVisualStyle.Colors.SecondaryActionDark, () => { });
            CreateNavButton(row, "Squad", EmojiWarVisualStyle.Colors.SecondaryActionDark, () => controller?.OpenDeckBuilder());
            CreateNavButton(row, "Codex", EmojiWarVisualStyle.Colors.SecondaryActionDark, () => controller?.OpenCodex());
            CreateNavButton(row, "Ranks", EmojiWarVisualStyle.Colors.SecondaryActionDark, () => controller?.OpenLeaderboard());
        }

        private void CreateNavButton(RectTransform parent, string label, Color color, Action action)
        {
            var button = RescueStickerFactory.CreateSecondaryActionButton(
                parent,
                label,
                new Vector2(120f, 42f));
            PolishHomeButton(button, 16f, keepHighlight: false);
            var layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 42f;
            layout.minHeight = 38f;
            WireButton(button, action, 0.05f);
        }

        private static void PolishHomeButton(Button button, float labelSize, bool keepHighlight)
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
                if (!keepHighlight)
                {
                    highlight.gameObject.SetActive(false);
                }
                else
                {
                    SetAnchors(highlight as RectTransform, new Vector2(0.10f, 0.68f), new Vector2(0.90f, 0.88f));
                    var image = highlight.GetComponent<Image>();
                    if (image != null)
                    {
                        image.color = new Color(1f, 1f, 1f, 0.18f);
                    }
                }
            }

            var buttonGraphic = button.GetComponent<Image>();
            if (buttonGraphic != null && !keepHighlight)
            {
                buttonGraphic.color = Color.Lerp(buttonGraphic.color, RescueStickerFactory.Palette.DeepIndigo, 0.12f);
            }

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSize = labelSize;
                label.alignment = TextAlignmentOptions.Center;
                var image = button.GetComponent<Image>();
                var usesGoldBody = image != null && image.color.r > 0.85f && image.color.g > 0.68f;
                label.color = usesGoldBody ? EmojiWarVisualStyle.Colors.GoldText : RescueStickerFactory.Palette.SoftWhite;
            }
        }

        private void WireButton(Button button, Action action, float delay)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                NativeMotionKit.PunchScale(this, button.transform as RectTransform, 0.08f, 0.14f);
                if (action != null)
                {
                    StartCoroutine(InvokeAfter(delay, action));
                }
            });
        }

        private IEnumerator<WaitForSecondsRealtime> InvokeAfter(float delay, Action action)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));
            action?.Invoke();
        }

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
                NativeMotionKit.SlideFadeIn(this, target, group, new Vector2(0f, -22f + index * -2f), 0.30f + index * 0.025f);
            }
        }

        private IReadOnlyList<EmojiId> ResolveCurrentSquad()
        {
            var bootstrap = AppBootstrap.Instance;
            if (bootstrap != null)
            {
                bootstrap.ActiveDeckService.EnsureInitialized(bootstrap.SessionState.UserId);
                var activeDeck = bootstrap.ActiveDeckService.ActiveDeckEmojiIds;
                if (activeDeck is { Count: 6 })
                {
                    return activeDeck;
                }
            }

            var pending = LaunchSelections.GetPendingSquad();
            if (pending is { Count: 6 })
            {
                return pending;
            }

            // TODO: Replace this with real persisted Home squad data if Home ever loads without AppBootstrap.
            return StarterSquad;
        }

        private void AddEnterTarget(RectTransform target)
        {
            if (target != null)
            {
                enterTargets.Add(target);
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
    }
}
