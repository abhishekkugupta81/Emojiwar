using UnityEngine;

namespace EmojiWar.Client.UI.Common
{
    public static class EmojiWarVisualStyle
    {
        public static class Colors
        {
            public static readonly Color BgTop = new Color32(0x3B, 0x26, 0xD9, 0xFF);
            public static readonly Color BgMid = new Color32(0x42, 0x60, 0xF2, 0xFF);
            public static readonly Color BgBottom = new Color32(0x27, 0xD4, 0xE7, 0xFF);
            public static readonly Color Depth = new Color32(0x24, 0x19, 0x5C, 0xFF);
            public static readonly Color PanelFillSoft = new Color(112f / 255f, 124f / 255f, 1f, 0.22f);
            public static readonly Color PanelFillStrong = new Color(112f / 255f, 124f / 255f, 1f, 0.30f);
            public static readonly Color PanelBorder = new Color(1f, 1f, 1f, 0.18f);
            public static readonly Color PanelHighlight = new Color(1f, 1f, 1f, 0.12f);
            public static readonly Color GoldLight = new Color32(0xFF, 0xD8, 0x4D, 0xFF);
            public static readonly Color GoldDark = new Color32(0xFF, 0xB9, 0x1C, 0xFF);
            public static readonly Color GoldText = new Color32(0x3C, 0x26, 0x7F, 0xFF);
            public static readonly Color MintAccent = new Color32(0x55, 0xE6, 0xB7, 0xFF);
            public static readonly Color BottomPlate = new Color(0.93f, 0.95f, 1f, 0.92f);
            public static readonly Color BottomPlateBorder = new Color(1f, 1f, 1f, 0.62f);
            public static readonly Color BottomPlateText = new Color32(0x67, 0x72, 0x96, 0xFF);
            public static readonly Color SecondaryAction = new Color(0.19f, 0.30f, 0.72f, 0.92f);
            public static readonly Color SecondaryActionDark = new Color(0.21f, 0.24f, 0.58f, 0.92f);
        }

        public static class Layout
        {
            public const float ScreenSidePadding = 0.055f;
            public const float PanelRoundness = 28f;
            public const float HeroTitleMaxWidth = 0.56f;
            public const float StickerBoardTitleHeight = 0.10f;
            public const float StickerBoardContentTop = 0.82f;
            public const float HeroStageTop = 0.88f;
            public const float HeroStageBottom = 0.46f;
            public const float SquadStripTop = 0.45f;
            public const float SquadStripBottom = 0.31f;
            public const float PrimaryCtaTop = 0.29f;
            public const float PrimaryCtaBottom = 0.21f;
            public const float SecondaryActionsTop = 0.19f;
            public const float SecondaryActionsBottom = 0.11f;
            public const float BottomNavTop = 0.075f;
            public const float BottomNavBottom = 0.015f;
            public static readonly Vector2 LargeHeroAvatar = new(248f, 248f);
            public static readonly Vector2 ClashHeroAvatar = new(228f, 250f);
            public static readonly Vector2 SquadStripTile = new(152f, 136f);
            public static readonly Vector2 RosterTile = new(138f, 110f);
            public static readonly Vector2 ClashCard = new(360f, 430f);
            public static readonly Vector2 QuickResultHeroFighter = new(224f, 244f);
            public static readonly Vector2 ResultHeroTile = new(132f, 144f);
            public static readonly Vector2 ResultHeroPortrait = new(170f, 196f);
            public static readonly Vector2 CompactTimelineRow = new(0f, 54f);
        }
    }
}
