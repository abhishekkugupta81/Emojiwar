using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Common
{
    /// <summary>
    /// Native UGUI factory for the Emoji War rescue screens.
    /// This creates bright, layered, toy-like primitives without relying on DOTween,
    /// paid packages, scene prefabs, or a new art pipeline.
    /// </summary>
    public static class RescueStickerFactory
    {
        private static Sprite roundedSprite;
        private static Sprite circleSprite;
        private static readonly Dictionary<string, Sprite> ResultUiSprites = new();

        public enum FighterVisualMode
        {
            Hero,
            Clash,
            ResultHero,
            Roster,
            BattleBench,
            Mini,
            Small
        }

        public static class Palette
        {
            public static readonly Color HotPink = new Color32(0xFF, 0x4F, 0xD8, 0xFF);
            public static readonly Color ElectricPurple = new Color32(0x8B, 0x5C, 0xFF, 0xFF);
            public static readonly Color Aqua = new Color32(0x34, 0xD6, 0xFF, 0xFF);
            public static readonly Color SunnyYellow = new Color32(0xFF, 0xD8, 0x4D, 0xFF);
            public static readonly Color Coral = new Color32(0xFF, 0x6E, 0x61, 0xFF);
            public static readonly Color Mint = new Color32(0x61, 0xFF, 0xB8, 0xFF);
            public static readonly Color InkPurple = new Color32(0x18, 0x12, 0x35, 0xFF);
            public static readonly Color SoftWhite = new Color32(0xF8, 0xF7, 0xFF, 0xFF);
            public static readonly Color Grape = new Color32(0x3A, 0x18, 0x74, 0xFF);
            public static readonly Color DeepIndigo = new Color32(0x12, 0x19, 0x46, 0xFF);
            public static readonly Color BlueViolet = new Color32(0x5E, 0x59, 0xFF, 0xFF);
            public static readonly Color Sky = new Color32(0x74, 0xE7, 0xFF, 0xFF);
            public static readonly Color Teal = new Color32(0x37, 0xD8, 0xC6, 0xFF);
            public static readonly Color Cloud = new Color32(0xE8, 0xF2, 0xFF, 0xFF);
        }

        public static GameObject CreateScreenRoot(Transform parent, string name)
        {
            EnsureSprites();
            var root = CreateRectObject(parent, string.IsNullOrWhiteSpace(name) ? "RescueScreenRoot" : name);
            Stretch(root.GetComponent<RectTransform>());
            root.AddComponent<CanvasGroup>();
            return root;
        }

        public static Graphic CreateGradientLikeBackground(Transform parent, string name, Color topColor, Color bottomColor)
        {
            EnsureSprites();
            var background = CreateRectObject(parent, string.IsNullOrWhiteSpace(name) ? "RescueGradientBackground" : name);
            Stretch(background.GetComponent<RectTransform>());
            var gradient = background.AddComponent<GradientQuadGraphic>();
            var midColor = Color.Lerp(topColor, bottomColor, 0.48f);
            var baseColor = Color.Lerp(bottomColor, EmojiWarVisualStyle.Colors.BgBottom, 0.22f);
            gradient.SetColors(topColor, midColor, baseColor);
            background.transform.SetAsFirstSibling();

            CreateBlob(parent, $"{name}BlobA", Color.Lerp(topColor, Palette.HotPink, 0.36f), new Vector2(-136f, 238f), new Vector2(224f, 224f), 0.16f);
            CreateBlob(parent, $"{name}BlobB", Color.Lerp(bottomColor, Palette.Sky, 0.28f), new Vector2(164f, -112f), new Vector2(264f, 264f), 0.14f);
            CreateBlob(parent, $"{name}BlobC", Color.Lerp(Palette.SunnyYellow, Palette.Coral, 0.20f), new Vector2(174f, 256f), new Vector2(132f, 132f), 0.08f);
            CreateBlob(parent, $"{name}BlobD", Color.Lerp(Palette.BlueViolet, Palette.Aqua, 0.38f), new Vector2(-188f, -228f), new Vector2(196f, 196f), 0.08f);
            CreateBlob(parent, $"{name}BlobArena", Color.Lerp(bottomColor, EmojiWarVisualStyle.Colors.BgBottom, 0.30f), new Vector2(0f, -324f), new Vector2(560f, 180f), 0.10f);
            return gradient;
        }

        public static GameObject CreateBlob(Transform parent, string name, Color color, Vector2 anchoredPosition, Vector2 size, float alpha = 0.22f)
        {
            EnsureSprites();
            var blob = CreateRectObject(parent, string.IsNullOrWhiteSpace(name) ? "ColorBlob" : name);
            var rect = blob.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            var image = blob.AddComponent<Image>();
            image.sprite = circleSprite;
            image.color = WithAlpha(color, alpha);
            image.raycastTarget = false;
            return blob;
        }

        public static GameObject CreateResultArtPanel(Transform parent, string name, string spriteName, Vector2 size, Color fallbackColor)
        {
            EnsureSprites();
            var panel = CreateRectObject(parent, string.IsNullOrWhiteSpace(name) ? "ResultArtPanel" : name);
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            var image = panel.AddComponent<Image>();
            image.raycastTarget = false;
            if (!TryApplyResultArt(image, spriteName))
            {
                image.sprite = roundedSprite;
                image.type = Image.Type.Sliced;
                image.color = fallbackColor;
            }

            return panel;
        }

        public static bool TryApplyResultArt(Image image, string spriteName)
        {
            if (image == null || string.IsNullOrWhiteSpace(spriteName))
            {
                return false;
            }

            var sprite = LoadResultUiSprite(spriteName);
            if (sprite == null)
            {
                return false;
            }

            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.color = Color.white;
            return true;
        }

        public static GameObject CreatePhaseHeader(Transform parent, string eyebrow, string title, string progressText)
        {
            var root = CreateRectObject(parent, "PhaseHeader");
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.06f, 0.86f);
            rect.anchorMax = new Vector2(0.94f, 0.965f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CreateLabel(root.transform, "Eyebrow", eyebrow, 14f, FontStyles.Bold, Palette.SunnyYellow, TextAlignmentOptions.Left, new Vector2(0f, 0.70f), new Vector2(0.70f, 1f));
            CreateLabel(root.transform, "Title", title, 28f, FontStyles.Bold, Palette.SoftWhite, TextAlignmentOptions.Left, new Vector2(0f, 0.22f), new Vector2(1f, 0.74f));
            var progressChip = CreateStatusChip(root.transform, progressText, WithAlpha(Palette.InkPurple, 0.72f), Palette.Mint);
            var chipRect = progressChip.GetComponent<RectTransform>();
            chipRect.anchorMin = new Vector2(0.58f, 0.68f);
            chipRect.anchorMax = new Vector2(1f, 1f);
            chipRect.offsetMin = Vector2.zero;
            chipRect.offsetMax = Vector2.zero;

            return root;
        }

        public static GameObject CreateStatusChip(Transform parent, string text, Color bodyColor, Color textColor)
        {
            EnsureSprites();
            var chip = CreateRectObject(parent, "StatusChip");
            var rect = chip.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(132f, 34f);
            var image = chip.AddComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = bodyColor;
            AddOutlineAndShadow(chip, textColor, new Vector2(0f, -3f), 1.5f);
            CreateLabel(chip.transform, "Label", text, 14f, FontStyles.Bold, textColor, TextAlignmentOptions.Center, Vector2.zero, Vector2.one);
            return chip;
        }

        public static Button CreateToyButton(Transform parent, string label, Color bodyColor, Color textColor, Vector2 size, bool primary = false)
        {
            EnsureSprites();
            var buttonObject = CreateRectObject(parent, $"{label}ToyButton");
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            var usesStretchDecoration = size.x <= 0.01f || size.y <= 0.01f;

            var shadow = CreateLayer(buttonObject.transform, "Shadow", Palette.InkPurple, new Vector2(0f, -8f), size, roundedSprite);
            shadow.GetComponent<Image>().color = WithAlpha(Palette.InkPurple, primary ? 0.50f : 0.34f);
            if (usesStretchDecoration)
            {
                SetStretchOffsets(shadow.GetComponent<RectTransform>(), new Vector2(6f, -2f), new Vector2(6f, 10f));
            }

            var body = buttonObject.AddComponent<Image>();
            body.sprite = roundedSprite;
            body.type = Image.Type.Sliced;
            body.color = bodyColor;
            AddOutlineAndShadow(
                buttonObject,
                primary ? Color.Lerp(bodyColor, Palette.SunnyYellow, 0.44f) : WithAlpha(Color.Lerp(Palette.Cloud, bodyColor, 0.10f), 0.92f),
                new Vector2(0f, -4f),
                primary ? 4f : 2.25f);

            var highlight = CreateLayer(buttonObject.transform, "Highlight", Color.white, new Vector2(0f, size.y * 0.22f), new Vector2(size.x * 0.86f, size.y * 0.24f), roundedSprite);
            highlight.GetComponent<Image>().color = WithAlpha(Color.white, primary ? 0.28f : 0.12f);
            if (usesStretchDecoration)
            {
                SetAnchors(highlight.GetComponent<RectTransform>(), new Vector2(0.08f, 0.62f), new Vector2(0.92f, 0.84f));
            }

            var bottomTint = CreateLayer(
                buttonObject.transform,
                "BottomTint",
                primary ? WithAlpha(Color.Lerp(bodyColor, Palette.Coral, 0.18f), 0.44f) : WithAlpha(Palette.DeepIndigo, 0.18f),
                Vector2.zero,
                size,
                roundedSprite);
            bottomTint.transform.SetAsFirstSibling();
            if (usesStretchDecoration)
            {
                SetStretchOffsets(bottomTint.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            }

            var button = buttonObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = body;
            CreateLabel(buttonObject.transform, "Label", label, primary ? 26f : 20f, FontStyles.Bold, textColor, TextAlignmentOptions.Center, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
            return button;
        }

        public static Button CreatePrimaryGoldButton(Transform parent, string label, Vector2 size)
        {
            var button = CreateToyButton(parent, label, EmojiWarVisualStyle.Colors.GoldLight, EmojiWarVisualStyle.Colors.GoldText, size, primary: true);
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = Color.Lerp(EmojiWarVisualStyle.Colors.GoldLight, EmojiWarVisualStyle.Colors.GoldDark, 0.26f);
            }

            var bottomTint = button.transform.Find("BottomTint")?.GetComponent<Image>();
            if (bottomTint != null)
            {
                bottomTint.color = WithAlpha(EmojiWarVisualStyle.Colors.GoldDark, 0.44f);
            }

            var labelText = button.GetComponentInChildren<TMP_Text>(true);
            if (labelText != null)
            {
                labelText.color = EmojiWarVisualStyle.Colors.GoldText;
                labelText.fontSize = 32f;
            }

            return button;
        }

        public static Button CreateSecondaryActionButton(Transform parent, string label, Vector2 size)
        {
            var button = CreateToyButton(parent, label, EmojiWarVisualStyle.Colors.SecondaryActionDark, Palette.SoftWhite, size, primary: false);
            var image = button.GetComponent<Image>();
            if (image != null)
            {
                image.color = Color.Lerp(EmojiWarVisualStyle.Colors.SecondaryAction, EmojiWarVisualStyle.Colors.SecondaryActionDark, 0.50f);
            }

            var accent = CreateLayer(button.transform, "ActionAccent", WithAlpha(EmojiWarVisualStyle.Colors.GoldLight, 0.22f), new Vector2(0f, size.y * 0.22f), new Vector2(size.x * 0.28f, Mathf.Max(10f, size.y * 0.12f)), roundedSprite);
            accent.transform.SetSiblingIndex(2);
            var accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0.5f, 0.78f);
            accentRect.anchorMax = new Vector2(0.5f, 0.78f);

            var labelText = button.GetComponentInChildren<TMP_Text>(true);
            if (labelText != null)
            {
                labelText.fontSize = 18f;
            }

            return button;
        }

        public static GameObject CreateLightBottomNavPlate(Transform parent, string name, Vector2 size)
        {
            var plate = CreateArenaSurface(parent, name, EmojiWarVisualStyle.Colors.BottomPlate, EmojiWarVisualStyle.Colors.BottomPlateBorder, size);
            var image = plate.GetComponent<Image>();
            if (image != null)
            {
                image.color = EmojiWarVisualStyle.Colors.BottomPlate;
            }

            return plate;
        }

        public static GameObject CreateGlassPanel(Transform parent, string name, Vector2 size, bool strong = false)
        {
            return CreateArenaSurface(
                parent,
                name,
                strong ? EmojiWarVisualStyle.Colors.PanelFillStrong : EmojiWarVisualStyle.Colors.PanelFillSoft,
                EmojiWarVisualStyle.Colors.PanelBorder,
                size);
        }

        public static RectTransform CreateOpenHeroStage(Transform parent, string name)
        {
            var stage = CreateRectObject(parent, string.IsNullOrWhiteSpace(name) ? "OpenHeroStage" : name).GetComponent<RectTransform>();
            Stretch(stage);

            CreateBlob(stage, $"{stage.name}TopAura", Color.Lerp(Palette.HotPink, Palette.ElectricPurple, 0.42f), new Vector2(0f, 130f), new Vector2(520f, 240f), 0.12f);
            CreateBlob(stage, $"{stage.name}FloorGlow", Color.Lerp(Palette.Aqua, Palette.Mint, 0.20f), new Vector2(0f, -170f), new Vector2(560f, 180f), 0.16f);
            CreateBlob(stage, $"{stage.name}SideGlowLeft", Color.Lerp(Palette.HotPink, Palette.SunnyYellow, 0.18f), new Vector2(-230f, 10f), new Vector2(180f, 220f), 0.08f);
            CreateBlob(stage, $"{stage.name}SideGlowRight", Color.Lerp(Palette.Aqua, Palette.BlueViolet, 0.28f), new Vector2(230f, 0f), new Vector2(180f, 220f), 0.08f);
            return stage;
        }

        public static GameObject CreateResultHeroScoreCard(Transform parent, string title, string scoreLine)
        {
            var card = CreateResultArtPanel(
                parent,
                "HeroScoreCard",
                "result_score_plaque",
                Vector2.zero,
                WithAlpha(Color.Lerp(EmojiWarVisualStyle.Colors.Depth, Palette.InkPurple, 0.18f), 0.95f));
            var image = card.GetComponent<Image>();
            if (image != null && image.sprite == roundedSprite)
            {
                image.color = WithAlpha(Color.Lerp(EmojiWarVisualStyle.Colors.Depth, Palette.InkPurple, 0.18f), 0.95f);
            }

            var pill = CreateStatusChip(card.transform, title.ToUpperInvariant(), Palette.SunnyYellow, Palette.InkPurple);
            var pillRect = pill.GetComponent<RectTransform>();
            pillRect.anchorMin = new Vector2(0.285f, 0.790f);
            pillRect.anchorMax = new Vector2(0.715f, 0.945f);
            pillRect.offsetMin = Vector2.zero;
            pillRect.offsetMax = Vector2.zero;
            var score = CreateLabel(card.transform, "ScoreLine", scoreLine, 106f, FontStyles.Bold, EmojiWarVisualStyle.Colors.GoldLight, TextAlignmentOptions.Center, new Vector2(0.05f, 0.225f), new Vector2(0.95f, 0.765f));
            score.enableAutoSizing = true;
            score.fontSizeMax = 106f;
            score.fontSizeMin = 58f;
            score.richText = true;
            AddOutlineAndShadow(score.gameObject, Palette.SoftWhite, new Vector2(0f, -6f), 3.4f);
            CreateLabel(card.transform, "ScoreSupport", "You vs Rival", 20f, FontStyles.Bold, Palette.SoftWhite, TextAlignmentOptions.Center, new Vector2(0.10f, 0.070f), new Vector2(0.90f, 0.225f));
            return card;
        }

        public static GameObject CreateCompactTimelineRow(Transform parent, string leading, string body)
        {
            var row = CreateGlassPanel(parent, "CompactTimelineRow", EmojiWarVisualStyle.Layout.CompactTimelineRow);
            CreateLabel(row.transform, "Lead", leading, 17f, FontStyles.Bold, EmojiWarVisualStyle.Colors.GoldLight, TextAlignmentOptions.Center, new Vector2(0.03f, 0.18f), new Vector2(0.18f, 0.82f));
            CreateLabel(row.transform, "Body", body, 16f, FontStyles.Bold, Palette.SoftWhite, TextAlignmentOptions.Left, new Vector2(0.22f, 0.16f), new Vector2(0.96f, 0.84f));
            return row;
        }

        public static GameObject CreateEmojiAvatar(Transform parent, string emoji, string fallbackText, Color auraColor, Vector2 size)
        {
            return CreateSmallUnitAvatar(parent, emoji, fallbackText, auraColor, size);
        }

        public static GameObject CreateHeroFighter(Transform parent, string unitKey, string displayName, Color auraColor, Vector2 size)
        {
            return CreateLargeFighterVisual(parent, unitKey, displayName, auraColor, size, FighterVisualMode.Hero);
        }

        public static GameObject CreateClashFighter(Transform parent, string unitKey, string displayName, Color auraColor, Vector2 size)
        {
            return CreateLargeFighterVisual(parent, unitKey, displayName, auraColor, size, FighterVisualMode.Clash);
        }

        public static GameObject CreateResultHeroFighter(Transform parent, string unitKey, string displayName, Color auraColor, Vector2 size)
        {
            return CreateLargeFighterVisual(parent, unitKey, displayName, auraColor, size, FighterVisualMode.ResultHero);
        }

        public static GameObject CreateRosterFighter(Transform parent, string unitKey, string displayName, Color auraColor, Vector2 size)
        {
            return CreateStickerAvatar(parent, unitKey, displayName, auraColor, size, FighterVisualMode.Roster);
        }

        public static GameObject CreateBattleBenchFighter(Transform parent, string unitKey, string displayName, Color auraColor, Vector2 size)
        {
            return CreateStickerAvatar(parent, unitKey, displayName, auraColor, size, FighterVisualMode.BattleBench);
        }

        public static GameObject CreateSmallUnitAvatar(Transform parent, string unitKey, string displayName, Color auraColor, Vector2 size)
        {
            return CreateStickerAvatar(parent, unitKey, displayName, auraColor, size, FighterVisualMode.Small);
        }

        private static GameObject CreateLargeFighterVisual(Transform parent, string unitKey, string displayName, Color auraColor, Vector2 size, FighterVisualMode mode)
        {
            EnsureSprites();
            var normalizedKey = UnitIconLibrary.NormalizeUnitKey(unitKey);
            var sprite = UnitIconLibrary.GetPortraitSprite(normalizedKey);
            var portraitBacked = UnitIconLibrary.HasPortraitSprite(normalizedKey);
            var primary = UnitIconLibrary.GetPrimaryColor(normalizedKey);
            var secondary = UnitIconLibrary.GetSecondaryColor(normalizedKey);
            var fighter = CreateRectObject(parent, "EmojiAvatar");
            var rect = fighter.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            fighter.AddComponent<CanvasGroup>();

            var dramatic = mode == FighterVisualMode.Clash;
            var resultHero = mode == FighterVisualMode.ResultHero;
            var portraitGlowAlpha = portraitBacked ? (dramatic ? 0.16f : resultHero ? 0.13f : 0.10f) : dramatic ? 0.22f : resultHero ? 0.18f : 0.16f;
            var portraitScaleBoost = portraitBacked
                ? dramatic
                    ? 1.14f
                    : resultHero
                        ? 1.10f
                        : 1.04f
                : 1f;
            var shadow = CreateLayer(
                fighter.transform,
                "GroundShadow",
                EmojiWarVisualStyle.Colors.Depth,
                new Vector2(0f, portraitBacked ? (dramatic ? -size.y * 0.33f : resultHero ? -size.y * 0.31f : -size.y * 0.30f) : -size.y * 0.27f),
                new Vector2(size.x * (portraitBacked ? (dramatic ? 0.54f : resultHero ? 0.52f : 0.50f) : 0.56f), size.y * (portraitBacked ? 0.10f : 0.13f)),
                circleSprite);
            shadow.GetComponent<Image>().color = WithAlpha(EmojiWarVisualStyle.Colors.Depth, portraitBacked ? 0.34f : dramatic ? 0.52f : resultHero ? 0.46f : 0.42f);

            var floorGlow = CreateLayer(
                fighter.transform,
                "FloorGlow",
                Color.Lerp(primary, Palette.SunnyYellow, 0.18f),
                new Vector2(0f, portraitBacked ? (dramatic ? -size.y * 0.28f : resultHero ? -size.y * 0.26f : -size.y * 0.25f) : -size.y * 0.23f),
                new Vector2(size.x * (portraitBacked ? (dramatic ? 0.52f : resultHero ? 0.48f : 0.46f) : 0.54f), size.y * (portraitBacked ? 0.10f : 0.13f)),
                circleSprite);
            floorGlow.GetComponent<Image>().color = WithAlpha(Color.Lerp(primary, Palette.SunnyYellow, 0.18f), portraitBacked ? 0.18f : dramatic ? 0.34f : resultHero ? 0.30f : 0.26f);

            var auraBurst = CreateLayer(
                fighter.transform,
                "AuraBurst",
                Color.Lerp(auraColor, secondary, 0.20f),
                new Vector2(0f, portraitBacked ? size.y * 0.02f : -size.y * 0.02f),
                size * (portraitBacked ? (dramatic ? 1.00f : 0.94f) : dramatic ? 1.12f : resultHero ? 1.06f : 1.02f),
                circleSprite);
            auraBurst.GetComponent<Image>().color = WithAlpha(Color.Lerp(auraColor, secondary, 0.20f), portraitGlowAlpha);

            var backGlow = CreateLayer(
                fighter.transform,
                "BackGlow",
                Color.Lerp(primary, Palette.Cloud, 0.16f),
                new Vector2(0f, portraitBacked ? size.y * 0.02f : -size.y * 0.01f),
                size * (portraitBacked ? 0.80f : 0.90f),
                circleSprite);
            backGlow.GetComponent<Image>().color = WithAlpha(Color.Lerp(primary, Palette.Cloud, 0.16f), portraitBacked ? 0.08f : 0.12f);

            var spriteAnchor = CreateRectObject(fighter.transform, "FighterVisual");
            SetAnchors(
                spriteAnchor.GetComponent<RectTransform>(),
                portraitBacked
                    ? dramatic
                        ? new Vector2(0.00f, 0.01f)
                        : resultHero
                            ? new Vector2(0.01f, 0.02f)
                            : new Vector2(0.01f, 0.03f)
                    : new Vector2(0.06f, 0.04f),
                portraitBacked
                    ? dramatic
                        ? new Vector2(1.00f, 1.00f)
                        : resultHero
                            ? new Vector2(0.99f, 0.99f)
                            : new Vector2(0.99f, 0.98f)
                    : new Vector2(0.94f, 0.94f));

            CreateSpriteLayer(
                spriteAnchor.transform,
                "SpriteShadow",
                sprite,
                WithAlpha(EmojiWarVisualStyle.Colors.Depth, 0.46f),
                new Vector2(portraitBacked ? size.x * 0.016f : size.x * 0.026f, portraitBacked ? -size.y * 0.016f : -size.y * 0.010f),
                (dramatic ? 1.01f : resultHero ? 1.00f : 0.99f) * portraitScaleBoost);
            CreateSpriteLayer(
                spriteAnchor.transform,
                "SpriteAura",
                sprite,
                WithAlpha(Color.Lerp(auraColor, secondary, 0.18f), portraitBacked ? 0.20f : dramatic ? 0.54f : resultHero ? 0.46f : 0.44f),
                Vector2.zero,
                (portraitBacked ? (dramatic ? 1.03f : resultHero ? 1.02f : 1.01f) : dramatic ? 1.05f : resultHero ? 1.04f : 1.03f) * portraitScaleBoost);
            CreateSpriteLayer(
                spriteAnchor.transform,
                "SpriteOutline",
                sprite,
                WithAlpha(Palette.SoftWhite, 0.98f),
                Vector2.zero,
                (portraitBacked ? (dramatic ? 1.00f : resultHero ? 0.998f : 0.995f) : dramatic ? 0.985f : resultHero ? 0.980f : 0.975f) * portraitScaleBoost);
            var main = CreateSpriteLayer(
                spriteAnchor.transform,
                "UnitIconSprite",
                sprite,
                Color.white,
                Vector2.zero,
                (portraitBacked ? (dramatic ? 1.02f : resultHero ? 1.01f : 1.00f) : dramatic ? 0.93f : resultHero ? 0.925f : 0.91f) * portraitScaleBoost);
            var mainRect = main.GetComponent<RectTransform>();
            mainRect.anchorMin = new Vector2(0.5f, portraitBacked ? (dramatic ? 0.48f : resultHero ? 0.49f : 0.50f) : 0.52f);
            mainRect.anchorMax = new Vector2(0.5f, portraitBacked ? (dramatic ? 0.48f : resultHero ? 0.49f : 0.50f) : 0.52f);

            var shine = CreateLayer(
                fighter.transform,
                "Shine",
                Color.white,
                new Vector2(-size.x * 0.12f, size.y * 0.16f),
                new Vector2(size.x * 0.14f, size.y * 0.08f),
                circleSprite);
            shine.GetComponent<Image>().color = WithAlpha(Color.white, portraitBacked ? 0.08f : 0.14f);

            var sparkleA = CreateLayer(
                fighter.transform,
                "SparkleA",
                Palette.SunnyYellow,
                new Vector2(size.x * 0.22f, size.y * 0.18f),
                new Vector2(size.x * 0.06f, size.x * 0.06f),
                circleSprite);
            sparkleA.GetComponent<Image>().color = WithAlpha(Palette.SunnyYellow, portraitBacked ? 0.08f : 0.18f);

            var sparkleB = CreateLayer(
                fighter.transform,
                "SparkleB",
                Palette.Cloud,
                new Vector2(-size.x * 0.24f, size.y * 0.02f),
                new Vector2(size.x * 0.05f, size.x * 0.05f),
                circleSprite);
            sparkleB.GetComponent<Image>().color = WithAlpha(Palette.Cloud, portraitBacked ? 0.08f : 0.18f);

            return fighter;
        }

        private static GameObject CreateStickerAvatar(Transform parent, string unitKey, string displayName, Color auraColor, Vector2 size, FighterVisualMode mode)
        {
            EnsureSprites();
            var normalizedKey = UnitIconLibrary.NormalizeUnitKey(unitKey);
            var sprite = UnitIconLibrary.GetSmallIconSprite(normalizedKey);
            var primary = UnitIconLibrary.GetPrimaryColor(normalizedKey);
            var secondary = UnitIconLibrary.GetSecondaryColor(normalizedKey);
            var avatar = CreateRectObject(parent, "EmojiAvatar");
            var rect = avatar.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            avatar.AddComponent<CanvasGroup>();
            var isMini = mode == FighterVisualMode.Mini;
            var isSmall = mode == FighterVisualMode.Small;
            var isBattleBench = mode == FighterVisualMode.BattleBench;

            var shadow = CreateLayer(
                avatar.transform,
                "GroundShadow",
                Palette.InkPurple,
                new Vector2(0f, -size.y * (isBattleBench ? 0.20f : isMini ? 0.14f : 0.17f)),
                new Vector2(size.x * (isBattleBench ? 0.50f : 0.62f), size.y * (isBattleBench ? 0.14f : 0.20f)),
                circleSprite);
            shadow.GetComponent<Image>().color = WithAlpha(EmojiWarVisualStyle.Colors.Depth, isBattleBench ? 0.18f : isSmall ? 0.24f : 0.32f);

            var burst = CreateLayer(
                avatar.transform,
                "AvatarBurst",
                Color.Lerp(auraColor, Palette.Cloud, 0.16f),
                new Vector2(0f, size.y * (isBattleBench ? 0.01f : isMini ? 0.02f : 0.04f)),
                new Vector2(size.x * (isBattleBench ? 1.04f : 0.92f), size.y * (isBattleBench ? 0.84f : 0.72f)),
                circleSprite);
            burst.GetComponent<Image>().color = WithAlpha(Color.Lerp(auraColor, Palette.Cloud, 0.16f), isBattleBench ? 0.14f : isSmall ? 0.14f : 0.20f);

            if (!isBattleBench)
            {
                var plate = CreateLayer(
                    avatar.transform,
                    "StickerPlate",
                    Palette.SoftWhite,
                    new Vector2(0f, size.y * 0.03f),
                    new Vector2(size.x * 0.78f, size.x * 0.78f),
                    circleSprite);
                plate.GetComponent<Image>().color = WithAlpha(Palette.SoftWhite, 0.96f);

                var backplate = CreateLayer(
                    avatar.transform,
                    "StickerBacking",
                    Color.Lerp(primary, secondary, 0.24f),
                    new Vector2(0f, size.y * 0.04f),
                    new Vector2(size.x * 0.66f, size.x * 0.66f),
                    circleSprite);
                backplate.GetComponent<Image>().color = WithAlpha(Color.Lerp(primary, secondary, 0.24f), isMini ? 0.70f : 0.78f);
            }
            else
            {
                var streak = CreateLayer(
                    avatar.transform,
                    "BenchGlow",
                    Color.Lerp(primary, secondary, 0.26f),
                    new Vector2(0f, -size.y * 0.07f),
                    new Vector2(size.x * 0.74f, size.y * 0.26f),
                    circleSprite);
                streak.GetComponent<Image>().color = WithAlpha(Color.Lerp(primary, secondary, 0.26f), 0.12f);
            }

            var iconRoot = CreateRectObject(avatar.transform, "FighterVisual");
            SetAnchors(
                iconRoot.GetComponent<RectTransform>(),
                isBattleBench ? new Vector2(0.01f, 0.06f) : new Vector2(0.08f, 0.14f),
                isBattleBench ? new Vector2(0.99f, 0.98f) : new Vector2(0.92f, isMini ? 0.96f : 0.94f));

            CreateSpriteLayer(iconRoot.transform, "SpriteAura", sprite, WithAlpha(auraColor, isBattleBench ? 0.26f : 0.42f), Vector2.zero, isBattleBench ? 1.12f : 1.04f);
            CreateSpriteLayer(iconRoot.transform, "SpriteOutline", sprite, Palette.SoftWhite, Vector2.zero, isBattleBench ? 1.04f : 0.985f);
            CreateSpriteLayer(iconRoot.transform, "UnitIconSprite", sprite, Color.white, Vector2.zero, isBattleBench ? 1.00f : 0.90f);

            if (!isMini)
            {
                var shine = CreateLayer(
                    avatar.transform,
                    "Shine",
                    Color.white,
                    new Vector2(-size.x * (isBattleBench ? 0.08f : 0.12f), size.y * (isBattleBench ? 0.10f : 0.14f)),
                    new Vector2(size.x * (isBattleBench ? 0.10f : 0.12f), size.y * (isBattleBench ? 0.06f : 0.08f)),
                    circleSprite);
                shine.GetComponent<Image>().color = WithAlpha(Color.white, isBattleBench ? 0.12f : 0.18f);
            }

            return avatar;
        }

        public static GameObject CreateUnitStickerCard(
            Transform parent,
            string unitName,
            string emoji,
            string role,
            Color cardColor,
            Color auraColor,
            bool selected,
            bool disabled,
            Vector2 size)
        {
            EnsureSprites();
            var card = CreateRectObject(parent, $"{unitName}StickerCard");
            var rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            card.AddComponent<CanvasGroup>().alpha = disabled ? 0.48f : 1f;

            var shadow = CreateLayer(card.transform, "CardShadow", Palette.InkPurple, new Vector2(0f, -9f), size, roundedSprite);
            shadow.GetComponent<Image>().color = WithAlpha(Palette.InkPurple, 0.50f);

            var body = card.AddComponent<Image>();
            body.sprite = roundedSprite;
            body.type = Image.Type.Sliced;
            body.color = selected ? Color.Lerp(cardColor, auraColor, 0.22f) : cardColor;
            AddOutlineAndShadow(card, selected ? Palette.SunnyYellow : Palette.SoftWhite, new Vector2(0f, -5f), selected ? 5f : 2.5f);

            var avatar = CreateClashFighter(card.transform, emoji, unitName, auraColor, new Vector2(size.x * 0.88f, size.y * 0.72f));
            var avatarRect = avatar.GetComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0.5f, 0.58f);
            avatarRect.anchorMax = new Vector2(0.5f, 0.58f);
            avatarRect.anchoredPosition = Vector2.zero;

            CreateLabel(card.transform, "UnitName", unitName, Mathf.Max(19f, size.y * 0.12f), FontStyles.Bold, Palette.SoftWhite, TextAlignmentOptions.Center, new Vector2(0.08f, 0.145f), new Vector2(0.92f, 0.29f));
            var roleChip = CreateStatusChip(card.transform, role, WithAlpha(auraColor, 0.92f), Palette.InkPurple);
            var roleRect = roleChip.GetComponent<RectTransform>();
            roleRect.anchorMin = new Vector2(0.20f, 0.035f);
            roleRect.anchorMax = new Vector2(0.80f, 0.145f);
            roleRect.offsetMin = Vector2.zero;
            roleRect.offsetMax = Vector2.zero;

            if (selected)
            {
                var badge = CreateStatusChip(card.transform, "PICKED", Palette.SunnyYellow, Palette.InkPurple);
                var badgeRect = badge.GetComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(0.52f, 0.84f);
                badgeRect.anchorMax = new Vector2(0.98f, 0.98f);
                badgeRect.offsetMin = Vector2.zero;
                badgeRect.offsetMax = Vector2.zero;
            }

            if (disabled)
            {
                var overlay = CreateLayer(card.transform, "DisabledOverlay", Palette.InkPurple, Vector2.zero, size, roundedSprite);
                overlay.GetComponent<Image>().color = WithAlpha(Palette.InkPurple, 0.46f);
            }

            return card;
        }

        public static GameObject CreateCompactUnitStickerTile(
            Transform parent,
            string unitName,
            string unitKey,
            string role,
            Color cardColor,
            Color auraColor,
            bool selected,
            bool disabled,
            Vector2 size,
            int selectedOrder)
        {
            EnsureSprites();
            var card = CreateRectObject(parent, $"{unitName}CompactStickerTile");
            var rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            card.AddComponent<CanvasGroup>().alpha = disabled ? 0.48f : 1f;

            var shadow = CreateLayer(card.transform, "TileShadow", Palette.InkPurple, new Vector2(0f, -5f), size, roundedSprite);
            shadow.GetComponent<Image>().color = WithAlpha(Palette.InkPurple, 0.42f);

            var body = card.AddComponent<Image>();
            body.sprite = roundedSprite;
            body.type = Image.Type.Sliced;
            body.color = selected ? Color.Lerp(cardColor, auraColor, 0.30f) : Color.Lerp(cardColor, Palette.InkPurple, 0.10f);
            AddOutlineAndShadow(card, selected ? Palette.SunnyYellow : WithAlpha(Palette.SoftWhite, 0.86f), new Vector2(0f, -3f), selected ? 4.5f : 2f);

            var iconSide = Mathf.Min(size.x * 0.68f, size.y * 0.72f);
            var avatar = CreateSmallUnitAvatar(card.transform, unitKey, unitName, auraColor, new Vector2(iconSide, iconSide));
            var avatarRect = avatar.GetComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0.5f, 0.68f);
            avatarRect.anchorMax = new Vector2(0.5f, 0.68f);
            avatarRect.anchoredPosition = Vector2.zero;

            var nameLabel = CreateLabel(
                card.transform,
                "UnitName",
                unitName,
                Mathf.Clamp(size.y * 0.12f, 10f, 15f),
                FontStyles.Bold,
                Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.145f),
                new Vector2(0.95f, 0.295f));
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;

            var rolePill = CreateLayer(
                card.transform,
                "RolePill",
                WithAlpha(Color.Lerp(auraColor, Palette.SoftWhite, 0.08f), 1f),
                Vector2.zero,
                new Vector2(size.x * 0.52f, Mathf.Max(16f, size.y * 0.135f)),
                roundedSprite);
            var roleRect = rolePill.GetComponent<RectTransform>();
            roleRect.anchorMin = new Vector2(0.5f, 0.082f);
            roleRect.anchorMax = new Vector2(0.5f, 0.082f);
            roleRect.anchoredPosition = Vector2.zero;
            CreateLabel(
                rolePill.transform,
                "Role",
                role,
                Mathf.Clamp(size.y * 0.085f, 7f, 10f),
                FontStyles.Bold,
                Palette.InkPurple,
                TextAlignmentOptions.Center,
                Vector2.zero,
                Vector2.one);

            if (selected)
            {
                var badgeSize = Mathf.Clamp(size.y * 0.22f, 22f, 30f);
                var badge = CreateLayer(card.transform, "SelectedOrderBadge", Palette.SunnyYellow, Vector2.zero, new Vector2(badgeSize, badgeSize), circleSprite);
                var badgeRect = badge.GetComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(0.88f, 0.84f);
                badgeRect.anchorMax = new Vector2(0.88f, 0.84f);
                badgeRect.anchoredPosition = Vector2.zero;
                AddOutlineAndShadow(badge, Palette.SoftWhite, new Vector2(0f, -2f), 1.5f);
                CreateLabel(
                    badge.transform,
                    "Order",
                    selectedOrder > 0 ? selectedOrder.ToString() : "OK",
                    Mathf.Clamp(size.y * 0.13f, 11f, 15f),
                    FontStyles.Bold,
                    Palette.InkPurple,
                    TextAlignmentOptions.Center,
                    Vector2.zero,
                    Vector2.one);
            }

            if (disabled)
            {
                var overlay = CreateLayer(card.transform, "DisabledOverlay", Palette.InkPurple, Vector2.zero, size, roundedSprite);
                overlay.GetComponent<Image>().color = WithAlpha(Palette.InkPurple, 0.38f);
            }

            return card;
        }

        public static GameObject CreateRosterStickerTile(
            Transform parent,
            string unitName,
            string unitKey,
            Color cardColor,
            Color auraColor,
            bool selected,
            bool disabled,
            Vector2 size,
            int selectedOrder)
        {
            EnsureSprites();
            var card = CreateRectObject(parent, $"{unitName}CompactStickerTile");
            var rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            card.AddComponent<CanvasGroup>().alpha = disabled ? 0.48f : 1f;

            var shadow = CreateLayer(card.transform, "TileShadow", Palette.InkPurple, new Vector2(0f, -5f), size, roundedSprite);
            shadow.GetComponent<Image>().color = WithAlpha(Palette.InkPurple, 0.42f);

            var body = card.AddComponent<Image>();
            body.sprite = roundedSprite;
            body.type = Image.Type.Sliced;
            body.color = selected ? Color.Lerp(cardColor, auraColor, 0.30f) : Color.Lerp(cardColor, Palette.InkPurple, 0.10f);
            AddOutlineAndShadow(card, selected ? Palette.SunnyYellow : WithAlpha(Palette.SoftWhite, 0.86f), new Vector2(0f, -3f), selected ? 4.5f : 2f);

            var iconSide = Mathf.Min(size.x * 0.68f, size.y * 0.72f);
            var avatar = CreateRosterFighter(card.transform, unitKey, unitName, auraColor, new Vector2(iconSide, iconSide));
            var avatarRect = avatar.GetComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0.5f, 0.68f);
            avatarRect.anchorMax = new Vector2(0.5f, 0.68f);
            avatarRect.anchoredPosition = Vector2.zero;

            var nameLabel = CreateLabel(
                card.transform,
                "UnitName",
                unitName,
                Mathf.Clamp(size.y * 0.12f, 10f, 15f),
                FontStyles.Bold,
                Palette.SoftWhite,
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.145f),
                new Vector2(0.95f, 0.295f));
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;

            if (selected)
            {
                var badgeSize = Mathf.Clamp(size.y * 0.22f, 22f, 30f);
                var badge = CreateLayer(card.transform, "SelectedOrderBadge", Palette.SunnyYellow, Vector2.zero, new Vector2(badgeSize, badgeSize), circleSprite);
                var badgeRect = badge.GetComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(0.88f, 0.84f);
                badgeRect.anchorMax = new Vector2(0.88f, 0.84f);
                badgeRect.anchoredPosition = Vector2.zero;
                AddOutlineAndShadow(badge, Palette.SoftWhite, new Vector2(0f, -2f), 1.5f);
                CreateLabel(
                    badge.transform,
                    "Order",
                    selectedOrder > 0 ? selectedOrder.ToString() : "OK",
                    Mathf.Clamp(size.y * 0.13f, 11f, 15f),
                    FontStyles.Bold,
                    Palette.InkPurple,
                    TextAlignmentOptions.Center,
                    Vector2.zero,
                    Vector2.one);
            }

            if (disabled)
            {
                var overlay = CreateLayer(card.transform, "DisabledOverlay", Palette.InkPurple, Vector2.zero, size, roundedSprite);
                overlay.GetComponent<Image>().color = WithAlpha(Palette.InkPurple, 0.38f);
            }

            return card;
        }

        public static GameObject CreateBattleBenchSticker(
            Transform parent,
            string unitName,
            string unitKey,
            Color auraColor,
            bool selected,
            bool disabled,
            Vector2 size,
            int selectedOrder)
        {
            EnsureSprites();
            var tile = CreateRectObject(parent, $"{unitName}BattleBenchSticker");
            var rect = tile.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            tile.AddComponent<CanvasGroup>().alpha = disabled ? 0.54f : 1f;

            var footing = CreateLayer(
                tile.transform,
                "BenchFooting",
                Color.Lerp(auraColor, Palette.Cloud, 0.14f),
                new Vector2(0f, -size.y * 0.22f),
                new Vector2(size.x * 0.88f, size.y * 0.16f),
                roundedSprite);
            footing.GetComponent<Image>().color = WithAlpha(Color.Lerp(auraColor, Palette.Cloud, 0.14f), disabled ? 0.05f : selected ? 0.20f : 0.12f);

            var shadow = CreateLayer(
                tile.transform,
                "TileShadow",
                EmojiWarVisualStyle.Colors.Depth,
                new Vector2(0f, -size.y * 0.24f),
                new Vector2(size.x * 0.82f, size.y * 0.18f),
                roundedSprite);
            shadow.GetComponent<Image>().color = WithAlpha(EmojiWarVisualStyle.Colors.Depth, selected ? 0.16f : 0.045f);

            var body = tile.AddComponent<Image>();
            body.sprite = roundedSprite;
            body.type = Image.Type.Sliced;
            body.color = disabled
                ? new Color(0.10f, 0.13f, 0.28f, 0.018f)
                : selected
                    ? new Color(0.15f, 0.22f, 0.46f, 0.11f)
                    : new Color(0.10f, 0.12f, 0.28f, 0.006f);
            if (selected)
            {
                AddOutlineAndShadow(tile, EmojiWarVisualStyle.Colors.GoldLight, new Vector2(0f, -2f), 2.1f);
            }

            var glow = CreateLayer(
                tile.transform,
                "BenchGlow",
                Color.Lerp(auraColor, Palette.SoftWhite, 0.08f),
                new Vector2(0f, size.y * 0.06f),
                new Vector2(size.x * 1.08f, size.y * 0.82f),
                circleSprite);
            glow.GetComponent<Image>().color = WithAlpha(Color.Lerp(auraColor, Palette.SoftWhite, 0.08f), disabled ? 0.035f : selected ? 0.18f : 0.075f);

            var laneDust = CreateLayer(
                tile.transform,
                "LaneDust",
                Color.Lerp(auraColor, Palette.Cloud, 0.10f),
                new Vector2(0f, -size.y * 0.08f),
                new Vector2(size.x * 0.98f, size.y * 0.42f),
                circleSprite);
            laneDust.GetComponent<Image>().color = WithAlpha(Color.Lerp(auraColor, Palette.Cloud, 0.10f), disabled ? 0.03f : selected ? 0.10f : 0.05f);

            var iconSide = Mathf.Min(size.x * 1.10f, size.y * 1.45f);
            var avatar = CreateBattleBenchFighter(tile.transform, unitKey, unitName, auraColor, new Vector2(iconSide, iconSide));
            var avatarRect = avatar.GetComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0.5f, 0.59f);
            avatarRect.anchorMax = new Vector2(0.5f, 0.59f);
            avatarRect.anchoredPosition = Vector2.zero;

            var nameLabel = CreateLabel(
                tile.transform,
                "UnitName",
                unitName,
                Mathf.Clamp(size.y * 0.080f, 10f, 12f),
                FontStyles.Bold,
                WithAlpha(Palette.SoftWhite, disabled ? 0.42f : 0.58f),
                TextAlignmentOptions.Center,
                new Vector2(0.08f, 0.02f),
                new Vector2(0.92f, 0.07f));
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.gameObject.SetActive(false);

            if (selected)
            {
                var badgeSize = Mathf.Clamp(size.y * 0.17f, 18f, 22f);
                var badge = CreateLayer(tile.transform, "SelectedOrderBadge", EmojiWarVisualStyle.Colors.GoldLight, Vector2.zero, new Vector2(badgeSize, badgeSize), circleSprite);
                var badgeRect = badge.GetComponent<RectTransform>();
                badgeRect.anchorMin = new Vector2(0.84f, 0.80f);
                badgeRect.anchorMax = new Vector2(0.84f, 0.80f);
                badgeRect.anchoredPosition = Vector2.zero;
                AddOutlineAndShadow(badge, Palette.SoftWhite, new Vector2(0f, -1.5f), 1.25f);
                CreateLabel(
                    badge.transform,
                    "Order",
                    selectedOrder > 0 ? selectedOrder.ToString() : "OK",
                    Mathf.Clamp(size.y * 0.11f, 10f, 12f),
                    FontStyles.Bold,
                    Palette.InkPurple,
                    TextAlignmentOptions.Center,
                    Vector2.zero,
                    Vector2.one);
            }

            if (disabled)
            {
                var overlay = CreateLayer(tile.transform, "DisabledOverlay", Palette.InkPurple, Vector2.zero, size, roundedSprite);
                overlay.GetComponent<Image>().color = WithAlpha(EmojiWarVisualStyle.Colors.Depth, 0.14f);
            }

            return tile;
        }

        public static GameObject CreateMiniSquadSticker(Transform parent, string unitName, string emoji, Color color, Vector2 size)
        {
            EnsureSprites();
            var card = CreateRectObject(parent, $"{unitName}MiniSquadSticker");
            card.GetComponent<RectTransform>().sizeDelta = size;
            var image = card.AddComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.Lerp(EmojiWarVisualStyle.Colors.Depth, color, 0.52f);
            AddOutlineAndShadow(card, Color.Lerp(color, Palette.Cloud, 0.22f), new Vector2(0f, -4f), 3.0f);

            var sheen = CreateLayer(card.transform, "TopSheen", Color.white, Vector2.zero, new Vector2(size.x * 0.82f, Mathf.Max(12f, size.y * 0.16f)), roundedSprite);
            var sheenRect = sheen.GetComponent<RectTransform>();
            sheenRect.anchorMin = new Vector2(0.5f, 0.84f);
            sheenRect.anchorMax = new Vector2(0.5f, 0.84f);
            sheen.GetComponent<Image>().color = WithAlpha(Color.white, 0.12f);

            var avatar = CreateStickerAvatar(card.transform, emoji, unitName, color, new Vector2(size.x * 1.00f, size.y * 0.88f), FighterVisualMode.Mini);
            var avatarRect = avatar.GetComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0.5f, 0.64f);
            avatarRect.anchorMax = new Vector2(0.5f, 0.64f);
            avatarRect.anchoredPosition = Vector2.zero;
            CreateLabel(card.transform, "Name", unitName, Mathf.Max(11f, size.y * 0.115f), FontStyles.Bold, Palette.SoftWhite, TextAlignmentOptions.Center, new Vector2(0.04f, 0.01f), new Vector2(0.96f, 0.18f));
            return card;
        }

        public static GameObject CreateFloatingMiniSquadSticker(Transform parent, string unitName, string unitKey, Color color, Vector2 size)
        {
            EnsureSprites();
            var sticker = CreateRectObject(parent, $"{unitName}FloatingMiniSquadSticker");
            var stickerRect = sticker.GetComponent<RectTransform>();
            stickerRect.sizeDelta = size;

            var visual = CreateRectObject(sticker.transform, "FloatingStickerVisual");
            SetAnchors(visual.GetComponent<RectTransform>(), new Vector2(0.01f, 0.12f), new Vector2(0.99f, 1f));

            var normalizedKey = UnitIconLibrary.NormalizeUnitKey(unitKey);
            var sprite = UnitIconLibrary.GetSmallIconSprite(normalizedKey);
            var primary = UnitIconLibrary.GetPrimaryColor(normalizedKey);
            var secondary = UnitIconLibrary.GetSecondaryColor(normalizedKey);
            var baseSize = new Vector2(size.x * 0.98f, size.y * 0.88f);

            var shadow = CreateLayer(
                visual.transform,
                "GroundShadow",
                EmojiWarVisualStyle.Colors.Depth,
                new Vector2(0f, -baseSize.y * 0.35f),
                new Vector2(baseSize.x * 0.62f, baseSize.y * 0.18f),
                circleSprite);
            shadow.GetComponent<Image>().color = WithAlpha(EmojiWarVisualStyle.Colors.Depth, 0.22f);

            var floorGlow = CreateLayer(
                visual.transform,
                "FloorGlow",
                Color.Lerp(color, Palette.Cloud, 0.18f),
                new Vector2(0f, -baseSize.y * 0.29f),
                new Vector2(baseSize.x * 0.58f, baseSize.y * 0.14f),
                circleSprite);
            floorGlow.GetComponent<Image>().color = WithAlpha(Color.Lerp(color, Palette.Cloud, 0.18f), 0.12f);

            var aura = CreateLayer(
                visual.transform,
                "StickerAura",
                Color.Lerp(color, secondary, 0.20f),
                new Vector2(0f, -baseSize.y * 0.02f),
                new Vector2(baseSize.x * 0.98f, baseSize.y * 0.90f),
                circleSprite);
            aura.GetComponent<Image>().color = WithAlpha(Color.Lerp(color, secondary, 0.20f), 0.10f);

            var iconRoot = CreateRectObject(visual.transform, "StickerIcon");
            SetAnchors(iconRoot.GetComponent<RectTransform>(), new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.96f));

            CreateSpriteLayer(iconRoot.transform, "SpriteShadow", sprite, WithAlpha(EmojiWarVisualStyle.Colors.Depth, 0.28f), new Vector2(0f, -4f), 1.08f);
            CreateSpriteLayer(iconRoot.transform, "SpriteAura", sprite, WithAlpha(color, 0.18f), Vector2.zero, 1.16f);
            CreateSpriteLayer(iconRoot.transform, "SpriteOutline", sprite, Palette.SoftWhite, Vector2.zero, 1.10f);
            CreateSpriteLayer(iconRoot.transform, "UnitIconSprite", sprite, Color.white, Vector2.zero, 1.02f);

            var label = CreateLabel(
                sticker.transform,
                "Name",
                unitName,
                Mathf.Max(10f, size.y * 0.096f),
                FontStyles.Bold,
                WithAlpha(Palette.SoftWhite, 0.88f),
                TextAlignmentOptions.Center,
                new Vector2(0.05f, 0.00f),
                new Vector2(0.95f, 0.15f));
            label.textWrappingMode = TextWrappingModes.NoWrap;

            return sticker;
        }

        private static GameObject CreateSpriteLayer(Transform parent, string name, Sprite sprite, Color color, Vector2 anchoredPosition, float scale)
        {
            var layer = CreateRectObject(parent, name);
            var rect = layer.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            var baseSize = ResolveBaseIconSize(parent);
            rect.sizeDelta = new Vector2(baseSize * scale, baseSize * scale);
            var image = layer.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return layer;
        }

        private static float ResolveBaseIconSize(Transform parent)
        {
            var rect = parent as RectTransform;
            if (rect == null)
            {
                return 128f;
            }

            var width = rect.rect.width > 1f ? rect.rect.width : rect.sizeDelta.x;
            var height = rect.rect.height > 1f ? rect.rect.height : rect.sizeDelta.y;
            var baseSize = Mathf.Min(width, height);
            return baseSize > 1f ? baseSize : 128f;
        }

        public static GameObject CreateArenaSurface(Transform parent, string name, Color bodyColor, Color outlineColor, Vector2 size)
        {
            EnsureSprites();
            var surface = CreateRectObject(parent, string.IsNullOrWhiteSpace(name) ? "ArenaSurface" : name);
            surface.GetComponent<RectTransform>().sizeDelta = size;
            var usesStretchDecoration = size.x <= 0.01f || size.y <= 0.01f;
            var shadow = CreateLayer(surface.transform, "SurfaceShadow", EmojiWarVisualStyle.Colors.Depth, new Vector2(0f, -10f), size, roundedSprite);
            shadow.GetComponent<Image>().color = WithAlpha(EmojiWarVisualStyle.Colors.Depth, 0.30f);
            if (usesStretchDecoration)
            {
                SetStretchOffsets(shadow.GetComponent<RectTransform>(), new Vector2(6f, -2f), new Vector2(6f, 10f));
            }
            var image = surface.AddComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.Lerp(bodyColor, Palette.Cloud, 0.06f);
            AddOutlineAndShadow(surface, Color.Lerp(outlineColor, Palette.Cloud, 0.14f), new Vector2(0f, -5f), 2.8f);

            var sheen = CreateLayer(surface.transform, "SurfaceSheen", Color.white, Vector2.zero, new Vector2(size.x * 0.82f, Mathf.Max(16f, size.y * 0.11f)), roundedSprite);
            sheen.GetComponent<Image>().color = WithAlpha(Color.white, 0.10f);
            if (usesStretchDecoration)
            {
                SetAnchors(sheen.GetComponent<RectTransform>(), new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.95f));
            }
            else
            {
                var sheenRect = sheen.GetComponent<RectTransform>();
                sheenRect.anchorMin = new Vector2(0.5f, 0.88f);
                sheenRect.anchorMax = new Vector2(0.5f, 0.88f);
                sheenRect.anchoredPosition = Vector2.zero;
            }
            return surface;
        }

        public static void Stretch(RectTransform rect)
        {
            SetAnchors(rect, Vector2.zero, Vector2.one);
        }

        public static void SetAnchors(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static void SetSizeAndPosition(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
        {
            if (rect == null)
            {
                return;
            }

            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(GameObject target, float spacing, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var layout = GetOrAdd<HorizontalLayoutGroup>(target);
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static VerticalLayoutGroup AddVerticalLayout(GameObject target, float spacing, TextAnchor alignment = TextAnchor.UpperCenter)
        {
            var layout = GetOrAdd<VerticalLayoutGroup>(target);
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static GridLayoutGroup AddGridLayout(GameObject target, Vector2 cellSize, Vector2 spacing, int columns)
        {
            var layout = GetOrAdd<GridLayoutGroup>(target);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = Mathf.Max(1, columns);
            layout.cellSize = cellSize;
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            return layout;
        }

        public static ContentSizeFitter AddContentSizeFitter(GameObject target, ContentSizeFitter.FitMode horizontal, ContentSizeFitter.FitMode vertical)
        {
            var fitter = GetOrAdd<ContentSizeFitter>(target);
            fitter.horizontalFit = horizontal;
            fitter.verticalFit = vertical;
            return fitter;
        }

        public static TMP_Text CreateLabel(
            Transform parent,
            string name,
            string text,
            float fontSize,
            FontStyles fontStyle,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            var labelObject = CreateRectObject(parent, string.IsNullOrWhiteSpace(name) ? "TMPLabel" : name);
            SetAnchors(labelObject.GetComponent<RectTransform>(), anchorMin, anchorMax);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            label.text = text ?? string.Empty;
            label.fontSize = fontSize;
            label.fontStyle = fontStyle;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            return label;
        }

        private static GameObject CreateLayer(Transform parent, string name, Color color, Vector2 offset, Vector2 size, Sprite sprite)
        {
            var layer = CreateRectObject(parent, name);
            var rect = layer.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            var image = layer.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite == roundedSprite ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return layer;
        }

        private static void SetStretchOffsets(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = -offsetMax;
        }

        private static GameObject CreateRectObject(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            var rect = gameObject.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            return gameObject;
        }

        private static void AddOutlineAndShadow(GameObject target, Color outlineColor, Vector2 shadowDistance, float outlineSize)
        {
            var graphic = target != null ? target.GetComponent<Graphic>() : null;
            if (graphic == null)
            {
                return;
            }

            var outline = GetOrAdd<UnityEngine.UI.Outline>(target);
            outline.effectColor = WithAlpha(outlineColor, 0.82f);
            outline.effectDistance = new Vector2(outlineSize, outlineSize);

            var shadow = GetOrAdd<Shadow>(target);
            shadow.effectColor = WithAlpha(Palette.InkPurple, 0.44f);
            shadow.effectDistance = shadowDistance;
        }

        private static Sprite LoadResultUiSprite(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName))
            {
                return null;
            }

            if (ResultUiSprites.TryGetValue(spriteName, out var cached))
            {
                return cached;
            }

            var sprite = Resources.Load<Sprite>($"EmojiWar/ResultUi/{spriteName}");
            ResultUiSprites[spriteName] = sprite;
            return sprite;
        }

        private static T GetOrAdd<T>(GameObject target) where T : Component
        {
            if (target == null)
            {
                return null;
            }

            var component = target.GetComponent<T>();
            if (component == null)
            {
                component = target.AddComponent<T>();
            }

            return component;
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private static void EnsureSprites()
        {
            if (roundedSprite == null)
            {
                roundedSprite = BuildRoundedSprite();
            }

            if (circleSprite == null)
            {
                circleSprite = BuildCircleSprite();
            }
        }

        private static Sprite BuildRoundedSprite()
        {
            const int size = 64;
            const float radius = 14f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RescueRoundedSprite"
            };

            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var dx = Mathf.Max(radius - x, 0f, x - (size - 1 - radius));
                    var dy = Mathf.Max(radius - y, 0f, y - (size - 1 - radius));
                    var distance = Mathf.Sqrt(dx * dx + dy * dy);
                    var alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(18f, 18f, 18f, 18f));
        }

        private static Sprite BuildCircleSprite()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RescueCircleSprite"
            };
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            var radius = size * 0.46f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), center);
                    var alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
