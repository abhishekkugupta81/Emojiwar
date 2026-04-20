using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Gameplay.Contracts;

namespace EmojiWar.Client.Gameplay.Bots
{
    public static class LocalRoundPreviewResolver
    {
        private static readonly Dictionary<string, EmojiRules> Rules = new()
        {
            ["fire"] = new("Fire", new[] { "plant", "ice" }, new[] { "water", "soap" }),
            ["water"] = new("Water", new[] { "fire", "lightning", "hole" }, new[] { "ice", "plant" }),
            ["lightning"] = new("Lightning", new[] { "magnet", "ice" }, new[] { "water", "mirror", "hole" }),
            ["ice"] = new("Ice", new[] { "water", "bomb" }, new[] { "fire", "lightning" }),
            ["magnet"] = new("Magnet", new[] { "bomb", "shield" }, new[] { "lightning", "mirror", "soap" }),
            ["bomb"] = new("Bomb", new[] { "fire", "water", "lightning", "plant" }, new[] { "ice", "hole", "magnet" }),
            ["mirror"] = new("Mirror", new[] { "lightning", "magnet" }, new[] { "bomb", "plant" }),
            ["hole"] = new("Hole", new[] { "bomb", "lightning" }, new[] { "water" }),
            ["shield"] = new("Shield", new[] { "fire", "lightning", "snake" }, new[] { "magnet", "plant" }),
            ["snake"] = new("Snake", new[] { "water", "magnet", "plant" }, new[] { "soap", "ice" }),
            ["soap"] = new("Soap", new[] { "fire", "snake" }, new[] { "plant" }),
            ["plant"] = new("Plant", new[] { "water", "shield", "mirror", "soap" }, new[] { "fire", "snake", "bomb" })
        };

        public static ResolveRoundServiceResponseDto Resolve(
            string playerPick,
            string botPick,
            ResolveSideStateDto sideStateA,
            ResolveSideStateDto sideStateB)
        {
            if (playerPick == botPick)
            {
                return Draw("MIRROR_MATCH_DRAW", $"{Name(playerPick)} matched itself.");
            }

            if (playerPick == "mirror" && IsTargeted(botPick))
            {
                return PlayerWin("MIRROR_REFLECTED_TARGETED_EFFECT", $"Mirror reflected {Name(botPick)}.");
            }

            if (botPick == "mirror" && IsTargeted(playerPick))
            {
                return BotWin("MIRROR_REFLECTED_TARGETED_EFFECT", $"Mirror reflected {Name(playerPick)}.");
            }

            if (playerPick == "hole" && (botPick == "bomb" || botPick == "lightning"))
            {
                return PlayerWin("HOLE_DELETED_PROJECTILE", $"Hole deleted {Name(botPick)}.");
            }

            if (botPick == "hole" && (playerPick == "bomb" || playerPick == "lightning"))
            {
                return BotWin("HOLE_DELETED_PROJECTILE", $"Hole deleted {Name(playerPick)}.");
            }

            if (playerPick == "lightning" && botPick == "magnet")
            {
                return PlayerWin("LIGHTNING_STUNNED_MAGNET", "Lightning stunned Magnet before it could pull.");
            }

            if (botPick == "lightning" && playerPick == "magnet")
            {
                return BotWin("LIGHTNING_STUNNED_MAGNET", "Lightning stunned Magnet before it could pull.");
            }

            if (playerPick == "ice" && botPick == "bomb")
            {
                return PlayerWin("ICE_DEFUSED_BOMB", "Ice defused Bomb.");
            }

            if (botPick == "ice" && playerPick == "bomb")
            {
                return BotWin("ICE_DEFUSED_BOMB", "Ice defused Bomb.");
            }

            if (playerPick == "magnet" && botPick == "bomb")
            {
                return PlayerWin("MAGNET_PULLED_BOMB", "Magnet pulled Bomb into the enemy side.");
            }

            if (botPick == "magnet" && playerPick == "bomb")
            {
                return BotWin("MAGNET_PULLED_BOMB", "Magnet pulled Bomb into the enemy side.");
            }

            if (playerPick == "magnet" && botPick == "shield")
            {
                return PlayerWin("MAGNET_STOLE_SHIELD", "Magnet stole Shield before impact.");
            }

            if (botPick == "magnet" && playerPick == "shield")
            {
                return BotWin("MAGNET_STOLE_SHIELD", "Magnet stole Shield before impact.");
            }

            if (playerPick == "soap" && (botPick == "fire" || botPick == "snake"))
            {
                return PlayerWin("SOAP_CLEANSED_STATUS", $"Soap cleansed {Name(botPick)}.");
            }

            if (botPick == "soap" && (playerPick == "fire" || playerPick == "snake"))
            {
                return BotWin("SOAP_CLEANSED_STATUS", $"Soap cleansed {Name(playerPick)}.");
            }

            if (playerPick == "shield" && botPick is "fire" or "lightning" or "snake")
            {
                return Draw("SHIELD_BLOCKED_IMPACT", $"Shield blocked {Name(botPick)}, but dealt no damage back.");
            }

            if (botPick == "shield" && playerPick is "fire" or "lightning" or "snake")
            {
                return Draw("SHIELD_BLOCKED_IMPACT", $"Shield blocked {Name(playerPick)}, but dealt no damage back.");
            }

            if (playerPick == "snake" && botPick == "ice")
            {
                return Draw("ICE_DELAYED_POISON", "Ice delayed Snake's poison long enough for a draw.");
            }

            if (botPick == "snake" && playerPick == "ice")
            {
                return Draw("ICE_DELAYED_POISON", "Ice delayed Snake's poison long enough for a draw.");
            }

            if (playerPick == "plant" && botPick is "shield" or "mirror" or "soap")
            {
                return PlayerWin("PLANT_OUTSCALED_PASSIVE_LINE", $"Plant outscaled {Name(botPick)}.");
            }

            if (botPick == "plant" && playerPick is "shield" or "mirror" or "soap")
            {
                return BotWin("PLANT_OUTSCALED_PASSIVE_LINE", $"Plant outscaled {Name(playerPick)}.");
            }

            if (playerPick == "bomb" && botPick is not ("ice" or "hole" or "magnet"))
            {
                return PlayerWin("BOMB_OVERWHELMED_TARGET", $"Bomb overwhelmed {Name(botPick)}.");
            }

            if (botPick == "bomb" && playerPick is not ("ice" or "hole" or "magnet"))
            {
                return BotWin("BOMB_OVERWHELMED_TARGET", $"Bomb overwhelmed {Name(playerPick)}.");
            }

            if (Rules[playerPick].Strengths.Contains(botPick))
            {
                return PlayerWin("DIRECT_COUNTER", $"{Name(playerPick)} countered {Name(botPick)}.");
            }

            if (Rules[botPick].Strengths.Contains(playerPick))
            {
                return BotWin("DIRECT_COUNTER", $"{Name(botPick)} countered {Name(playerPick)}.");
            }

            return Draw("MUTUAL_NEUTRALIZATION", $"{Name(playerPick)} and {Name(botPick)} neutralized each other.");
        }

        private static bool IsTargeted(string emojiId)
        {
            return emojiId is "lightning" or "magnet";
        }

        private static string Name(string emojiId)
        {
            return Rules.TryGetValue(emojiId, out var rules) ? rules.DisplayName : emojiId;
        }

        private static ResolveRoundServiceResponseDto PlayerWin(string reasonCode, string whyText)
        {
            return Build("player_a", false, reasonCode, whyText);
        }

        private static ResolveRoundServiceResponseDto BotWin(string reasonCode, string whyText)
        {
            return Build("player_b", false, reasonCode, whyText);
        }

        private static ResolveRoundServiceResponseDto Draw(string reasonCode, string whyText)
        {
            return Build("draw", true, reasonCode, whyText);
        }

        private static ResolveRoundServiceResponseDto Build(string winner, bool isDraw, string reasonCode, string whyText)
        {
            return new ResolveRoundServiceResponseDto
            {
                winner = winner,
                isDraw = isDraw,
                reasonCode = reasonCode,
                whyText = whyText,
                whyChain = new[] { whyText },
                nextSideStateA = new ResolveSideStateDto(),
                nextSideStateB = new ResolveSideStateDto()
            };
        }

        private readonly struct EmojiRules
        {
            public EmojiRules(string displayName, IEnumerable<string> strengths, IEnumerable<string> weaknesses)
            {
                DisplayName = displayName;
                Strengths = strengths.ToHashSet();
                Weaknesses = weaknesses.ToHashSet();
            }

            public string DisplayName { get; }
            public HashSet<string> Strengths { get; }
            public HashSet<string> Weaknesses { get; }
        }
    }
}
