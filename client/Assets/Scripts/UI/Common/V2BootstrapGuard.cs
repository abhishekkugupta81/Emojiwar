namespace EmojiWar.Client.UI.Common
{
    public static class V2BootstrapGuard
    {
        public static bool EnsureReady(out string message, bool requireSlides = false)
        {
            if (!UiThemeRuntime.ValidateV2(out message, requireSlides))
            {
                var mode = GetMotionModeSummary();
                message =
                    $"{message}\n\n" +
                    "V2 strict mode is enabled (prefab-first). Partial fallback rendering is blocked.\n" +
                    $"Motion mode: {mode}";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static string GetMotionModeSummary()
        {
#if EMOJIWAR_DOTWEEN && DOTWEEN_ENABLED
            return "DOTween enabled";
#elif EMOJIWAR_DOTWEEN
            return "DOTween define enabled (DOTween package not detected)";
#else
            return "DOTween disabled (fallback motion)";
#endif
        }
    }
}
