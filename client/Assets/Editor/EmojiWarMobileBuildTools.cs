using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using EmojiWar.Client.Core.Supabase;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class EmojiWarMobileBuildTools
{
    private const string SupabaseConfigPath = "Assets/Data/Config/SupabaseProjectConfig.asset";
    private const string AndroidBundleIdSuggestion = "com.emojiwar.game";
    private const string IOSBundleIdSuggestion = "com.emojiwar.game";
    private const string BuildOutputRoot = "Builds";
    private const string AndroidOutputFolder = "Builds/Android";
    private const string IOSOutputFolder = "Builds/iOS";

    [MenuItem("EmojiWar/Mobile/Apply Launch Mobile Defaults")]
    public static void ApplyLaunchMobileDefaults()
    {
        PlayerSettings.companyName = "EmojiWar";
        PlayerSettings.productName = "EmojiWar";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.useAnimatedAutorotation = false;
        PlayerSettings.runInBackground = true;
        PlayerSettings.Android.forceInternetPermission = true;

        AssetDatabase.SaveAssets();
        Debug.Log("EmojiWar mobile defaults applied: portrait-only, run in background, Android internet permission.");
        EditorUtility.DisplayDialog(
            "EmojiWar Mobile",
            "Applied launch mobile defaults.\n\nPortrait-only UI, run-in-background, and Android internet permission are now enabled.",
            "OK");
    }

    [MenuItem("EmojiWar/Mobile/Use Local Supabase (127.0.0.1)")]
    public static void UseLocalSupabase()
    {
        var config = LoadSupabaseConfig();
        if (config == null)
        {
            return;
        }

        SetStringField(config, "projectUrl", "http://127.0.0.1:54321");
        AssetDatabase.SaveAssets();
        Debug.Log("Supabase project URL set to local loopback: http://127.0.0.1:54321");
        EditorUtility.DisplayDialog(
            "EmojiWar Mobile",
            "Supabase project URL set to http://127.0.0.1:54321.\n\nUse this for Unity Editor and Windows standalone on the same machine.",
            "OK");
    }

    [MenuItem("EmojiWar/Mobile/Use This PC LAN Supabase")]
    public static void UseLanSupabase()
    {
        var config = LoadSupabaseConfig();
        if (config == null)
        {
            return;
        }

        if (!TryGetLanIpv4Address(out var ipAddress))
        {
            EditorUtility.DisplayDialog(
                "EmojiWar Mobile",
                "No LAN IPv4 address was found.\n\nConnect the PC to a network and try again, or set the Supabase URL manually.",
                "OK");
            return;
        }

        var lanUrl = $"http://{ipAddress}:54321";
        SetStringField(config, "projectUrl", lanUrl);
        AssetDatabase.SaveAssets();

        Debug.Log($"Supabase project URL set to LAN address: {lanUrl}");
        EditorUtility.DisplayDialog(
            "EmojiWar Mobile",
            $"Supabase project URL set to {lanUrl}.\n\nUse this for Android/iPhone device testing on the same Wi-Fi network.\nKeep the local Supabase stack running.",
            "OK");
    }

    [MenuItem("EmojiWar/Mobile/Validate Android Readiness")]
    public static void ValidateAndroidReadiness()
    {
        var issues = new List<string>();
        var warnings = new List<string>();

        ValidateSharedMobileSettings(issues, warnings);

        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            issues.Add("Android Build Support is not installed in this Unity editor.");
        }

        var androidIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
        if (string.IsNullOrWhiteSpace(androidIdentifier))
        {
            issues.Add($"Android package name is not set. Suggested starting point: {AndroidBundleIdSuggestion}");
        }

        if (PlayerSettings.Android.minSdkVersion < AndroidSdkVersions.AndroidApiLevel26)
        {
            warnings.Add("Android min SDK is below 26. Review this before external device distribution.");
        }

        if (PlayerSettings.Android.targetSdkVersion == AndroidSdkVersions.AndroidApiLevelAuto)
        {
            warnings.Add("Android target SDK is set to Automatic. Confirm this resolves to the intended API level on your build machine.");
        }

        if (!PlayerSettings.Android.forceInternetPermission)
        {
            issues.Add("Android internet permission is not forced. Networked gameplay may fail on device.");
        }

        ShowValidationDialog("Android", issues, warnings);
    }

    [MenuItem("EmojiWar/Mobile/Validate iPhone Readiness")]
    public static void ValidateIPhoneReadiness()
    {
        var issues = new List<string>();
        var warnings = new List<string>();

        ValidateSharedMobileSettings(issues, warnings);

        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
        {
            issues.Add("iOS Build Support is not installed in this Unity editor.");
        }

        if (Application.platform != RuntimePlatform.OSXEditor)
        {
            issues.Add("Unity iPhone build/export requires macOS. This machine cannot produce the final Xcode build.");
        }

        var iosIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.iOS);
        if (string.IsNullOrWhiteSpace(iosIdentifier))
        {
            issues.Add($"iPhone bundle identifier is not set. Suggested starting point: {IOSBundleIdSuggestion}");
        }

        if (string.IsNullOrWhiteSpace(PlayerSettings.iOS.appleDeveloperTeamID))
        {
            warnings.Add("Apple Developer Team ID is not configured. Xcode signing will still need to be set up.");
        }

        ShowValidationDialog("iPhone", issues, warnings);
    }

    [MenuItem("EmojiWar/Mobile/Set Suggested Bundle Identifiers")]
    public static void SetSuggestedBundleIdentifiers()
    {
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AndroidBundleIdSuggestion);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, IOSBundleIdSuggestion);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "EmojiWar Mobile",
            "Set suggested bundle identifiers:\n\nAndroid: com.emojiwar.game\niPhone: com.emojiwar.game\n\nChange these before external distribution.",
            "OK");
    }

    [MenuItem("EmojiWar/Mobile/Build Android APK (Development)")]
    public static void BuildAndroidApkDevelopment()
    {
        var issues = new List<string>();
        var warnings = new List<string>();
        ValidateSharedMobileSettings(issues, warnings);

        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            issues.Add("Android Build Support is not installed in this Unity editor.");
        }

        if (issues.Count > 0)
        {
            ShowValidationDialog("Android Build Blocked", issues, warnings);
            return;
        }

        EnsureBuildOutputFolders();
        var previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
        EditorUserBuildSettings.buildAppBundle = false;

        try
        {
            var outputPath = $"{AndroidOutputFolder}/EmojiWar-android-dev.apk";
            var report = BuildPlayerForTarget(BuildTargetGroup.Android, BuildTarget.Android, outputPath, BuildOptions.Development);
            ShowBuildResult("Android APK", outputPath, report, warnings);
        }
        finally
        {
            EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle;
        }
    }

    [MenuItem("EmojiWar/Mobile/Build Android App Bundle (Release)")]
    public static void BuildAndroidAabRelease()
    {
        var issues = new List<string>();
        var warnings = new List<string>();
        ValidateSharedMobileSettings(issues, warnings);

        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
        {
            issues.Add("Android Build Support is not installed in this Unity editor.");
        }

        if (issues.Count > 0)
        {
            ShowValidationDialog("Android Build Blocked", issues, warnings);
            return;
        }

        EnsureBuildOutputFolders();
        var previousBuildAppBundle = EditorUserBuildSettings.buildAppBundle;
        EditorUserBuildSettings.buildAppBundle = true;

        try
        {
            var outputPath = $"{AndroidOutputFolder}/EmojiWar-android-release.aab";
            var report = BuildPlayerForTarget(BuildTargetGroup.Android, BuildTarget.Android, outputPath, BuildOptions.None);
            ShowBuildResult("Android App Bundle", outputPath, report, warnings);
        }
        finally
        {
            EditorUserBuildSettings.buildAppBundle = previousBuildAppBundle;
        }
    }

    [MenuItem("EmojiWar/Mobile/Export iPhone Xcode Project")]
    public static void ExportIPhoneXcodeProject()
    {
        var issues = new List<string>();
        var warnings = new List<string>();
        ValidateSharedMobileSettings(issues, warnings);

        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
        {
            issues.Add("iOS Build Support is not installed in this Unity editor.");
        }

        if (Application.platform != RuntimePlatform.OSXEditor)
        {
            issues.Add("Xcode export requires Unity running on macOS.");
        }

        if (issues.Count > 0)
        {
            ShowValidationDialog("iPhone Export Blocked", issues, warnings);
            return;
        }

        EnsureBuildOutputFolders();
        var outputPath = IOSOutputFolder;
        var report = BuildPlayerForTarget(BuildTargetGroup.iOS, BuildTarget.iOS, outputPath, BuildOptions.None);
        ShowBuildResult("iPhone Xcode Export", outputPath, report, warnings);
    }

    [MenuItem("EmojiWar/Mobile/Open Build Output Folder")]
    public static void OpenBuildOutputFolder()
    {
        EnsureBuildOutputFolders();
        EditorUtility.RevealInFinder(BuildOutputRoot);
    }

    private static void ValidateSharedMobileSettings(List<string> issues, List<string> warnings)
    {
        var config = LoadSupabaseConfig();
        if (config == null)
        {
            issues.Add($"Missing Supabase config asset at {SupabaseConfigPath}.");
        }
        else
        {
            ValidateSupabaseConfig(config, warnings);
        }

        if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
        {
            issues.Add("Default orientation is not Portrait.");
        }

        if (!PlayerSettings.allowedAutorotateToPortrait ||
            PlayerSettings.allowedAutorotateToPortraitUpsideDown ||
            PlayerSettings.allowedAutorotateToLandscapeLeft ||
            PlayerSettings.allowedAutorotateToLandscapeRight)
        {
            warnings.Add("Autorotation is not locked to portrait-only.");
        }

        if (!PlayerSettings.runInBackground)
        {
            warnings.Add("runInBackground is disabled. Resume/reconnect behavior may be harder to validate.");
        }

        if (string.IsNullOrWhiteSpace(PlayerSettings.companyName) || string.IsNullOrWhiteSpace(PlayerSettings.productName))
        {
            issues.Add("Company name or product name is empty.");
        }
    }

    private static void ValidateSupabaseConfig(SupabaseProjectConfig config, List<string> warnings)
    {
        if (!config.IsConfigured)
        {
            warnings.Add("Supabase config is not fully configured.");
            return;
        }

        if (!Uri.TryCreate(config.ProjectUrl, UriKind.Absolute, out var uri))
        {
            warnings.Add("Supabase project URL is invalid.");
            return;
        }

        if (uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("Supabase project URL still points to localhost. Real Android/iPhone devices will not reach this host.");
        }
    }

    private static void ShowValidationDialog(string target, IReadOnlyList<string> issues, IReadOnlyList<string> warnings)
    {
        var lines = new List<string>();
        if (issues.Count == 0)
        {
            lines.Add("Blocking issues: none");
        }
        else
        {
            lines.Add("Blocking issues:");
            lines.AddRange(issues.Select(issue => $"- {issue}"));
        }

        if (warnings.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Warnings:");
            lines.AddRange(warnings.Select(warning => $"- {warning}"));
        }

        var report = string.Join("\n", lines);
        Debug.Log($"{target} mobile readiness report:\n{report}");

        EditorUtility.DisplayDialog(
            $"EmojiWar {target} Validation",
            report,
            "OK");
    }

    private static BuildReport BuildPlayerForTarget(BuildTargetGroup group, BuildTarget target, string outputPath, BuildOptions options)
    {
        if (EditorUserBuildSettings.activeBuildTarget != target)
        {
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
            {
                throw new InvalidOperationException($"Unable to switch active build target to {target}.");
            }
        }

        var enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        if (enabledScenes.Length == 0)
        {
            throw new InvalidOperationException("No enabled scenes in Build Settings.");
        }

        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = enabledScenes,
            locationPathName = outputPath,
            target = target,
            options = options
        };

        return BuildPipeline.BuildPlayer(buildPlayerOptions);
    }

    private static void ShowBuildResult(string buildType, string outputPath, BuildReport report, IReadOnlyList<string> warnings)
    {
        var summary = report.summary;
        var warningText = warnings.Count == 0
            ? string.Empty
            : $"\n\nPre-build warnings:\n{string.Join("\n", warnings.Select(w => $"- {w}"))}";

        if (summary.result == BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog(
                "EmojiWar Mobile",
                $"{buildType} completed.\n\nOutput:\n{outputPath}\n\nSize: {summary.totalSize / (1024f * 1024f):0.00} MB\nTime: {summary.totalTime.TotalSeconds:0.0}s{warningText}",
                "OK");
            return;
        }

        EditorUtility.DisplayDialog(
            "EmojiWar Mobile",
            $"{buildType} failed.\n\nResult: {summary.result}\nErrors: {summary.totalErrors}\nWarnings: {summary.totalWarnings}{warningText}",
            "OK");
    }

    private static SupabaseProjectConfig LoadSupabaseConfig()
    {
        var config = AssetDatabase.LoadAssetAtPath<SupabaseProjectConfig>(SupabaseConfigPath);
        if (config == null)
        {
            EditorUtility.DisplayDialog(
                "EmojiWar Mobile",
                $"Supabase config was not found at {SupabaseConfigPath}.\nRun EmojiWar -> Setup -> Generate All Starter Assets first.",
                "OK");
        }

        return config;
    }

    private static void SetStringField(UnityEngine.Object target, string propertyName, string value)
    {
        var serializedObject = new SerializedObject(target);
        var property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            return;
        }

        property.stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static bool TryGetLanIpv4Address(out string ipAddress)
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                networkInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            var properties = networkInterface.GetIPProperties();
            foreach (var unicastAddress in properties.UnicastAddresses)
            {
                if (unicastAddress.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                if (IPAddress.IsLoopback(unicastAddress.Address))
                {
                    continue;
                }

                if (IsPrivateIpv4(unicastAddress.Address))
                {
                    ipAddress = unicastAddress.Address.ToString();
                    return true;
                }
            }
        }

        ipAddress = string.Empty;
        return false;
    }

    private static bool IsPrivateIpv4(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }

    private static void EnsureBuildOutputFolders()
    {
        System.IO.Directory.CreateDirectory(BuildOutputRoot);
        System.IO.Directory.CreateDirectory(AndroidOutputFolder);
        System.IO.Directory.CreateDirectory(IOSOutputFolder);
    }
}
