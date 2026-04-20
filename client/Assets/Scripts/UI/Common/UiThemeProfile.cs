using System;
using UnityEngine;

namespace EmojiWar.Client.UI.Common
{
    [CreateAssetMenu(menuName = "EmojiWar/UI/Theme Profile", fileName = "UiThemeProfile")]
    public sealed class UiThemeProfile : ScriptableObject
    {
        [Serializable]
        public struct GradientPair
        {
            public Color Top;
            public Color Bottom;
        }

        [Serializable]
        public struct CardPalette
        {
            public Color Default;
            public Color Selected;
            public Color Banned;
            public Color Disabled;
        }

        [Header("Screen Gradients")]
        public GradientPair HomeGradient = new()
        {
            Top = new Color32(0x2A, 0x1E, 0x5C, 0xFF),
            Bottom = new Color32(0x14, 0x1F, 0x52, 0xFF)
        };

        public GradientPair SquadGradient = new()
        {
            Top = new Color32(0x2A, 0x1E, 0x5C, 0xFF),
            Bottom = new Color32(0x1A, 0x2D, 0x6F, 0xFF)
        };

        public GradientPair BanGradient = new()
        {
            Top = new Color32(0x2D, 0x14, 0x4D, 0xFF),
            Bottom = new Color32(0x3D, 0x21, 0x44, 0xFF)
        };

        public GradientPair FormationGradient = new()
        {
            Top = new Color32(0x2A, 0x1E, 0x5C, 0xFF),
            Bottom = new Color32(0x1A, 0x2B, 0x66, 0xFF)
        };

        public GradientPair ResultGradient = new()
        {
            Top = new Color32(0x2A, 0x1E, 0x5C, 0xFF),
            Bottom = new Color32(0x1C, 0x3E, 0x58, 0xFF)
        };

        [Header("Card Palette")]
        public CardPalette CardColors = new()
        {
            Default = new Color32(0x1F, 0x3A, 0x5D, 0xF0),
            Selected = new Color32(0x3D, 0x6E, 0xA8, 0xFF),
            Banned = new Color32(0x6D, 0x45, 0x4B, 0xF2),
            Disabled = new Color(0.12f, 0.20f, 0.32f, 0.62f)
        };

        [Header("Role Accents")]
        public Color AttackAccent = new Color32(0xFF, 0x8A, 0x3D, 0xFF);
        public Color ControlAccent = new Color32(0x34, 0xB6, 0xFF, 0xFF);
        public Color BurstAccent = new Color32(0xFF, 0x5B, 0x6E, 0xFF);
        public Color SupportAccent = new Color32(0x3D, 0xFF, 0xD1, 0xFF);
        public Color RampAccent = new Color32(0x7D, 0xFF, 0x6A, 0xFF);

        [Header("Typography")]
        [Range(18, 72)] public int HeroFontSize = 56;
        [Range(14, 42)] public int HeadingFontSize = 30;
        [Range(12, 32)] public int BodyFontSize = 22;
        [Range(12, 28)] public int ChipFontSize = 18;

        [Header("Surface/Glow")]
        public Color SurfaceColor = new Color32(0x17, 0x1F, 0x48, 0xEB);
        public Color SurfaceOutline = new Color32(0xC9, 0xD1, 0xFF, 0xF2);
        public Color PrimaryCtaColor = new Color32(0xFF, 0x4F, 0xD8, 0xFF);
        public Color SecondaryCtaColor = new Color32(0x35, 0x4D, 0x87, 0xFF);
        [Range(0.1f, 2.5f)] public float ShadowLift = 1.2f;
        [Range(0.1f, 2.5f)] public float GlowStrength = 1.45f;
    }
}
