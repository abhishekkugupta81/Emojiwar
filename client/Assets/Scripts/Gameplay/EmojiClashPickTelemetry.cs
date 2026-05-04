using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EmojiWar.Client.Gameplay.Clash
{
    public static class EmojiClashPickTelemetry
    {
        public const string FileName = "emoji_clash_pick_pairs.csv";
        public const string MatchSequenceFileName = "emoji_clash_live_match_sequences.csv";
        public const string AccumulatedHistorySessionId = "accumulated-local-history";

        private static string activeSessionId = AccumulatedHistorySessionId;
        private static bool recordingEnabled = true;

        public static string ActiveSessionId => activeSessionId;

        public static void BeginSession(string sessionId)
        {
            activeSessionId = string.IsNullOrWhiteSpace(sessionId)
                ? AccumulatedHistorySessionId
                : sessionId.Trim();
        }

        public static void SetRecordingEnabled(bool enabled)
        {
            recordingEnabled = enabled;
        }

        public static void ClearLocalTelemetry()
        {
            DeleteIfExists(ResolveTelemetryPath());
            DeleteIfExists(ResolveMatchSequenceTelemetryPath());
        }

        public static void RecordLocalPickPair(EmojiClashMatchState visibleState, string playerPick, string botPick)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (visibleState == null ||
                string.IsNullOrWhiteSpace(playerPick) ||
                string.IsNullOrWhiteSpace(botPick) ||
                !recordingEnabled)
            {
                return;
            }

            try
            {
                var path = ResolveTelemetryPath();
                var exists = File.Exists(path);
                using var writer = new StreamWriter(path, append: true, Encoding.UTF8);
                if (!exists)
                {
                    writer.WriteLine("timestampUtc,sessionId,matchId,turnNumber,playerPick,botPick,visibleStateFingerprint,matchSeed,botPolicyVersion,botPolicySeedSalt,buildVersion");
                }

                writer.WriteLine(string.Join(",", new[]
                {
                    EscapeCsv(DateTimeOffset.UtcNow.ToString("O")),
                    EscapeCsv(activeSessionId),
                    EscapeCsv(ResolveMatchId(visibleState)),
                    EscapeCsv((Math.Clamp(visibleState.CurrentTurnIndex, 0, EmojiClashRules.TotalTurns - 1) + 1).ToString()),
                    EscapeCsv(EmojiClashRules.NormalizeUnitKey(playerPick)),
                    EscapeCsv(EmojiClashRules.NormalizeUnitKey(botPick)),
                    EscapeCsv(BuildVisibleStateFingerprint(visibleState)),
                    EscapeCsv(visibleState.MatchSeed.ToString()),
                    EscapeCsv(EmojiClashBotOpponent.BotPolicyVersion),
                    EscapeCsv(EmojiClashBotOpponent.BotPolicySeedSalt),
                    EscapeCsv(Application.version)
                }));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Emoji Clash pick telemetry write failed: {exception.Message}");
            }
#endif
        }

        public static void RecordMatchCompleted(EmojiClashMatchState state)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (state == null || state.TurnHistory.Count == 0 || !recordingEnabled)
            {
                return;
            }

            try
            {
                var path = ResolveMatchSequenceTelemetryPath();
                var exists = File.Exists(path);
                using var writer = new StreamWriter(path, append: true, Encoding.UTF8);
                if (!exists)
                {
                    writer.WriteLine("timestampUtc,sessionId,matchId,matchSeed,playAgain,botPolicyVersion,botPolicySeedSalt,playerSequence,botSequence,finalResult,playerScore,botScore,buildVersion");
                }

                writer.WriteLine(string.Join(",", new[]
                {
                    EscapeCsv(DateTimeOffset.UtcNow.ToString("O")),
                    EscapeCsv(activeSessionId),
                    EscapeCsv(ResolveMatchId(state)),
                    EscapeCsv(state.MatchSeed.ToString()),
                    EscapeCsv(state.StartedFromPlayAgain ? "true" : "false"),
                    EscapeCsv(EmojiClashBotOpponent.BotPolicyVersion),
                    EscapeCsv(EmojiClashBotOpponent.BotPolicySeedSalt),
                    EscapeCsv(string.Join(">", state.TurnHistory.Select(record => EmojiClashRules.NormalizeUnitKey(record.PlayerUnitKey)))),
                    EscapeCsv(string.Join(">", state.TurnHistory.Select(record => EmojiClashRules.NormalizeUnitKey(record.OpponentUnitKey)))),
                    EscapeCsv(ResolveFinalResult(state)),
                    EscapeCsv(state.PlayerScore.ToString()),
                    EscapeCsv(state.OpponentScore.ToString()),
                    EscapeCsv(Application.version)
                }));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Emoji Clash match sequence telemetry write failed: {exception.Message}");
            }
#endif
        }

        public static string ResolveTelemetryPath()
        {
            return Path.Combine(Application.persistentDataPath, FileName);
        }

        public static string ResolveMatchSequenceTelemetryPath()
        {
            return Path.Combine(Application.persistentDataPath, MatchSequenceFileName);
        }

        public static string BuildVisibleStateFingerprint(EmojiClashMatchState state)
        {
            if (state == null)
            {
                return string.Empty;
            }

            unchecked
            {
                var hash = state.MatchSeed;
                hash = (hash * 397) ^ state.CurrentTurnIndex;
                hash = (hash * 397) ^ state.PlayerScore;
                hash = (hash * 397) ^ (state.OpponentScore << 8);
                foreach (var unitKey in state.PlayerUsedUnitKeys.OrderBy(unitKey => unitKey, StringComparer.OrdinalIgnoreCase))
                {
                    hash = MixString(hash, unitKey);
                }

                hash = (hash * 397) ^ 0x5A17;
                foreach (var unitKey in state.OpponentUsedUnitKeys.OrderBy(unitKey => unitKey, StringComparer.OrdinalIgnoreCase))
                {
                    hash = MixString(hash, unitKey);
                }

                foreach (var record in state.TurnHistory)
                {
                    hash = (hash * 397) ^ record.TurnNumber;
                    hash = MixString(hash, record.PlayerUnitKey);
                    hash = MixString(hash, record.OpponentUnitKey);
                    hash = (hash * 397) ^ record.PlayerScoreAfter;
                    hash = (hash * 397) ^ (record.OpponentScoreAfter << 8);
                }

                return hash.ToString("X8");
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

        private static string ResolveMatchId(EmojiClashMatchState state)
        {
            return string.IsNullOrWhiteSpace(state.MatchId)
                ? state.MatchSeed.ToString()
                : state.MatchId;
        }

        private static string ResolveFinalResult(EmojiClashMatchState state)
        {
            if (state.PlayerScore > state.OpponentScore)
            {
                return "player";
            }

            return state.OpponentScore > state.PlayerScore ? "bot" : "draw";
        }

        private static void DeleteIfExists(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static string EscapeCsv(string value)
        {
            var text = value ?? string.Empty;
            if (text.Contains('"'))
            {
                text = text.Replace("\"", "\"\"");
            }

            return text.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? $"\"{text}\"" : text;
        }
    }
}
