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
            gradient.SetColors(topColor, Color.Lerp(topColor, bottomColor, 0.52f), bottomColor);
            background.transform.SetAsFirstSibling();

            CreateBlob(parent, $"{name}BlobA", Palette.HotPink, new Vector2(-130f, 230f), new Vector2(210f, 210f), 0.20f);
            CreateBlob(parent, $"{name}BlobB", Palette.Aqua, new Vector2(150f, -120f), new Vector2(250f, 250f), 0.18f);
            CreateBlob(parent, $"{name}BlobC", Palette.SunnyYellow, new Vector2(170f, 260f), new Vector2(120f, 120f), 0.14f);
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

            var shadow = CreateLayer(buttonObject.transform, "Shadow", Palette.InkPurple, new Vector2(0f, -8f), size, roundedSprite);
            shadow.GetComponent<Image>().color = WithAlpha(Palette.InkPurple, 0.58f);

            var body = buttonObject.AddComponent<Image>();
            body.sprite = roundedSprite;
            body.type = Image.Type.Sliced;
            body.color = bodyColor;
            AddOutlineAndShadow(buttonObject, primary ? Palette.SunnyYellow : Palette.SoftWhite, new Vector2(0f, -4f), primary ? 4f : 2.5f);

            var highlight = CreateLayer(buttonObject.transform, "Highlight", Color.white, new Vector2(0f, size.y * 0.22f), new Vector2(size.x * 0.86f, size.y * 0.24f), roundedSprite);
            highlight.GetComponent<Image>().color = WithAlpha(Color.white, primary ? 0.24f : 0.14f);

            var button = buttonObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = body;
            CreateLabel(buttonObject.transform, "Label", label, primary ? 26f : 20f, FontStyles.Bold, textColor, TextAlignmentOptions.Center, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.92f));
            return button;
        }

        public static GameObject CreateEmojiAvatar(Transform parent, string emoji, string fallbackText, Color auraColor, Vector2 size)
        {
            EnsureSprites();
            var avatar = CreateRectObject(parent, "EmojiAvatar");
            avatar.GetComponent<RectTransform>().sizeDelta = size;

            var glow = CreateLayer(avatar.transform, "Glow", auraColor, Vector2.zero, size, circleSprite);
            glow.GetComponent<Image>().color = WithAlpha(auraColor, 0.46f);

            var sticker = CreateLayer(avatar.transform, "StickerBase", Palette.SoftWhite, Vector2.zero, size * 0.82f, circleSprite);
            sticker.GetComponent<Image>().color = WithAlpha(Palette.SoftWhite, 0.30f);
            AddOutlineAndShadow(sticker, Palette.SoftWhite, new Vector2(0f, -5f), 3f);

            var iconObject = CreateRectObject(avatar.transform, "UnitIconSprite");
            SetAnchors(iconObject.GetComponent<RectTransform>(), new Vector2(0.07f, 0.08f), new Vector2(0.93f, 0.94f));
            var iconImage = iconObject.AddComponent<Image>();
            iconImage.sprite = UnitIconLibrary.GetIconSprite(fallbackText);
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var fallback = FirstReadableToken(fallbackText);
            var fallbackLabel = CreateLabel(
                avatar.transform,
                "TinyFallback",
                fallback,
                Mathf.Max(8f, size.y * 0.075f),
                FontStyles.Bold,
                WithAlpha(auraColor, 0.30f),
                TextAlignmentOptions.Center,
                new Vector2(0.20f, 0.02f),
                new Vector2(0.80f, 0.17f));
            fallbackLabel.textWrappingMode = TextWrappingModes.NoWrap;
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

            var avatar = CreateEmojiAvatar(card.transform, emoji, unitName, auraColor, new Vector2(size.x * 0.88f, size.y * 0.62f));
            var avatarRect = avatar.GetComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0.5f, 0.61f);
            avatarRect.anchorMax = new Vector2(0.5f, 0.61f);
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
            var avatar = CreateEmojiAvatar(card.transform, unitKey, unitName, auraColor, new Vector2(iconSide, iconSide));
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
                    selectedOrder > 0 ? selectedOrder.ToString() : "✓",
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

        public static GameObject CreateMiniSquadSticker(Transform parent, string unitName, string emoji, Color color, Vector2 size)
        {
            EnsureSprites();
            var card = CreateRectObject(parent, $"{unitName}MiniSquadSticker");
            card.GetComponent<RectTransform>().sizeDelta = size;
            var image = card.AddComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.Lerp(Palette.InkPurple, color, 0.35f);
            AddOutlineAndShadow(card, Color.Lerp(color, Palette.SoftWhite, 0.16f), new Vector2(0f, -4f), 3.25f);

            var avatar = CreateEmojiAvatar(card.transform, emoji, unitName, color, new Vector2(size.x * 0.96f, size.y * 0.84f));
            var avatarRect = avatar.GetComponent<RectTransform>();
            avatarRect.anchorMin = new Vector2(0.5f, 0.66f);
            avatarRect.anchorMax = new Vector2(0.5f, 0.66f);
            avatarRect.anchoredPosition = Vector2.zero;
            CreateLabel(card.transform, "Name", unitName, Mathf.Max(11f, size.y * 0.115f), FontStyles.Bold, Palette.SoftWhite, TextAlignmentOptions.Center, new Vector2(0.04f, 0.01f), new Vector2(0.96f, 0.18f));
            return card;
        }

        public static GameObject CreateArenaSurface(Transform parent, string name, Color bodyColor, Color outlineColor, Vector2 size)
        {
            EnsureSprites();
            var surface = CreateRectObject(parent, string.IsNullOrWhiteSpace(name) ? "ArenaSurface" : name);
            surface.GetComponent<RectTransform>().sizeDelta = size;
            var shadow = CreateLayer(surface.transform, "SurfaceShadow", Palette.InkPurple, new Vector2(0f, -10f), size, roundedSprite);
            shadow.GetComponent<Image>().color = WithAlpha(Palette.InkPurple, 0.38f);
            var image = surface.AddComponent<Image>();
            image.sprite = roundedSprite;
            image.type = Image.Type.Sliced;
            image.color = bodyColor;
            AddOutlineAndShadow(surface, Color.Lerp(outlineColor, Palette.SoftWhite, 0.08f), new Vector2(0f, -5f), 3.25f);
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

        private static string FirstReadableToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "?";
            }

            var trimmed = value.Trim();
            return trimmed.Length <= 2 ? trimmed.ToUpperInvariant() : trimmed.Substring(0, 2).ToUpperInvariant();
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
