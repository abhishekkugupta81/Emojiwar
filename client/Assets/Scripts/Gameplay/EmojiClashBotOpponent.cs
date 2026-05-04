using System;
using System.Collections.Generic;
using System.Linq;
namespace EmojiWar.Client.Gameplay.Clash
{
    public sealed class EmojiClashBotOpponent : IEmojiClashOpponent
    {
        public const string BotPolicyVersion = "opponent-policy-v2";
        public const string BotPolicySeedSalt = "opponent-policy-v2";

        private const int CandidatePoolSize = 8;
        private const int ExplorationPoolSize = 10;
        private const double ScoreJitterRange = 0.28;
        private const double SoftmaxTemperature = 1.25;
        private const double ExplorationChance = 0.18;
        private const double ReasonableScoreWindow = 2.2;

        private static readonly Dictionary<int, Dictionary<string, double>> TurnOverusePenalty = new()
        {
            [0] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["fire"] = 0.24,
                ["wind"] = 0.34,
                ["bomb"] = 0.2,
                ["lightning"] = 0.2,
                ["magnet"] = 0.14,
                ["mirror"] = 0.1
            },
            [1] = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["wind"] = 0.2,
                ["bomb"] = 0.12,
                ["lightning"] = 0.1
            }
        };

        public string PickUnit(EmojiClashMatchState state, IReadOnlyList<string> availableUnitKeys)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (availableUnitKeys == null || availableUnitKeys.Count == 0)
            {
                return string.Empty;
            }

            var legalPicks = availableUnitKeys
                .Select(EmojiClashRules.NormalizeUnitKey)
                .Where(unitKey => !string.IsNullOrWhiteSpace(unitKey) && !state.OpponentUsedUnitKeys.Contains(unitKey))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (legalPicks.Length == 0)
            {
                return EmojiClashRules.NormalizeUnitKey(availableUnitKeys[0]);
            }

            var playerRemaining = EmojiClashRules.LaunchRoster
                .Where(unitKey => !state.PlayerUsedUnitKeys.Contains(unitKey))
                .ToArray();
            var turnIndex = Math.Clamp(state.CurrentTurnIndex, 0, EmojiClashRules.TotalTurns - 1);
            var turnValue = EmojiClashRules.GetTurnValue(turnIndex);
            var scoredPicks = legalPicks
                .Select(unitKey => new ScoredPick(unitKey, ScoreCandidate(state, unitKey, playerRemaining, turnIndex, turnValue) +
                    ResolveTurnOverusePenalty(unitKey, turnIndex) +
                    ResolveSeededJitter(state, unitKey, turnIndex)))
                .OrderByDescending(pick => pick.Score)
                .ThenByDescending(pick => StableTieBreak(state.MatchSeed, turnIndex, pick.UnitKey))
                .ToArray();

            var bestScore = scoredPicks[0].Score;
            var pool = scoredPicks
                .Where(pick => pick.Score >= bestScore - ReasonableScoreWindow)
                .Take(Math.Min(CandidatePoolSize, scoredPicks.Length))
                .ToArray();
            if (pool.Length < Math.Min(3, scoredPicks.Length))
            {
                pool = scoredPicks.Take(Math.Min(3, scoredPicks.Length)).ToArray();
            }

            return PickWeighted(state, pool, scoredPicks, turnIndex);
        }

        private static double ScoreCandidate(
            EmojiClashMatchState state,
            string candidate,
            IReadOnlyList<string> playerRemaining,
            int turnIndex,
            int turnValue)
        {
            var profile = EmojiClashRules.GetUnitProfile(candidate);
            var turnNumber = turnIndex + 1;
            var expectedOutcome = ResolveExpectedOutcome(state, candidate, playerRemaining);
            var timingScore = ResolveTimingScore(profile, turnNumber, turnValue);
            var counterCoverage = ResolveCounterOpportunity(candidate, playerRemaining) * 0.30;
            var comebackScore = state.OpponentScore < state.PlayerScore
                ? profile.BehindBonus * (1.1 + Math.Min(3, state.PlayerScore - state.OpponentScore) * 0.25)
                : 0.0;
            var riskAdjustment = profile.Tags.Contains("safe", StringComparer.OrdinalIgnoreCase) ? 0.25 : 0.0;
            var flexibility = profile.Tags.Contains("flexible", StringComparer.OrdinalIgnoreCase) && turnIndex <= 1 ? 0.35 : 0.0;

            return expectedOutcome * 1.25 +
                timingScore +
                counterCoverage +
                comebackScore +
                riskAdjustment +
                flexibility;
        }

        private static double ResolveExpectedOutcome(EmojiClashMatchState state, string candidate, IReadOnlyList<string> playerRemaining)
        {
            if (playerRemaining == null || playerRemaining.Count == 0)
            {
                return 0.0;
            }

            var evaluationState = new EmojiClashMatchState
            {
                CurrentTurnIndex = state.CurrentTurnIndex,
                PlayerScore = state.PlayerScore,
                OpponentScore = state.OpponentScore,
                MatchSeed = state.MatchSeed
            };
            var total = 0.0;
            foreach (var playerUnit in playerRemaining)
            {
                var resolved = EmojiClashRules.ResolveTurn(evaluationState, playerUnit, candidate);
                total += resolved.Outcome switch
                {
                    EmojiClashTurnOutcome.OpponentWin => 1.0,
                    EmojiClashTurnOutcome.PlayerWin => -1.0,
                    _ => 0.0
                };
            }

            return total / playerRemaining.Count;
        }

        private static double ResolveTimingScore(EmojiClashProfile profile, int turnNumber, int turnValue)
        {
            var timingWeight = 0.0;
            if (turnNumber <= 2)
            {
                timingWeight += profile.EarlyBonus * 0.45;
            }

            if (turnNumber >= 4)
            {
                timingWeight += profile.LateBonus * 0.55;
            }

            if (turnNumber == EmojiClashRules.TotalTurns)
            {
                timingWeight += profile.FinalTurnBonus * 0.65;
            }

            if (turnValue >= 2 && profile.Tags.Contains("scaling", StringComparer.OrdinalIgnoreCase))
            {
                timingWeight += 0.35;
            }

            return timingWeight;
        }

        private static int ResolveCounterOpportunity(string candidate, IReadOnlyList<string> playerRemaining)
        {
            var candidateProfile = EmojiClashRules.GetUnitProfile(candidate);
            var total = 0;
            foreach (var playerUnitKey in playerRemaining)
            {
                var opponentProfile = EmojiClashRules.GetUnitProfile(playerUnitKey);
                total += opponentProfile.Tags.Count(tag => candidateProfile.CounterTags.Contains(tag, StringComparer.OrdinalIgnoreCase));
            }

            return total;
        }

        private static double ResolveTurnOverusePenalty(string unitKey, int turnIndex)
        {
            return TurnOverusePenalty.TryGetValue(turnIndex, out var penalties) &&
                penalties.TryGetValue(unitKey, out var penalty)
                ? -penalty
                : 0.0;
        }

        private static double ResolveSeededJitter(EmojiClashMatchState state, string unitKey, int turnIndex)
        {
            unchecked
            {
                var hash = state.MatchSeed ^ 0x3A71B5;
                hash = (hash * 397) ^ turnIndex;
                hash = (hash * 397) ^ state.PlayerScore;
                hash = (hash * 397) ^ (state.OpponentScore << 7);
                hash = MixString(hash, unitKey);
                var normalized = (hash & 0x7FFFFFFF) / (double)int.MaxValue;
                return (normalized - 0.5) * ScoreJitterRange;
            }
        }

        private static string PickWeighted(EmojiClashMatchState state, IReadOnlyList<ScoredPick> pool, IReadOnlyList<ScoredPick> scoredPicks, int turnIndex)
        {
            if (pool == null || pool.Count == 0)
            {
                return string.Empty;
            }

            var random = new Random(BuildPickSeed(state, turnIndex, scoredPicks) & int.MaxValue);
            var activePool = random.NextDouble() < ExplorationChance
                ? scoredPicks.Take(Math.Min(ExplorationPoolSize, scoredPicks.Count)).ToArray()
                : pool.ToArray();
            var maxScore = activePool.Max(pick => pick.Score);
            var weights = activePool
                .Select(pick => Math.Exp(Math.Clamp((pick.Score - maxScore) / SoftmaxTemperature, -8.0, 0.0)))
                .ToArray();
            var totalWeight = weights.Sum();
            if (totalWeight <= 0.0)
            {
                return activePool[0].UnitKey;
            }

            var roll = random.NextDouble() * totalWeight;
            for (var index = 0; index < activePool.Length; index++)
            {
                roll -= weights[index];
                if (roll <= 0.0)
                {
                    return activePool[index].UnitKey;
                }
            }

            return activePool[^1].UnitKey;
        }

        private static int BuildPickSeed(EmojiClashMatchState state, int turnIndex, IReadOnlyList<ScoredPick> pool)
        {
            unchecked
            {
                var hash = state.MatchSeed;
                hash = MixString(hash, BotPolicySeedSalt);
                hash = MixString(hash, "opponent");
                hash = (hash * 397) ^ turnIndex;
                hash = (hash * 397) ^ state.PlayerScore;
                hash = (hash * 397) ^ (state.OpponentScore << 8);
                foreach (var unitKey in state.PlayerUsedUnitKeys.OrderBy(unitKey => unitKey, StringComparer.OrdinalIgnoreCase))
                {
                    hash = MixString(hash, unitKey);
                }

                hash = (hash * 397) ^ 0x51f15e;
                foreach (var unitKey in state.OpponentUsedUnitKeys.OrderBy(unitKey => unitKey, StringComparer.OrdinalIgnoreCase))
                {
                    hash = MixString(hash, unitKey);
                }

                foreach (var pick in pool)
                {
                    hash = MixString(hash, pick.UnitKey);
                    hash = (hash * 397) ^ (int)Math.Round(pick.Score * 100.0);
                }

                return hash;
            }
        }

        private static int StableTieBreak(int seed, int turnIndex, string candidate)
        {
            unchecked
            {
                var hash = seed;
                hash = (hash * 397) ^ turnIndex;
                return MixString(hash, candidate);
            }
        }

        private static int MixString(int hash, string value)
        {
            unchecked
            {
                foreach (var character in EmojiClashRules.NormalizeUnitKey(value))
                {
                    hash = (hash * 31) ^ character;
                }

                return hash;
            }
        }

        private readonly struct ScoredPick
        {
            public readonly string UnitKey;
            public readonly double Score;

            public ScoredPick(string unitKey, double score)
            {
                UnitKey = unitKey;
                Score = score;
            }
        }
    }
}
