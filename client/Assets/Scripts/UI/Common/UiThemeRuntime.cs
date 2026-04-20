using System;
using System.Collections.Generic;
using System.Linq;
using EmojiWar.Client.Content;
using EmojiWar.Client.Core;
using UnityEngine;

namespace EmojiWar.Client.UI.Common
{
    public static class UiThemeRuntime
    {
        private const string ThemeResourcePath = "UI/V2/UiThemeProfile";
        private const string MotionResourcePath = "UI/V2/UiMotionProfile";
        private const string SlideResourcePath = "UI/V2/Slides";
        private const int MinHeroFontSize = 52;
        private const int MinHeadingFontSize = 30;
        private const int MinBodyFontSize = 22;
        private const int MinChipFontSize = 17;

        private static UiThemeProfile activeTheme;
        private static UiMotionProfile activeMotion;
        private static bool attemptedThemeLoad;
        private static bool attemptedMotionLoad;
        private static bool usingFallbackTheme;
        private static bool usingFallbackMotion;
        private static string themeLoadIssue = string.Empty;
        private static string motionLoadIssue = string.Empty;
        private static bool attemptedSlidesLoad;
        private static Sprite[] loadedSlides = Array.Empty<Sprite>();
        private static readonly List<Sprite> generatedSlides = new();
        private static readonly Dictionary<int, Sprite> portraitSlideVariants = new();

        public static bool IsUsingFallbackAssets => usingFallbackTheme || usingFallbackMotion;

        public static UiThemeProfile Theme
        {
            get
            {
                if (activeTheme != null)
                {
                    return activeTheme;
                }

                EnsureThemeLoaded();

                return activeTheme;
            }
        }

        public static UiMotionProfile Motion
        {
            get
            {
                if (activeMotion != null)
                {
                    return activeMotion;
                }

                EnsureMotionLoaded();

                return activeMotion;
            }
        }

        public static void SetTheme(UiThemeProfile theme)
        {
            activeTheme = theme;
            attemptedThemeLoad = true;
            usingFallbackTheme = theme == null;
            themeLoadIssue = usingFallbackTheme ? "Theme was manually set to null." : string.Empty;
        }

        public static void SetMotion(UiMotionProfile motion)
        {
            activeMotion = motion;
            attemptedMotionLoad = true;
            usingFallbackMotion = motion == null;
            motionLoadIssue = usingFallbackMotion ? "Motion profile was manually set to null." : string.Empty;
        }

        public static bool TryGetSlideSprite(int slideNumber, out Sprite sprite)
        {
            sprite = null;
            EnsureSlidesLoaded();
            if (loadedSlides == null || loadedSlides.Length == 0)
            {
                return false;
            }

            var safeIndex = Mathf.Clamp(slideNumber <= 0 ? 1 : slideNumber, 1, loadedSlides.Length) - 1;
            sprite = GetPortraitSlideVariant(loadedSlides[safeIndex]);
            return sprite != null;
        }

        public static bool ValidateV2(out string message, bool requireSlides)
        {
            EnsureThemeLoaded();
            EnsureMotionLoaded();

            var issues = new List<string>();
            if (usingFallbackTheme)
            {
                issues.Add(themeLoadIssue);
            }

            if (usingFallbackMotion)
            {
                issues.Add(motionLoadIssue);
            }

            if (requireSlides && !TryGetSlideSprite(1, out _))
            {
                issues.Add(
                    "No V2 slide assets were found at Resources/UI/V2/Slides. " +
                    "Use EmojiWar > V2 > Import Sticker Pop Arena PPT Assets.");
            }

            if (issues.Count == 0)
            {
                message = string.Empty;
                return true;
            }

            message =
                "V2 UI setup is incomplete.\n\n" +
                string.Join("\n", issues) +
                "\n\nFix: EmojiWar > V2 > Create Default Theme Assets (and import PPT assets if needed).";
            return false;
        }

        public static Color ResolveRoleAccent(EmojiId emojiId)
        {
            var roleTag = EmojiUiFormatter.BuildRoleTag(emojiId);
            return ResolveRoleAccent(roleTag);
        }

        public static Color ResolveRoleAccent(string roleTag)
        {
            if (string.Equals(roleTag, "ATK", StringComparison.OrdinalIgnoreCase))
            {
                return Theme.AttackAccent;
            }

            if (string.Equals(roleTag, "CTL", StringComparison.OrdinalIgnoreCase))
            {
                return Theme.ControlAccent;
            }

            if (string.Equals(roleTag, "BURST", StringComparison.OrdinalIgnoreCase))
            {
                return Theme.BurstAccent;
            }

            if (string.Equals(roleTag, "SUP", StringComparison.OrdinalIgnoreCase))
            {
                return Theme.SupportAccent;
            }

            if (string.Equals(roleTag, "RAMP", StringComparison.OrdinalIgnoreCase))
            {
                return Theme.RampAccent;
            }

            return Theme.ControlAccent;
        }

        public static Color ResolveCardColor(UnitCardState state)
        {
            if (state.HasFlag(UnitCardState.Banned))
            {
                return Theme.CardColors.Banned;
            }

            if (state.HasFlag(UnitCardState.Selected))
            {
                return Theme.CardColors.Selected;
            }

            if (state.HasFlag(UnitCardState.Disabled))
            {
                return Theme.CardColors.Disabled;
            }

            return Theme.CardColors.Default;
        }

        public static Color ResolvePanelTop(MatchUiPanelState panelState)
        {
            return panelState switch
            {
                MatchUiPanelState.Ban => Theme.BanGradient.Top,
                MatchUiPanelState.Formation => Theme.FormationGradient.Top,
                MatchUiPanelState.Result => Theme.ResultGradient.Top,
                MatchUiPanelState.Waiting => Theme.BanGradient.Top,
                _ => Theme.HomeGradient.Top
            };
        }

        public static Color ResolvePanelBottom(MatchUiPanelState panelState)
        {
            return panelState switch
            {
                MatchUiPanelState.Ban => Theme.BanGradient.Bottom,
                MatchUiPanelState.Formation => Theme.FormationGradient.Bottom,
                MatchUiPanelState.Result => Theme.ResultGradient.Bottom,
                MatchUiPanelState.Waiting => Theme.BanGradient.Bottom,
                _ => Theme.HomeGradient.Bottom
            };
        }

        public static string BuildSquadStickerRow(System.Collections.Generic.IEnumerable<EmojiId> emojiIds)
        {
            if (emojiIds == null)
            {
                return "No squad";
            }

            var tokens = new System.Collections.Generic.List<string>();
            foreach (var emojiId in emojiIds)
            {
                tokens.Add(EmojiIdUtility.ToEmojiGlyph(emojiId));
            }

            return tokens.Count == 0 ? "No squad" : string.Join("   ", tokens);
        }

        private static void EnsureThemeLoaded()
        {
            if (attemptedThemeLoad)
            {
                return;
            }

            attemptedThemeLoad = true;
            activeTheme = Resources.Load<UiThemeProfile>(ThemeResourcePath);
            if (activeTheme != null)
            {
                usingFallbackTheme = false;
                themeLoadIssue = string.Empty;
                return;
            }

            usingFallbackTheme = true;
            themeLoadIssue =
                "UiThemeProfile.asset is missing in Resources/UI/V2 (expected at Assets/Resources/UI/V2/UiThemeProfile.asset).";
            activeTheme = BuildFallbackTheme();
        }

        private static void EnsureMotionLoaded()
        {
            if (attemptedMotionLoad)
            {
                return;
            }

            attemptedMotionLoad = true;
            activeMotion = Resources.Load<UiMotionProfile>(MotionResourcePath);
            if (activeMotion != null)
            {
                usingFallbackMotion = false;
                motionLoadIssue = string.Empty;
                return;
            }

            usingFallbackMotion = true;
            motionLoadIssue =
                "UiMotionProfile.asset is missing in Resources/UI/V2 (expected at Assets/Resources/UI/V2/UiMotionProfile.asset).";
            activeMotion = ScriptableObject.CreateInstance<UiMotionProfile>();
        }

        private static void EnsureSlidesLoaded()
        {
            if (attemptedSlidesLoad)
            {
                return;
            }

            attemptedSlidesLoad = true;

            var sprites = Resources.LoadAll<Sprite>(SlideResourcePath);
            if (sprites != null && sprites.Length > 0)
            {
                loadedSlides = sprites
                    .Where(candidate => candidate != null)
                    .OrderBy(candidate => candidate.name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return;
            }

            var textures = Resources.LoadAll<Texture2D>(SlideResourcePath);
            if (textures == null || textures.Length == 0)
            {
                loadedSlides = Array.Empty<Sprite>();
                return;
            }

            var generated = new List<Sprite>(textures.Length);
            foreach (var texture in textures
                         .Where(candidate => candidate != null)
                         .OrderBy(candidate => candidate.name, StringComparer.OrdinalIgnoreCase))
            {
                var runtimeSprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f,
                    0,
                    SpriteMeshType.FullRect);
                runtimeSprite.name = texture.name;
                generatedSlides.Add(runtimeSprite);
                generated.Add(runtimeSprite);
            }

            loadedSlides = generated.ToArray();
        }

        private static Sprite GetPortraitSlideVariant(Sprite source)
        {
            if (source == null)
            {
                return null;
            }

            var sourceRect = source.rect;
            if (sourceRect.height <= 0f || sourceRect.width <= sourceRect.height * 1.10f)
            {
                return source;
            }

            var key = source.GetInstanceID();
            if (portraitSlideVariants.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var targetWidth = Mathf.Clamp(
                Mathf.RoundToInt(sourceRect.height * (9f / 16f)),
                1,
                Mathf.RoundToInt(sourceRect.width));
            var maxOffset = Mathf.Max(0, Mathf.RoundToInt(sourceRect.width) - targetWidth);
            var leftBiasOffset = Mathf.Clamp(Mathf.RoundToInt(maxOffset * 0.12f), 0, maxOffset);

            var croppedRect = new Rect(
                sourceRect.x + leftBiasOffset,
                sourceRect.y,
                targetWidth,
                sourceRect.height);

            var portrait = Sprite.Create(
                source.texture,
                croppedRect,
                new Vector2(0.5f, 0.5f),
                source.pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            portrait.name = $"{source.name}-portrait";
            portraitSlideVariants[key] = portrait;
            generatedSlides.Add(portrait);
            return portrait;
        }

        private static UiThemeProfile BuildFallbackTheme()
        {
            var fallback = ScriptableObject.CreateInstance<UiThemeProfile>();

            // Recovery palette tuned for sticker-pop readability with higher chroma.
            fallback.HomeGradient = new UiThemeProfile.GradientPair
            {
                Top = new Color32(0x2A, 0x1E, 0x5C, 0xFF),
                Bottom = new Color32(0x14, 0x1F, 0x52, 0xFF)
            };
            fallback.SquadGradient = new UiThemeProfile.GradientPair
            {
                Top = new Color32(0x2A, 0x1E, 0x5C, 0xFF),
                Bottom = new Color32(0x1A, 0x2D, 0x6F, 0xFF)
            };
            fallback.BanGradient = new UiThemeProfile.GradientPair
            {
                Top = new Color32(0x2D, 0x14, 0x4D, 0xFF),
                Bottom = new Color32(0x3D, 0x21, 0x44, 0xFF)
            };
            fallback.FormationGradient = new UiThemeProfile.GradientPair
            {
                Top = new Color32(0x2A, 0x1E, 0x5C, 0xFF),
                Bottom = new Color32(0x1A, 0x2B, 0x66, 0xFF)
            };
            fallback.ResultGradient = new UiThemeProfile.GradientPair
            {
                Top = new Color32(0x2A, 0x1E, 0x5C, 0xFF),
                Bottom = new Color32(0x1C, 0x3E, 0x58, 0xFF)
            };

            fallback.CardColors = new UiThemeProfile.CardPalette
            {
                Default = new Color32(0x2A, 0x47, 0x6D, 0xF5),
                Selected = new Color32(0x47, 0x73, 0xA8, 0xFF),
                Banned = new Color32(0x7D, 0x4A, 0x54, 0xF2),
                Disabled = new Color(0.14f, 0.22f, 0.33f, 0.62f)
            };

            fallback.PrimaryCtaColor = new Color32(0xFF, 0x4F, 0xD8, 0xFF);
            fallback.SecondaryCtaColor = new Color32(0x35, 0x4D, 0x87, 0xFF);
            fallback.SurfaceColor = new Color32(0x17, 0x1F, 0x48, 0xEB);
            fallback.HeroFontSize = 54;
            fallback.HeadingFontSize = 30;
            fallback.BodyFontSize = 22;
            fallback.ChipFontSize = 17;

            ApplyReadabilityFloor(fallback);
            return fallback;
        }

        private static UiThemeProfile BuildReadableRuntimeTheme(UiThemeProfile source)
        {
            if (source == null)
            {
                return BuildFallbackTheme();
            }

            var runtimeTheme = CloneTheme(source);
            ApplyReadabilityFloor(runtimeTheme);

            var homeTopLuma = ComputeLuminance(runtimeTheme.HomeGradient.Top);
            var homeBottomLuma = ComputeLuminance(runtimeTheme.HomeGradient.Bottom);
            var surfaceLuma = ComputeLuminance(runtimeTheme.SurfaceColor);
            var looksReadable = homeTopLuma <= 0.33f && homeBottomLuma <= 0.29f && surfaceLuma <= 0.35f;
            if (looksReadable)
            {
                return runtimeTheme;
            }

            var safe = BuildFallbackTheme();
            safe.AttackAccent = runtimeTheme.AttackAccent;
            safe.ControlAccent = runtimeTheme.ControlAccent;
            safe.BurstAccent = runtimeTheme.BurstAccent;
            safe.SupportAccent = runtimeTheme.SupportAccent;
            safe.RampAccent = runtimeTheme.RampAccent;
            safe.PrimaryCtaColor = runtimeTheme.PrimaryCtaColor;
            safe.SecondaryCtaColor = runtimeTheme.SecondaryCtaColor;
            safe.HeroFontSize = runtimeTheme.HeroFontSize;
            safe.HeadingFontSize = runtimeTheme.HeadingFontSize;
            safe.BodyFontSize = runtimeTheme.BodyFontSize;
            safe.ChipFontSize = runtimeTheme.ChipFontSize;

            ApplyReadabilityFloor(safe);
            return safe;
        }

        private static float ComputeLuminance(Color color)
        {
            return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        }

        private static UiThemeProfile CloneTheme(UiThemeProfile source)
        {
            var clone = ScriptableObject.CreateInstance<UiThemeProfile>();
            clone.HomeGradient = source.HomeGradient;
            clone.SquadGradient = source.SquadGradient;
            clone.BanGradient = source.BanGradient;
            clone.FormationGradient = source.FormationGradient;
            clone.ResultGradient = source.ResultGradient;
            clone.CardColors = source.CardColors;
            clone.AttackAccent = source.AttackAccent;
            clone.ControlAccent = source.ControlAccent;
            clone.BurstAccent = source.BurstAccent;
            clone.SupportAccent = source.SupportAccent;
            clone.RampAccent = source.RampAccent;
            clone.HeroFontSize = source.HeroFontSize;
            clone.HeadingFontSize = source.HeadingFontSize;
            clone.BodyFontSize = source.BodyFontSize;
            clone.ChipFontSize = source.ChipFontSize;
            clone.SurfaceColor = source.SurfaceColor;
            clone.SurfaceOutline = source.SurfaceOutline;
            clone.PrimaryCtaColor = source.PrimaryCtaColor;
            clone.SecondaryCtaColor = source.SecondaryCtaColor;
            clone.ShadowLift = source.ShadowLift;
            clone.GlowStrength = source.GlowStrength;
            return clone;
        }

        private static void ApplyReadabilityFloor(UiThemeProfile theme)
        {
            if (theme == null)
            {
                return;
            }

            theme.HeroFontSize = Mathf.Max(theme.HeroFontSize, MinHeroFontSize);
            theme.HeadingFontSize = Mathf.Max(theme.HeadingFontSize, MinHeadingFontSize);
            theme.BodyFontSize = Mathf.Max(theme.BodyFontSize, MinBodyFontSize);
            theme.ChipFontSize = Mathf.Max(theme.ChipFontSize, MinChipFontSize);

            theme.SurfaceColor = DarkenIfTooBright(theme.SurfaceColor, 0.36f);
            theme.PrimaryCtaColor = EnsureCtaContrast(theme.PrimaryCtaColor, 0.68f);
            theme.SecondaryCtaColor = EnsureCtaContrast(theme.SecondaryCtaColor, 0.56f);

            var palette = theme.CardColors;
            palette.Default = EnsureAlpha(palette.Default, 0.90f);
            palette.Selected = EnsureAlpha(palette.Selected, 0.96f);
            palette.Banned = EnsureAlpha(palette.Banned, 0.92f);
            palette.Disabled = new Color(palette.Disabled.r, palette.Disabled.g, palette.Disabled.b, Mathf.Clamp(palette.Disabled.a, 0.50f, 0.70f));
            theme.CardColors = palette;
        }

        private static Color EnsureAlpha(Color color, float minAlpha)
        {
            color.a = Mathf.Max(color.a, minAlpha);
            return color;
        }

        private static Color DarkenIfTooBright(Color color, float maxLuminance)
        {
            var luminance = ComputeLuminance(color);
            if (luminance <= maxLuminance)
            {
                return color;
            }

            var scale = maxLuminance / Mathf.Max(0.001f, luminance);
            return new Color(color.r * scale, color.g * scale, color.b * scale, color.a);
        }

        private static Color EnsureCtaContrast(Color color, float maxLuminance)
        {
            var adjusted = DarkenIfTooBright(color, maxLuminance);
            adjusted.a = Mathf.Max(adjusted.a, 1f);
            return adjusted;
        }
    }
}
