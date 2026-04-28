using System.Collections.Generic;
using System.IO;
using System.Linq;
using EmojiWar.Client.Gameplay.Clash;
using EmojiWar.Client.UI.Match;
using NUnit.Framework;

namespace EmojiWar.Client.Tests.EditMode
{
    public sealed class EmojiClashRulesTests
    {
        [Test]
        public void GetTurnValue_UsesLockedWeights()
        {
            Assert.That(EmojiClashRules.GetTurnValue(0), Is.EqualTo(1));
            Assert.That(EmojiClashRules.GetTurnValue(1), Is.EqualTo(1));
            Assert.That(EmojiClashRules.GetTurnValue(2), Is.EqualTo(2));
            Assert.That(EmojiClashRules.GetTurnValue(3), Is.EqualTo(2));
            Assert.That(EmojiClashRules.GetTurnValue(4), Is.EqualTo(3));
        }

        [Test]
        public void ResolveTurn_GhostWinsFinalTurnThroughLateBonuses()
        {
            var state = new EmojiClashMatchState
            {
                CurrentTurnIndex = 4
            };

            var result = EmojiClashRules.ResolveTurn(state, "ghost", "shield");

            Assert.That(result.Outcome, Is.EqualTo(EmojiClashTurnOutcome.PlayerWin));
            Assert.That(result.PlayerBreakdown.TotalPower, Is.GreaterThan(result.OpponentBreakdown.TotalPower));
            Assert.That(result.PlayerFacingReason, Does.Contain("Ghost"));
        }

        [Test]
        public void ResolveTurn_WhenBothSidesMatch_ReturnsDraw()
        {
            var state = new EmojiClashMatchState
            {
                CurrentTurnIndex = 0
            };

            var result = EmojiClashRules.ResolveTurn(state, "fire", "fire");

            Assert.That(result.Outcome, Is.EqualTo(EmojiClashTurnOutcome.Draw));
            Assert.That(result.PlayerBreakdown.TotalPower, Is.EqualTo(result.OpponentBreakdown.TotalPower));
        }

        [Test]
        public void ResolveTurn_BehindBonusBoostsHeart()
        {
            var evenState = new EmojiClashMatchState
            {
                CurrentTurnIndex = 2,
                PlayerScore = 2,
                OpponentScore = 2
            };
            var behindState = new EmojiClashMatchState
            {
                CurrentTurnIndex = 2,
                PlayerScore = 0,
                OpponentScore = 2
            };

            var evenResult = EmojiClashRules.ResolveTurn(evenState, "heart", "chain");
            var behindResult = EmojiClashRules.ResolveTurn(behindState, "heart", "chain");

            Assert.That(behindResult.PlayerBreakdown.TotalPower - evenResult.PlayerBreakdown.TotalPower, Is.EqualTo(3));
            Assert.That(behindResult.PlayerFacingReason, Does.Contain("Heart"));
        }
    }

    public sealed class EmojiClashBotAndControllerTests
    {
        [Test]
        public void BotOpponent_UsesSeededFinalTurnVariety()
        {
            var bot = new EmojiClashBotOpponent();
            var picks = Enumerable.Range(0, 64)
                .Select(seed => bot.PickUnit(
                    new EmojiClashMatchState
                    {
                        CurrentTurnIndex = 4,
                        MatchSeed = seed
                    },
                    EmojiClashRules.LaunchRoster))
                .Distinct()
                .ToArray();

            Assert.That(picks, Has.Length.GreaterThan(1));
            Assert.That(picks, Contains.Item("ghost"));
        }

        [Test]
        public void BotOpponent_UsesComebackPickWhenBehind()
        {
            var bot = new EmojiClashBotOpponent();
            var state = new EmojiClashMatchState
            {
                CurrentTurnIndex = 3,
                MatchSeed = 42,
                PlayerScore = 4,
                OpponentScore = 0
            };

            var pick = bot.PickUnit(state, new[] { "heart", "shield", "wind" });

            Assert.That(pick, Is.EqualTo("heart"));
        }

        [Test]
        public void Controller_TracksIndependentRosters()
        {
            var controller = new EmojiClashController(new SequenceOpponent("fire"), 7);
            controller.StartEmojiClash();

            Assert.That(controller.HandlePlayerPick("water"), Is.True);
            var record = controller.ResolveLockedTurn();

            Assert.That(record.PlayerUnitKey, Is.EqualTo("water"));
            Assert.That(record.OpponentUnitKey, Is.EqualTo("fire"));
            Assert.That(controller.GetAvailablePlayerUnitKeys(), Contains.Item("fire"));
            Assert.That(controller.GetAvailableOpponentUnitKeys(), Contains.Item("water"));
            Assert.That(controller.GetAvailablePlayerUnitKeys(), Does.Not.Contain("water"));
            Assert.That(controller.GetAvailableOpponentUnitKeys(), Does.Not.Contain("fire"));
        }

        [Test]
        public void Controller_CompletesFiveTurnsAndResetsWithPlayAgain()
        {
            var controller = new EmojiClashController(
                new SequenceOpponent("fire", "water", "lightning", "ice", "magnet"),
                99);
            controller.StartEmojiClash();

            var playerPicks = new[] { "ghost", "heart", "shield", "chain", "bomb" };
            foreach (var pick in playerPicks)
            {
                Assert.That(controller.HandlePlayerPick(pick), Is.True);
                Assert.That(controller.ResolveLockedTurn(), Is.Not.Null);
                if (!controller.IsMatchComplete)
                {
                    Assert.That(controller.AdvanceToNextTurn(), Is.True);
                }
            }

            Assert.That(controller.IsMatchComplete, Is.True);
            Assert.That(controller.State.TurnHistory, Has.Count.EqualTo(5));
            Assert.That(controller.BuildResultViewModel().TurnLines, Has.Length.EqualTo(5));

            controller.StartEmojiClash();

            Assert.That(controller.IsMatchComplete, Is.False);
            Assert.That(controller.State.PlayerScore, Is.Zero);
            Assert.That(controller.State.OpponentScore, Is.Zero);
            Assert.That(controller.State.TurnHistory, Is.Empty);
            Assert.That(controller.GetAvailablePlayerUnitKeys(), Has.Count.EqualTo(16));
        }

        [Test]
        public void BotPick_IsDeterministicForSameState()
        {
            var bot = new EmojiClashBotOpponent();
            var state = new EmojiClashMatchState
            {
                CurrentTurnIndex = 1,
                MatchSeed = 1234,
                PlayerScore = 1,
                OpponentScore = 0,
                PlayerUsedUnitKeys = new HashSet<string> { "ghost", "heart" },
                OpponentUsedUnitKeys = new HashSet<string> { "fire" }
            };
            var available = new[] { "water", "shield", "wind", "soap" };

            var firstPick = bot.PickUnit(state, available);
            var secondPick = bot.PickUnit(state, available);

            Assert.That(firstPick, Is.EqualTo(secondPick));
            Assert.That(available, Contains.Item(firstPick));
            Assert.That(state.OpponentUsedUnitKeys, Does.Not.Contain(firstPick));
        }

        [Test]
        public void BalanceSimulation_TinyRun_GeneratesReportsWithoutChangingRules()
        {
            var beforeTurnValues = Enumerable.Range(0, EmojiClashRules.TotalTurns)
                .Select(EmojiClashRules.GetTurnValue)
                .ToArray();
            var outputRoot = Path.Combine(Path.GetTempPath(), $"emoji-clash-balance-test-{System.Guid.NewGuid():N}");
            var config = new EmojiClashBalanceSimulationConfig
            {
                Seed = 12345,
                RandomMatchCount = 100,
                BotMatchCount = 100,
                FixedOrderSampleCount = 12,
                FixedOrderOpponentSamples = 5,
                FixedOrderCounterSamples = 12,
                OutputRoot = outputRoot
            };

            try
            {
                var result = EmojiClashBalanceSimulationRunner.RunSimulation(config);

                Assert.That(result.OutputDirectory, Is.Not.Empty);
                Assert.That(File.Exists(Path.Combine(result.OutputDirectory, "balance_summary.md")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.OutputDirectory, "matchup_matrix.csv")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.OutputDirectory, "unit_stats.csv")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.OutputDirectory, "unit_turn_stats.csv")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.OutputDirectory, "turn_stats.csv")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.OutputDirectory, "bot_pick_stats.csv")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.OutputDirectory, "match_distribution.csv")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.OutputDirectory, "top_fixed_orders.csv")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.OutputDirectory, "top_order_counterability.csv")), Is.True);
                Assert.That(File.Exists(Path.Combine(result.OutputDirectory, "flagged_findings.csv")), Is.True);
                Assert.That(result.ScenarioSummaries.Any(row => row.Scenario == "RandomVsRandom"), Is.True);
                Assert.That(result.MatchupRows, Has.Count.EqualTo(16 * 16 * 5));
            }
            finally
            {
                if (Directory.Exists(outputRoot))
                {
                    Directory.Delete(outputRoot, recursive: true);
                }
            }

            var afterTurnValues = Enumerable.Range(0, EmojiClashRules.TotalTurns)
                .Select(EmojiClashRules.GetTurnValue)
                .ToArray();
            Assert.That(afterTurnValues, Is.EqualTo(beforeTurnValues));
            Assert.That(afterTurnValues, Is.EqualTo(new[] { 1, 1, 2, 2, 3 }));
        }

        private sealed class SequenceOpponent : IEmojiClashOpponent
        {
            private readonly Queue<string> unitKeys;

            public SequenceOpponent(params string[] unitKeys)
            {
                this.unitKeys = new Queue<string>(unitKeys);
            }

            public string PickUnit(EmojiClashMatchState state, IReadOnlyList<string> availableUnitKeys)
            {
                while (unitKeys.Count > 0)
                {
                    var next = unitKeys.Dequeue();
                    if (availableUnitKeys.Contains(next))
                    {
                        return next;
                    }
                }

                return availableUnitKeys.FirstOrDefault() ?? string.Empty;
            }
        }
    }
}
