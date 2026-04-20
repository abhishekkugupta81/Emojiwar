using System.Collections.Generic;
using UnityEngine;

namespace EmojiWar.Client.Content
{
    [CreateAssetMenu(fileName = "EmojiCatalog", menuName = "EmojiWar/Content/Emoji Catalog")]
    public sealed class StaticEmojiCatalog : ScriptableObject
    {
        [SerializeField] private List<EmojiDefinition> emojis = new();

        public IReadOnlyList<EmojiDefinition> Emojis => emojis;

        public EmojiDefinition Find(EmojiId id)
        {
            return emojis.Find(emoji => emoji != null && emoji.Id == id);
        }
    }
}
