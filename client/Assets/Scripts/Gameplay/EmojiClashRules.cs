using System;
using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Content;

namespace EmojiWar.Client.Gameplay.Clash
{
    public static class EmojiClashRules
    {
        public const int TotalTurns = 5;
        private static readonly int[] TurnValues = { 1, 1, 2, 2, 3 };

        private static readonly Dictionary<string, EmojiClashProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
        {
            ["fire"] = CreateProfile("fire", "Tempo", 5, 2, 0, 0, 0, new[] { "aggressive", "flame", "projectile", "tempo" }, new[] { "nature", "frozen", "passive", "cleanse" }),
            ["water"] = CreateProfile("water", "Control", 5, 0, 1, 0, 0, new[] { "control", "liquid", "cleanse" }, new[] { "flame", "explosive", "tech" }),
            ["lightning"] = CreateProfile("lightning", "Tempo", 5, 2, 0, 0, 0, new[] { "aggressive", "electric", "projectile", "tempo" }, new[] { "liquid", "flexible" }),
            ["ice"] = CreateProfile("ice", "Control", 5, 0, 0, 0, 0, new[] { "control", "frozen", "defense" }, new[] { "aggressive", "explosive", "elusive" }),
            ["magnet"] = CreateProfile("magnet", "Disrupt", 5, 0, 0, 0, 0, new[] { "disrupt", "tech", "control" }, new[] { "projectile", "electric", "guard" }),
            ["bomb"] = CreateProfile("bomb", "Burst", 6, 1, 0, 0, 0, new[] { "aggressive", "explosive", "projectile" }, new[] { "scaling", "guard", "reactive" }),
            ["mirror"] = CreateProfile("mirror", "Trick", 5, 0, 0, 1, 0, new[] { "reactive", "trick", "flexible" }, new[] { "tempo", "elusive" }),
            ["hole"] = CreateProfile("hole", "Trap", 5, 0, 1, 0, 0, new[] { "disrupt", "void", "trap" }, new[] { "projectile", "tech", "electric" }),
            ["shield"] = CreateProfile("shield", "Defense", 5, 0, 0, 0, 1, new[] { "defense", "guard", "safe" }, new[] { "aggressive", "explosive", "poison" }),
            ["snake"] = CreateProfile("snake", "Pressure", 5, 1, 1, 0, 0, new[] { "poison", "trick", "scaling" }, new[] { "support", "passive", "nature", "reactive" }),
            ["soap"] = CreateProfile("soap", "Cleanse", 5, 0, 0, 0, 1, new[] { "cleanse", "liquid", "control" }, new[] { "poison", "flame", "tech" }),
            ["plant"] = CreateProfile("plant", "Scaling", 5, 0, 1, 1, 0, new[] { "scaling", "nature", "passive" }, new[] { "liquid", "guard", "void" }),
            ["wind"] = CreateProfile("wind", "Flex", 5, 1, 0, 0, 0, new[] { "flexible", "tempo", "control" }, new[] { "explosive", "disrupt", "void" }),
            ["heart"] = CreateProfile("heart", "Comeback", 4, 0, 1, 1, 3, new[] { "support", "comeback", "flexible" }, new[] { "aggressive", "tempo", "late", "elusive" }),
            ["ghost"] = CreateProfile("ghost", "Late", 5, 0, 1, 1, 0, new[] { "late", "elusive", "trick" }, new[] { "guard", "passive" }),
            ["chain"] = CreateProfile("chain", "Bind", 5, 0, 0, 0, 1, new[] { "defense", "bind", "disrupt" }, new[] { "explosive", "poison" })
        };

        private static readonly Dictionary<string, Dictionary<string, int>> HeadToHeadBonuses = new(StringComparer.OrdinalIgnoreCase)
        {
            ["water"] = BuildBonusMap(("fire", 2), ("bomb", 2), ("magnet", 1)),
            ["fire"] = BuildBonusMap(("plant", 2), ("ice", 1), ("snake", 1)),
            ["lightning"] = BuildBonusMap(("water", 2), ("wind", 1), ("soap", 1)),
            ["ice"] = BuildBonusMap(("bomb", 2), ("lightning", 1), ("ghost", 1)),
            ["magnet"] = BuildBonusMap(("lightning", 2), ("fire", 1), ("bomb", 1), ("shield", 1), ("mirror", 1), ("chain", 1)),
            ["bomb"] = BuildBonusMap(("plant", 2), ("shield", 1), ("mirror", 1), ("chain", 1)),
            ["mirror"] = BuildBonusMap(("lightning", 1), ("ghost", 2), ("bomb", 1), ("snake", 1), ("chain", 1)),
            ["hole"] = BuildBonusMap(("lightning", 2), ("magnet", 2), ("bomb", 1), ("mirror", 1)),
            ["shield"] = BuildBonusMap(("fire", 2), ("bomb", 1), ("snake", 2), ("lightning", 1)),
            ["snake"] = BuildBonusMap(("plant", 2), ("heart", 2), ("water", 1), ("mirror", 1)),
            ["soap"] = BuildBonusMap(("snake", 2), ("fire", 1), ("magnet", 2), ("ghost", 1)),
            ["plant"] = BuildBonusMap(("water", 2), ("shield", 1), ("hole", 2)),
            ["wind"] = BuildBonusMap(("bomb", 2), ("magnet", 2), ("hole", 2), ("ghost", 2)),
            ["heart"] = BuildBonusMap(("ghost", 3), ("lightning", 1), ("bomb", 1), ("fire", 1), ("ice", 1)),
            ["ghost"] = BuildBonusMap(("shield", 2), ("plant", 1), ("chain", 1)),
            ["chain"] = BuildBonusMap(("bomb", 2), ("fire", 1), ("wind", 1), ("snake", 1))
        };

        private static readonly Dictionary<string, string> TimingReasonText = new(StringComparer.OrdinalIgnoreCase)
        {
            ["early"] = "hits hard early.",
            ["late"] = "spikes late in the match.",
            ["final"] = "peaks on the final turn.",
            ["behind"] = "gets stronger while behind."
        };

        public static IReadOnlyList<string> LaunchRoster => EmojiIdUtility.LaunchRoster.Select(EmojiIdUtility.ToApiId).ToArray();

        public static int GetTurnValue(int turnIndex)
        {
            if (turnIndex < 0 || turnIndex >= TurnValues.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(turnIndex), turnIndex, "Emoji Clash has exactly five turns.");
            }

            return TurnValues[turnIndex];
        }

        public static EmojiClashProfile GetUnitProfile(string unitKey)
        {
            var normalizedKey = NormalizeUnitKey(unitKey);
            if (!Profiles.TryGetValue(normalizedKey, out var profile))
            {
                throw new ArgumentOutOfRangeException(nameof(unitKey), unitKey, "Unknown Emoji Clash unit.");
            }

            return profile;
        }

        public static EmojiClashResolvedTurn ResolveTurn(EmojiClashMatchState state, string playerUnitKey, string opponentUnitKey)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var turnIndex = Math.Clamp(state.CurrentTurnIndex, 0, TotalTurns - 1);
            var turnNumber = turnIndex + 1;
            var playerProfile = GetUnitProfile(playerUnitKey);
            var opponentProfile = GetUnitProfile(opponentUnitKey);
            var playerBreakdown = BuildBreakdown(playerProfile, opponentProfile, turnNumber, state.PlayerScore, state.OpponentScore);
            var opponentBreakdown = BuildBreakdown(opponentProfile, playerProfile, turnNumber, state.OpponentScore, state.PlayerScore);
            var outcome = ResolveOutcome(playerBreakdown.TotalPower, opponentBreakdown.TotalPower);

            return new EmojiClashResolvedTurn
            {
                TurnNumber = turnNumber,
                TurnValue = GetTurnValue(turnIndex),
                Outcome = outcome,
                PlayerUnitKey = playerProfile.UnitKey,
                OpponentUnitKey = opponentProfile.UnitKey,
                PlayerBreakdown = playerBreakdown,
                OpponentBreakdown = opponentBreakdown,
                PlayerFacingReason = GetReadableReason(playerProfile, opponentProfile, playerBreakdown, opponentBreakdown, outcome)
            };
        }

        public static string GetReadableReason(EmojiClashResolvedTurn resolvedTurn)
        {
            if (resolvedTurn == null)
            {
                return string.Empty;
            }

            return resolvedTurn.PlayerFacingReason ?? string.Empty;
        }

        public static string BuildTurnSummary(EmojiClashTurnRecord record)
        {
            if (record == null)
            {
                return string.Empty;
            }

            var playerName = ToDisplayName(record.PlayerUnitKey);
            var opponentName = ToDisplayName(record.OpponentUnitKey);
            return record.Outcome switch
            {
                EmojiClashTurnOutcome.PlayerWin => $"T{record.TurnNumber} +{record.TurnValue}  {playerName} beat {opponentName}",
                EmojiClashTurnOutcome.OpponentWin => $"T{record.TurnNumber} +{record.TurnValue}  {playerName} lost to {opponentName}",
                _ => $"T{record.TurnNumber} +{record.TurnValue}  Draw"
            };
        }

        public static string ToDisplayName(string unitKey)
        {
            var normalizedKey = NormalizeUnitKey(unitKey);
            if (EmojiIdUtility.TryFromApiId(normalizedKey, out var emojiId))
            {
                return EmojiIdUtility.ToDisplayName(emojiId);
            }

            return string.IsNullOrWhiteSpace(unitKey)
                ? "Unit"
                : char.ToUpperInvariant(normalizedKey[0]) + normalizedKey[1..];
        }

        public static string NormalizeUnitKey(string unitKey)
        {
            if (string.IsNullOrWhiteSpace(unitKey))
            {
                return "unknown";
            }

            var key = unitKey.Trim().ToLowerInvariant();
            var dot = key.LastIndexOf('.');
            if (dot >= 0 && dot < key.Length - 1)
            {
                key = key[(dot + 1)..];
            }

            key = key.Replace("emoji_", string.Empty)
                .Replace("emoji-", string.Empty)
                .Replace("unit_", string.Empty)
                .Replace("unit-", string.Empty)
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);

            return string.IsNullOrWhiteSpace(key) ? "unknown" : key;
        }

        private static EmojiClashTurnOutcome ResolveOutcome(int playerPower, int opponentPower)
        {
            if (playerPower > opponentPower)
            {
                return EmojiClashTurnOutcome.PlayerWin;
            }

            if (opponentPower > playerPower)
            {
                return EmojiClashTurnOutcome.OpponentWin;
            }

            return EmojiClashTurnOutcome.Draw;
        }

        private static EmojiClashPowerBreakdown BuildBreakdown(
            EmojiClashProfile profile,
            EmojiClashProfile opponent,
            int turnNumber,
            int sideScore,
            int otherScore)
        {
            var matchupBonus = ComputeMatchupBonus(profile, opponent);
            var timingBonus = ComputeTimingBonus(profile, turnNumber);
            var behindBonus = sideScore < otherScore ? profile.BehindBonus : 0;
            var totalPower = profile.BasePower + matchupBonus + timingBonus + behindBonus;

            return new EmojiClashPowerBreakdown
            {
                UnitKey = profile.UnitKey,
                BasePower = profile.BasePower,
                MatchupBonus = matchupBonus,
                TimingBonus = timingBonus,
                BehindBonus = behindBonus,
                TotalPower = totalPower,
                PrimaryReason = ResolvePrimaryReason(profile, opponent, matchupBonus, timingBonus, behindBonus, turnNumber)
            };
        }

        private static int ComputeMatchupBonus(EmojiClashProfile profile, EmojiClashProfile opponent)
        {
            var counterMatches = opponent.Tags
                .Where(tag => profile.CounterTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            var tagBonus = Math.Min(2, counterMatches);
            var directBonus = 0;
            if (HeadToHeadBonuses.TryGetValue(profile.UnitKey, out var directMap) &&
                directMap.TryGetValue(opponent.UnitKey, out var bonus))
            {
                directBonus = bonus;
            }

            return tagBonus + directBonus;
        }

        private static int ComputeTimingBonus(EmojiClashProfile profile, int turnNumber)
        {
            var total = 0;
            if (turnNumber <= 2)
            {
                total += profile.EarlyBonus;
            }

            if (turnNumber >= 4)
            {
                total += profile.LateBonus;
            }

            if (turnNumber == TotalTurns)
            {
                total += profile.FinalTurnBonus;
            }

            return total;
        }

        private static string ResolvePrimaryReason(
            EmojiClashProfile profile,
            EmojiClashProfile opponent,
            int matchupBonus,
            int timingBonus,
            int behindBonus,
            int turnNumber)
        {
            if (matchupBonus >= Math.Max(timingBonus, behindBonus) && matchupBonus > 0)
            {
                return $"{profile.DisplayName} countered {opponent.DisplayName.ToLowerInvariant()}.";
            }

            if (turnNumber == TotalTurns && profile.FinalTurnBonus > 0 && timingBonus > 0)
            {
                return $"{profile.DisplayName} {TimingReasonText["final"]}";
            }

            if (turnNumber >= 4 && profile.LateBonus > 0 && timingBonus > 0)
            {
                return $"{profile.DisplayName} {TimingReasonText["late"]}";
            }

            if (turnNumber <= 2 && profile.EarlyBonus > 0 && timingBonus > 0)
            {
                return $"{profile.DisplayName} {TimingReasonText["early"]}";
            }

            if (behindBonus > 0)
            {
                return $"{profile.DisplayName} {TimingReasonText["behind"]}";
            }

            return $"{profile.DisplayName} won the cleaner clash.";
        }

        private static string GetReadableReason(
            EmojiClashProfile playerProfile,
            EmojiClashProfile opponentProfile,
            EmojiClashPowerBreakdown playerBreakdown,
            EmojiClashPowerBreakdown opponentBreakdown,
            EmojiClashTurnOutcome outcome)
        {
            return outcome switch
            {
                EmojiClashTurnOutcome.PlayerWin => playerBreakdown.PrimaryReason,
                EmojiClashTurnOutcome.OpponentWin => opponentBreakdown.PrimaryReason,
                _ when playerBreakdown.MatchupBonus > 0 && opponentBreakdown.MatchupBonus > 0 => $"{playerProfile.DisplayName} and {opponentProfile.DisplayName} traded clean counters.",
                _ => $"{playerProfile.DisplayName} and {opponentProfile.DisplayName} finished even. No points awarded."
            };
        }

        private static EmojiClashProfile CreateProfile(
            string unitKey,
            string role,
            int basePower,
            int earlyBonus,
            int lateBonus,
            int finalTurnBonus,
            int behindBonus,
            string[] tags,
            string[] counters)
        {
            return new EmojiClashProfile
            {
                UnitKey = unitKey,
                DisplayName = ToDisplayName(unitKey),
                BasePower = basePower,
                Role = role,
                Tags = tags ?? Array.Empty<string>(),
                CounterTags = counters ?? Array.Empty<string>(),
                EarlyBonus = earlyBonus,
                LateBonus = lateBonus,
                FinalTurnBonus = finalTurnBonus,
                BehindBonus = behindBonus
            };
        }

        private static Dictionary<string, int> BuildBonusMap(params (string unitKey, int bonus)[] entries)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                map[entry.unitKey] = entry.bonus;
            }

            return map;
        }
    }
}
