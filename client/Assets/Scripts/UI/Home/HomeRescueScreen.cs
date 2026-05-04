using System;
using System.Collections;
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
        private RectTransform headerRoot;
        private RectTransform heroStageRoot;
        private RectTransform heroTitleStackRoot;
        private RectTransform primaryCtaRoot;
        private RectTransform secondaryActionsRoot;
        private Coroutine homeEntranceCoroutine;
        private int homeMotionVersion;
        private readonly List<RectTransform> enterTargets = new();
        private readonly List<RectTransform> heroStickerTargets = new();
        private readonly List<RectTransform> secondaryButtonTargets = new();
        private readonly List<RectTransform> decorativeAccentTargets = new();

        public void Initialize(HomeScreenController owner)
        {
            controller = owner;
        }

        public void Show()
        {
            CancelHomeMotion();
            if (root != null)
            {
                root.gameObject.SetActive(false);
                Destroy(root.gameObject);
                root = null;
            }

            BuildLayout();
            PlayEnterMotion();
        }

        public void Hide()
        {
            CancelHomeMotion();
            if (root != null)
            {
                root.gameObject.SetActive(false);
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
            heroStickerTargets.Clear();
            secondaryButtonTargets.Clear();
            decorativeAccentTargets.Clear();
            headerRoot = null;
            heroStageRoot = null;
            heroTitleStackRoot = null;
            primaryCtaRoot = null;
            secondaryActionsRoot = null;

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
            CreateModeCtas();
            CreateSecondaryActions();
        }

        private void CreateHeader()
        {
            var header = CreateRect("HomeHeader", contentRoot);
            headerRoot = header;
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
                "Clash",
                RescueStickerFactory.Palette.Mint,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(rankedChip.GetComponent<RectTransform>(), new Vector2(0.82f, 0.38f), new Vector2(1f, 0.94f));
        }

        private void CreateHeroStage()
        {
            var heroRect = RescueStickerFactory.CreateOpenHeroStage(contentRoot, "HomeHeroStage");
            heroStageRoot = heroRect;
            SetAnchors(heroRect, new Vector2(0.035f, EmojiWarVisualStyle.Layout.HeroStageBottom), new Vector2(0.965f, EmojiWarVisualStyle.Layout.HeroStageTop));
            AddEnterTarget(heroRect);

            var titleStack = CreateRect("HeroTitleStack", heroRect);
            heroTitleStackRoot = titleStack;
            SetAnchors(titleStack, new Vector2(0.18f, 0.21f), new Vector2(0.82f, 0.76f));

            var modeBadge = RescueStickerFactory.CreateStatusChip(
                titleStack,
                "Quick Clash",
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
                "Pick fast. Outsmart rivals. Clash now.",
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
                EnsureCanvasGroup(rect);
                heroStickerTargets.Add(rect);
            }

            CreateHomeHeroAccents(heroRect);
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
            var clashButton = RescueStickerFactory.CreatePrimaryGoldButton(
                contentRoot,
                "Play Quick Clash",
                new Vector2(560f, 90f));
            var clashRect = clashButton.transform as RectTransform;
            primaryCtaRoot = clashRect;
            SetAnchors(clashRect, new Vector2(0.05f, 0.325f), new Vector2(0.95f, 0.425f));
            PolishHomeButton(clashButton, 34f, keepHighlight: true);
            AddEnterTarget(clashRect);
            WireButton(clashButton, () => controller?.OpenEmojiClashPvp(), 0.07f);
        }

        private void CreateSecondaryActions()
        {
            var rootRect = CreateRect("HomeSecondaryActions", contentRoot);
            secondaryActionsRoot = rootRect;
            SetAnchors(rootRect, new Vector2(0.05f, 0.205f), new Vector2(0.95f, 0.305f));
            AddEnterTarget(rootRect);

            CreateSecondaryButton(rootRect, "Clash Bot", new Vector2(0f, 0f), new Vector2(0.31f, 1f), () => controller?.OpenEmojiClash());
            CreateSecondaryButton(rootRect, "Codex", new Vector2(0.345f, 0f), new Vector2(0.655f, 1f), () => controller?.OpenCodex());
            CreateSecondaryButton(rootRect, "Ranks", new Vector2(0.69f, 0f), new Vector2(1f, 1f), () => controller?.OpenLeaderboard());
        }

        private void CreateSecondaryButton(RectTransform parent, string label, Vector2 min, Vector2 max, Action action)
        {
            var button = RescueStickerFactory.CreateSecondaryActionButton(
                parent,
                label,
                new Vector2(134f, 58f));
            SetAnchors(button.transform as RectTransform, min, max);
            PolishHomeButton(button, 18f, keepHighlight: false);
            secondaryButtonTargets.Add(button.transform as RectTransform);
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

        private void CreateHomeHeroAccents(RectTransform heroRect)
        {
            if (heroRect == null)
            {
                return;
            }

            CreateHomeAccent(heroRect, "HomeSparkA", "+", new Vector2(0.28f, 0.73f), 18f, RescueStickerFactory.Palette.Aqua);
            CreateHomeAccent(heroRect, "HomeSparkB", "*", new Vector2(0.70f, 0.76f), 17f, RescueStickerFactory.Palette.HotPink);
            CreateHomeAccent(heroRect, "HomeSparkC", "+", new Vector2(0.17f, 0.58f), 14f, RescueStickerFactory.Palette.SunnyYellow);
            CreateHomeAccent(heroRect, "HomeSparkD", "*", new Vector2(0.84f, 0.55f), 15f, RescueStickerFactory.Palette.Mint);
        }

        private void CreateHomeAccent(RectTransform parent, string name, string text, Vector2 anchor, float fontSize, Color color)
        {
            var accent = RescueStickerFactory.CreateLabel(
                parent,
                name,
                text,
                fontSize,
                FontStyles.Bold,
                color,
                TextAlignmentOptions.Center,
                anchor - new Vector2(0.025f, 0.025f),
                anchor + new Vector2(0.025f, 0.025f));
            accent.raycastTarget = false;
            var rect = accent.rectTransform;
            decorativeAccentTargets.Add(rect);
            EnsureCanvasGroup(rect);
        }

        private IEnumerator<WaitForSecondsRealtime> InvokeAfter(float delay, Action action)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, delay));
            action?.Invoke();
        }

        private void PlayEnterMotion()
        {
            CancelHomeMotion();
            PrepareHomeEntranceState();
            homeMotionVersion++;
            homeEntranceCoroutine = StartCoroutine(PlayHomeEntrance(homeMotionVersion));
        }

        private void CancelHomeMotion()
        {
            homeMotionVersion++;
            if (homeEntranceCoroutine != null)
            {
                StopCoroutine(homeEntranceCoroutine);
                homeEntranceCoroutine = null;
            }
        }

        private void PrepareHomeEntranceState()
        {
            PrepareHomeTarget(headerRoot, 1f);
            PrepareHomeTarget(heroTitleStackRoot, 0.88f);
            PrepareHomeTarget(primaryCtaRoot, 0.88f);
            PrepareHomeTarget(secondaryActionsRoot, 1f);

            foreach (var sticker in heroStickerTargets)
            {
                PrepareHomeTarget(sticker, 0.82f);
            }

            foreach (var button in secondaryButtonTargets)
            {
                PrepareHomeTarget(button, 0.94f);
            }

            foreach (var accent in decorativeAccentTargets)
            {
                PrepareHomeTarget(accent, 0.70f);
            }
        }

        private void PrepareHomeTarget(RectTransform target, float scale)
        {
            if (target == null)
            {
                return;
            }

            var group = EnsureCanvasGroup(target);
            if (group == null)
            {
                return;
            }

            group.alpha = 0f;
            target.localScale = Vector3.one;
        }

        private IEnumerator PlayHomeEntrance(int version)
        {
            if (headerRoot != null)
            {
                NativeMotionKit.SlideFadeIn(this, headerRoot, EnsureCanvasGroup(headerRoot), new Vector2(0f, 18f), 0.22f);
            }

            yield return new WaitForSecondsRealtime(0.08f);
            if (!IsHomeMotionCurrent(version))
            {
                yield break;
            }

            for (var index = 0; index < heroStickerTargets.Count; index++)
            {
                var sticker = heroStickerTargets[index];
                if (sticker == null)
                {
                    continue;
                }

                NativeMotionKit.PopIn(this, sticker, EnsureCanvasGroup(sticker), 0.20f + index * 0.018f, 0.70f);
            }

            foreach (var accent in decorativeAccentTargets)
            {
                if (accent != null)
                {
                    NativeMotionKit.PopIn(this, accent, EnsureCanvasGroup(accent), 0.18f, 0.68f);
                    NativeMotionKit.PunchScale(this, accent, 0.10f, 0.18f);
                }
            }

            yield return new WaitForSecondsRealtime(0.13f);
            if (!IsHomeMotionCurrent(version))
            {
                yield break;
            }

            if (heroTitleStackRoot != null)
            {
                NativeMotionKit.StampSlam(this, heroTitleStackRoot, 1.08f, 0.24f);
                var group = EnsureCanvasGroup(heroTitleStackRoot);
                if (group != null)
                {
                    group.alpha = 1f;
                }
            }

            yield return new WaitForSecondsRealtime(0.16f);
            if (!IsHomeMotionCurrent(version))
            {
                yield break;
            }

            if (primaryCtaRoot != null)
            {
                NativeMotionKit.SlideFadeIn(this, primaryCtaRoot, EnsureCanvasGroup(primaryCtaRoot), new Vector2(0f, -28f), 0.24f);
                yield return new WaitForSecondsRealtime(0.10f);
                if (IsHomeMotionCurrent(version))
                {
                    NativeMotionKit.PunchScale(this, primaryCtaRoot, 0.055f, 0.16f);
                }
            }

            yield return new WaitForSecondsRealtime(0.10f);
            if (!IsHomeMotionCurrent(version))
            {
                yield break;
            }

            if (secondaryActionsRoot != null)
            {
                var group = EnsureCanvasGroup(secondaryActionsRoot);
                if (group != null)
                {
                    group.alpha = 1f;
                }
            }

            for (var index = 0; index < secondaryButtonTargets.Count; index++)
            {
                var button = secondaryButtonTargets[index];
                if (button != null)
                {
                    NativeMotionKit.SlideFadeIn(this, button, EnsureCanvasGroup(button), new Vector2(0f, -18f), 0.18f + index * 0.025f);
                }
            }

            yield return new WaitForSecondsRealtime(0.12f);
            if (!IsHomeMotionCurrent(version))
            {
                yield break;
            }

            StartHomeIdleMotion();
            homeEntranceCoroutine = null;
        }

        private bool IsHomeMotionCurrent(int version)
        {
            return root != null &&
                   root.gameObject.activeInHierarchy &&
                   homeMotionVersion == version &&
                   gameObject.activeInHierarchy;
        }

        private void StartHomeIdleMotion()
        {
            for (var index = 0; index < heroStickerTargets.Count; index++)
            {
                var sticker = heroStickerTargets[index];
                if (sticker == null)
                {
                    continue;
                }

                NativeMotionKit.IdleBob(this, sticker, 5.4f + index * 0.45f, 1.20f + index * 0.09f, true);
                NativeMotionKit.BreatheScale(this, sticker, 0.018f + index * 0.0015f, 1.32f + index * 0.07f, true);
            }

            if (primaryCtaRoot != null)
            {
                NativeMotionKit.BreatheScale(this, primaryCtaRoot, 0.012f, 1.42f, true);
            }

            for (var index = 0; index < decorativeAccentTargets.Count; index++)
            {
                var accent = decorativeAccentTargets[index];
                if (accent != null)
                {
                    NativeMotionKit.IdleBob(this, accent, 1.4f + index * 0.35f, 1.6f + index * 0.12f, true);
                }
            }
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
