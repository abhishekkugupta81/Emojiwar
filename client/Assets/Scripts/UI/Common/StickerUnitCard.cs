using EmojiWar.Client.Content;
using UnityEngine;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Common
{
    public sealed class StickerUnitCard : MonoBehaviour
    {
        [SerializeField] private bool compactMode = true;
        [SerializeField] private Image background;
        [SerializeField] private Image aura;
        [SerializeField] private Text glyphLabel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text rolePillLabel;
        [SerializeField] private Text stateBadgeLabel;

        public void SetCompactMode(bool isCompact)
        {
            compactMode = isCompact;
        }

        public void Bind(StickerUnitCardViewModel viewModel)
        {
            Bind(viewModel.EmojiId, viewModel.State);
        }

        public void Bind(EmojiId emojiId, UnitCardState state)
        {
            EnsureReferences();
            var roleTag = EmojiUiFormatter.BuildRoleTag(emojiId);

            if (glyphLabel != null)
            {
                glyphLabel.text = EmojiIdUtility.ToEmojiGlyph(emojiId);
                glyphLabel.fontSize = compactMode
                    ? Mathf.Max(UiThemeRuntime.Theme.ChipFontSize - 1, 15)
                    : Mathf.Max(UiThemeRuntime.Theme.ChipFontSize + 1, 18);
                glyphLabel.color = Color.white;
                glyphLabel.gameObject.SetActive(!compactMode || state.HasFlag(UnitCardState.Selected));
            }

            if (titleLabel != null)
            {
                var baseTitle = EmojiIdUtility.ToDisplayName(emojiId);
                titleLabel.text = compactMode && state.HasFlag(UnitCardState.Selected)
                    ? $"✓ {baseTitle}"
                    : baseTitle;
                titleLabel.fontSize = compactMode
                    ? Mathf.Max(UiThemeRuntime.Theme.BodyFontSize - 3, 18)
                    : Mathf.Max(UiThemeRuntime.Theme.BodyFontSize, 22);
                titleLabel.color = Color.white;
            }

            if (rolePillLabel != null)
            {
                rolePillLabel.text = roleTag;
                rolePillLabel.fontSize = compactMode
                    ? Mathf.Max(UiThemeRuntime.Theme.ChipFontSize - 4, 11)
                    : Mathf.Max(UiThemeRuntime.Theme.ChipFontSize - 1, 14);
                rolePillLabel.color = UiThemeRuntime.ResolveRoleAccent(roleTag);
                rolePillLabel.gameObject.SetActive(!compactMode || !state.HasFlag(UnitCardState.Selected));
            }

            if (stateBadgeLabel != null)
            {
                stateBadgeLabel.text = state.HasFlag(UnitCardState.Banned)
                    ? "BAN"
                    : string.Empty;
                stateBadgeLabel.fontSize = Mathf.Max(UiThemeRuntime.Theme.ChipFontSize - 2, 12);
                stateBadgeLabel.color = Color.white;
                stateBadgeLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(stateBadgeLabel.text));
            }

            if (background != null)
            {
                background.color = UiThemeRuntime.ResolveCardColor(state);
            }

            if (aura != null)
            {
                var accent = UiThemeRuntime.ResolveRoleAccent(roleTag);
                aura.color = accent * new Color(1f, 1f, 1f, state.HasFlag(UnitCardState.Disabled) ? 0.16f : 0.30f);
            }
        }

        private void EnsureReferences()
        {
            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (glyphLabel == null || titleLabel == null || rolePillLabel == null || stateBadgeLabel == null)
            {
                var labels = GetComponentsInChildren<Text>(true);
                foreach (var label in labels)
                {
                    var lowerName = label.name.ToLowerInvariant();
                    if (glyphLabel == null && lowerName.Contains("glyph"))
                    {
                        glyphLabel = label;
                        continue;
                    }

                    if (titleLabel == null && lowerName.Contains("title"))
                    {
                        titleLabel = label;
                        continue;
                    }

                    if (rolePillLabel == null && (lowerName.Contains("role") || lowerName.Contains("pill")))
                    {
                        rolePillLabel = label;
                        continue;
                    }

                    if (stateBadgeLabel == null && (lowerName.Contains("state") || lowerName.Contains("badge")))
                    {
                        stateBadgeLabel = label;
                    }
                }
            }

            if (aura == null)
            {
                var images = GetComponentsInChildren<Image>(true);
                foreach (var image in images)
                {
                    if (image == background)
                    {
                        continue;
                    }

                    if (image.name.ToLowerInvariant().Contains("aura"))
                    {
                        aura = image;
                        break;
                    }
                }
            }
        }
    }
}
