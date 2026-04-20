using System;
using EmojiWar.Client.Content;
using EmojiWar.Client.Core;

namespace EmojiWar.Client.UI.Common
{
    public static class EmojiUiFormatter
    {
        public static string BuildPhaseBar(PhaseStep phaseStep)
        {
            return phaseStep switch
            {
                PhaseStep.Squad => "[SQUAD]  BAN  FORM  RESULT",
                PhaseStep.Ban => "SQUAD  [BAN]  FORM  RESULT",
                PhaseStep.Formation => "SQUAD  BAN  [FORM]  RESULT",
                PhaseStep.Result => "SQUAD  BAN  FORM  [RESULT]",
                _ => "SQUAD  BAN  FORM  RESULT"
            };
        }

        public static string BuildRoleTag(EmojiId emojiId)
        {
            var bootstrap = AppBootstrap.Instance;
            var definition = bootstrap?.EmojiCatalog?.Find(emojiId);
            var role = definition?.Role ?? EmojiRole.Element;
            return role switch
            {
                EmojiRole.Element => "ATK",
                EmojiRole.Trick => "CTL",
                EmojiRole.Hazard => "BURST",
                EmojiRole.GuardSupport => "SUP",
                EmojiRole.StatusRamp => "RAMP",
                _ => "UNIT"
            };
        }

        public static string BuildUnitCardLabel(EmojiId emojiId, UnitCardState cardState)
        {
            var displayName = EmojiIdUtility.ToDisplayName(emojiId);
            var glyph = EmojiIdUtility.ToEmojiGlyph(emojiId);
            var roleTag = BuildRoleTag(emojiId);
            var selectedPrefix = cardState.HasFlag(UnitCardState.Selected) ? "✓ " : string.Empty;
            var bannedSuffix = cardState.HasFlag(UnitCardState.Banned) ? "  BAN" : string.Empty;
            return $"{selectedPrefix}{glyph} {displayName} [{roleTag}]{bannedSuffix}";
        }

        public static string BuildShortToken(EmojiId emojiId)
        {
            var displayName = EmojiIdUtility.ToDisplayName(emojiId);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return "?";
            }

            return displayName.Length <= 2
                ? displayName.ToUpperInvariant()
                : displayName.Substring(0, 2).ToUpperInvariant();
        }

        public static string BuildStatusChip(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
        }

        public static string BuildCountdownChip(int secondsRemaining, string prefix)
        {
            if (secondsRemaining <= 0)
            {
                return string.Empty;
            }

            return $"{prefix}: {secondsRemaining}s";
        }
    }
}
