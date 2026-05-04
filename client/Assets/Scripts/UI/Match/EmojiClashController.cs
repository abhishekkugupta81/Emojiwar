using System;
using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Gameplay.Clash;
using EmojiWar.Client.UI.Common;
using UnityEngine;

namespace EmojiWar.Client.UI.Match
{
    public sealed class EmojiClashController
    {
        private const int MaxScore = 9;

        private readonly IEmojiClashOpponent opponent;
        private readonly int? fixedSeed;
        private bool currentTurnResolved;
        private bool matchCompletionTelemetryRecorded;

        public EmojiClashController(IEmojiClashOpponent opponent = null, int? seed = null)
        {
            this.opponent = opponent ?? new EmojiClashBotOpponent();
            fixedSeed = seed;
            State = new EmojiClashMatchState();
        }

        public EmojiClashMatchState State { get; private set; }

        public bool IsMatchComplete => State.TurnHistory.Count >= EmojiClashRules.TotalTurns;

        public void StartEmojiClash(bool startedFromPlayAgain = false)
        {
            State = new EmojiClashMatchState
            {
                CurrentTurnIndex = 0,
                MatchSeed = fixedSeed ?? GenerateFreshMatchSeed(),
                MatchId = Guid.NewGuid().ToString("N"),
                StartedFromPlayAgain = startedFromPlayAgain
            };
            currentTurnResolved = false;
            matchCompletionTelemetryRecorded = false;
        }

        public static int GenerateFreshMatchSeed()
        {
            unchecked
            {
                var bytes = Guid.NewGuid().ToByteArray();
                var hash = Environment.TickCount;
                hash = (hash * 397) ^ (int)DateTime.UtcNow.Ticks;
                for (var index = 0; index < bytes.Length; index += sizeof(int))
                {
                    hash = (hash * 397) ^ BitConverter.ToInt32(bytes, index);
                }

                return hash == 0 ? 1 : hash;
            }
        }

        public IReadOnlyList<string> GetAvailablePlayerUnitKeys()
        {
            return EmojiClashRules.LaunchRoster
                .Where(unitKey => !State.PlayerUsedUnitKeys.Contains(unitKey))
                .ToArray();
        }

        public IReadOnlyList<string> GetAvailableOpponentUnitKeys()
        {
            return EmojiClashRules.LaunchRoster
                .Where(unitKey => !State.OpponentUsedUnitKeys.Contains(unitKey))
                .ToArray();
        }

        public bool HandlePlayerPick(string unitKey)
        {
            if (IsMatchComplete || currentTurnResolved)
            {
                return false;
            }

            var normalizedKey = EmojiClashRules.NormalizeUnitKey(unitKey);
            if (string.IsNullOrWhiteSpace(normalizedKey) ||
                State.PlayerUsedUnitKeys.Contains(normalizedKey) ||
                !EmojiClashRules.LaunchRoster.Contains(normalizedKey, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            State.PendingPlayerPick = normalizedKey;
            var botVisibleState = CreateBotVisibleState();
            AssertBotStateDoesNotExposeHiddenPick(botVisibleState);
            State.PendingOpponentPick = opponent.PickUnit(botVisibleState, GetAvailableOpponentUnitKeys());
            if (!string.IsNullOrWhiteSpace(State.PendingOpponentPick))
            {
                EmojiClashPickTelemetry.RecordLocalPickPair(botVisibleState, normalizedKey, State.PendingOpponentPick);
                return true;
            }

            State.PendingPlayerPick = string.Empty;
            return false;
        }

        private EmojiClashMatchState CreateBotVisibleState()
        {
            var visibleState = new EmojiClashMatchState
            {
                CurrentTurnIndex = State.CurrentTurnIndex,
                PlayerScore = State.PlayerScore,
                OpponentScore = State.OpponentScore,
                MatchSeed = State.MatchSeed,
                MatchId = State.MatchId,
                StartedFromPlayAgain = State.StartedFromPlayAgain
            };

            foreach (var unitKey in State.PlayerUsedUnitKeys)
            {
                visibleState.PlayerUsedUnitKeys.Add(unitKey);
            }

            foreach (var unitKey in State.OpponentUsedUnitKeys)
            {
                visibleState.OpponentUsedUnitKeys.Add(unitKey);
            }

            foreach (var record in State.TurnHistory)
            {
                visibleState.TurnHistory.Add(new EmojiClashTurnRecord
                {
                    TurnNumber = record.TurnNumber,
                    TurnValue = record.TurnValue,
                    PlayerUnitKey = record.PlayerUnitKey,
                    OpponentUnitKey = record.OpponentUnitKey,
                    PlayerCombatPower = record.PlayerCombatPower,
                    OpponentCombatPower = record.OpponentCombatPower,
                    Outcome = record.Outcome,
                    PlayerScoreAfter = record.PlayerScoreAfter,
                    OpponentScoreAfter = record.OpponentScoreAfter,
                    PlayerFacingReason = record.PlayerFacingReason
                });
            }

            return visibleState;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void AssertBotStateDoesNotExposeHiddenPick(EmojiClashMatchState botState)
        {
            if (botState == null)
            {
                Debug.LogWarning("Emoji Clash bot selection was invoked without a visible-state snapshot.");
                return;
            }

            if (!string.IsNullOrWhiteSpace(botState.PendingPlayerPick) ||
                !string.IsNullOrWhiteSpace(botState.PendingOpponentPick))
            {
                Debug.LogWarning("Emoji Clash bot selection received unresolved pending pick state. The bot must only receive visible match state.");
            }
        }

        public EmojiClashTurnRecord ResolveLockedTurn()
        {
            if (currentTurnResolved)
            {
                return State.TurnHistory.LastOrDefault();
            }

            if (string.IsNullOrWhiteSpace(State.PendingPlayerPick) || string.IsNullOrWhiteSpace(State.PendingOpponentPick))
            {
                throw new InvalidOperationException("Both picks must be locked before resolving an Emoji Clash turn.");
            }

            var resolvedTurn = EmojiClashRules.ResolveTurn(State, State.PendingPlayerPick, State.PendingOpponentPick);
            ApplyResolvedTurn(resolvedTurn);
            currentTurnResolved = true;
            return State.TurnHistory[^1];
        }

        public bool AdvanceToNextTurn()
        {
            if (!currentTurnResolved || IsMatchComplete)
            {
                return false;
            }

            State.CurrentTurnIndex++;
            currentTurnResolved = false;
            return true;
        }

        public EmojiClashTurnViewModel BuildTurnViewModel()
        {
            var turnNumber = ResolveDisplayedTurnNumber();
            var turnValue = ResolveDisplayedTurnValue();
            var playerPickKey = ResolveDisplayedPlayerPick();
            var opponentPickKey = ResolveDisplayedOpponentPick();
            var lastRecord = State.TurnHistory.LastOrDefault();
            var boardItems = EmojiClashRules.LaunchRoster
                .Select(unitKey =>
                {
                    var profile = EmojiClashRules.GetUnitProfile(unitKey);
                    return new EmojiClashBoardItemViewModel
                    {
                        UnitKey = unitKey,
                        DisplayName = profile.DisplayName,
                        Role = profile.Role.ToUpperInvariant(),
                        CardColor = Color.Lerp(UnitIconLibrary.GetPrimaryColor(unitKey), RescueStickerFactory.Palette.InkPurple, 0.16f),
                        AuraColor = Color.Lerp(UnitIconLibrary.GetPrimaryColor(unitKey), UnitIconLibrary.GetSecondaryColor(unitKey), 0.32f),
                        IsAvailable = !State.PlayerUsedUnitKeys.Contains(unitKey) && !currentTurnResolved && string.IsNullOrWhiteSpace(State.PendingPlayerPick),
                        IsSelected = string.Equals(playerPickKey, unitKey, StringComparison.OrdinalIgnoreCase),
                        IsUsed = State.PlayerUsedUnitKeys.Contains(unitKey)
                    };
                })
                .ToArray();

            return new EmojiClashTurnViewModel
            {
                TurnNumber = turnNumber,
                TotalTurns = EmojiClashRules.TotalTurns,
                TurnValue = turnValue,
                WeightedTurnValues = new[] { 1, 1, 2, 2, 3 },
                PlayerScore = State.PlayerScore,
                OpponentScore = State.OpponentScore,
                ScoreSummary = $"You {State.PlayerScore} - {State.OpponentScore} Rival",
                MomentumNormalized = Mathf.Clamp((State.PlayerScore - State.OpponentScore) / (float)MaxScore, -1f, 1f),
                PlayerPickKey = playerPickKey,
                OpponentPickKey = opponentPickKey,
                ShowOpponentMystery = !currentTurnResolved,
                IsResolved = currentTurnResolved,
                IsLocked = !currentTurnResolved && !string.IsNullOrWhiteSpace(State.PendingPlayerPick),
                OutcomeTitle = BuildTurnOutcomeTitle(lastRecord),
                ReasonText = BuildTurnReasonText(lastRecord),
                PrimaryActionLabel = string.Empty,
                BoardItems = boardItems
            };
        }

        public EmojiClashResultViewModel BuildResultViewModel()
        {
            var outcomeTitle = State.PlayerScore > State.OpponentScore
                ? "VICTORY"
                : State.PlayerScore < State.OpponentScore
                    ? "DEFEAT"
                    : "DRAW";

            return new EmojiClashResultViewModel
            {
                OutcomeTitle = outcomeTitle,
                FinalScoreLine = $"Final Score: You {State.PlayerScore} - {State.OpponentScore} Rival",
                RecapLines = BuildRecapLines(),
                TurnLines = State.TurnHistory.Select(EmojiClashRules.BuildTurnSummary).ToArray(),
                IsDraw = State.PlayerScore == State.OpponentScore
            };
        }

        private void ApplyResolvedTurn(EmojiClashResolvedTurn resolvedTurn)
        {
            State.PlayerUsedUnitKeys.Add(resolvedTurn.PlayerUnitKey);
            State.OpponentUsedUnitKeys.Add(resolvedTurn.OpponentUnitKey);

            if (resolvedTurn.Outcome == EmojiClashTurnOutcome.PlayerWin)
            {
                State.PlayerScore += resolvedTurn.TurnValue;
            }
            else if (resolvedTurn.Outcome == EmojiClashTurnOutcome.OpponentWin)
            {
                State.OpponentScore += resolvedTurn.TurnValue;
            }

            State.TurnHistory.Add(new EmojiClashTurnRecord
            {
                TurnNumber = resolvedTurn.TurnNumber,
                TurnValue = resolvedTurn.TurnValue,
                PlayerUnitKey = resolvedTurn.PlayerUnitKey,
                OpponentUnitKey = resolvedTurn.OpponentUnitKey,
                PlayerCombatPower = resolvedTurn.PlayerBreakdown.TotalPower,
                OpponentCombatPower = resolvedTurn.OpponentBreakdown.TotalPower,
                Outcome = resolvedTurn.Outcome,
                PlayerScoreAfter = State.PlayerScore,
                OpponentScoreAfter = State.OpponentScore,
                PlayerFacingReason = resolvedTurn.PlayerFacingReason
            });

            State.PendingPlayerPick = string.Empty;
            State.PendingOpponentPick = string.Empty;

            if (resolvedTurn.TurnNumber >= EmojiClashRules.TotalTurns)
            {
                State.CurrentTurnIndex = EmojiClashRules.TotalTurns;
                if (!matchCompletionTelemetryRecorded)
                {
                    EmojiClashPickTelemetry.RecordMatchCompleted(State);
                    matchCompletionTelemetryRecorded = true;
                }
            }
        }

        private int ResolveDisplayedTurnNumber()
        {
            if (currentTurnResolved && State.TurnHistory.Count > 0)
            {
                return State.TurnHistory[^1].TurnNumber;
            }

            return Mathf.Clamp(State.CurrentTurnIndex, 0, EmojiClashRules.TotalTurns - 1) + 1;
        }

        private int ResolveDisplayedTurnValue()
        {
            if (currentTurnResolved && State.TurnHistory.Count > 0)
            {
                return State.TurnHistory[^1].TurnValue;
            }

            return EmojiClashRules.GetTurnValue(Mathf.Clamp(State.CurrentTurnIndex, 0, EmojiClashRules.TotalTurns - 1));
        }

        private string ResolveDisplayedPlayerPick()
        {
            if (currentTurnResolved && State.TurnHistory.Count > 0)
            {
                return State.TurnHistory[^1].PlayerUnitKey;
            }

            return State.PendingPlayerPick;
        }

        private string ResolveDisplayedOpponentPick()
        {
            if (currentTurnResolved && State.TurnHistory.Count > 0)
            {
                return State.TurnHistory[^1].OpponentUnitKey;
            }

            return State.PendingOpponentPick;
        }

        private string BuildTurnOutcomeTitle(EmojiClashTurnRecord record)
        {
            if (record == null || !currentTurnResolved)
            {
                return string.Empty;
            }

            return record.Outcome switch
            {
                EmojiClashTurnOutcome.PlayerWin => $"YOU WIN +{record.TurnValue}",
                EmojiClashTurnOutcome.OpponentWin => $"RIVAL WINS +{record.TurnValue}",
                _ => "CLASH DRAW"
            };
        }

        private string BuildTurnReasonText(EmojiClashTurnRecord record)
        {
            if (currentTurnResolved)
            {
                return record?.PlayerFacingReason ?? string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(State.PendingPlayerPick))
            {
                return "Locked in. Rival reveal incoming.";
            }

            return "Tap a sticker or drag it into the clash slot.";
        }

        private string[] BuildRecapLines()
        {
            var lines = new List<string>();
            var finalTurn = State.TurnHistory.LastOrDefault();
            if (finalTurn != null)
            {
                lines.Add(finalTurn.Outcome == EmojiClashTurnOutcome.Draw
                    ? "The final +3 clash ended in a draw."
                    : $"Turn 5 swung on {EmojiClashRules.ToDisplayName(finalTurn.Outcome == EmojiClashTurnOutcome.PlayerWin ? finalTurn.PlayerUnitKey : finalTurn.OpponentUnitKey)}.");
            }

            var biggestTurn = State.TurnHistory
                .OrderByDescending(record => record.TurnValue)
                .ThenByDescending(record => record.Outcome == EmojiClashTurnOutcome.PlayerWin ? 1 : 0)
                .FirstOrDefault(record => record.Outcome != EmojiClashTurnOutcome.Draw);
            if (biggestTurn != null)
            {
                lines.Add(biggestTurn.PlayerFacingReason);
            }

            var behindSwing = FindComebackMoment();
            if (!string.IsNullOrWhiteSpace(behindSwing))
            {
                lines.Add(behindSwing);
            }

            return lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.Ordinal)
                .Take(3)
                .ToArray();
        }

        private string FindComebackMoment()
        {
            var playerScoreBefore = 0;
            var opponentScoreBefore = 0;
            foreach (var record in State.TurnHistory)
            {
                if (playerScoreBefore < opponentScoreBefore && record.Outcome == EmojiClashTurnOutcome.PlayerWin)
                {
                    return $"{EmojiClashRules.ToDisplayName(record.PlayerUnitKey)} helped you recover while behind.";
                }

                playerScoreBefore = record.PlayerScoreAfter;
                opponentScoreBefore = record.OpponentScoreAfter;
            }

            return string.Empty;
        }
    }

    public sealed class EmojiClashTurnViewModel
    {
        public int TurnNumber;
        public int TotalTurns;
        public int TurnValue;
        public int[] WeightedTurnValues = Array.Empty<int>();
        public int PlayerScore;
        public int OpponentScore;
        public string ScoreSummary = string.Empty;
        public float MomentumNormalized;
        public string PlayerPickKey = string.Empty;
        public string OpponentPickKey = string.Empty;
        public bool ShowOpponentMystery = true;
        public bool IsResolved;
        public bool IsLocked;
        public string OutcomeTitle = string.Empty;
        public string ReasonText = string.Empty;
        public string PrimaryActionLabel = string.Empty;
        public EmojiClashBoardItemViewModel[] BoardItems = Array.Empty<EmojiClashBoardItemViewModel>();
    }

    public sealed class EmojiClashBoardItemViewModel
    {
        public string UnitKey = string.Empty;
        public string DisplayName = string.Empty;
        public string Role = string.Empty;
        public Color CardColor;
        public Color AuraColor;
        public bool IsAvailable;
        public bool IsSelected;
        public bool IsUsed;
    }

    public sealed class EmojiClashResultViewModel
    {
        public string OutcomeTitle = string.Empty;
        public string FinalScoreLine = string.Empty;
        public string[] RecapLines = Array.Empty<string>();
        public string[] TurnLines = Array.Empty<string>();
        public bool IsDraw;
        public string PrimaryActionLabel = "PLAY AGAIN";
    }
}
