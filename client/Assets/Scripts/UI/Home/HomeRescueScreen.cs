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
        private static readonly Vector2 SquadMiniSize = new(110f, 104f);
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
                RescueStickerFactory.Palette.ElectricPurple,
                RescueStickerFactory.Palette.Mint);

            contentRoot = CreateRect("HomeRescueContent", root);
            Stretch(contentRoot);

            CreateHeader();
            CreateHeroArena();
            CreateCurrentSquad();
            CreatePrimaryCta();
            CreateSecondaryActions();
            CreateBottomNav();
        }

        private void CreateHeader()
        {
            var header = CreateRect("HomeHeader", contentRoot);
            SetAnchors(header, new Vector2(0.06f, 0.82f), new Vector2(0.94f, 0.965f));
            AddEnterTarget(header);

            RescueStickerFactory.CreateLabel(
                header,
                "Logo",
                "Emoji War",
                18f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SunnyYellow,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.72f),
                new Vector2(0.42f, 1f));

            RescueStickerFactory.CreateLabel(
                header,
                "Title",
                "Ranked PvP",
                42f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0.18f),
                new Vector2(0.70f, 0.76f));

            var seasonChip = RescueStickerFactory.CreateStatusChip(
                header,
                "Season 1",
                RescueStickerFactory.Palette.SunnyYellow,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(seasonChip.GetComponent<RectTransform>(), new Vector2(0.58f, 0.62f), new Vector2(0.82f, 0.94f));

            var rankedChip = RescueStickerFactory.CreateStatusChip(
                header,
                "Ranked",
                RescueStickerFactory.Palette.Mint,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(rankedChip.GetComponent<RectTransform>(), new Vector2(0.84f, 0.62f), new Vector2(1f, 0.94f));

            RescueStickerFactory.CreateLabel(
                header,
                "Subtitle",
                "Build a squad. Ban one. Battle fast.",
                18f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0f, 0f),
                new Vector2(0.90f, 0.24f));
        }

        private void CreateHeroArena()
        {
            var hero = RescueStickerFactory.CreateArenaSurface(
                contentRoot,
                "HomeHeroStickerArena",
                new Color(1f, 1f, 1f, 0.19f),
                RescueStickerFactory.Palette.HotPink,
                Vector2.zero);
            var heroRect = hero.GetComponent<RectTransform>();
            SetAnchors(heroRect, new Vector2(0.055f, 0.565f), new Vector2(0.945f, 0.795f));
            AddEnterTarget(heroRect);

            RescueStickerFactory.CreateLabel(
                hero.transform,
                "HeroCopy",
                "Pick stickers. Ban one. Battle fast.",
                23f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.055f, 0.08f),
                new Vector2(0.46f, 0.30f));

            var centerGlow = RescueStickerFactory.CreateBlob(
                hero.transform,
                "HeroCenterGlow",
                RescueStickerFactory.Palette.SunnyYellow,
                Vector2.zero,
                new Vector2(178f, 178f),
                0.18f);
            centerGlow.transform.SetAsFirstSibling();

            var battleBadge = RescueStickerFactory.CreateStatusChip(
                hero.transform,
                "Sticker Battle",
                RescueStickerFactory.Palette.SunnyYellow,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(battleBadge.GetComponent<RectTransform>(), new Vector2(0.39f, 0.43f), new Vector2(0.63f, 0.57f));

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
                new Vector2(0.24f, 0.67f),
                new Vector2(0.44f, 0.73f),
                new Vector2(0.68f, 0.67f),
                new Vector2(0.83f, 0.44f),
                new Vector2(0.60f, 0.30f),
                new Vector2(0.37f, 0.36f)
            };
            var sizes = new[] { 146f, 124f, 142f, 120f, 124f, 116f };
            var tilts = new[] { -8f, 6f, 9f, -6f, 7f, -4f };

            for (var index = 0; index < picks.Length; index++)
            {
                var unit = picks[index];
                var key = EmojiIdUtility.ToApiId(unit);
                var name = EmojiIdUtility.ToDisplayName(unit);
                var color = UnitIconLibrary.GetPrimaryColor(key);
                var avatar = RescueStickerFactory.CreateEmojiAvatar(
                    hero.transform,
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
            var tray = RescueStickerFactory.CreateArenaSurface(
                contentRoot,
                "HomeCurrentSquadTray",
                new Color(1f, 1f, 1f, 0.23f),
                RescueStickerFactory.Palette.Aqua,
                Vector2.zero);
            var trayRect = tray.GetComponent<RectTransform>();
            SetAnchors(trayRect, new Vector2(0.055f, 0.355f), new Vector2(0.945f, 0.535f));
            AddEnterTarget(trayRect);

            RescueStickerFactory.CreateLabel(
                tray.transform,
                "Title",
                "Current Squad",
                22f,
                FontStyles.Bold,
                RescueStickerFactory.Palette.SoftWhite,
                TextAlignmentOptions.Left,
                new Vector2(0.045f, 0.72f),
                new Vector2(0.52f, 0.96f));

            var readyChip = RescueStickerFactory.CreateStatusChip(
                tray.transform,
                "6/6 READY",
                RescueStickerFactory.Palette.Mint,
                RescueStickerFactory.Palette.InkPurple);
            SetAnchors(readyChip.GetComponent<RectTransform>(), new Vector2(0.70f, 0.72f), new Vector2(0.955f, 0.96f));

            var row = CreateRect("CurrentSquadRow", trayRect);
            SetAnchors(row, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.72f));
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
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
                var mini = RescueStickerFactory.CreateMiniSquadSticker(
                    row,
                    name,
                    key,
                    UnitIconLibrary.GetPrimaryColor(key),
                    SquadMiniSize);
                AddFixedLayout(mini, SquadMiniSize.x, SquadMiniSize.y);
                NativeMotionKit.PopIn(this, mini.transform as RectTransform, null, 0.22f + index * 0.015f, 0.74f);
            }
        }

        private void CreatePrimaryCta()
        {
            var button = RescueStickerFactory.CreateToyButton(
                contentRoot,
                "Play Ranked",
                Color.Lerp(RescueStickerFactory.Palette.HotPink, RescueStickerFactory.Palette.Coral, 0.12f),
                RescueStickerFactory.Palette.SoftWhite,
                new Vector2(560f, 86f),
                primary: true);
            var rect = button.transform as RectTransform;
            SetAnchors(rect, new Vector2(0.08f, 0.235f), new Vector2(0.92f, 0.325f));
            PolishHomeButton(button, 30f, keepHighlight: true);
            AddEnterTarget(rect);
            NativeMotionKit.BreatheScale(this, rect, 0.025f, 1.10f, true);
            WireButton(button, () => controller?.OpenBattlePlayers(), 0.07f);
        }

        private void CreateSecondaryActions()
        {
            var rootRect = CreateRect("HomeSecondaryActions", contentRoot);
            SetAnchors(rootRect, new Vector2(0.075f, 0.135f), new Vector2(0.925f, 0.215f));
            AddEnterTarget(rootRect);

            CreateSecondaryButton(rootRect, "Edit Squad", new Vector2(0f, 0f), new Vector2(0.235f, 1f), () => controller?.OpenDeckBuilder());
            CreateSecondaryButton(rootRect, "Practice", new Vector2(0.255f, 0f), new Vector2(0.49f, 1f), () => controller?.OpenBattleBot());
            CreateSecondaryButton(rootRect, "Codex", new Vector2(0.51f, 0f), new Vector2(0.745f, 1f), () => controller?.OpenCodex());
            CreateSecondaryButton(rootRect, "Ranks", new Vector2(0.765f, 0f), new Vector2(1f, 1f), () => controller?.OpenLeaderboard());
        }

        private void CreateSecondaryButton(RectTransform parent, string label, Vector2 min, Vector2 max, Action action)
        {
            var button = RescueStickerFactory.CreateToyButton(
                parent,
                label,
                RescueStickerFactory.Palette.ElectricPurple,
                RescueStickerFactory.Palette.SoftWhite,
                new Vector2(134f, 54f),
                primary: false);
            SetAnchors(button.transform as RectTransform, min, max);
            PolishHomeButton(button, 18f, keepHighlight: true);
            WireButton(button, action, 0.06f);
        }

        private void CreateBottomNav()
        {
            var nav = RescueStickerFactory.CreateArenaSurface(
                contentRoot,
                "HomeBottomNav",
                new Color(0.10f, 0.07f, 0.23f, 0.34f),
                RescueStickerFactory.Palette.Aqua,
                Vector2.zero);
            var navRect = nav.GetComponent<RectTransform>();
            SetAnchors(navRect, new Vector2(0.08f, 0.035f), new Vector2(0.92f, 0.105f));
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

            CreateNavButton(row, "Home", RescueStickerFactory.Palette.HotPink, () => { });
            CreateNavButton(row, "Squad", RescueStickerFactory.Palette.ElectricPurple, () => controller?.OpenDeckBuilder());
            CreateNavButton(row, "Codex", RescueStickerFactory.Palette.ElectricPurple, () => controller?.OpenCodex());
            CreateNavButton(row, "Ranks", RescueStickerFactory.Palette.ElectricPurple, () => controller?.OpenLeaderboard());
        }

        private void CreateNavButton(RectTransform parent, string label, Color color, Action action)
        {
            var button = RescueStickerFactory.CreateToyButton(
                parent,
                label,
                color,
                RescueStickerFactory.Palette.SoftWhite,
                new Vector2(120f, 42f),
                primary: label == "Home");
            PolishHomeButton(button, 16f, keepHighlight: label == "Home");
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
                        image.color = new Color(1f, 1f, 1f, 0.16f);
                    }
                }
            }

            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.fontSize = labelSize;
                label.alignment = TextAlignmentOptions.Center;
                label.color = RescueStickerFactory.Palette.SoftWhite;
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
