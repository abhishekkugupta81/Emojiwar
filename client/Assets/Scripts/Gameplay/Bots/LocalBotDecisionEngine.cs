using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Core;

namespace EmojiWar.Client.Gameplay.Bots
{
    public static class LocalBotDecisionEngine
    {
        private static readonly Dictionary<string, EmojiScoreProfile> Profiles = new()
        {
            ["fire"] = new(new[] { "plant", "ice" }, new[] { "water", "soap" }),
            ["water"] = new(new[] { "fire", "lightning", "hole" }, new[] { "ice", "plant" }),
            ["lightning"] = new(new[] { "magnet", "ice" }, new[] { "water", "mirror", "hole" }),
            ["ice"] = new(new[] { "water", "bomb" }, new[] { "fire", "lightning" }),
            ["magnet"] = new(new[] { "bomb", "shield" }, new[] { "lightning", "mirror", "soap" }),
            ["bomb"] = new(new[] { "fire", "water", "lightning", "plant" }, new[] { "ice", "hole", "magnet" }),
            ["mirror"] = new(new[] { "lightning", "magnet" }, new[] { "bomb", "plant" }),
            ["hole"] = new(new[] { "bomb", "lightning" }, new[] { "water" }),
            ["shield"] = new(new[] { "fire", "lightning", "snake" }, new[] { "magnet", "plant" }),
            ["snake"] = new(new[] { "water", "magnet", "plant" }, new[] { "soap", "ice" }),
            ["soap"] = new(new[] { "fire", "snake" }, new[] { "plant" }),
            ["plant"] = new(new[] { "water", "shield", "mirror", "soap" }, new[] { "fire", "snake", "bomb" })
        };

        public static string ChoosePick(string mode, IReadOnlyList<string> botRemaining, IReadOnlyList<string> opponentRemaining)
        {
            var bestChoice = botRemaining.FirstOrDefault() ?? "fire";
            var bestScore = float.MinValue;

            foreach (var candidate in botRemaining)
            {
                var score = 0f;
                var profile = Profiles[candidate];

                foreach (var opponent in opponentRemaining)
                {
                    if (profile.Strengths.Contains(opponent))
                    {
                        score += 3f;
                    }
                    else if (profile.Weaknesses.Contains(opponent))
                    {
                        score -= 2f;
                    }
                    else
                    {
                        score += 1f;
                    }
                }

                if (candidate is "bomb" or "magnet")
                {
                    score += mode == LaunchSelections.BotSmart ? 0.8f : 0.35f;
                }

                if (candidate is "shield" or "soap" or "ice")
                {
                    score += mode == LaunchSelections.BotSmart ? 0.55f : 0.7f;
                }

                if (candidate is "fire" or "lightning" or "snake")
                {
                    score += mode == LaunchSelections.BotSmart ? 0.7f : 0.45f;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestChoice = candidate;
                }
            }

            return bestChoice;
        }

        private readonly struct EmojiScoreProfile
        {
            public EmojiScoreProfile(IEnumerable<string> strengths, IEnumerable<string> weaknesses)
            {
                Strengths = strengths.ToHashSet();
                Weaknesses = weaknesses.ToHashSet();
            }

            public HashSet<string> Strengths { get; }
            public HashSet<string> Weaknesses { get; }
        }
    }
}
