using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using EmojiWar.Client.Gameplay.Clash;

public sealed class EmojiClashBalanceSimulationConfig
{
    public int Seed = 12345;
    public int RandomMatchCount = 100000;
    public int BotMatchCount = 100000;
    public int FixedOrderSampleCount = 10000;
    public int FixedOrderOpponentSamples = 200;
    public int FixedOrderCounterSamples = 2000;
    public string OutputRoot = string.Empty;
}

public sealed class EmojiClashBalanceSimulationResult
{
    public string OutputDirectory = string.Empty;
    public List<EmojiClashExactMatchupRow> MatchupRows = new();
    public List<EmojiClashUnitTurnReportRow> UnitTurnRows = new();
    public List<EmojiClashUnitReportRow> UnitRows = new();
    public List<EmojiClashTurnReportRow> TurnRows = new();
    public List<EmojiClashBotPickReportRow> BotPickRows = new();
    public List<EmojiClashMatchDistributionRow> MatchDistributionRows = new();
    public List<EmojiClashScenarioSummary> ScenarioSummaries = new();
    public List<EmojiClashFixedOrderReportRow> TopFixedOrders = new();
    public List<EmojiClashTopOrderCounterabilityRow> TopOrderCounterabilityRows = new();
    public List<EmojiClashFlaggedFinding> FlaggedFindings = new();
    public Dictionary<string, int> TopFixedOrderUnitCounts = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> TopFixedOrderTurnFiveCounts = new(StringComparer.OrdinalIgnoreCase);
    public double AverageSampledFixedOrderWinRate;
}

public sealed class EmojiClashScenarioSummary
{
    public string Scenario = string.Empty;
    public int MatchCount;
    public int PlayerWins;
    public int OpponentWins;
    public int Draws;
    public double PlayerWinRate;
    public double OpponentWinRate;
    public double DrawRate;
    public double AveragePlayerScore;
    public double AverageOpponentScore;
    public double AverageScoreMargin;
    public int TurnFiveMeaningfulCount;
    public int DecidedBeforeTurnFiveCount;
    public double TurnFiveMeaningfulRate;
    public double DecidedBeforeTurnFiveRate;
}

public sealed class EmojiClashExactMatchupRow
{
    public string PlayerUnit = string.Empty;
    public string OpponentUnit = string.Empty;
    public int TurnNumber;
    public int TurnValue;
    public EmojiClashTurnOutcome Outcome;
    public int PlayerCombatPower;
    public int OpponentCombatPower;
    public string ReadableReason = string.Empty;
}

public sealed class EmojiClashUnitReportRow
{
    public string Scenario = string.Empty;
    public string Unit = string.Empty;
    public int Appearances;
    public int Picks;
    public int Wins;
    public int Losses;
    public int Draws;
    public double PickRate;
    public double WinRate;
    public double LossRate;
    public double DrawRate;
    public double AverageRawPointDelta;
    public double NormalizedScoreImpact;
}

public sealed class EmojiClashUnitTurnReportRow
{
    public string Scenario = string.Empty;
    public string Unit = string.Empty;
    public int TurnNumber;
    public int TurnValue;
    public int Appearances;
    public int Wins;
    public int Losses;
    public int Draws;
    public double WinRate;
    public double LossRate;
    public double DrawRate;
    public double AverageRawPointDelta;
    public double NormalizedScoreImpact;
    public int FavorableMatchups;
    public int UnfavorableMatchups;
    public int DrawMatchups;
    public double ExactNormalizedMatchupScore;
}

public sealed class EmojiClashTurnReportRow
{
    public string Scenario = string.Empty;
    public int TurnNumber;
    public int TurnValue;
    public int PlayerWins;
    public int OpponentWins;
    public int Draws;
    public double PlayerWinRate;
    public double OpponentWinRate;
    public double DrawRate;
    public double AveragePointSwing;
}

public sealed class EmojiClashBotPickReportRow
{
    public string Scenario = string.Empty;
    public string BotSide = string.Empty;
    public int TurnNumber;
    public string Unit = string.Empty;
    public int Count;
    public double PickRate;
    public double AverageRawPointDelta;
}

public sealed class EmojiClashMatchDistributionRow
{
    public string Scenario = string.Empty;
    public int PlayerScore;
    public int OpponentScore;
    public int ScoreMargin;
    public int Count;
}

public sealed class EmojiClashFixedOrderReportRow
{
    public int Rank;
    public string Order = string.Empty;
    public int EvaluatedMatches;
    public double WinRate;
    public double DrawRate;
    public double AverageScoreMargin;
}

public sealed class EmojiClashTopOrderCounterabilityRow
{
    public int Rank;
    public string TopOrder = string.Empty;
    public int EvaluatedCounterOrders;
    public string BestCounterOrder = string.Empty;
    public int TopOrderScore;
    public int BestCounterScore;
    public double TopOrderWinRateAgainstBestCounter;
    public double TopOrderDrawRateAgainstBestCounter;
    public int ScoreMarginAgainstBestCounter;
    public bool AppearsCounterable;
}

public sealed class EmojiClashFlaggedFinding
{
    public string Severity = "Warning";
    public string Category = string.Empty;
    public string Finding = string.Empty;
    public string Metric = string.Empty;
    public string Threshold = string.Empty;
}

public sealed class EmojiClashUnitAccumulator
{
    public int Appearances;
    public int Wins;
    public int Losses;
    public int Draws;
    public int RawPointDelta;
    public int NormalizedDelta;

    public void Record(EmojiClashTurnOutcome outcome, int turnValue, bool isPlayerPerspective)
    {
        Appearances++;
        var signedOutcome = outcome switch
        {
            EmojiClashTurnOutcome.Draw => 0,
            EmojiClashTurnOutcome.PlayerWin => isPlayerPerspective ? 1 : -1,
            EmojiClashTurnOutcome.OpponentWin => isPlayerPerspective ? -1 : 1,
            _ => 0
        };

        if (signedOutcome > 0)
        {
            Wins++;
        }
        else if (signedOutcome < 0)
        {
            Losses++;
        }
        else
        {
            Draws++;
        }

        RawPointDelta += signedOutcome * turnValue;
        NormalizedDelta += signedOutcome;
    }
}

public static class EmojiClashBalanceReport
{
    public static void WriteReport(string outputDirectory, EmojiClashBalanceSimulationConfig config, EmojiClashBalanceSimulationResult result)
    {
        Directory.CreateDirectory(outputDirectory);
        result.OutputDirectory = outputDirectory;

        WriteText(Path.Combine(outputDirectory, "balance_summary.md"), BuildSummaryMarkdown(config, result));
        WriteCsv(Path.Combine(outputDirectory, "matchup_matrix.csv"), BuildMatchupMatrixCsv(result.MatchupRows));
        WriteCsv(Path.Combine(outputDirectory, "unit_stats.csv"), BuildUnitStatsCsv(result.UnitRows));
        WriteCsv(Path.Combine(outputDirectory, "unit_turn_stats.csv"), BuildUnitTurnStatsCsv(result.UnitTurnRows));
        WriteCsv(Path.Combine(outputDirectory, "turn_stats.csv"), BuildTurnStatsCsv(result.TurnRows));
        WriteCsv(Path.Combine(outputDirectory, "bot_pick_stats.csv"), BuildBotPickStatsCsv(result.BotPickRows));
        WriteCsv(Path.Combine(outputDirectory, "match_distribution.csv"), BuildMatchDistributionCsv(result.MatchDistributionRows));
        WriteCsv(Path.Combine(outputDirectory, "top_fixed_orders.csv"), BuildTopFixedOrdersCsv(result.TopFixedOrders));
        WriteCsv(Path.Combine(outputDirectory, "top_order_counterability.csv"), BuildTopOrderCounterabilityCsv(result.TopOrderCounterabilityRows));
        WriteCsv(Path.Combine(outputDirectory, "flagged_findings.csv"), BuildFlaggedFindingsCsv(result.FlaggedFindings));
    }

    public static string BuildSummaryMarkdown(EmojiClashBalanceSimulationConfig config, EmojiClashBalanceSimulationResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Emoji Clash Balance Summary");
        builder.AppendLine();
        builder.AppendLine("## 1. Simulation Settings");
        builder.AppendLine($"- Seed: {config.Seed}");
        builder.AppendLine($"- Random match count: {config.RandomMatchCount}");
        builder.AppendLine($"- Bot scenario match count: {config.BotMatchCount}");
        builder.AppendLine($"- Fixed order samples: {config.FixedOrderSampleCount}");
        builder.AppendLine($"- Fixed order opponent samples: {config.FixedOrderOpponentSamples}");
        builder.AppendLine($"- Fixed order counter samples: {config.FixedOrderCounterSamples}");
        builder.AppendLine($"- Unit count: {EmojiClashRules.LaunchRoster.Count}");
        builder.AppendLine("- Turn values: 1, 1, 2, 2, 3");
        builder.AppendLine("- Batch command: `Unity -batchmode -projectPath <project> -executeMethod EmojiClashBalanceSimulationRunner.RunDefaultBatchAndExit -quit`");
        builder.AppendLine();

        builder.AppendLine("## 2. High-Level Result");
        foreach (var scenario in result.ScenarioSummaries)
        {
            builder.AppendLine($"- {scenario.Scenario}: player {Pct(scenario.PlayerWinRate)}, opponent {Pct(scenario.OpponentWinRate)}, draw {Pct(scenario.DrawRate)}, avg score {Num(scenario.AveragePlayerScore)}-{Num(scenario.AverageOpponentScore)}");
        }
        builder.AppendLine();

        builder.AppendLine("## 3. Unit Health");
        AppendUnitList(builder, "Strongest units", result.UnitRows.Where(row => row.Scenario == "RandomVsRandom").OrderByDescending(row => row.NormalizedScoreImpact).Take(5));
        AppendUnitList(builder, "Weakest units", result.UnitRows.Where(row => row.Scenario == "RandomVsRandom").OrderBy(row => row.NormalizedScoreImpact).Take(5));
        AppendUnitList(
            builder,
            "Strongest final-turn units",
            result.UnitTurnRows
                .Where(row => row.Scenario == "RandomVsRandom" && row.TurnNumber == EmojiClashRules.TotalTurns)
                .OrderByDescending(row => row.NormalizedScoreImpact)
                .Take(5));
        AppendUnitList(builder, "Lowest bot-usage units", result.UnitRows.Where(row => row.Scenario.Contains("Bot", StringComparison.OrdinalIgnoreCase)).OrderBy(row => row.PickRate).Take(5));
        builder.AppendLine();

        builder.AppendLine("## 4. Turn Health");
        var random = result.ScenarioSummaries.FirstOrDefault(row => row.Scenario == "RandomVsRandom");
        if (random != null)
        {
            builder.AppendLine($"- Turn 5 meaningful rate: {Pct(random.TurnFiveMeaningfulRate)}");
            builder.AppendLine($"- Already decided before Turn 5: {Pct(random.DecidedBeforeTurnFiveRate)}");
            builder.AppendLine($"- Average score margin: {Num(random.AverageScoreMargin)}");
        }
        foreach (var turn in result.TurnRows.Where(row => row.Scenario == "RandomVsRandom").OrderBy(row => row.TurnNumber))
        {
            builder.AppendLine($"- Turn {turn.TurnNumber}: player {Pct(turn.PlayerWinRate)}, opponent {Pct(turn.OpponentWinRate)}, draw {Pct(turn.DrawRate)}");
        }
        builder.AppendLine();

        builder.AppendLine("## 5. Bot Behavior");
        builder.AppendLine("- Hidden-pick check: the simulator calls `EmojiClashBotOpponent` with only scores, used rosters, seed, turn index, and available bot units. The current hidden player pick is not passed to the bot.");
        foreach (var scenario in result.ScenarioSummaries.Where(row => row.Scenario.Contains("Bot", StringComparison.OrdinalIgnoreCase)))
        {
            builder.AppendLine($"- {scenario.Scenario}: player {Pct(scenario.PlayerWinRate)}, opponent {Pct(scenario.OpponentWinRate)}, draw {Pct(scenario.DrawRate)}");
        }
        foreach (var group in result.BotPickRows.GroupBy(row => new { row.Scenario, row.BotSide }).OrderBy(group => group.Key.Scenario).ThenBy(group => group.Key.BotSide))
        {
            var aggregate = group
                .GroupBy(row => row.Unit)
                .Select(unitGroup => new { Unit = unitGroup.Key, Count = unitGroup.Sum(row => row.Count) })
                .OrderByDescending(row => row.Count)
                .ThenBy(row => row.Unit, StringComparer.Ordinal)
                .ToArray();
            var total = Math.Max(1, aggregate.Sum(row => row.Count));
            var most = aggregate.Take(3).Select(row => $"{row.Unit} {Pct(row.Count / (double)total)}");
            var least = aggregate.Reverse().Take(3).Select(row => $"{row.Unit} {Pct(row.Count / (double)total)}");
            var finalTurn = group
                .Where(row => row.TurnNumber == EmojiClashRules.TotalTurns)
                .OrderByDescending(row => row.Count)
                .ThenBy(row => row.Unit, StringComparer.Ordinal)
                .Take(3)
                .Select(row => $"{row.Unit} {Pct(row.PickRate)}");
            builder.AppendLine($"- {group.Key.Scenario} {group.Key.BotSide} most used: {string.Join(", ", most)}");
            builder.AppendLine($"- {group.Key.Scenario} {group.Key.BotSide} least used: {string.Join(", ", least)}");
            builder.AppendLine($"- {group.Key.Scenario} {group.Key.BotSide} final-turn picks: {string.Join(", ", finalTurn)}");
        }
        var botTurnRows = result.BotPickRows
            .GroupBy(row => new { row.Scenario, row.BotSide, row.TurnNumber })
            .OrderBy(group => group.Key.Scenario)
            .ThenBy(group => group.Key.BotSide)
            .ThenBy(group => group.Key.TurnNumber);
        foreach (var group in botTurnRows)
        {
            var top = group.OrderByDescending(row => row.Count).FirstOrDefault();
            if (top != null)
            {
                builder.AppendLine($"- {group.Key.Scenario} {group.Key.BotSide} Turn {group.Key.TurnNumber}: most common {top.Unit} at {Pct(top.PickRate)}");
            }
        }
        builder.AppendLine();

        builder.AppendLine("## 6. Predictability");
        builder.AppendLine($"- Average sampled fixed-order win rate: {Pct(result.AverageSampledFixedOrderWinRate)}");
        foreach (var order in result.TopFixedOrders.Take(5))
        {
            builder.AppendLine($"- #{order.Rank}: {order.Order} | win {Pct(order.WinRate)} | draw {Pct(order.DrawRate)} | margin {Num(order.AverageScoreMargin)}");
        }
        builder.AppendLine("- Most common units in top 50: " + JoinCounts(result.TopFixedOrderUnitCounts));
        builder.AppendLine("- Most common Turn 5 units in top 50: " + JoinCounts(result.TopFixedOrderTurnFiveCounts));
        builder.AppendLine();
        builder.AppendLine("## 7. Top Order Counterability");
        foreach (var row in result.TopOrderCounterabilityRows.Take(5))
        {
            builder.AppendLine($"- #{row.Rank}: {row.TopOrder} best counter {row.BestCounterOrder} | top order score {row.TopOrderScore}-{row.BestCounterScore} | counterable {(row.AppearsCounterable ? "yes" : "no")}");
        }
        builder.AppendLine();

        builder.AppendLine("## 8. Flagged Findings");
        if (result.FlaggedFindings.Count == 0)
        {
            builder.AppendLine("- No configured warning thresholds were crossed.");
        }
        else
        {
            foreach (var finding in result.FlaggedFindings)
            {
                builder.AppendLine($"- [{finding.Severity}] {finding.Category}: {finding.Finding} ({finding.Metric}; threshold {finding.Threshold})");
            }
        }
        builder.AppendLine();

        builder.AppendLine("## 9. Suggested Next Steps");
        builder.AppendLine("- Do not auto-change balance from this report.");
        builder.AppendLine("- Review flagged units and turns manually in `unit_stats.csv`, `unit_turn_stats.csv`, and `matchup_matrix.csv`.");
        builder.AppendLine("- If bot predictability flags appear, review bot pick scoring separately from combat rules.");
        builder.AppendLine("- If Turn 5 meaningfulness is low, review scoring or late-turn profiles with a designer before changing production values.");
        return builder.ToString();
    }

    private static string BuildMatchupMatrixCsv(IEnumerable<EmojiClashExactMatchupRow> rows)
    {
        var builder = CsvHeader("playerUnit", "opponentUnit", "turnNumber", "turnValue", "outcome", "playerCombatPower", "opponentCombatPower", "readableReason");
        foreach (var row in rows)
        {
            CsvRow(builder, row.PlayerUnit, row.OpponentUnit, row.TurnNumber, row.TurnValue, row.Outcome, row.PlayerCombatPower, row.OpponentCombatPower, row.ReadableReason);
        }

        return builder.ToString();
    }

    private static string BuildUnitStatsCsv(IEnumerable<EmojiClashUnitReportRow> rows)
    {
        var builder = CsvHeader("scenario", "unit", "appearances", "picks", "wins", "losses", "draws", "pickRate", "winRate", "lossRate", "drawRate", "averageRawPointDelta", "normalizedScoreImpact");
        foreach (var row in rows.OrderBy(row => row.Scenario).ThenBy(row => row.Unit))
        {
            CsvRow(builder, row.Scenario, row.Unit, row.Appearances, row.Picks, row.Wins, row.Losses, row.Draws, Num(row.PickRate), Num(row.WinRate), Num(row.LossRate), Num(row.DrawRate), Num(row.AverageRawPointDelta), Num(row.NormalizedScoreImpact));
        }

        return builder.ToString();
    }

    private static string BuildUnitTurnStatsCsv(IEnumerable<EmojiClashUnitTurnReportRow> rows)
    {
        var builder = CsvHeader("scenario", "unit", "turnNumber", "turnValue", "appearances", "wins", "losses", "draws", "winRate", "lossRate", "drawRate", "averageRawPointDelta", "normalizedScoreImpact", "favorableMatchups", "unfavorableMatchups", "drawMatchups", "exactNormalizedMatchupScore");
        foreach (var row in rows.OrderBy(row => row.Scenario).ThenBy(row => row.Unit).ThenBy(row => row.TurnNumber))
        {
            CsvRow(builder, row.Scenario, row.Unit, row.TurnNumber, row.TurnValue, row.Appearances, row.Wins, row.Losses, row.Draws, Num(row.WinRate), Num(row.LossRate), Num(row.DrawRate), Num(row.AverageRawPointDelta), Num(row.NormalizedScoreImpact), row.FavorableMatchups, row.UnfavorableMatchups, row.DrawMatchups, Num(row.ExactNormalizedMatchupScore));
        }

        return builder.ToString();
    }

    private static string BuildTurnStatsCsv(IEnumerable<EmojiClashTurnReportRow> rows)
    {
        var builder = CsvHeader("scenario", "turnNumber", "turnValue", "playerWins", "opponentWins", "draws", "playerWinRate", "opponentWinRate", "drawRate", "averagePointSwing");
        foreach (var row in rows.OrderBy(row => row.Scenario).ThenBy(row => row.TurnNumber))
        {
            CsvRow(builder, row.Scenario, row.TurnNumber, row.TurnValue, row.PlayerWins, row.OpponentWins, row.Draws, Num(row.PlayerWinRate), Num(row.OpponentWinRate), Num(row.DrawRate), Num(row.AveragePointSwing));
        }

        return builder.ToString();
    }

    private static string BuildBotPickStatsCsv(IEnumerable<EmojiClashBotPickReportRow> rows)
    {
        var builder = CsvHeader("scenario", "botSide", "turnNumber", "unit", "count", "pickRate", "averageRawPointDelta");
        foreach (var row in rows.OrderBy(row => row.Scenario).ThenBy(row => row.BotSide).ThenBy(row => row.TurnNumber).ThenBy(row => row.Unit))
        {
            CsvRow(builder, row.Scenario, row.BotSide, row.TurnNumber, row.Unit, row.Count, Num(row.PickRate), Num(row.AverageRawPointDelta));
        }

        return builder.ToString();
    }

    private static string BuildMatchDistributionCsv(IEnumerable<EmojiClashMatchDistributionRow> rows)
    {
        var builder = CsvHeader("scenario", "playerScore", "opponentScore", "scoreMargin", "count");
        foreach (var row in rows.OrderBy(row => row.Scenario).ThenBy(row => row.PlayerScore).ThenBy(row => row.OpponentScore))
        {
            CsvRow(builder, row.Scenario, row.PlayerScore, row.OpponentScore, row.ScoreMargin, row.Count);
        }

        return builder.ToString();
    }

    private static string BuildTopFixedOrdersCsv(IEnumerable<EmojiClashFixedOrderReportRow> rows)
    {
        var builder = CsvHeader("rank", "order", "evaluatedMatches", "winRate", "drawRate", "averageScoreMargin");
        foreach (var row in rows.OrderBy(row => row.Rank))
        {
            CsvRow(builder, row.Rank, row.Order, row.EvaluatedMatches, Num(row.WinRate), Num(row.DrawRate), Num(row.AverageScoreMargin));
        }

        return builder.ToString();
    }

    private static string BuildTopOrderCounterabilityCsv(IEnumerable<EmojiClashTopOrderCounterabilityRow> rows)
    {
        var builder = CsvHeader("rank", "topOrder", "evaluatedCounterOrders", "bestCounterOrder", "topOrderScore", "bestCounterScore", "topOrderWinRateAgainstBestCounter", "topOrderDrawRateAgainstBestCounter", "scoreMarginAgainstBestCounter", "appearsCounterable");
        foreach (var row in rows.OrderBy(row => row.Rank))
        {
            CsvRow(builder, row.Rank, row.TopOrder, row.EvaluatedCounterOrders, row.BestCounterOrder, row.TopOrderScore, row.BestCounterScore, Num(row.TopOrderWinRateAgainstBestCounter), Num(row.TopOrderDrawRateAgainstBestCounter), row.ScoreMarginAgainstBestCounter, row.AppearsCounterable);
        }

        return builder.ToString();
    }

    private static string BuildFlaggedFindingsCsv(IEnumerable<EmojiClashFlaggedFinding> rows)
    {
        var builder = CsvHeader("severity", "category", "finding", "metric", "threshold");
        foreach (var row in rows)
        {
            CsvRow(builder, row.Severity, row.Category, row.Finding, row.Metric, row.Threshold);
        }

        return builder.ToString();
    }

    private static void AppendUnitList(StringBuilder builder, string label, IEnumerable<EmojiClashUnitReportRow> rows)
    {
        builder.AppendLine($"- {label}:");
        foreach (var row in rows)
        {
            builder.AppendLine($"  - {row.Unit}: impact {Num(row.NormalizedScoreImpact)}, avg points {Num(row.AverageRawPointDelta)}, appearances {row.Appearances}");
        }
    }

    private static void AppendUnitList(StringBuilder builder, string label, IEnumerable<EmojiClashUnitTurnReportRow> rows)
    {
        builder.AppendLine($"- {label}:");
        foreach (var row in rows)
        {
            builder.AppendLine($"  - {row.Unit}: impact {Num(row.NormalizedScoreImpact)}, avg points {Num(row.AverageRawPointDelta)}, appearances {row.Appearances}");
        }
    }

    private static string JoinCounts(Dictionary<string, int> counts)
    {
        if (counts == null || counts.Count == 0)
        {
            return "none";
        }

        return string.Join(", ", counts.OrderByDescending(pair => pair.Value).ThenBy(pair => pair.Key).Take(8).Select(pair => $"{pair.Key} ({pair.Value})"));
    }

    private static void WriteText(string path, string content)
    {
        File.WriteAllText(path, content ?? string.Empty, Encoding.UTF8);
    }

    private static void WriteCsv(string path, string content)
    {
        File.WriteAllText(path, content ?? string.Empty, Encoding.UTF8);
    }

    private static StringBuilder CsvHeader(params object[] values)
    {
        var builder = new StringBuilder();
        CsvRow(builder, values);
        return builder;
    }

    private static void CsvRow(StringBuilder builder, params object[] values)
    {
        builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
    }

    private static string EscapeCsv(object value)
    {
        var text = value?.ToString() ?? string.Empty;
        if (text.Contains('"'))
        {
            text = text.Replace("\"", "\"\"");
        }

        return text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? $"\"{text}\""
            : text;
    }

    public static string Num(double value)
    {
        return value.ToString("0.####", CultureInfo.InvariantCulture);
    }

    public static string Pct(double value)
    {
        return (value * 100.0).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }
}
