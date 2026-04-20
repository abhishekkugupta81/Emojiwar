using System;
using EmojiWar.Client.Content;

namespace EmojiWar.Client.UI.Common
{
    [Serializable]
    public sealed class StickerUnitCardViewModel
    {
        public EmojiId EmojiId = EmojiId.Fire;
        public string DisplayName = string.Empty;
        public string Glyph = string.Empty;
        public string RoleTag = string.Empty;
        public UnitCardState State = UnitCardState.Default;
    }
}
