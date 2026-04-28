using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmojiWar.Client.Gameplay.Clash;
using UnityEditor;
using UnityEngine;
using Random = System.Random;

public static class EmojiClashBalanceSimulationRunner
{
    private const int DefaultSeed = 12345;
    private const int DefaultRandomMatchCount = 100000;
    private const int DefaultBotMatchCount = 100000;
    private const int DefaultFixedOrderSampleCount = 10000;
    private const int DefaultFixedOrderOpponentSamples = 200;
    private const int DefaultFixedOrderCounterSamples = 2000;

    [MenuItem("Emoji War/Emoji Clash/Run Balance Simulation")]
    public static void RunDefaultFromMenu()
    {
        try
        {
            var result = RunDefaultSimulation();
            EditorUtility.DisplayDialog(
                "Emoji Clash Balance Simulation",
                $"Reports written to:\n{result.OutputDirectory}",
                "OK");
            EditorUtility.RevealInFinder(result.OutputDirectory);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Emoji Clash Balance Simulation Failed", exception.Message, "OK");
        }
    }

    public static void RunDefaultBatch()
    {
        var result = RunDefaultSimulation();
        Debug.Log($"Emoji Clash balance reports written to: {result.OutputDirectory}");
    }

    public static EmojiClashBalanceSimulationResult RunDefaultSimulation()
    {
        return RunSimulation(CreateDefaultConfig());
    }

    public static void RunDefaultBatchAndExit()
    {
        try
        {
            var result = RunDefaultSimulation();
            Debug.Log($"Emoji Clash balance reports written to: {result.OutputDirectory}");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static EmojiClashBalanceSimulationConfig CreateDefaultConfig()
    {
        return new EmojiClashBalanceSimulationConfig
        {
            Seed = DefaultSeed,
            RandomMatchCount = DefaultRandomMatchCount,
            BotMatchCount = DefaultBotMatchCount,
            FixedOrderSampleCount = DefaultFixedOrderSampleCount,
            FixedOrderOpponentSamples = DefaultFixedOrderOpponentSamples,
            FixedOrderCounterSamples = DefaultFixedOrderCounterSamples
        };
    }

    public static EmojiClashBalanceSimulationResult RunSimulation(EmojiClashBalanceSimulationConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var result = new EmojiClashBalanceSimulationResult();
        BuildExactMatchupMatrix(result);

        RunScenario(
            result,
            "RandomVsRandom",
            new EmojiClashUniformRandomStrategy(),
            new EmojiClashUniformRandomStrategy(),
            Math.Max(1, config.RandomMatchCount),
            config.Seed + 11,
            playerIsBot: false,
            opponentIsBot: false);

        RunScenario(
            result,
            "BotVsRandom",
            new EmojiClashCurrentBotStrategy(),
            new EmojiClashUniformRandomStrategy(),
            Math.Max(1, config.BotMatchCount),
            config.Seed + 101,
            playerIsBot: true,
            opponentIsBot: false);

        RunScenario(
            result,
            "BotVsBot",
            new EmojiClashCurrentBotStrategy(),
            new EmojiClashCurrentBotStrategy(),
            Math.Max(1, config.BotMatchCount),
            config.Seed + 202,
            playerIsBot: true,
            opponentIsBot: true);

        RunScenario(
            result,
            "RandomVsBot",
            new EmojiClashUniformRandomStrategy(),
            new EmojiClashCurrentBotStrategy(),
            Math.Max(1, config.BotMatchCount),
            config.Seed + 303,
            playerIsBot: false,
            opponentIsBot: true);

        RunFixedOrderProbe(result, config);
        BuildFlaggedFindings(result);

        var outputDirectory = ResolveOutputDirectory(config);
        EmojiClashBalanceReport.WriteReport(outputDirectory, config, result);
        return result;
    }

    private static void BuildExactMatchupMatrix(EmojiClashBalanceSimulationResult result)
    {
        var unitTurnAccumulators = new Dictionary<(string unit, int turn), ExactUnitTurnAccumulator>();
        var roster = EmojiClashRules.LaunchRoster;
        foreach (var playerUnit in roster)
        {
            foreach (var opponentUnit in roster)
            {
                for (var turnIndex = 0; turnIndex < EmojiClashRules.TotalTurns; turnIndex++)
                {
                    var state = new EmojiClashMatchState
                    {
                        CurrentTurnIndex = turnIndex
                    };
                    var resolved = EmojiClashRules.ResolveTurn(state, playerUnit, opponentUnit);
                    result.MatchupRows.Add(new EmojiClashExactMatchupRow
                    {
                        PlayerUnit = playerUnit,
                        OpponentUnit = opponentUnit,
                        TurnNumber = turnIndex + 1,
                        TurnValue = resolved.TurnValue,
                        Outcome = resolved.Outcome,
                        PlayerCombatPower = resolved.PlayerBreakdown.TotalPower,
                        OpponentCombatPower = resolved.OpponentBreakdown.TotalPower,
                        ReadableReason = resolved.PlayerFacingReason
                    });

                    if (string.Equals(playerUnit, opponentUnit, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var key = (playerUnit, turnIndex + 1);
                    if (!unitTurnAccumulators.TryGetValue(key, out var accumulator))
                    {
                        accumulator = new ExactUnitTurnAccumulator();
                        unitTurnAccumulators[key] = accumulator;
                    }

                    accumulator.Record(resolved.Outcome, resolved.TurnValue);
                }
            }
        }

        foreach (var pair in unitTurnAccumulators)
        {
            var count = Math.Max(1, pair.Value.Favorable + pair.Value.Unfavorable + pair.Value.Draws);
            result.UnitTurnRows.Add(new EmojiClashUnitTurnReportRow
            {
                Scenario = "ExactMatrix",
                Unit = pair.Key.unit,
                TurnNumber = pair.Key.turn,
                TurnValue = EmojiClashRules.GetTurnValue(pair.Key.turn - 1),
                Appearances = count,
                Wins = pair.Value.Favorable,
                Losses = pair.Value.Unfavorable,
                Draws = pair.Value.Draws,
                WinRate = pair.Value.Favorable / (double)count,
                LossRate = pair.Value.Unfavorable / (double)count,
                DrawRate = pair.Value.Draws / (double)count,
                AverageRawPointDelta = pair.Value.RawPointDelta / (double)count,
                NormalizedScoreImpact = pair.Value.NormalizedDelta / (double)count,
                FavorableMatchups = pair.Value.Favorable,
                UnfavorableMatchups = pair.Value.Unfavorable,
                DrawMatchups = pair.Value.Draws,
                ExactNormalizedMatchupScore = pair.Value.NormalizedDelta / (double)count
            });
        }
    }

    private static void RunScenario(
        EmojiClashBalanceSimulationResult result,
        string scenario,
        IEmojiClashBalancePickStrategy playerStrategy,
        IEmojiClashBalancePickStrategy opponentStrategy,
        int matchCount,
        int seed,
        bool playerIsBot,
        bool opponentIsBot)
    {
        var random = new Random(seed);
        var accumulator = new ScenarioAccumulator(scenario, matchCount, playerIsBot, opponentIsBot);
        for (var matchIndex = 0; matchIndex < matchCount; matchIndex++)
        {
            var matchSeed = seed + matchIndex * 9973;
            var match = SimulateMatch(playerStrategy, opponentStrategy, random, matchSeed);
            accumulator.RecordMatch(match);
        }

        accumulator.AppendTo(result);
    }

    private static SimulatedMatchResult SimulateMatch(
        IEmojiClashBalancePickStrategy playerStrategy,
        IEmojiClashBalancePickStrategy opponentStrategy,
        Random random,
        int matchSeed)
    {
        var state = new EmojiClashMatchState
        {
            MatchSeed = matchSeed
        };
        var records = new List<EmojiClashTurnRecord>();
        var turnFiveMeaningful = false;
        var decidedBeforeTurnFive = false;

        for (var turnIndex = 0; turnIndex < EmojiClashRules.TotalTurns; turnIndex++)
        {
            state.CurrentTurnIndex = turnIndex;
            var playerAvailable = EmojiClashRules.LaunchRoster
                .Where(unitKey => !state.PlayerUsedUnitKeys.Contains(unitKey))
                .ToArray();
            var opponentAvailable = EmojiClashRules.LaunchRoster
                .Where(unitKey => !state.OpponentUsedUnitKeys.Contains(unitKey))
                .ToArray();
            var view = new EmojiClashBalanceMatchView
            {
                TurnIndex = turnIndex,
                PlayerScore = state.PlayerScore,
                OpponentScore = state.OpponentScore,
                MatchSeed = matchSeed,
                PlayerUsedUnitKeys = state.PlayerUsedUnitKeys.ToArray(),
                OpponentUsedUnitKeys = state.OpponentUsedUnitKeys.ToArray(),
                PlayerAvailableUnitKeys = playerAvailable,
                OpponentAvailableUnitKeys = opponentAvailable,
                Random = random
            };

            var playerPick = ResolveLegalPick(playerStrategy.PickUnit(view, EmojiClashBalanceSide.Player), playerAvailable);
            var opponentPick = ResolveLegalPick(opponentStrategy.PickUnit(view, EmojiClashBalanceSide.Opponent), opponentAvailable);
            var resolved = EmojiClashRules.ResolveTurn(state, playerPick, opponentPick);

            if (resolved.Outcome == EmojiClashTurnOutcome.PlayerWin)
            {
                state.PlayerScore += resolved.TurnValue;
            }
            else if (resolved.Outcome == EmojiClashTurnOutcome.OpponentWin)
            {
                state.OpponentScore += resolved.TurnValue;
            }

            state.PlayerUsedUnitKeys.Add(resolved.PlayerUnitKey);
            state.OpponentUsedUnitKeys.Add(resolved.OpponentUnitKey);

            var record = new EmojiClashTurnRecord
            {
                TurnNumber = resolved.TurnNumber,
                TurnValue = resolved.TurnValue,
                PlayerUnitKey = resolved.PlayerUnitKey,
                OpponentUnitKey = resolved.OpponentUnitKey,
                PlayerCombatPower = resolved.PlayerBreakdown.TotalPower,
                OpponentCombatPower = resolved.OpponentBreakdown.TotalPower,
                Outcome = resolved.Outcome,
                PlayerScoreAfter = state.PlayerScore,
                OpponentScoreAfter = state.OpponentScore,
                PlayerFacingReason = resolved.PlayerFacingReason
            };
            records.Add(record);

            if (turnIndex == EmojiClashRules.TotalTurns - 2)
            {
                var marginBeforeFinal = Math.Abs(state.PlayerScore - state.OpponentScore);
                turnFiveMeaningful = marginBeforeFinal <= EmojiClashRules.GetTurnValue(EmojiClashRules.TotalTurns - 1);
                decidedBeforeTurnFive = !turnFiveMeaningful;
            }
        }

        return new SimulatedMatchResult
        {
            PlayerScore = state.PlayerScore,
            OpponentScore = state.OpponentScore,
            Records = records,
            TurnFiveMeaningful = turnFiveMeaningful,
            DecidedBeforeTurnFive = decidedBeforeTurnFive
        };
    }

    private static string ResolveLegalPick(string requestedPick, IReadOnlyList<string> available)
    {
        if (available == null || available.Count == 0)
        {
            return string.Empty;
        }

        var normalized = EmojiClashRules.NormalizeUnitKey(requestedPick);
        return available.Contains(normalized, StringComparer.OrdinalIgnoreCase)
            ? normalized
            : available[0];
    }

    private static void RunFixedOrderProbe(EmojiClashBalanceSimulationResult result, EmojiClashBalanceSimulationConfig config)
    {
        var random = new Random(config.Seed + 404);
        var fixedOrderCount = Math.Max(1, config.FixedOrderSampleCount);
        var opponentSamples = Math.Max(1, config.FixedOrderOpponentSamples);
        var sampledOrders = SampleFixedOrders(random, fixedOrderCount);
        var fixedOrderResults = new List<EmojiClashFixedOrderReportRow>(sampledOrders.Count);

        for (var orderIndex = 0; orderIndex < sampledOrders.Count; orderIndex++)
        {
            var playerOrder = sampledOrders[orderIndex];
            var playerStrategy = new EmojiClashFixedOrderStrategy(playerOrder);
            var wins = 0;
            var draws = 0;
            var marginTotal = 0;

            for (var opponentIndex = 0; opponentIndex < opponentSamples; opponentIndex++)
            {
                var opponentOrder = SampleOneFixedOrder(random);
                var match = SimulateMatch(
                    playerStrategy,
                    new EmojiClashFixedOrderStrategy(opponentOrder),
                    random,
                    config.Seed + 500000 + orderIndex * 1000 + opponentIndex);

                if (match.PlayerScore > match.OpponentScore)
                {
                    wins++;
                }
                else if (match.PlayerScore == match.OpponentScore)
                {
                    draws++;
                }

                marginTotal += match.PlayerScore - match.OpponentScore;
            }

            fixedOrderResults.Add(new EmojiClashFixedOrderReportRow
            {
                Order = string.Join(">", playerOrder),
                EvaluatedMatches = opponentSamples,
                WinRate = wins / (double)opponentSamples,
                DrawRate = draws / (double)opponentSamples,
                AverageScoreMargin = marginTotal / (double)opponentSamples
            });
        }

        result.AverageSampledFixedOrderWinRate = fixedOrderResults.Count == 0
            ? 0.0
            : fixedOrderResults.Average(row => row.WinRate);

        result.TopFixedOrders = fixedOrderResults
            .OrderByDescending(row => row.WinRate)
            .ThenByDescending(row => row.AverageScoreMargin)
            .ThenBy(row => row.Order, StringComparer.Ordinal)
            .Take(50)
            .Select((row, index) =>
            {
                row.Rank = index + 1;
                return row;
            })
            .ToList();

        result.TopFixedOrderUnitCounts = CountTopOrderUnits(result.TopFixedOrders, turnFiveOnly: false);
        result.TopFixedOrderTurnFiveCounts = CountTopOrderUnits(result.TopFixedOrders, turnFiveOnly: true);
        BuildTopOrderCounterability(result, config);
    }

    private static void BuildTopOrderCounterability(EmojiClashBalanceSimulationResult result, EmojiClashBalanceSimulationConfig config)
    {
        var topOrders = result.TopFixedOrders.Take(10).ToArray();
        if (topOrders.Length == 0)
        {
            return;
        }

        var random = new Random(config.Seed + 909);
        var counterSamples = Math.Max(1, config.FixedOrderCounterSamples);
        foreach (var topOrder in topOrders)
        {
            var playerOrder = ParseFixedOrder(topOrder.Order);
            if (playerOrder.Length == 0)
            {
                continue;
            }

            var playerStrategy = new EmojiClashFixedOrderStrategy(playerOrder);
            string[] bestCounterOrder = Array.Empty<string>();
            SimulatedMatchResult bestMatch = null;
            var bestOutcomeScore = int.MaxValue;
            var bestMargin = int.MaxValue;

            for (var counterIndex = 0; counterIndex < counterSamples; counterIndex++)
            {
                var counterOrder = SampleOneFixedOrder(random);
                var match = SimulateMatch(
                    playerStrategy,
                    new EmojiClashFixedOrderStrategy(counterOrder),
                    random,
                    config.Seed + 900000 + topOrder.Rank * 10000 + counterIndex);
                var outcomeScore = match.PlayerScore > match.OpponentScore
                    ? 2
                    : match.PlayerScore == match.OpponentScore ? 1 : 0;
                var margin = match.PlayerScore - match.OpponentScore;

                if (outcomeScore < bestOutcomeScore || (outcomeScore == bestOutcomeScore && margin < bestMargin))
                {
                    bestOutcomeScore = outcomeScore;
                    bestMargin = margin;
                    bestCounterOrder = counterOrder;
                    bestMatch = match;
                }
            }

            if (bestMatch == null)
            {
                continue;
            }

            result.TopOrderCounterabilityRows.Add(new EmojiClashTopOrderCounterabilityRow
            {
                Rank = topOrder.Rank,
                TopOrder = topOrder.Order,
                EvaluatedCounterOrders = counterSamples,
                BestCounterOrder = string.Join(">", bestCounterOrder),
                TopOrderScore = bestMatch.PlayerScore,
                BestCounterScore = bestMatch.OpponentScore,
                TopOrderWinRateAgainstBestCounter = bestMatch.PlayerScore > bestMatch.OpponentScore ? 1.0 : 0.0,
                TopOrderDrawRateAgainstBestCounter = bestMatch.PlayerScore == bestMatch.OpponentScore ? 1.0 : 0.0,
                ScoreMarginAgainstBestCounter = bestMatch.PlayerScore - bestMatch.OpponentScore,
                AppearsCounterable = bestMatch.PlayerScore <= bestMatch.OpponentScore
            });
        }
    }

    private static List<string[]> SampleFixedOrders(Random random, int count)
    {
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orders = new List<string[]>(count);
        var maxAttempts = count * 20;
        var attempts = 0;
        while (orders.Count < count && attempts < maxAttempts)
        {
            attempts++;
            var order = SampleOneFixedOrder(random);
            var key = string.Join(">", order);
            if (unique.Add(key))
            {
                orders.Add(order);
            }
        }

        return orders;
    }

    private static string[] SampleOneFixedOrder(Random random)
    {
        var roster = EmojiClashRules.LaunchRoster.ToArray();
        for (var index = roster.Length - 1; index > 0; index--)
        {
            var swapIndex = random.Next(index + 1);
            (roster[index], roster[swapIndex]) = (roster[swapIndex], roster[index]);
        }

        return roster.Take(EmojiClashRules.TotalTurns).ToArray();
    }

    private static string[] ParseFixedOrder(string order)
    {
        if (string.IsNullOrWhiteSpace(order))
        {
            return Array.Empty<string>();
        }

        return order
            .Split(new[] { '>' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(EmojiClashRules.NormalizeUnitKey)
            .Where(unitKey => !string.IsNullOrWhiteSpace(unitKey))
            .Take(EmojiClashRules.TotalTurns)
            .ToArray();
    }

    private static Dictionary<string, int> CountTopOrderUnits(IEnumerable<EmojiClashFixedOrderReportRow> rows, bool turnFiveOnly)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var units = (row.Order ?? string.Empty).Split(new[] { '>' }, StringSplitOptions.RemoveEmptyEntries);
            if (turnFiveOnly)
            {
                if (units.Length >= EmojiClashRules.TotalTurns)
                {
                    Increment(counts, units[EmojiClashRules.TotalTurns - 1]);
                }
                continue;
            }

            foreach (var unit in units)
            {
                Increment(counts, unit);
            }
        }

        return counts;
    }

    private static void BuildFlaggedFindings(EmojiClashBalanceSimulationResult result)
    {
        foreach (var unit in result.UnitRows.Where(row => row.Scenario == "RandomVsRandom"))
        {
            if (unit.NormalizedScoreImpact > 0.15)
            {
                AddFlag(result, "Warning", "Unit Impact", $"{unit.Unit} has high positive normalized score impact.", unit.NormalizedScoreImpact, "> +0.15");
            }
            else if (unit.NormalizedScoreImpact < -0.15)
            {
                AddFlag(result, "Warning", "Unit Impact", $"{unit.Unit} has low negative normalized score impact.", unit.NormalizedScoreImpact, "< -0.15");
            }
        }

        var exactOpposingRows = result.MatchupRows
            .Where(row => !string.Equals(row.PlayerUnit, row.OpponentUnit, StringComparison.OrdinalIgnoreCase))
            .GroupBy(row => row.PlayerUnit);
        foreach (var group in exactOpposingRows)
        {
            var turnGroups = group.GroupBy(row => row.TurnNumber);
            var dominantTurns = turnGroups.Count(turnGroup => turnGroup.Count(row => row.Outcome == EmojiClashTurnOutcome.PlayerWin) >= 10);
            var weakTurns = turnGroups.Count(turnGroup => turnGroup.Count(row => row.Outcome == EmojiClashTurnOutcome.OpponentWin) >= 10);
            if (dominantTurns >= 3)
            {
                AddFlag(result, "Warning", "Exact Matchups", $"{group.Key} beats 10 or more opposing units on {dominantTurns} turns.", dominantTurns, ">= 3 turns");
            }

            if (weakTurns >= 3)
            {
                AddFlag(result, "Warning", "Exact Matchups", $"{group.Key} loses to 10 or more opposing units on {weakTurns} turns.", weakTurns, ">= 3 turns");
            }
        }

        foreach (var botPick in result.BotPickRows)
        {
            if (botPick.PickRate > 0.50)
            {
                AddFlag(result, "Warning", "Bot Picks", $"{botPick.Scenario} {botPick.BotSide} picks {botPick.Unit} more than half the time on Turn {botPick.TurnNumber}.", botPick.PickRate, "> 50%");
            }
        }

        foreach (var group in result.BotPickRows.GroupBy(row => new { row.Scenario, row.BotSide, row.Unit }))
        {
            var total = result.BotPickRows
                .Where(row => row.Scenario == group.Key.Scenario && row.BotSide == group.Key.BotSide)
                .Sum(row => row.Count);
            if (total <= 0)
            {
                continue;
            }

            var rate = group.Sum(row => row.Count) / (double)total;
            if (rate < 0.005)
            {
                AddFlag(result, "Info", "Bot Picks", $"{group.Key.Scenario} {group.Key.BotSide} almost never picks {group.Key.Unit}.", rate, "< 0.5%");
            }
        }

        var random = result.ScenarioSummaries.FirstOrDefault(row => row.Scenario == "RandomVsRandom");
        if (random != null)
        {
            if (random.TurnFiveMeaningfulRate < 0.60)
            {
                AddFlag(result, "Warning", "Turn Health", "Turn 5 is meaningful in too few random-vs-random matches.", random.TurnFiveMeaningfulRate, ">= 60%");
            }

            if (random.DrawRate > 0.25)
            {
                AddFlag(result, "Warning", "Draw Rate", "Random-vs-random draw rate is extremely high.", random.DrawRate, "<= 25%");
            }
            else if (random.DrawRate < 0.01)
            {
                AddFlag(result, "Info", "Draw Rate", "Random-vs-random draw rate is extremely low.", random.DrawRate, ">= 1%");
            }
        }

        var botVsRandom = result.ScenarioSummaries.FirstOrDefault(row => row.Scenario == "BotVsRandom");
        if (botVsRandom != null && (botVsRandom.PlayerWinRate < 0.50 || botVsRandom.PlayerWinRate > 0.70))
        {
            AddFlag(result, "Warning", "Bot Strength", "Current bot as player is outside the target range against uniform random.", botVsRandom.PlayerWinRate, "50%-70%");
        }

        var randomVsBot = result.ScenarioSummaries.FirstOrDefault(row => row.Scenario == "RandomVsBot");
        if (randomVsBot != null && (randomVsBot.OpponentWinRate < 0.50 || randomVsBot.OpponentWinRate > 0.70))
        {
            AddFlag(result, "Warning", "Bot Strength", "Current bot as opponent is outside the target range against uniform random.", randomVsBot.OpponentWinRate, "50%-70%");
        }

        var botVsBot = result.ScenarioSummaries.FirstOrDefault(row => row.Scenario == "BotVsBot");
        if (botVsBot != null && Math.Abs(botVsBot.PlayerWinRate - botVsBot.OpponentWinRate) > 0.03)
        {
            AddFlag(result, "Warning", "Bot Symmetry", "Bot-vs-bot is not close to symmetric.", Math.Abs(botVsBot.PlayerWinRate - botVsBot.OpponentWinRate), "<= 3pp gap");
        }

        var topOrder = result.TopFixedOrders.FirstOrDefault();
        if (topOrder != null && topOrder.WinRate > result.AverageSampledFixedOrderWinRate + 0.15)
        {
            AddFlag(result, "Warning", "Fixed Orders", "Top fixed order is dramatically stronger than the sampled field.", topOrder.WinRate - result.AverageSampledFixedOrderWinRate, "<= +15pp over average");
        }

        var topUnitCounts = result.TopFixedOrderUnitCounts
            .OrderByDescending(pair => pair.Value)
            .Take(4)
            .ToArray();
        if (topUnitCounts.Length >= 4 && topUnitCounts.Take(4).All(pair => pair.Value >= 30))
        {
            AddFlag(result, "Warning", "Fixed Orders", "Top 50 fixed orders rely on the same four-unit core too often.", string.Join(" ", topUnitCounts.Select(pair => $"{pair.Key}:{pair.Value}")), "top four units each < 30/50");
        }
        else if (topUnitCounts.Length >= 3 && topUnitCounts.Take(3).All(pair => pair.Value >= 35))
        {
            AddFlag(result, "Warning", "Fixed Orders", "Top 50 fixed orders rely on the same three-unit core too often.", string.Join(" ", topUnitCounts.Take(3).Select(pair => $"{pair.Key}:{pair.Value}")), "top three units each < 35/50");
        }
    }

    private static string ResolveOutputDirectory(EmojiClashBalanceSimulationConfig config)
    {
        var root = string.IsNullOrWhiteSpace(config.OutputRoot)
            ? Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath, "EmojiClashBalanceReports")
            : config.OutputRoot;
        return Path.Combine(root, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
    }

    private static void AddFlag(EmojiClashBalanceSimulationResult result, string severity, string category, string finding, object metric, string threshold)
    {
        result.FlaggedFindings.Add(new EmojiClashFlaggedFinding
        {
            Severity = severity,
            Category = category,
            Finding = finding,
            Metric = metric?.ToString() ?? string.Empty,
            Threshold = threshold
        });
    }

    private static void Increment(Dictionary<string, int> counts, string unit)
    {
        var key = EmojiClashRules.NormalizeUnitKey(unit);
        counts.TryGetValue(key, out var count);
        counts[key] = count + 1;
    }

    private sealed class ExactUnitTurnAccumulator
    {
        public int Favorable;
        public int Unfavorable;
        public int Draws;
        public int RawPointDelta;
        public int NormalizedDelta;

        public void Record(EmojiClashTurnOutcome outcome, int turnValue)
        {
            switch (outcome)
            {
                case EmojiClashTurnOutcome.PlayerWin:
                    Favorable++;
                    RawPointDelta += turnValue;
                    NormalizedDelta++;
                    break;
                case EmojiClashTurnOutcome.OpponentWin:
                    Unfavorable++;
                    RawPointDelta -= turnValue;
                    NormalizedDelta--;
                    break;
                default:
                    Draws++;
                    break;
            }
        }
    }

    private sealed class SimulatedMatchResult
    {
        public int PlayerScore;
        public int OpponentScore;
        public List<EmojiClashTurnRecord> Records = new();
        public bool TurnFiveMeaningful;
        public bool DecidedBeforeTurnFive;
    }

    private sealed class ScenarioAccumulator
    {
        private readonly string scenario;
        private readonly int matchCount;
        private readonly bool playerIsBot;
        private readonly bool opponentIsBot;
        private readonly Dictionary<string, EmojiClashUnitAccumulator> unitStats = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<(string unit, int turn), EmojiClashUnitAccumulator> unitTurnStats = new();
        private readonly Dictionary<int, TurnAccumulator> turnStats = new();
        private readonly Dictionary<(string side, int turn, string unit), BotPickAccumulator> botPickStats = new();
        private readonly Dictionary<(int playerScore, int opponentScore), int> scoreDistribution = new();
        private int playerWins;
        private int opponentWins;
        private int draws;
        private int playerScoreTotal;
        private int opponentScoreTotal;
        private int absoluteMarginTotal;
        private int turnFiveMeaningfulCount;
        private int decidedBeforeTurnFiveCount;

        public ScenarioAccumulator(string scenario, int matchCount, bool playerIsBot, bool opponentIsBot)
        {
            this.scenario = scenario;
            this.matchCount = matchCount;
            this.playerIsBot = playerIsBot;
            this.opponentIsBot = opponentIsBot;
        }

        public void RecordMatch(SimulatedMatchResult match)
        {
            if (match.PlayerScore > match.OpponentScore)
            {
                playerWins++;
            }
            else if (match.OpponentScore > match.PlayerScore)
            {
                opponentWins++;
            }
            else
            {
                draws++;
            }

            playerScoreTotal += match.PlayerScore;
            opponentScoreTotal += match.OpponentScore;
            absoluteMarginTotal += Math.Abs(match.PlayerScore - match.OpponentScore);
            if (match.TurnFiveMeaningful)
            {
                turnFiveMeaningfulCount++;
            }

            if (match.DecidedBeforeTurnFive)
            {
                decidedBeforeTurnFiveCount++;
            }

            var distributionKey = (match.PlayerScore, match.OpponentScore);
            scoreDistribution.TryGetValue(distributionKey, out var distributionCount);
            scoreDistribution[distributionKey] = distributionCount + 1;

            foreach (var record in match.Records)
            {
                RecordUnit(record.PlayerUnitKey, record, isPlayerPerspective: true);
                RecordUnit(record.OpponentUnitKey, record, isPlayerPerspective: false);
                RecordTurn(record);

                if (playerIsBot)
                {
                    RecordBotPick("PlayerBot", record.TurnNumber, record.PlayerUnitKey, record, isPlayerPerspective: true);
                }

                if (opponentIsBot)
                {
                    RecordBotPick("OpponentBot", record.TurnNumber, record.OpponentUnitKey, record, isPlayerPerspective: false);
                }
            }
        }

        public void AppendTo(EmojiClashBalanceSimulationResult result)
        {
            var safeMatchCount = Math.Max(1, matchCount);
            result.ScenarioSummaries.Add(new EmojiClashScenarioSummary
            {
                Scenario = scenario,
                MatchCount = matchCount,
                PlayerWins = playerWins,
                OpponentWins = opponentWins,
                Draws = draws,
                PlayerWinRate = playerWins / (double)safeMatchCount,
                OpponentWinRate = opponentWins / (double)safeMatchCount,
                DrawRate = draws / (double)safeMatchCount,
                AveragePlayerScore = playerScoreTotal / (double)safeMatchCount,
                AverageOpponentScore = opponentScoreTotal / (double)safeMatchCount,
                AverageScoreMargin = absoluteMarginTotal / (double)safeMatchCount,
                TurnFiveMeaningfulCount = turnFiveMeaningfulCount,
                DecidedBeforeTurnFiveCount = decidedBeforeTurnFiveCount,
                TurnFiveMeaningfulRate = turnFiveMeaningfulCount / (double)safeMatchCount,
                DecidedBeforeTurnFiveRate = decidedBeforeTurnFiveCount / (double)safeMatchCount
            });

            var totalUnitPicks = safeMatchCount * EmojiClashRules.TotalTurns * 2;
            foreach (var pair in unitStats)
            {
                result.UnitRows.Add(BuildUnitRow(scenario, pair.Key, pair.Value, totalUnitPicks));
            }

            foreach (var pair in unitTurnStats)
            {
                result.UnitTurnRows.Add(BuildUnitTurnRow(scenario, pair.Key.unit, pair.Key.turn, pair.Value));
            }

            foreach (var pair in turnStats)
            {
                result.TurnRows.Add(pair.Value.ToRow(scenario, pair.Key));
            }

            AppendBotPickRows(result, safeMatchCount, playerIsBot, "PlayerBot");
            AppendBotPickRows(result, safeMatchCount, opponentIsBot, "OpponentBot");

            foreach (var pair in scoreDistribution)
            {
                result.MatchDistributionRows.Add(new EmojiClashMatchDistributionRow
                {
                    Scenario = scenario,
                    PlayerScore = pair.Key.playerScore,
                    OpponentScore = pair.Key.opponentScore,
                    ScoreMargin = pair.Key.playerScore - pair.Key.opponentScore,
                    Count = pair.Value
                });
            }
        }

        private void RecordUnit(string unit, EmojiClashTurnRecord record, bool isPlayerPerspective)
        {
            if (!unitStats.TryGetValue(unit, out var unitAccumulator))
            {
                unitAccumulator = new EmojiClashUnitAccumulator();
                unitStats[unit] = unitAccumulator;
            }

            unitAccumulator.Record(record.Outcome, record.TurnValue, isPlayerPerspective);

            var key = (unit, record.TurnNumber);
            if (!unitTurnStats.TryGetValue(key, out var turnAccumulator))
            {
                turnAccumulator = new EmojiClashUnitAccumulator();
                unitTurnStats[key] = turnAccumulator;
            }

            turnAccumulator.Record(record.Outcome, record.TurnValue, isPlayerPerspective);
        }

        private void RecordTurn(EmojiClashTurnRecord record)
        {
            if (!turnStats.TryGetValue(record.TurnNumber, out var accumulator))
            {
                accumulator = new TurnAccumulator();
                turnStats[record.TurnNumber] = accumulator;
            }

            accumulator.Record(record);
        }

        private void RecordBotPick(string side, int turnNumber, string unit, EmojiClashTurnRecord record, bool isPlayerPerspective)
        {
            var key = (side, turnNumber, unit);
            if (!botPickStats.TryGetValue(key, out var accumulator))
            {
                accumulator = new BotPickAccumulator();
                botPickStats[key] = accumulator;
            }

            accumulator.Record(record, isPlayerPerspective);
        }

        private void AppendBotPickRows(EmojiClashBalanceSimulationResult result, int safeMatchCount, bool sideIsBot, string side)
        {
            if (!sideIsBot)
            {
                return;
            }

            for (var turnNumber = 1; turnNumber <= EmojiClashRules.TotalTurns; turnNumber++)
            {
                foreach (var unit in EmojiClashRules.LaunchRoster)
                {
                    var key = (side, turnNumber, unit);
                    if (!botPickStats.TryGetValue(key, out var accumulator))
                    {
                        accumulator = new BotPickAccumulator();
                    }

                    result.BotPickRows.Add(accumulator.ToRow(scenario, side, turnNumber, unit, safeMatchCount));
                }
            }
        }

        private static EmojiClashUnitReportRow BuildUnitRow(string scenario, string unit, EmojiClashUnitAccumulator accumulator, int totalUnitPicks)
        {
            var appearances = Math.Max(1, accumulator.Appearances);
            return new EmojiClashUnitReportRow
            {
                Scenario = scenario,
                Unit = unit,
                Appearances = accumulator.Appearances,
                Picks = accumulator.Appearances,
                Wins = accumulator.Wins,
                Losses = accumulator.Losses,
                Draws = accumulator.Draws,
                PickRate = totalUnitPicks <= 0 ? 0.0 : accumulator.Appearances / (double)totalUnitPicks,
                WinRate = accumulator.Wins / (double)appearances,
                LossRate = accumulator.Losses / (double)appearances,
                DrawRate = accumulator.Draws / (double)appearances,
                AverageRawPointDelta = accumulator.RawPointDelta / (double)appearances,
                NormalizedScoreImpact = accumulator.NormalizedDelta / (double)appearances
            };
        }

        private static EmojiClashUnitTurnReportRow BuildUnitTurnRow(string scenario, string unit, int turnNumber, EmojiClashUnitAccumulator accumulator)
        {
            var appearances = Math.Max(1, accumulator.Appearances);
            return new EmojiClashUnitTurnReportRow
            {
                Scenario = scenario,
                Unit = unit,
                TurnNumber = turnNumber,
                TurnValue = EmojiClashRules.GetTurnValue(turnNumber - 1),
                Appearances = accumulator.Appearances,
                Wins = accumulator.Wins,
                Losses = accumulator.Losses,
                Draws = accumulator.Draws,
                WinRate = accumulator.Wins / (double)appearances,
                LossRate = accumulator.Losses / (double)appearances,
                DrawRate = accumulator.Draws / (double)appearances,
                AverageRawPointDelta = accumulator.RawPointDelta / (double)appearances,
                NormalizedScoreImpact = accumulator.NormalizedDelta / (double)appearances
            };
        }
    }

    private sealed class TurnAccumulator
    {
        private int playerWins;
        private int opponentWins;
        private int draws;
        private int pointSwingTotal;

        public void Record(EmojiClashTurnRecord record)
        {
            switch (record.Outcome)
            {
                case EmojiClashTurnOutcome.PlayerWin:
                    playerWins++;
                    pointSwingTotal += record.TurnValue;
                    break;
                case EmojiClashTurnOutcome.OpponentWin:
                    opponentWins++;
                    pointSwingTotal -= record.TurnValue;
                    break;
                default:
                    draws++;
                    break;
            }
        }

        public EmojiClashTurnReportRow ToRow(string scenario, int turnNumber)
        {
            var total = Math.Max(1, playerWins + opponentWins + draws);
            return new EmojiClashTurnReportRow
            {
                Scenario = scenario,
                TurnNumber = turnNumber,
                TurnValue = EmojiClashRules.GetTurnValue(turnNumber - 1),
                PlayerWins = playerWins,
                OpponentWins = opponentWins,
                Draws = draws,
                PlayerWinRate = playerWins / (double)total,
                OpponentWinRate = opponentWins / (double)total,
                DrawRate = draws / (double)total,
                AveragePointSwing = pointSwingTotal / (double)total
            };
        }
    }

    private sealed class BotPickAccumulator
    {
        private int count;
        private int rawPointDelta;

        public void Record(EmojiClashTurnRecord record, bool isPlayerPerspective)
        {
            count++;
            var signedOutcome = record.Outcome switch
            {
                EmojiClashTurnOutcome.Draw => 0,
                EmojiClashTurnOutcome.PlayerWin => isPlayerPerspective ? 1 : -1,
                EmojiClashTurnOutcome.OpponentWin => isPlayerPerspective ? -1 : 1,
                _ => 0
            };
            rawPointDelta += signedOutcome * record.TurnValue;
        }

        public EmojiClashBotPickReportRow ToRow(string scenario, string side, int turnNumber, string unit, int matchCount)
        {
            return new EmojiClashBotPickReportRow
            {
                Scenario = scenario,
                BotSide = side,
                TurnNumber = turnNumber,
                Unit = unit,
                Count = count,
                PickRate = matchCount <= 0 ? 0.0 : count / (double)matchCount,
                AverageRawPointDelta = count <= 0 ? 0.0 : rawPointDelta / (double)count
            };
        }
    }
}
