using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using EmojiWar.Client.UI.Common;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class EmojiWarV2VisualTools
{
    private const string DefaultDeckFileName = "Emoji_War_Sticker_Pop_Arena_Visual_Direction_Deck.pptx";
    private const string TargetFolder = "Assets/Resources/UI/V2/Slides";
    private const string ThemeAssetPath = "Assets/Resources/UI/V2/UiThemeProfile.asset";
    private const string MotionAssetPath = "Assets/Resources/UI/V2/UiMotionProfile.asset";
    private const string LegacyImportedFolder = "Assets/UI/V2/Imported";
    private const string LegacyPptExportFolder = "Assets/UI/V2/PptExports";

    [InitializeOnLoadMethod]
    private static void EnsureV2RuntimeAssetsOnEditorLoad()
    {
        CreateDefaultThemeAssetsIfMissing();
    }

    [MenuItem("EmojiWar/V2/Import Sticker Pop Arena PPT Assets")]
    public static void ImportPptAssets()
    {
        var pptPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            DefaultDeckFileName);

        if (!File.Exists(pptPath))
        {
            EditorUtility.DisplayDialog(
                "EmojiWar V2",
                $"PPT not found at:\n{pptPath}\n\nSelect the PPTX file manually.",
                "OK");
            var picked = EditorUtility.OpenFilePanel("Select Sticker Pop Arena PPTX", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "pptx");
            if (string.IsNullOrWhiteSpace(picked) || !File.Exists(picked))
            {
                return;
            }

            pptPath = picked;
        }

        Directory.CreateDirectory(TargetFolder);
        var temp = Path.Combine(Path.GetTempPath(), $"emojiwar-v2-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            ZipFile.ExtractToDirectory(pptPath, temp);
            var mediaFolder = Path.Combine(temp, "ppt", "media");
            if (!Directory.Exists(mediaFolder))
            {
                EditorUtility.DisplayDialog("EmojiWar V2", "No media folder found in PPT archive.", "OK");
                return;
            }

            var slideImages = Directory.GetFiles(mediaFolder, "image-*-1.png")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (slideImages.Length == 0)
            {
                EditorUtility.DisplayDialog("EmojiWar V2", "No slide images were found in PPT media.", "OK");
                return;
            }

            for (var index = 0; index < slideImages.Length; index++)
            {
                var destination = Path.Combine(TargetFolder, $"sticker-pop-slide-{index + 1:D2}.png");
                File.Copy(slideImages[index], destination, overwrite: true);
            }

            AssetDatabase.Refresh();
            ConfigureSlideImporters();
            CreateDefaultThemeAssetsIfMissing();
            EditorUtility.DisplayDialog(
                "EmojiWar V2",
                $"Imported {slideImages.Length} slide image(s) into:\n{TargetFolder}\n\nTheme/motion assets are available under Resources/UI/V2.",
                "OK");
        }
        finally
        {
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch
            {
                // Ignore temp cleanup errors.
            }
        }
    }

    [MenuItem("EmojiWar/V2/Create Default Theme Assets")]
    public static void CreateDefaultThemeAssetsIfMissing()
    {
        EnsureFolder("Assets", "Resources");
        EnsureFolder("Assets/Resources", "UI");
        EnsureFolder("Assets/Resources/UI", "V2");
        EnsureFolder("Assets/Resources/UI/V2", "Slides");

        CreateAssetIfMissing<UiThemeProfile>(ThemeAssetPath);
        CreateAssetIfMissing<UiMotionProfile>(MotionAssetPath);
        MirrorLegacySlidesIfPresent();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("EmojiWar/V2/Enable DOTween Scripting Define (EMOJIWAR_DOTWEEN)")]
    public static void EnableDotweenDefine()
    {
        ApplyDefine("EMOJIWAR_DOTWEEN", true);
        EditorUtility.DisplayDialog(
            "EmojiWar V2",
            "Added EMOJIWAR_DOTWEEN define to current build target group.\n\nInstall DOTween package to activate typed integration.",
            "OK");
    }

    [MenuItem("EmojiWar/V2/Disable DOTween Scripting Define (EMOJIWAR_DOTWEEN)")]
    public static void DisableDotweenDefine()
    {
        ApplyDefine("EMOJIWAR_DOTWEEN", false);
        EditorUtility.DisplayDialog("EmojiWar V2", "Removed EMOJIWAR_DOTWEEN define.", "OK");
    }

    [MenuItem("EmojiWar/V2/Rebuild Core V2 Scenes (Home/Builder/Match)")]
    public static void RebuildCoreV2Scenes()
    {
        EmojiWarProjectSetup.RebuildV2CoreScenes();
    }

    private static void ConfigureSlideImporters()
    {
        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TargetFolder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                continue;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }

    private static void MirrorLegacySlidesIfPresent()
    {
        if (!Directory.Exists(TargetFolder))
        {
            Directory.CreateDirectory(TargetFolder);
        }

        var legacySources = new[] { LegacyImportedFolder, LegacyPptExportFolder };
        foreach (var source in legacySources)
        {
            if (!Directory.Exists(source))
            {
                continue;
            }

            var pngs = Directory.GetFiles(source, "*.png")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            for (var index = 0; index < pngs.Length; index++)
            {
                var destination = Path.Combine(TargetFolder, $"sticker-pop-slide-{index + 1:D2}.png");
                if (!File.Exists(destination))
                {
                    File.Copy(pngs[index], destination, overwrite: false);
                }
            }
        }
    }

    private static void EnsureFolder(string parent, string name)
    {
        var full = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(full))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static void CreateAssetIfMissing<T>(string path) where T : ScriptableObject
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) != null)
        {
            return;
        }

        var asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
    }

    private static void ApplyDefine(string define, bool enabled)
    {
        var group = EditorUserBuildSettings.selectedBuildTargetGroup;
        var namedTarget = NamedBuildTarget.FromBuildTargetGroup(group);
        var defines = PlayerSettings.GetScriptingDefineSymbols(namedTarget)
            .Split(';')
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();

        var has = defines.Contains(define);
        if (enabled && !has)
        {
            defines.Add(define);
        }
        else if (!enabled && has)
        {
            defines.Remove(define);
        }

        PlayerSettings.SetScriptingDefineSymbols(namedTarget, string.Join(";", defines));
    }
}
