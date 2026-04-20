using System;
using UnityEngine;

namespace EmojiWar.Client.UI.Common
{
    [CreateAssetMenu(menuName = "EmojiWar/UI/Motion Profile", fileName = "UiMotionProfile")]
    public sealed class UiMotionProfile : ScriptableObject
    {
        [Serializable]
        public struct Range
        {
            public float Min;
            public float Max;
        }

        [Header("Idle")]
        public Range IdleBobSeconds = new() { Min = 1.8f, Max = 2.4f };
        public Range IdleTiltSeconds = new() { Min = 1.5f, Max = 2.3f };
        public Range IdleScaleSeconds = new() { Min = 1.8f, Max = 2.4f };
        public float IdleBobPixels = 4f;
        public float IdleTiltDegrees = 1.5f;
        public float IdleScaleAmount = 0.03f;

        [Header("Actions")]
        public Range JumpSelectSeconds = new() { Min = 0.18f, Max = 0.24f };
        public Range StickerSlamSeconds = new() { Min = 0.18f, Max = 0.22f };
        public Range SnapToSlotSeconds = new() { Min = 0.24f, Max = 0.32f };
        public float JumpScaleOvershoot = 1.08f;
        public float SnapArcPixels = 28f;

        [Header("Loops")]
        public float CtaBreatheSeconds = 2.2f;
        public float CtaBreatheScale = 0.04f;
        public Range VictoryBurstSeconds = new() { Min = 0.26f, Max = 0.42f };
    }
}
