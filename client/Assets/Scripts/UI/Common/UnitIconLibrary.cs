using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmojiWar.Client.UI.Common
{
    /// <summary>
    /// Deterministic unit icon provider for rescue UI.
    /// Final art can replace generated placeholders by adding sprites at
    /// Resources/EmojiWar/UnitIcons/{normalized-key} and
    /// Resources/EmojiWar/FighterPortraits/{normalized-key}.
    /// </summary>
    public static class UnitIconLibrary
    {
        private const int IconSize = 256;
        private const int PortraitSize = 320;
        private static readonly Dictionary<string, Sprite> SmallIconCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<string, Sprite> PortraitCache = new Dictionary<string, Sprite>();

        public static Sprite GetIconSprite(string unitIdOrName)
        {
            return GetSmallIconSprite(unitIdOrName);
        }

        public static Sprite GetSmallIconSprite(string unitIdOrName)
        {
            var key = NormalizeUnitKey(unitIdOrName);
            if (SmallIconCache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var resource = LoadSmallIconResource(key);
            if (resource != null)
            {
                SmallIconCache[key] = resource;
                return resource;
            }

            var generated = BuildGeneratedSprite(key);
            SmallIconCache[key] = generated;
            return generated;
        }

        public static Sprite GetPortraitSprite(string unitIdOrName)
        {
            var key = NormalizeUnitKey(unitIdOrName);
            if (PortraitCache.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var portrait = LoadPortraitResource(key);
            if (portrait != null)
            {
                PortraitCache[key] = portrait;
                return portrait;
            }

            var smallIcon = LoadSmallIconResource(key);
            if (smallIcon != null)
            {
                PortraitCache[key] = smallIcon;
                return smallIcon;
            }

            var generated = BuildGeneratedPortraitSprite(key);
            PortraitCache[key] = generated;
            return generated;
        }

        public static bool HasPortraitSprite(string unitIdOrName)
        {
            var key = NormalizeUnitKey(unitIdOrName);
            return LoadPortraitResource(key) != null || LoadSmallIconResource(key) != null;
        }

        public static bool HasSmallIconSprite(string unitIdOrName)
        {
            return LoadSmallIconResource(NormalizeUnitKey(unitIdOrName)) != null;
        }

        public static Color GetPrimaryColor(string unitIdOrName)
        {
            return NormalizeUnitKey(unitIdOrName) switch
            {
                "fire" => new Color32(0xFF, 0x4E, 0x2E, 0xFF),
                "water" => new Color32(0x25, 0xB8, 0xFF, 0xFF),
                "lightning" => new Color32(0xFF, 0xE1, 0x35, 0xFF),
                "ice" => new Color32(0x9E, 0xF4, 0xFF, 0xFF),
                "magnet" => new Color32(0xFF, 0x4F, 0x65, 0xFF),
                "bomb" => new Color32(0x23, 0x22, 0x3A, 0xFF),
                "shield" => new Color32(0x62, 0xC7, 0xFF, 0xFF),
                "heart" => new Color32(0xFF, 0x4F, 0x9A, 0xFF),
                "wind" => new Color32(0x75, 0xF3, 0xFF, 0xFF),
                "snake" => new Color32(0x6F, 0xE8, 0x58, 0xFF),
                "hole" => new Color32(0x14, 0x10, 0x28, 0xFF),
                "plant" => new Color32(0x42, 0xE0, 0x67, 0xFF),
                "mirror" => new Color32(0x81, 0xEE, 0xFF, 0xFF),
                "soap" => new Color32(0x9B, 0xF6, 0xFF, 0xFF),
                "light" => new Color32(0xFF, 0xE7, 0x68, 0xFF),
                "ghost" => new Color32(0xE9, 0xE7, 0xFF, 0xFF),
                "chain" => new Color32(0xBA, 0xC0, 0xD8, 0xFF),
                _ => new Color32(0xFF, 0xD8, 0x4D, 0xFF)
            };
        }

        public static Color GetSecondaryColor(string unitIdOrName)
        {
            return NormalizeUnitKey(unitIdOrName) switch
            {
                "fire" => new Color32(0xFF, 0xB0, 0x34, 0xFF),
                "water" => new Color32(0x1C, 0x6C, 0xFF, 0xFF),
                "lightning" => new Color32(0xFF, 0x92, 0x28, 0xFF),
                "ice" => new Color32(0x42, 0xC7, 0xFF, 0xFF),
                "magnet" => new Color32(0x3A, 0xA5, 0xFF, 0xFF),
                "bomb" => new Color32(0xFF, 0xD8, 0x4D, 0xFF),
                "shield" => new Color32(0x8B, 0x5C, 0xFF, 0xFF),
                "heart" => new Color32(0xFF, 0xD0, 0xE4, 0xFF),
                "wind" => new Color32(0x24, 0xB6, 0xE8, 0xFF),
                "snake" => new Color32(0x24, 0x9B, 0x4C, 0xFF),
                "hole" => new Color32(0x8B, 0x5C, 0xFF, 0xFF),
                "plant" => new Color32(0xB8, 0xFF, 0x66, 0xFF),
                "mirror" => new Color32(0xFF, 0xF8, 0xFF, 0xFF),
                "soap" => new Color32(0xFF, 0xF8, 0xFF, 0xFF),
                "light" => new Color32(0xFF, 0x9E, 0x35, 0xFF),
                "ghost" => new Color32(0x9F, 0xA7, 0xFF, 0xFF),
                "chain" => new Color32(0x66, 0x72, 0x93, 0xFF),
                _ => new Color32(0xFF, 0x4F, 0xD8, 0xFF)
            };
        }

        public static string NormalizeUnitKey(string unitIdOrName)
        {
            if (string.IsNullOrWhiteSpace(unitIdOrName))
            {
                return "unknown";
            }

            var key = unitIdOrName.Trim().ToLowerInvariant();
            var dot = key.LastIndexOf('.');
            if (dot >= 0 && dot < key.Length - 1)
            {
                key = key.Substring(dot + 1);
            }

            key = key.Replace("emoji_", string.Empty)
                .Replace("emoji-", string.Empty)
                .Replace("unit_", string.Empty)
                .Replace("unit-", string.Empty)
                .Replace(" ", string.Empty)
                .Replace("_", string.Empty)
                .Replace("-", string.Empty);

            return string.IsNullOrEmpty(key) ? "unknown" : key;
        }

        private static Sprite BuildGeneratedSprite(string key)
        {
            var texture = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false)
            {
                name = $"GeneratedUnitIcon_{key}",
                hideFlags = HideFlags.HideAndDontSave
            };

            Clear(texture);
            switch (key)
            {
                case "fire":
                    DrawFire(texture);
                    break;
                case "water":
                    DrawDroplet(texture, new Vector2(128f, 132f), 78f, GetPrimaryColor(key), GetSecondaryColor(key));
                    break;
                case "lightning":
                    FillPolygon(texture, new[]
                    {
                        new Vector2(144f, 18f), new Vector2(64f, 138f), new Vector2(122f, 138f),
                        new Vector2(96f, 238f), new Vector2(194f, 104f), new Vector2(136f, 104f)
                    }, GetPrimaryColor(key));
                    StrokePolygon(texture, new[]
                    {
                        new Vector2(144f, 18f), new Vector2(64f, 138f), new Vector2(122f, 138f),
                        new Vector2(96f, 238f), new Vector2(194f, 104f), new Vector2(136f, 104f)
                    }, Color.white, 7f);
                    break;
                case "ice":
                    DrawSnowflake(texture);
                    break;
                case "magnet":
                    DrawMagnet(texture);
                    break;
                case "bomb":
                    DrawBomb(texture);
                    break;
                case "shield":
                    DrawShield(texture);
                    break;
                case "heart":
                    DrawHeart(texture);
                    break;
                case "wind":
                    DrawWind(texture);
                    break;
                case "snake":
                    DrawSnake(texture);
                    break;
                case "hole":
                    DrawHole(texture);
                    break;
                case "plant":
                    DrawPlant(texture);
                    break;
                case "mirror":
                    DrawMirror(texture);
                    break;
                case "soap":
                    DrawSoap(texture);
                    break;
                case "light":
                    DrawSpark(texture, new Vector2(128f, 128f), 104f, 34f, GetPrimaryColor(key));
                    DrawSpark(texture, new Vector2(82f, 70f), 38f, 12f, Color.white);
                    break;
                case "ghost":
                    DrawGhost(texture);
                    break;
                case "chain":
                    DrawChain(texture);
                    break;
                default:
                    DrawSpark(texture, new Vector2(128f, 128f), 96f, 36f, GetPrimaryColor(key));
                    break;
            }

            DecorateCharacter(texture, key);
            ApplyStickerFraming(texture);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, IconSize, IconSize), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildGeneratedPortraitSprite(string key)
        {
            var source = BuildGeneratedSprite(key);
            var texture = new Texture2D(PortraitSize, PortraitSize, TextureFormat.RGBA32, false)
            {
                name = $"GeneratedFighterPortrait_{key}",
                hideFlags = HideFlags.HideAndDontSave
            };

            Clear(texture);
            BlitSpriteScaled(
                source,
                texture,
                new Rect(
                    PortraitSize * 0.10f,
                    PortraitSize * 0.08f,
                    PortraitSize * 0.80f,
                    PortraitSize * 0.80f));
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, PortraitSize, PortraitSize), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite LoadSmallIconResource(string key)
        {
            return Resources.Load<Sprite>($"EmojiWar/UnitIcons/{key}");
        }

        private static Sprite LoadPortraitResource(string key)
        {
            return Resources.Load<Sprite>($"EmojiWar/FighterPortraits/{key}");
        }

        private static void ApplyStickerFraming(Texture2D texture)
        {
            var source = texture.GetPixels32();
            var framed = new Color32[source.Length];
            var shadow = new Color32(0x23, 0x19, 0x52, 0x64);
            var outline = new Color32(0xFF, 0xFF, 0xFF, 0xFF);

            for (var y = 0; y < IconSize; y++)
            {
                for (var x = 0; x < IconSize; x++)
                {
                    var index = y * IconSize + x;
                    if (source[index].a <= 0)
                    {
                        continue;
                    }

                    BlendPixel(framed, x + 6, y - 8, shadow);
                }
            }

            for (var y = 0; y < IconSize; y++)
            {
                for (var x = 0; x < IconSize; x++)
                {
                    var index = y * IconSize + x;
                    if (source[index].a <= 0)
                    {
                        continue;
                    }

                    for (var oy = -6; oy <= 6; oy++)
                    {
                        for (var ox = -6; ox <= 6; ox++)
                        {
                            if ((ox * ox) + (oy * oy) > 34)
                            {
                                continue;
                            }

                            var targetX = x + ox;
                            var targetY = y + oy;
                            if (targetX < 0 || targetX >= IconSize || targetY < 0 || targetY >= IconSize)
                            {
                                continue;
                            }

                            var targetIndex = targetY * IconSize + targetX;
                            if (source[targetIndex].a > 0)
                            {
                                continue;
                            }

                            BlendPixel(framed, targetX, targetY, outline);
                        }
                    }
                }
            }

            for (var y = 0; y < IconSize; y++)
            {
                for (var x = 0; x < IconSize; x++)
                {
                    var index = y * IconSize + x;
                    if (source[index].a <= 0)
                    {
                        continue;
                    }

                    BlendPixel(framed, x, y, source[index]);
                }
            }

            texture.SetPixels32(framed);
        }

        private static void DecorateCharacter(Texture2D texture, string key)
        {
            switch (key)
            {
                case "fire":
                    DrawMascotPose(texture, new Vector2(128f, 150f), 1.05f, Color.white, new Color32(0x22, 0x15, 0x2F, 0xFF), openMouth: true, eyebrowsSharp: true, wink: false, cheeky: false);
                    DrawPunchArms(texture, new Vector2(128f, 164f), 58f, 46f, new Color32(0x9A, 0x34, 0x14, 0xFF));
                    DrawKickFeet(texture, new Vector2(128f, 214f), 38f, new Color32(0x8A, 0x28, 0x13, 0xFF));
                    break;
                case "water":
                    DrawMascotPose(texture, new Vector2(126f, 154f), 0.96f, Color.white, new Color32(0x16, 0x21, 0x5C, 0xFF), openMouth: true, eyebrowsSharp: true, wink: false, cheeky: false);
                    DrawPunchArms(texture, new Vector2(128f, 168f), 54f, 42f, new Color32(0x0D, 0x73, 0xD8, 0xFF));
                    DrawKickFeet(texture, new Vector2(128f, 214f), 30f, new Color32(0x0B, 0x77, 0xD4, 0xFF));
                    break;
                case "lightning":
                    DrawMascotPose(texture, new Vector2(130f, 150f), 0.92f, Color.white, new Color32(0x26, 0x1A, 0x22, 0xFF), openMouth: true, eyebrowsSharp: true, wink: false, cheeky: false);
                    DrawPunchArms(texture, new Vector2(130f, 162f), 50f, 40f, new Color32(0xD3, 0x8B, 0x12, 0xFF));
                    DrawKickFeet(texture, new Vector2(126f, 208f), 30f, new Color32(0xCC, 0x89, 0x16, 0xFF));
                    break;
                case "shield":
                    DrawMascotPose(texture, new Vector2(128f, 132f), 0.88f, Color.white, new Color32(0x1C, 0x17, 0x44, 0xFF), openMouth: true, eyebrowsSharp: true, wink: false, cheeky: false);
                    DrawPunchArms(texture, new Vector2(128f, 146f), 54f, 42f, new Color32(0x2D, 0x46, 0x98, 0xFF));
                    DrawKickFeet(texture, new Vector2(128f, 212f), 32f, new Color32(0x2B, 0x3C, 0x8A, 0xFF));
                    break;
                case "heart":
                    DrawMascotPose(texture, new Vector2(128f, 146f), 0.94f, Color.white, new Color32(0x4B, 0x14, 0x3B, 0xFF), openMouth: true, eyebrowsSharp: false, wink: true, cheeky: true);
                    DrawPunchArms(texture, new Vector2(126f, 162f), 56f, 42f, new Color32(0xD8, 0x3D, 0x7A, 0xFF));
                    DrawKickFeet(texture, new Vector2(130f, 214f), 34f, new Color32(0xCA, 0x3A, 0x74, 0xFF));
                    break;
                case "wind":
                    DrawMascotPose(texture, new Vector2(128f, 142f), 0.90f, Color.white, new Color32(0x1C, 0x3A, 0x66, 0xFF), openMouth: true, eyebrowsSharp: false, wink: true, cheeky: true);
                    DrawPunchArms(texture, new Vector2(130f, 158f), 54f, 40f, new Color32(0x39, 0xA5, 0xD0, 0xFF));
                    DrawKickFeet(texture, new Vector2(126f, 206f), 28f, new Color32(0x3C, 0xA7, 0xD4, 0xFF));
                    break;
                case "ice":
                    DrawMascotPose(texture, new Vector2(128f, 142f), 0.88f, Color.white, new Color32(0x1D, 0x4F, 0x86, 0xFF), openMouth: true, eyebrowsSharp: true, wink: false, cheeky: false);
                    DrawPunchArms(texture, new Vector2(128f, 156f), 52f, 40f, new Color32(0x47, 0xB5, 0xED, 0xFF));
                    DrawKickFeet(texture, new Vector2(128f, 206f), 28f, new Color32(0x45, 0xA8, 0xE0, 0xFF));
                    break;
                case "magnet":
                    DrawMascotPose(texture, new Vector2(128f, 146f), 0.82f, Color.white, new Color32(0x21, 0x16, 0x44, 0xFF), openMouth: true, eyebrowsSharp: true, wink: false, cheeky: false);
                    DrawPunchArms(texture, new Vector2(128f, 160f), 54f, 42f, new Color32(0x34, 0x31, 0x68, 0xFF));
                    DrawKickFeet(texture, new Vector2(128f, 214f), 30f, new Color32(0x34, 0x31, 0x68, 0xFF));
                    break;
                case "bomb":
                    DrawMascotPose(texture, new Vector2(122f, 142f), 0.88f, Color.white, new Color32(0x1C, 0x17, 0x2F, 0xFF), openMouth: true, eyebrowsSharp: true, wink: false, cheeky: false);
                    DrawPunchArms(texture, new Vector2(124f, 156f), 56f, 42f, new Color32(0x31, 0x2D, 0x4F, 0xFF));
                    DrawKickFeet(texture, new Vector2(124f, 212f), 30f, new Color32(0x31, 0x2D, 0x4F, 0xFF));
                    break;
                case "snake":
                    DrawMascotPose(texture, new Vector2(168f, 84f), 0.72f, Color.white, new Color32(0x1E, 0x20, 0x17, 0xFF), openMouth: false, eyebrowsSharp: true, wink: false, cheeky: false);
                    DrawTongue(texture, new Vector2(188f, 97f), 22f, new Color32(0xFF, 0x5B, 0x6E, 0xFF));
                    break;
                case "hole":
                    DrawMascotPose(texture, new Vector2(128f, 122f), 0.78f, Color.white, new Color32(0xF4, 0xF1, 0xFF, 0xFF), openMouth: false, eyebrowsSharp: true, wink: false, cheeky: false);
                    break;
                case "plant":
                    DrawMascotPose(texture, new Vector2(128f, 92f), 0.80f, Color.white, new Color32(0x1B, 0x37, 0x16, 0xFF), openMouth: true, eyebrowsSharp: true, wink: false, cheeky: true);
                    DrawPunchArms(texture, new Vector2(128f, 108f), 38f, 32f, new Color32(0x2E, 0x8D, 0x2D, 0xFF));
                    DrawKickFeet(texture, new Vector2(128f, 166f), 24f, new Color32(0x2E, 0x8D, 0x2D, 0xFF));
                    break;
                case "mirror":
                    DrawMascotPose(texture, new Vector2(128f, 120f), 0.82f, Color.white, new Color32(0x1C, 0x3C, 0x6B, 0xFF), openMouth: true, eyebrowsSharp: true, wink: false, cheeky: false);
                    DrawPunchArms(texture, new Vector2(128f, 132f), 46f, 32f, new Color32(0x53, 0x7E, 0xA5, 0xFF));
                    DrawKickFeet(texture, new Vector2(128f, 188f), 22f, new Color32(0x53, 0x7E, 0xA5, 0xFF));
                    break;
                case "soap":
                    DrawMascotPose(texture, new Vector2(130f, 140f), 0.84f, Color.white, new Color32(0x5D, 0x2B, 0x62, 0xFF), openMouth: true, eyebrowsSharp: true, wink: false, cheeky: false);
                    DrawPunchArms(texture, new Vector2(130f, 154f), 48f, 34f, new Color32(0xB8, 0x79, 0xC1, 0xFF));
                    DrawKickFeet(texture, new Vector2(130f, 206f), 24f, new Color32(0xB8, 0x79, 0xC1, 0xFF));
                    break;
                case "ghost":
                    DrawMascotPose(texture, new Vector2(128f, 122f), 0.88f, Color.white, new Color32(0x2A, 0x1D, 0x5A, 0xFF), openMouth: true, eyebrowsSharp: true, wink: false, cheeky: false);
                    DrawPunchArms(texture, new Vector2(128f, 138f), 44f, 34f, new Color32(0xD8, 0xD6, 0xFA, 0xFF));
                    break;
                case "chain":
                    DrawMascotPose(texture, new Vector2(128f, 130f), 0.78f, Color.white, new Color32(0x22, 0x22, 0x33, 0xFF), openMouth: true, eyebrowsSharp: true, wink: false, cheeky: false);
                    DrawPunchArms(texture, new Vector2(128f, 144f), 48f, 34f, new Color32(0x6D, 0x74, 0x90, 0xFF));
                    DrawKickFeet(texture, new Vector2(128f, 196f), 24f, new Color32(0x6D, 0x74, 0x90, 0xFF));
                    break;
            }
        }

        private static void DrawFire(Texture2D texture)
        {
            FillPolygon(texture, new[]
            {
                new Vector2(128f, 12f), new Vector2(72f, 98f), new Vector2(58f, 162f),
                new Vector2(85f, 224f), new Vector2(128f, 242f), new Vector2(176f, 220f),
                new Vector2(202f, 158f), new Vector2(174f, 88f)
            }, new Color32(0xFF, 0x4E, 0x2E, 0xFF));
            DrawEllipse(texture, new Vector2(128f, 152f), new Vector2(70f, 92f), new Color32(0xFF, 0x8E, 0x2C, 0xFF));
            FillPolygon(texture, new[]
            {
                new Vector2(132f, 76f), new Vector2(94f, 155f), new Vector2(112f, 220f),
                new Vector2(155f, 210f), new Vector2(168f, 146f)
            }, new Color32(0xFF, 0xD8, 0x4D, 0xFF));
            DrawEllipse(texture, new Vector2(126f, 184f), new Vector2(26f, 40f), Color.white);
        }

        private static void DrawSnowflake(Texture2D texture)
        {
            var center = new Vector2(128f, 128f);
            DrawCircle(texture, center, 22f, Color.white);
            for (var i = 0; i < 6; i++)
            {
                var angle = i * Mathf.PI / 3f;
                var end = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 96f;
                DrawThickLine(texture, center, end, 11f, GetPrimaryColor("ice"));
                DrawThickLine(texture, end, end - new Vector2(Mathf.Cos(angle + 0.7f), Mathf.Sin(angle + 0.7f)) * 28f, 8f, Color.white);
                DrawThickLine(texture, end, end - new Vector2(Mathf.Cos(angle - 0.7f), Mathf.Sin(angle - 0.7f)) * 28f, 8f, Color.white);
            }
            DrawCircle(texture, center, 14f, Color.white);
        }

        private static void DrawMagnet(Texture2D texture)
        {
            DrawThickLine(texture, new Vector2(76f, 70f), new Vector2(76f, 170f), 34f, GetPrimaryColor("magnet"));
            DrawThickLine(texture, new Vector2(180f, 70f), new Vector2(180f, 170f), 34f, GetSecondaryColor("magnet"));
            DrawThickLine(texture, new Vector2(76f, 70f), new Vector2(180f, 70f), 34f, GetPrimaryColor("magnet"));
            DrawThickLine(texture, new Vector2(76f, 188f), new Vector2(76f, 218f), 36f, Color.white);
            DrawThickLine(texture, new Vector2(180f, 188f), new Vector2(180f, 218f), 36f, Color.white);
        }

        private static void DrawBomb(Texture2D texture)
        {
            DrawCircle(texture, new Vector2(122f, 144f), 74f, GetPrimaryColor("bomb"));
            DrawCircle(texture, new Vector2(96f, 112f), 18f, new Color32(0x62, 0x61, 0x82, 0xFF));
            DrawThickLine(texture, new Vector2(168f, 88f), new Vector2(204f, 48f), 10f, GetSecondaryColor("bomb"));
            DrawSpark(texture, new Vector2(214f, 38f), 28f, 8f, new Color32(0xFF, 0xE8, 0x64, 0xFF));
        }

        private static void DrawShield(Texture2D texture)
        {
            FillPolygon(texture, new[]
            {
                new Vector2(128f, 24f), new Vector2(206f, 58f), new Vector2(194f, 158f),
                new Vector2(128f, 236f), new Vector2(62f, 158f), new Vector2(50f, 58f)
            }, GetPrimaryColor("shield"));
            FillPolygon(texture, new[]
            {
                new Vector2(128f, 52f), new Vector2(176f, 76f), new Vector2(168f, 148f),
                new Vector2(128f, 196f), new Vector2(88f, 148f), new Vector2(80f, 76f)
            }, GetSecondaryColor("shield"));
            DrawThickLine(texture, new Vector2(128f, 58f), new Vector2(128f, 194f), 8f, Color.white);
        }

        private static void DrawHeart(Texture2D texture)
        {
            var color = GetPrimaryColor("heart");
            for (var y = 0; y < IconSize; y++)
            {
                for (var x = 0; x < IconSize; x++)
                {
                    var nx = (x - 128f) / 82f;
                    var ny = (y - 122f) / 82f;
                    var value = Mathf.Pow(nx * nx + ny * ny - 1f, 3f) - nx * nx * ny * ny * ny;
                    if (value <= 0f)
                    {
                        BlendPixel(texture, x, y, color);
                    }
                }
            }
            DrawCircle(texture, new Vector2(98f, 154f), 15f, Color.white);
        }

        private static void DrawWind(Texture2D texture)
        {
            var a = GetPrimaryColor("wind");
            var b = GetSecondaryColor("wind");
            DrawThickLine(texture, new Vector2(42f, 98f), new Vector2(184f, 98f), 13f, a);
            DrawThickLine(texture, new Vector2(184f, 98f), new Vector2(210f, 76f), 13f, a);
            DrawThickLine(texture, new Vector2(54f, 138f), new Vector2(202f, 138f), 16f, Color.white);
            DrawThickLine(texture, new Vector2(202f, 138f), new Vector2(222f, 164f), 16f, Color.white);
            DrawThickLine(texture, new Vector2(70f, 180f), new Vector2(174f, 180f), 12f, b);
            DrawCircle(texture, new Vector2(204f, 76f), 13f, a);
            DrawCircle(texture, new Vector2(222f, 164f), 15f, Color.white);
        }

        private static void DrawSnake(Texture2D texture)
        {
            var color = GetPrimaryColor("snake");
            var dark = GetSecondaryColor("snake");
            Vector2 previous = default;
            for (var i = 0; i <= 24; i++)
            {
                var t = i / 24f;
                var point = new Vector2(68f + t * 120f, 54f + t * 150f);
                point.x += Mathf.Sin(t * Mathf.PI * 3f) * 44f;
                if (i > 0)
                {
                    DrawThickLine(texture, previous, point, 23f, color);
                }
                previous = point;
            }
            DrawCircle(texture, previous + new Vector2(12f, 6f), 26f, color);
            DrawCircle(texture, previous + new Vector2(20f, 13f), 5f, dark);
            DrawThickLine(texture, previous + new Vector2(34f, 2f), previous + new Vector2(52f, -8f), 4f, new Color32(0xFF, 0x5B, 0x6E, 0xFF));
        }

        private static void DrawHole(Texture2D texture)
        {
            DrawEllipse(texture, new Vector2(128f, 138f), new Vector2(100f, 58f), GetSecondaryColor("hole"));
            DrawEllipse(texture, new Vector2(128f, 138f), new Vector2(76f, 38f), GetPrimaryColor("hole"));
            DrawEllipse(texture, new Vector2(102f, 124f), new Vector2(28f, 10f), new Color32(0xF8, 0xF7, 0xFF, 0x80));
        }

        private static void DrawPlant(Texture2D texture)
        {
            DrawThickLine(texture, new Vector2(128f, 214f), new Vector2(128f, 88f), 12f, GetSecondaryColor("plant"));
            DrawEllipse(texture, new Vector2(92f, 126f), new Vector2(48f, 24f), GetPrimaryColor("plant"));
            DrawEllipse(texture, new Vector2(164f, 118f), new Vector2(52f, 25f), GetPrimaryColor("plant"));
            DrawCircle(texture, new Vector2(128f, 84f), 22f, new Color32(0xB8, 0xFF, 0x66, 0xFF));
        }

        private static void DrawMirror(Texture2D texture)
        {
            FillPolygon(texture, new[]
            {
                new Vector2(96f, 34f), new Vector2(176f, 34f), new Vector2(190f, 172f),
                new Vector2(128f, 220f), new Vector2(66f, 172f)
            }, GetSecondaryColor("mirror"));
            FillPolygon(texture, new[]
            {
                new Vector2(106f, 50f), new Vector2(166f, 50f), new Vector2(176f, 158f),
                new Vector2(128f, 196f), new Vector2(80f, 158f)
            }, GetPrimaryColor("mirror"));
            DrawThickLine(texture, new Vector2(108f, 70f), new Vector2(154f, 58f), 8f, Color.white);
            DrawThickLine(texture, new Vector2(96f, 108f), new Vector2(168f, 90f), 6f, new Color32(0xF8, 0xF7, 0xFF, 0xAA));
        }

        private static void DrawSoap(Texture2D texture)
        {
            DrawCircle(texture, new Vector2(104f, 150f), 46f, GetPrimaryColor("soap"));
            DrawCircle(texture, new Vector2(154f, 118f), 38f, new Color32(0xD7, 0xFF, 0xFF, 0xDD));
            DrawCircle(texture, new Vector2(172f, 176f), 24f, new Color32(0xFF, 0xF8, 0xFF, 0xCC));
            DrawCircle(texture, new Vector2(80f, 84f), 22f, new Color32(0xFF, 0xF8, 0xFF, 0xB5));
            DrawCircle(texture, new Vector2(94f, 134f), 11f, Color.white);
        }

        private static void DrawGhost(Texture2D texture)
        {
            DrawCircle(texture, new Vector2(128f, 104f), 62f, GetPrimaryColor("ghost"));
            FillPolygon(texture, new[]
            {
                new Vector2(66f, 108f), new Vector2(190f, 108f), new Vector2(190f, 210f),
                new Vector2(164f, 190f), new Vector2(140f, 214f), new Vector2(116f, 190f),
                new Vector2(92f, 214f), new Vector2(66f, 190f)
            }, GetPrimaryColor("ghost"));
            DrawCircle(texture, new Vector2(106f, 106f), 10f, new Color32(0x18, 0x12, 0x35, 0xFF));
            DrawCircle(texture, new Vector2(150f, 106f), 10f, new Color32(0x18, 0x12, 0x35, 0xFF));
        }

        private static void DrawChain(Texture2D texture)
        {
            DrawRing(texture, new Vector2(96f, 128f), new Vector2(48f, 34f), 13f, GetPrimaryColor("chain"));
            DrawRing(texture, new Vector2(160f, 128f), new Vector2(48f, 34f), 13f, GetSecondaryColor("chain"));
            DrawThickLine(texture, new Vector2(116f, 128f), new Vector2(140f, 128f), 12f, Color.white);
        }

        private static void DrawMascotPose(
            Texture2D texture,
            Vector2 faceCenter,
            float scale,
            Color eyeColor,
            Color browColor,
            bool openMouth,
            bool eyebrowsSharp,
            bool wink,
            bool cheeky)
        {
            var eyeOffsetX = 20f * scale;
            var eyeOffsetY = -2f * scale;
            var eyeRadius = 10f * scale;
            var browWidth = 26f * scale;
            var browThickness = 5f * scale;
            var mouthY = 18f * scale;

            DrawCircle(texture, faceCenter + new Vector2(-eyeOffsetX, eyeOffsetY), eyeRadius, eyeColor);
            if (wink)
            {
                DrawThickLine(texture, faceCenter + new Vector2(eyeOffsetX - 8f * scale, eyeOffsetY), faceCenter + new Vector2(eyeOffsetX + 10f * scale, eyeOffsetY + 2f * scale), 4f * scale, eyeColor);
            }
            else
            {
                DrawCircle(texture, faceCenter + new Vector2(eyeOffsetX, eyeOffsetY), eyeRadius, eyeColor);
            }

            var leftBrowA = faceCenter + new Vector2(-eyeOffsetX - browWidth * 0.35f, -18f * scale);
            var leftBrowB = faceCenter + new Vector2(-eyeOffsetX + browWidth * 0.35f, eyebrowsSharp ? -24f * scale : -20f * scale);
            var rightBrowA = faceCenter + new Vector2(eyeOffsetX - browWidth * 0.35f, eyebrowsSharp ? -24f * scale : -20f * scale);
            var rightBrowB = faceCenter + new Vector2(eyeOffsetX + browWidth * 0.35f, -18f * scale);
            DrawThickLine(texture, leftBrowA, leftBrowB, browThickness, browColor);
            DrawThickLine(texture, rightBrowA, rightBrowB, browThickness, browColor);

            if (openMouth)
            {
                DrawEllipse(texture, faceCenter + new Vector2(0f, mouthY), new Vector2(14f * scale, 12f * scale), new Color32(0x1E, 0x17, 0x2A, 0xFF));
                DrawEllipse(texture, faceCenter + new Vector2(0f, mouthY + 4f * scale), new Vector2(8f * scale, 4f * scale), new Color32(0xFF, 0x77, 0x8D, 0xFF));
            }
            else
            {
                DrawThickLine(texture, faceCenter + new Vector2(-10f * scale, mouthY), faceCenter + new Vector2(10f * scale, mouthY + 3f * scale), 4f * scale, browColor);
            }

            if (cheeky)
            {
                DrawCircle(texture, faceCenter + new Vector2(-34f * scale, 12f * scale), 6f * scale, new Color32(0xFF, 0xB7, 0xD4, 0xAA));
                DrawCircle(texture, faceCenter + new Vector2(32f * scale, 14f * scale), 6f * scale, new Color32(0xFF, 0xB7, 0xD4, 0xAA));
            }
        }

        private static void DrawPunchArms(Texture2D texture, Vector2 center, float reach, float drop, Color limbColor)
        {
            var leftShoulder = center + new Vector2(-26f, -6f);
            var rightShoulder = center + new Vector2(26f, -6f);
            var leftFist = center + new Vector2(-reach, drop * 0.12f);
            var rightFist = center + new Vector2(reach, -drop * 0.05f);
            DrawThickLine(texture, leftShoulder, leftFist, 12f, limbColor);
            DrawThickLine(texture, rightShoulder, rightFist, 12f, limbColor);
            DrawCircle(texture, leftFist, 14f, limbColor);
            DrawCircle(texture, rightFist, 14f, limbColor);
        }

        private static void DrawKickFeet(Texture2D texture, Vector2 center, float spread, Color limbColor)
        {
            var leftHip = center + new Vector2(-14f, -6f);
            var rightHip = center + new Vector2(14f, -6f);
            var leftFoot = center + new Vector2(-spread, 20f);
            var rightFoot = center + new Vector2(spread * 0.72f, 18f);
            DrawThickLine(texture, leftHip, leftFoot, 12f, limbColor);
            DrawThickLine(texture, rightHip, rightFoot, 12f, limbColor);
            DrawCircle(texture, leftFoot, 11f, limbColor);
            DrawCircle(texture, rightFoot, 11f, limbColor);
        }

        private static void DrawTongue(Texture2D texture, Vector2 start, float length, Color tongueColor)
        {
            DrawThickLine(texture, start, start + new Vector2(length, 2f), 4f, tongueColor);
            DrawThickLine(texture, start + new Vector2(length, 2f), start + new Vector2(length + 7f, -2f), 2f, tongueColor);
            DrawThickLine(texture, start + new Vector2(length, 2f), start + new Vector2(length + 7f, 6f), 2f, tongueColor);
        }

        private static void DrawDroplet(Texture2D texture, Vector2 center, float radius, Color primary, Color secondary)
        {
            FillPolygon(texture, new[]
            {
                new Vector2(center.x, center.y - radius - 48f),
                new Vector2(center.x - radius * 0.82f, center.y + 8f),
                new Vector2(center.x - radius * 0.45f, center.y + radius * 0.84f),
                new Vector2(center.x, center.y + radius),
                new Vector2(center.x + radius * 0.45f, center.y + radius * 0.84f),
                new Vector2(center.x + radius * 0.82f, center.y + 8f)
            }, primary);
            DrawEllipse(texture, center + new Vector2(0f, 24f), new Vector2(radius * 0.74f, radius * 0.64f), primary);
            DrawEllipse(texture, center + new Vector2(-24f, 18f), new Vector2(19f, 31f), Color.white);
            DrawEllipse(texture, center + new Vector2(16f, 38f), new Vector2(34f, 18f), secondary);
        }

        private static void DrawSpark(Texture2D texture, Vector2 center, float longRadius, float shortRadius, Color color)
        {
            FillPolygon(texture, new[]
            {
                center + new Vector2(0f, -longRadius),
                center + new Vector2(shortRadius, -shortRadius),
                center + new Vector2(longRadius, 0f),
                center + new Vector2(shortRadius, shortRadius),
                center + new Vector2(0f, longRadius),
                center + new Vector2(-shortRadius, shortRadius),
                center + new Vector2(-longRadius, 0f),
                center + new Vector2(-shortRadius, -shortRadius)
            }, color);
        }

        private static void DrawRing(Texture2D texture, Vector2 center, Vector2 radius, float thickness, Color color)
        {
            for (var y = Mathf.Max(0, Mathf.FloorToInt(center.y - radius.y - thickness)); y <= Mathf.Min(IconSize - 1, Mathf.CeilToInt(center.y + radius.y + thickness)); y++)
            {
                for (var x = Mathf.Max(0, Mathf.FloorToInt(center.x - radius.x - thickness)); x <= Mathf.Min(IconSize - 1, Mathf.CeilToInt(center.x + radius.x + thickness)); x++)
                {
                    var nx = (x - center.x) / radius.x;
                    var ny = (y - center.y) / radius.y;
                    var distance = Mathf.Abs(nx * nx + ny * ny - 1f);
                    if (distance < thickness / Mathf.Max(radius.x, radius.y))
                    {
                        BlendPixel(texture, x, y, color);
                    }
                }
            }
        }

        private static void DrawEllipse(Texture2D texture, Vector2 center, Vector2 radius, Color color)
        {
            for (var y = Mathf.Max(0, Mathf.FloorToInt(center.y - radius.y)); y <= Mathf.Min(IconSize - 1, Mathf.CeilToInt(center.y + radius.y)); y++)
            {
                for (var x = Mathf.Max(0, Mathf.FloorToInt(center.x - radius.x)); x <= Mathf.Min(IconSize - 1, Mathf.CeilToInt(center.x + radius.x)); x++)
                {
                    var nx = (x - center.x) / radius.x;
                    var ny = (y - center.y) / radius.y;
                    if (nx * nx + ny * ny <= 1f)
                    {
                        BlendPixel(texture, x, y, color);
                    }
                }
            }
        }

        private static void DrawCircle(Texture2D texture, Vector2 center, float radius, Color color)
        {
            DrawEllipse(texture, center, new Vector2(radius, radius), color);
        }

        private static void DrawThickLine(Texture2D texture, Vector2 start, Vector2 end, float thickness, Color color)
        {
            var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.x, end.x) - thickness));
            var maxX = Mathf.Min(IconSize - 1, Mathf.CeilToInt(Mathf.Max(start.x, end.x) + thickness));
            var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.y, end.y) - thickness));
            var maxY = Mathf.Min(IconSize - 1, Mathf.CeilToInt(Mathf.Max(start.y, end.y) + thickness));
            var segment = end - start;
            var lengthSqr = Mathf.Max(0.001f, segment.sqrMagnitude);
            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var point = new Vector2(x, y);
                    var t = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSqr);
                    var closest = start + segment * t;
                    if (Vector2.Distance(point, closest) <= thickness * 0.5f)
                    {
                        BlendPixel(texture, x, y, color);
                    }
                }
            }
        }

        private static void StrokePolygon(Texture2D texture, Vector2[] points, Color color, float thickness)
        {
            if (points == null || points.Length < 2)
            {
                return;
            }

            for (var i = 0; i < points.Length; i++)
            {
                DrawThickLine(texture, points[i], points[(i + 1) % points.Length], thickness, color);
            }
        }

        private static void FillPolygon(Texture2D texture, Vector2[] points, Color color)
        {
            if (points == null || points.Length < 3)
            {
                return;
            }

            var minX = IconSize - 1;
            var maxX = 0;
            var minY = IconSize - 1;
            var maxY = 0;
            foreach (var point in points)
            {
                minX = Mathf.Min(minX, Mathf.FloorToInt(point.x));
                maxX = Mathf.Max(maxX, Mathf.CeilToInt(point.x));
                minY = Mathf.Min(minY, Mathf.FloorToInt(point.y));
                maxY = Mathf.Max(maxY, Mathf.CeilToInt(point.y));
            }

            minX = Mathf.Clamp(minX, 0, IconSize - 1);
            maxX = Mathf.Clamp(maxX, 0, IconSize - 1);
            minY = Mathf.Clamp(minY, 0, IconSize - 1);
            maxY = Mathf.Clamp(maxY, 0, IconSize - 1);

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    if (ContainsPoint(points, new Vector2(x, y)))
                    {
                        BlendPixel(texture, x, y, color);
                    }
                }
            }
        }

        private static bool ContainsPoint(Vector2[] polygon, Vector2 point)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                if (((polygon[i].y > point.y) != (polygon[j].y > point.y)) &&
                    point.x < (polygon[j].x - polygon[i].x) * (point.y - polygon[i].y) / (polygon[j].y - polygon[i].y + 0.0001f) + polygon[i].x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static void Clear(Texture2D texture)
        {
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }

        private static void BlendPixel(Texture2D texture, int x, int y, Color source)
        {
            if (x < 0 || x >= texture.width || y < 0 || y >= texture.height)
            {
                return;
            }

            var destination = texture.GetPixel(x, y);
            var alpha = source.a + destination.a * (1f - source.a);
            if (alpha <= 0f)
            {
                texture.SetPixel(x, y, Color.clear);
                return;
            }

            var color = (source * source.a + destination * destination.a * (1f - source.a)) / alpha;
            color.a = alpha;
            texture.SetPixel(x, y, color);
        }

        private static void BlendPixel(Color32[] pixels, int x, int y, Color32 source)
        {
            if (x < 0 || x >= IconSize || y < 0 || y >= IconSize)
            {
                return;
            }

            var index = (y * IconSize) + x;
            var destination = pixels[index];
            var srcA = source.a / 255f;
            var dstA = destination.a / 255f;
            var outA = srcA + (dstA * (1f - srcA));
            if (outA <= 0f)
            {
                pixels[index] = new Color32(0, 0, 0, 0);
                return;
            }

            var src = (Color)source;
            var dst = (Color)destination;
            var outColor = ((src * srcA) + (dst * dstA * (1f - srcA))) / outA;
            outColor.a = outA;
            pixels[index] = outColor;
        }

        private static void BlitSpriteScaled(Sprite source, Texture2D destination, Rect destinationRect)
        {
            if (source == null || destination == null)
            {
                return;
            }

            var sourceTexture = source.texture;
            if (sourceTexture == null)
            {
                return;
            }

            var sourceRect = source.rect;
            var minX = Mathf.RoundToInt(destinationRect.xMin);
            var maxX = Mathf.RoundToInt(destinationRect.xMax);
            var minY = Mathf.RoundToInt(destinationRect.yMin);
            var maxY = Mathf.RoundToInt(destinationRect.yMax);
            for (var y = minY; y < maxY; y++)
            {
                var v = Mathf.InverseLerp(minY, maxY - 1f, y);
                var sourceY = Mathf.Clamp(
                    Mathf.RoundToInt(sourceRect.yMin + v * (sourceRect.height - 1f)),
                    Mathf.RoundToInt(sourceRect.yMin),
                    Mathf.RoundToInt(sourceRect.yMax - 1f));
                for (var x = minX; x < maxX; x++)
                {
                    var u = Mathf.InverseLerp(minX, maxX - 1f, x);
                    var sourceX = Mathf.Clamp(
                        Mathf.RoundToInt(sourceRect.xMin + u * (sourceRect.width - 1f)),
                        Mathf.RoundToInt(sourceRect.xMin),
                        Mathf.RoundToInt(sourceRect.xMax - 1f));
                    var color = sourceTexture.GetPixel(sourceX, sourceY);
                    if (color.a <= 0f)
                    {
                        continue;
                    }

                    BlendPixel(destination, x, y, color);
                }
            }
        }
    }
}
