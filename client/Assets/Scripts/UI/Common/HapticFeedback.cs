using UnityEngine;

namespace EmojiWar.Client.UI.Common
{
    public static class HapticFeedback
    {
        public static void TriggerLightImpact()
        {
            // MVP no-op in Editor/unsupported platforms.
#if UNITY_IOS || UNITY_ANDROID
            Handheld.Vibrate();
#endif
        }
    }
}

