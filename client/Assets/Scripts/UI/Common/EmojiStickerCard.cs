using System;
using EmojiWar.Client.Content;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Common
{
    public sealed class EmojiStickerCard : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image aura;
        [SerializeField] private Text glyphLabel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text roleLabel;
        [SerializeField] private Text badgeLabel;
        [SerializeField] private Button button;

        private static Font cachedEmojiFont;
        private EmojiId boundEmojiId;

        public EmojiId EmojiId => boundEmojiId;

        public void Bind(EmojiId emojiId, UnitCardState state, bool compact, UnityAction onClick = null)
        {
            boundEmojiId = emojiId;
            EnsureReferences();

            var role = EmojiUiFormatter.BuildRoleTag(emojiId);
            var accent = UiThemeRuntime.ResolveRoleAccent(role);
            var selected = state.HasFlag(UnitCardState.Selected);
            var banned = state.HasFlag(UnitCardState.Banned);
            var disabled = state.HasFlag(UnitCardState.Disabled);

            if (background != null)
            {
                background.color = disabled
                    ? UiThemeRuntime.Theme.CardColors.Disabled
                    : selected
                        ? Color.Lerp(UiThemeRuntime.Theme.CardColors.Selected, accent, 0.22f)
                        : UiThemeRuntime.Theme.CardColors.Default;
                var outline = background.GetComponent<Outline>();
                if (outline == null)
                {
                    outline = background.gameObject.AddComponent<Outline>();
                }
                outline.effectColor = banned ? new Color(1f, 0.35686275f, 0.43137255f, 1f) : selected ? accent : new Color(1f, 1f, 1f, 0.48f);
                outline.effectDistance = selected || banned ? new Vector2(6f, 6f) : new Vector2(3f, 3f);
                var shadow = background.GetComponent<Shadow>();
                if (shadow == null)
                {
                    shadow = background.gameObject.AddComponent<Shadow>();
                }
                shadow.effectColor = new Color(0f, 0f, 0f, 0.40f);
                shadow.effectDistance = new Vector2(0f, -8f);
            }

            if (aura != null)
            {
                aura.color = new Color(accent.r, accent.g, accent.b, selected ? 0.56f : 0.28f);
            }

            if (glyphLabel != null)
            {
                glyphLabel.font = ResolveEmojiFont();
                glyphLabel.text = EmojiIdUtility.ToEmojiGlyph(emojiId);
                glyphLabel.fontSize = compact ? 58 : 70;
                glyphLabel.fontStyle = FontStyle.Normal;
                glyphLabel.color = Color.white;
                glyphLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                glyphLabel.verticalOverflow = VerticalWrapMode.Overflow;
                glyphLabel.resizeTextForBestFit = false;
                var glyphOutline = glyphLabel.GetComponent<Outline>();
                if (glyphOutline == null)
                {
                    glyphOutline = glyphLabel.gameObject.AddComponent<Outline>();
                }
                glyphOutline.effectColor = new Color(1f, 1f, 1f, 0.46f);
                glyphOutline.effectDistance = new Vector2(4f, -4f);
                var glyphShadow = glyphLabel.GetComponent<Shadow>();
                if (glyphShadow == null)
                {
                    glyphShadow = glyphLabel.gameObject.AddComponent<Shadow>();
                }
                glyphShadow.effectColor = new Color(0f, 0f, 0f, 0.48f);
                glyphShadow.effectDistance = new Vector2(0f, -6f);
            }

            if (titleLabel != null)
            {
                titleLabel.text = EmojiIdUtility.ToDisplayName(emojiId);
                titleLabel.fontSize = compact ? 16 : 18;
                titleLabel.color = new Color32(0xF8, 0xF7, 0xFF, 0xFF);
            }

            if (roleLabel != null)
            {
                roleLabel.text = role;
                roleLabel.fontSize = compact ? 12 : 13;
                roleLabel.color = accent;
            }

            if (badgeLabel != null)
            {
                badgeLabel.text = banned ? "BAN" : selected ? "OK" : string.Empty;
                badgeLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(badgeLabel.text));
                badgeLabel.color = Color.white;
                badgeLabel.fontSize = compact ? 14 : 16;
            }

            if (button != null)
            {
                button.interactable = !disabled;
                button.onClick.RemoveAllListeners();
                if (onClick != null)
                {
                    button.onClick.AddListener(onClick);
                    button.onClick.AddListener(() =>
                    {
                        var motion = GetComponent<EmojiMotionController>();
                        if (motion == null)
                        {
                            motion = gameObject.AddComponent<EmojiMotionController>();
                        }
                        motion.JumpSelect();
                    });
                }
            }
        }

        private void EnsureReferences()
        {
            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (button == null)
            {
                button = GetComponent<Button>();
            }
            if (aura == null || glyphLabel == null || titleLabel == null || roleLabel == null || badgeLabel == null)
            {
                foreach (var image in GetComponentsInChildren<Image>(true))
                {
                    if (aura == null && image.name.IndexOf("Aura", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        aura = image;
                    }
                }

                foreach (var label in GetComponentsInChildren<Text>(true))
                {
                    if (glyphLabel == null && label.name.IndexOf("Glyph", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        glyphLabel = label;
                        continue;
                    }

                    if (titleLabel == null && label.name.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        titleLabel = label;
                        continue;
                    }

                    if (roleLabel == null && label.name.IndexOf("Role", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        roleLabel = label;
                        continue;
                    }

                    if (badgeLabel == null && label.name.IndexOf("Badge", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        badgeLabel = label;
                    }
                }
            }
        }

        private static Font ResolveEmojiFont()
        {
            if (cachedEmojiFont != null)
            {
                return cachedEmojiFont;
            }

            var candidates = new[]
            {
                "Segoe UI Emoji",
                "Segoe UI Symbol",
                "Noto Color Emoji",
                "Apple Color Emoji"
            };

            foreach (var candidate in candidates)
            {
                try
                {
                    cachedEmojiFont = Font.CreateDynamicFontFromOSFont(candidate, 128);
                }
                catch (Exception)
                {
                    cachedEmojiFont = null;
                }

                if (cachedEmojiFont != null)
                {
                    return cachedEmojiFont;
                }
            }

            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
