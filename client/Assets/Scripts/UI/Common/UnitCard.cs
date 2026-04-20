using EmojiWar.Client.Content;
using UnityEngine;
using UnityEngine.UI;

namespace EmojiWar.Client.UI.Common
{
    public sealed class UnitCard : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image aura;
        [SerializeField] private Text emojiGlyphLabel;
        [SerializeField] private Text titleLabel;
        [SerializeField] private Text roleLabel;
        [SerializeField] private Text stateLabel;

        public void Bind(EmojiId emojiId, UnitCardState state)
        {
            var roleTag = EmojiUiFormatter.BuildRoleTag(emojiId);
            if (emojiGlyphLabel != null)
            {
                emojiGlyphLabel.text = EmojiIdUtility.ToEmojiGlyph(emojiId);
            }

            if (titleLabel != null)
            {
                titleLabel.text = EmojiIdUtility.ToDisplayName(emojiId);
            }

            if (roleLabel != null)
            {
                roleLabel.text = roleTag;
                roleLabel.color = UiThemeRuntime.ResolveRoleAccent(roleTag);
            }

            if (stateLabel != null)
            {
                stateLabel.text = state.HasFlag(UnitCardState.Banned)
                    ? "BAN"
                    : state.HasFlag(UnitCardState.Selected)
                        ? "LOCKED"
                        : string.Empty;
            }

            if (background == null)
            {
                return;
            }

            background.color = UiThemeRuntime.ResolveCardColor(state);

            if (aura != null)
            {
                var roleColor = UiThemeRuntime.ResolveRoleAccent(emojiId);
                aura.color = roleColor * new Color(1f, 1f, 1f, state.HasFlag(UnitCardState.Disabled) ? 0.18f : 0.42f);
            }
        }

        public StickerUnitCardViewModel BuildViewModel(EmojiId emojiId, UnitCardState state)
        {
            return new StickerUnitCardViewModel
            {
                EmojiId = emojiId,
                DisplayName = EmojiIdUtility.ToDisplayName(emojiId),
                Glyph = EmojiIdUtility.ToEmojiGlyph(emojiId),
                RoleTag = EmojiUiFormatter.BuildRoleTag(emojiId),
                State = state
            };
        }
    }
}
