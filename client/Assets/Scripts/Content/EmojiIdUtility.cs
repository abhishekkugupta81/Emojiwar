using System;
using System.Collections.Generic;
using System.Linq;

namespace EmojiWar.Client.Content
{
    public static class EmojiIdUtility
    {
        public static readonly EmojiId[] LaunchRoster =
        {
            EmojiId.Fire,
            EmojiId.Water,
            EmojiId.Lightning,
            EmojiId.Ice,
            EmojiId.Magnet,
            EmojiId.Bomb,
            EmojiId.Mirror,
            EmojiId.Hole,
            EmojiId.Shield,
            EmojiId.Snake,
            EmojiId.Soap,
            EmojiId.Plant,
            EmojiId.Wind,
            EmojiId.Heart,
            EmojiId.Ghost,
            EmojiId.Chain
        };

        public static string ToApiId(EmojiId emojiId)
        {
            return emojiId switch
            {
                EmojiId.Fire => "fire",
                EmojiId.Water => "water",
                EmojiId.Lightning => "lightning",
                EmojiId.Ice => "ice",
                EmojiId.Magnet => "magnet",
                EmojiId.Bomb => "bomb",
                EmojiId.Mirror => "mirror",
                EmojiId.Hole => "hole",
                EmojiId.Shield => "shield",
                EmojiId.Snake => "snake",
                EmojiId.Soap => "soap",
                EmojiId.Plant => "plant",
                EmojiId.Wind => "wind",
                EmojiId.Heart => "heart",
                EmojiId.Ghost => "ghost",
                EmojiId.Chain => "chain",
                _ => throw new ArgumentOutOfRangeException(nameof(emojiId), emojiId, "Unsupported emoji id.")
            };
        }

        public static string ToDisplayName(EmojiId emojiId)
        {
            return emojiId switch
            {
                EmojiId.Fire => "Fire",
                EmojiId.Water => "Water",
                EmojiId.Lightning => "Lightning",
                EmojiId.Ice => "Ice",
                EmojiId.Magnet => "Magnet",
                EmojiId.Bomb => "Bomb",
                EmojiId.Mirror => "Mirror",
                EmojiId.Hole => "Hole",
                EmojiId.Shield => "Shield",
                EmojiId.Snake => "Snake",
                EmojiId.Soap => "Soap",
                EmojiId.Plant => "Plant",
                EmojiId.Wind => "Wind",
                EmojiId.Heart => "Heart",
                EmojiId.Ghost => "Ghost",
                EmojiId.Chain => "Chain",
                _ => emojiId.ToString()
            };
        }

        public static string ToEmojiGlyph(EmojiId emojiId)
        {
            return emojiId switch
            {
                EmojiId.Fire => "🔥",
                EmojiId.Water => "🌊",
                EmojiId.Lightning => "⚡",
                EmojiId.Ice => "🧊",
                EmojiId.Magnet => "🧲",
                EmojiId.Bomb => "💣",
                EmojiId.Mirror => "🪞",
                EmojiId.Hole => "🕳",
                EmojiId.Shield => "🛡️",
                EmojiId.Snake => "🐍",
                EmojiId.Soap => "🧼",
                EmojiId.Plant => "🌱",
                EmojiId.Wind => "💨",
                EmojiId.Heart => "❤️",
                EmojiId.Ghost => "👻",
                EmojiId.Chain => "⛓️",
                _ => "❔"
            };
        }

        public static bool TryFromApiId(string apiId, out EmojiId emojiId)
        {
            switch (apiId)
            {
                case "fire":
                    emojiId = EmojiId.Fire;
                    return true;
                case "water":
                    emojiId = EmojiId.Water;
                    return true;
                case "lightning":
                    emojiId = EmojiId.Lightning;
                    return true;
                case "ice":
                    emojiId = EmojiId.Ice;
                    return true;
                case "magnet":
                    emojiId = EmojiId.Magnet;
                    return true;
                case "bomb":
                    emojiId = EmojiId.Bomb;
                    return true;
                case "mirror":
                    emojiId = EmojiId.Mirror;
                    return true;
                case "hole":
                    emojiId = EmojiId.Hole;
                    return true;
                case "shield":
                    emojiId = EmojiId.Shield;
                    return true;
                case "snake":
                    emojiId = EmojiId.Snake;
                    return true;
                case "soap":
                    emojiId = EmojiId.Soap;
                    return true;
                case "plant":
                    emojiId = EmojiId.Plant;
                    return true;
                case "wind":
                    emojiId = EmojiId.Wind;
                    return true;
                case "heart":
                    emojiId = EmojiId.Heart;
                    return true;
                case "ghost":
                    emojiId = EmojiId.Ghost;
                    return true;
                case "chain":
                    emojiId = EmojiId.Chain;
                    return true;
                default:
                    emojiId = default;
                    return false;
            }
        }

        public static string[] ToApiIds(IEnumerable<EmojiId> emojiIds)
        {
            return emojiIds.Select(ToApiId).ToArray();
        }

        public static string ToDisplaySummary(IEnumerable<EmojiId> emojiIds)
        {
            return string.Join(" • ", emojiIds.Select(ToDisplayName));
        }

        public static IReadOnlyList<EmojiId> ParseApiIds(IEnumerable<string> emojiIds)
        {
            var parsed = new List<EmojiId>();

            foreach (var emojiId in emojiIds)
            {
                if (TryFromApiId(emojiId, out var parsedEmojiId))
                {
                    parsed.Add(parsedEmojiId);
                }
            }

            return parsed;
        }
    }
}
