using System;
using System.Linq;
using EmojiWar.Client.Content;
using EmojiWar.Client.Core;
using EmojiWar.Client.Gameplay.Contracts;

namespace EmojiWar.Client.Gameplay.Bots
{
    public static class LocalBotMatchPreviewFactory
    {
        private static readonly string[] DefaultPlayerDeck = EmojiIdUtility.ToApiIds(new[]
        {
            EmojiId.Fire,
            EmojiId.Water,
            EmojiId.Lightning,
            EmojiId.Ice,
            EmojiId.Shield,
            EmojiId.Magnet
        });

        private static readonly string[] PracticeBotDeck =
        {
            "fire",
            "water",
            "ice",
            "shield",
            "soap",
            "plant"
        };

        private static readonly string[] SmartBotDeck =
        {
            "water",
            "lightning",
            "magnet",
            "mirror",
            "bomb",
            "soap"
        };

        private static readonly string[] PracticeBanPriority =
        {
            "bomb",
            "magnet",
            "fire",
            "lightning",
            "plant",
            "snake"
        };

        private static readonly string[] SmartBanPriority =
        {
            "bomb",
            "magnet",
            "mirror",
            "plant",
            "lightning",
            "fire"
        };

        public static StartBotMatchResponseDto Create(string requestedMode, string[] playerDeck = null)
        {
            var normalizedMode = requestedMode == LaunchSelections.BotSmart ? LaunchSelections.BotSmart : LaunchSelections.BotPractice;
            var normalizedPlayerDeck = playerDeck is { Length: > 0 } ? playerDeck : DefaultPlayerDeck;
            var botDeck = normalizedMode == LaunchSelections.BotSmart ? SmartBotDeck : PracticeBotDeck;

            return new StartBotMatchResponseDto
            {
                matchId = Guid.NewGuid().ToString("N"),
                mode = normalizedMode,
                playerDeckId = Guid.NewGuid().ToString("N"),
                botProfile = normalizedMode == LaunchSelections.BotSmart
                    ? new BotProfileDto
                    {
                        id = "smart",
                        difficulty = LaunchSelections.BotSmart,
                        aggression = 0.7f,
                        defenseBias = 0.55f,
                        comboBias = 0.8f
                    }
                    : new BotProfileDto
                    {
                        id = "practice",
                        difficulty = LaunchSelections.BotPractice,
                        aggression = 0.45f,
                        defenseBias = 0.7f,
                        comboBias = 0.35f
                    },
                playerDeck = normalizedPlayerDeck.ToArray(),
                botDeck = botDeck.ToArray(),
                suggestedBan = ChooseSuggestedBan(normalizedMode, normalizedPlayerDeck),
                rulesVersion = "launch-001",
                status = "banning"
            };
        }

        private static string ChooseSuggestedBan(string mode, string[] playerDeck)
        {
            var priority = mode == LaunchSelections.BotSmart ? SmartBanPriority : PracticeBanPriority;
            return priority.FirstOrDefault(playerDeck.Contains) ?? playerDeck.FirstOrDefault() ?? "bomb";
        }
    }
}
