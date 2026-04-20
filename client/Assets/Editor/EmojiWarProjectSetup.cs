using System;
using System.Collections.Generic;
using EmojiWar.Client.Content;
using EmojiWar.Client.Core;
using EmojiWar.Client.Core.Supabase;
using EmojiWar.Client.Gameplay.Bots;
using EmojiWar.Client.UI.Common;
using EmojiWar.Client.UI.Codex;
using EmojiWar.Client.UI.DeckBuilder;
using EmojiWar.Client.UI.Home;
using EmojiWar.Client.UI.Leaderboard;
using EmojiWar.Client.UI.Match;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class EmojiWarProjectSetup
{
    private const string DataFolder = "Assets/Data";
    private const string ConfigFolder = "Assets/Data/Config";
    private const string ContentFolder = "Assets/Data/Content";
    private const string EmojiFolder = "Assets/Data/Content/Emojis";
    private const string BotFolder = "Assets/Data/Bots";
    private const string SceneFolder = "Assets/Scenes";

    private static readonly EmojiSeed[] EmojiSeeds =
    {
        new(EmojiId.Fire, "Fire", EmojiRole.Element, "Burn", "Scorches Plant and melts Ice", "Loses to Water and Shield lines", "Fire applies immediate frontline pressure.", 7, 3, 5, PreferredRow.Front),
        new(EmojiId.Water, "Water", EmojiRole.Element, "Extinguish", "Beats Fire, shorts Lightning, fills Hole", "Loses to Ice and Plant pressure", "Water shuts down heat and unstable energy.", 8, 2, 4, PreferredRow.Front),
        new(EmojiId.Lightning, "Lightning", EmojiRole.Element, "Stun", "Stops Magnet and snaps Ghost", "Loses to Water and Mirror", "Lightning wins on timing and disruption.", 6, 3, 7, PreferredRow.Back),
        new(EmojiId.Ice, "Ice", EmojiRole.Element, "Freeze", "Defuses Bomb and delays Snake", "Loses to Fire and Lightning tempo", "Ice stabilizes explosive lines.", 9, 2, 3, PreferredRow.Front),
        new(EmojiId.Magnet, "Magnet", EmojiRole.Trick, "Pull", "Drags Bomb and strips Shield", "Loses to Lightning, Mirror, and Chain", "Magnet creates the highest-read combo swings.", 6, 3, 6, PreferredRow.Back),
        new(EmojiId.Bomb, "Bomb", EmojiRole.Hazard, "Explode", "Punishes clumps and slow teams", "Loses to Ice, Hole, Chain, and Ghost pressure", "Bomb threatens a full-line collapse.", 5, 4, 4, PreferredRow.Back),
        new(EmojiId.Mirror, "Mirror", EmojiRole.Trick, "Reflect", "Reflects Lightning and Magnet", "Weak to splash, Ghost, and raw frontline damage", "Mirror punishes obvious targeted effects.", 6, 2, 5, PreferredRow.Back),
        new(EmojiId.Hole, "Hole", EmojiRole.Trick, "Delete", "Deletes Bomb and Lightning", "Loses to Water and straightforward bruisers", "Hole hard-stops narrow combo lines.", 7, 2, 4, PreferredRow.Back),
        new(EmojiId.Shield, "Shield", EmojiRole.GuardSupport, "Block", "Blocks first impact and anchors front", "Can be stripped by Magnet or outscaled by Plant", "Shield buys time for the rest of the squad.", 10, 1, 2, PreferredRow.Front),
        new(EmojiId.Snake, "Snake", EmojiRole.StatusRamp, "Poison", "Punishes slow comps and greedy heal lines", "Loses to Soap and Ice delay", "Snake adds delayed kill pressure.", 6, 2, 5, PreferredRow.Back),
        new(EmojiId.Soap, "Soap", EmojiRole.GuardSupport, "Cleanse", "Removes burn, poison, and bind", "Low direct pressure and vulnerable to Ghost", "Soap keeps hidden interactions fair and readable.", 6, 1, 5, PreferredRow.Back),
        new(EmojiId.Plant, "Plant", EmojiRole.StatusRamp, "Grow", "Outscales passive teams if protected", "Loses to Fire, Wind disruption, and Bomb", "Plant rewards protection and patience.", 7, 2, 3, PreferredRow.Back),
        new(EmojiId.Wind, "Wind", EmojiRole.Trick, "Push", "Scatters clumps and punishes backline setups", "Loses to Chain, Shield, and stable frontline lines", "Wind makes formation matter.", 6, 2, 8, PreferredRow.Back),
        new(EmojiId.Heart, "Heart", EmojiRole.GuardSupport, "Heal", "Keeps injured allies alive for another cycle", "Loses to Ghost dives and Lightning disruption", "Heart extends battles without reviving anyone.", 6, 1, 4, PreferredRow.Back),
        new(EmojiId.Ghost, "Ghost", EmojiRole.Trick, "Phase", "Threatens supports and dodges the first hit", "Loses to Lightning, Chain, and disciplined screening", "Ghost slips past slow frontlines.", 5, 3, 8, PreferredRow.Back),
        new(EmojiId.Chain, "Chain", EmojiRole.Trick, "Bind", "Stops Magnet, Wind, Ghost, and Bomb setup", "Weak into Soap cleanses and direct pressure", "Chain is the anti-chaos control piece.", 8, 2, 4, PreferredRow.Front)
    };

    [MenuItem("EmojiWar/Setup/Generate All Starter Assets")]
    public static void GenerateAllStarterAssets()
    {
        EnsureFolders();

        var supabaseConfig = LoadOrCreateAsset<SupabaseProjectConfig>($"{ConfigFolder}/SupabaseProjectConfig.asset");
        InitializeSupabaseConfigDefaults(supabaseConfig);

        var catalog = LoadOrCreateAsset<StaticEmojiCatalog>($"{ContentFolder}/EmojiCatalog.asset");
        var emojiAssets = CreateEmojiAssets();
        AssignCatalogEntries(catalog, emojiAssets);

        CreateBotProfile("PracticeBot", BotDifficulty.Practice, 0.45f, 0.7f, 0.35f);
        CreateBotProfile("SmartBot", BotDifficulty.Smart, 0.7f, 0.55f, 0.8f);

        CreateOrUpdateBootstrapScene(supabaseConfig, catalog);
        CreateSceneIfMissing(SceneNames.Home, CreateHomeScene);
        CreateSceneIfMissing(SceneNames.DeckBuilder, CreateDeckBuilderScene);
        CreateSceneIfMissing(SceneNames.Match, CreateMatchScene);
        CreateSceneIfMissing(SceneNames.Codex, CreateCodexScene);
        CreateSceneIfMissing(SceneNames.Leaderboard, CreateLeaderboardScene);
        ApplyBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("EmojiWar", "Starter assets and scenes generated.", "OK");
    }

    [MenuItem("EmojiWar/Setup/Rebuild V2 Core Scenes (Home + DeckBuilder + Match)")]
    public static void RebuildV2CoreScenes()
    {
        EnsureFolders();

        var supabaseConfig = LoadOrCreateAsset<SupabaseProjectConfig>($"{ConfigFolder}/SupabaseProjectConfig.asset");
        InitializeSupabaseConfigDefaults(supabaseConfig);

        var catalog = LoadOrCreateAsset<StaticEmojiCatalog>($"{ContentFolder}/EmojiCatalog.asset");
        var emojiAssets = CreateEmojiAssets();
        AssignCatalogEntries(catalog, emojiAssets);

        CreateOrUpdateBootstrapScene(supabaseConfig, catalog);
        CreateHomeScene();
        CreateDeckBuilderScene();
        CreateMatchScene();
        ApplyBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "EmojiWar",
            "Rebuilt V2 core scenes: Home, DeckBuilder, Match.\n\n" +
            "This is a forced prefab-first rebuild and overwrites existing scene wiring for those scenes.",
            "OK");
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets", "Data");
        EnsureFolder(DataFolder, "Config");
        EnsureFolder(DataFolder, "Content");
        EnsureFolder($"{DataFolder}/Content", "Emojis");
        EnsureFolder(DataFolder, "Bots");
    }

    private static void EnsureFolder(string parent, string name)
    {
        var fullPath = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(fullPath))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static T LoadOrCreateAsset<T>(string assetPath) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
        if (asset != null)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, assetPath);
        return asset;
    }

    private static List<EmojiDefinition> CreateEmojiAssets()
    {
        var assets = new List<EmojiDefinition>();

        foreach (var seed in EmojiSeeds)
        {
            var assetPath = $"{EmojiFolder}/{seed.Id}.asset";
            var asset = LoadOrCreateAsset<EmojiDefinition>(assetPath);
            SetEnumField(asset, "id", (int)seed.Id);
            SetStringField(asset, "displayName", seed.DisplayName);
            SetEnumField(asset, "role", (int)seed.Role);
            SetStringField(asset, "primaryVerb", seed.PrimaryVerb);
            SetStringField(asset, "strengths", seed.Strengths);
            SetStringField(asset, "weaknesses", seed.Weaknesses);
            SetStringField(asset, "whySummary", seed.WhySummary);
            SetBattleStats(asset, seed.Hp, seed.Attack, seed.Speed, seed.PreferredRow);
            assets.Add(asset);
        }

        return assets;
    }

    private static void AssignCatalogEntries(StaticEmojiCatalog catalog, IReadOnlyList<EmojiDefinition> emojiAssets)
    {
        var serializedObject = new SerializedObject(catalog);
        var listProperty = serializedObject.FindProperty("emojis");
        listProperty.ClearArray();

        for (var index = 0; index < emojiAssets.Count; index++)
        {
            listProperty.InsertArrayElementAtIndex(index);
            listProperty.GetArrayElementAtIndex(index).objectReferenceValue = emojiAssets[index];
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(catalog);
    }

    private static void CreateBotProfile(string assetName, BotDifficulty difficulty, float aggression, float defenseBias, float comboBias)
    {
        var botProfile = LoadOrCreateAsset<BotProfile>($"{BotFolder}/{assetName}.asset");
        SetStringField(botProfile, "id", assetName.ToLowerInvariant());
        SetEnumField(botProfile, "difficulty", (int)difficulty);
        SetFloatField(botProfile, "aggression", aggression);
        SetFloatField(botProfile, "defenseBias", defenseBias);
        SetFloatField(botProfile, "comboBias", comboBias);
    }

    private static void CreateBootstrapScene(SupabaseProjectConfig config, StaticEmojiCatalog catalog)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateCamera();

        var bootstrapObject = new GameObject("AppBootstrap");
        var bootstrap = bootstrapObject.AddComponent<AppBootstrap>();
        var serializedBootstrap = new SerializedObject(bootstrap);
        serializedBootstrap.FindProperty("supabaseConfig").objectReferenceValue = config;
        serializedBootstrap.FindProperty("emojiCatalog").objectReferenceValue = catalog;
        serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

        SaveScene(SceneNames.Bootstrap);
    }

    private static void CreateOrUpdateBootstrapScene(SupabaseProjectConfig config, StaticEmojiCatalog catalog)
    {
        var existingScenePath = $"{SceneFolder}/{SceneNames.Bootstrap}.unity";
        var existing = AssetDatabase.LoadAssetAtPath<SceneAsset>(existingScenePath);
        if (existing == null)
        {
            CreateBootstrapScene(config, catalog);
            return;
        }

        var opened = EditorSceneManager.OpenScene(existingScenePath, OpenSceneMode.Single);
        var bootstrap = UnityEngine.Object.FindObjectOfType<AppBootstrap>();
        if (bootstrap == null)
        {
            bootstrap = new GameObject("AppBootstrap").AddComponent<AppBootstrap>();
        }

        var serialized = new SerializedObject(bootstrap);
        serialized.FindProperty("supabaseConfig").objectReferenceValue = config;
        serialized.FindProperty("emojiCatalog").objectReferenceValue = catalog;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(opened);
    }

    private static void CreateSceneIfMissing(string sceneName, Action createAction)
    {
        var scenePath = $"{SceneFolder}/{sceneName}.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
        {
            Debug.Log($"EmojiWar setup: preserving existing scene wiring for {sceneName}.");
            return;
        }

        createAction.Invoke();
    }

    private static void CreateHomeScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        var canvas = CreateCanvas();
        CreateEventSystem();

        var controller = new GameObject("HomeScreenController").AddComponent<HomeScreenController>();
        var root = CreatePanel("HomePanel", canvas.transform, new Vector2(0.07f, 0.02f), new Vector2(0.93f, 0.98f), Vector2.zero);
        root.GetComponent<Image>().color = new Color(0.06f, 0.12f, 0.2f, 0.95f);

        var heroRoot = CreatePanel("HeroHeader", root.transform, new Vector2(0.08f, 0.80f), new Vector2(0.92f, 0.94f), Vector2.zero);
        heroRoot.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
        CreateAnchoredLabel("Title", heroRoot.transform, "EmojiWar", 72, new Vector2(0.08f, 0.45f), new Vector2(0.92f, 0.90f), TextAnchor.MiddleCenter);
        CreateAnchoredLabel(
            "Subtitle",
            heroRoot.transform,
            "Build a squad. Survive blind ban. Win the auto-battle.",
            23,
            new Vector2(0.08f, 0.08f),
            new Vector2(0.92f, 0.40f),
            TextAnchor.MiddleCenter);

        var profileSummaryLabel = CreateAnchoredLabel(
            "ProfileSummary",
            root.transform,
            "Profile",
            18,
            new Vector2(0.10f, 0.75f),
            new Vector2(0.90f, 0.78f),
            TextAnchor.MiddleCenter);
        profileSummaryLabel.gameObject.SetActive(false);

        var squadRoot = CreatePanel("SquadSection", root.transform, new Vector2(0.10f, 0.63f), new Vector2(0.90f, 0.74f), Vector2.zero);
        squadRoot.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        var deckSummaryLabel = CreateAnchoredLabel(
            "DeckSummary",
            squadRoot.transform,
            "Current Squad",
            19,
            new Vector2(0.08f, 0.74f),
            new Vector2(0.92f, 0.96f),
            TextAnchor.MiddleCenter);

        var squadChipsObject = new GameObject("ActiveSquadChips", typeof(RectTransform), typeof(GridLayoutGroup));
        squadChipsObject.transform.SetParent(squadRoot.transform, false);
        var squadChipsRect = squadChipsObject.GetComponent<RectTransform>();
        squadChipsRect.anchorMin = new Vector2(0.08f, 0.10f);
        squadChipsRect.anchorMax = new Vector2(0.92f, 0.66f);
        squadChipsRect.offsetMin = Vector2.zero;
        squadChipsRect.offsetMax = Vector2.zero;

        var squadLayout = squadChipsObject.GetComponent<GridLayoutGroup>();
        squadLayout.cellSize = new Vector2(172f, 36f);
        squadLayout.spacing = new Vector2(10f, 8f);
        squadLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        squadLayout.constraintCount = 3;
        squadLayout.childAlignment = TextAnchor.MiddleCenter;

        var actionStackObject = new GameObject("ActionStack", typeof(RectTransform), typeof(VerticalLayoutGroup));
        actionStackObject.transform.SetParent(root.transform, false);
        var actionStackRect = actionStackObject.GetComponent<RectTransform>();
        actionStackRect.anchorMin = new Vector2(0.10f, 0.26f);
        actionStackRect.anchorMax = new Vector2(0.90f, 0.61f);
        actionStackRect.offsetMin = Vector2.zero;
        actionStackRect.offsetMax = Vector2.zero;

        var actionLayout = actionStackObject.GetComponent<VerticalLayoutGroup>();
        actionLayout.spacing = 13f;
        actionLayout.padding = new RectOffset(0, 0, 0, 0);
        actionLayout.childAlignment = TextAnchor.UpperCenter;
        actionLayout.childControlWidth = true;
        actionLayout.childControlHeight = false;
        actionLayout.childForceExpandWidth = true;
        actionLayout.childForceExpandHeight = false;

        var rankedButton = CreateButton("Battle Players", actionStackObject.transform, controller.OpenBattlePlayers, 94f, 30);
        var resumeRankedButton = CreateButton("Resume Ranked Match", actionStackObject.transform, controller.OpenResumeRankedMatch, 84f, 23);
        var resumeRankedLabel = resumeRankedButton.GetComponentInChildren<Text>();
        var practiceButton = CreateButton("Battle Bot", actionStackObject.transform, controller.OpenBattleBot, 86f, 27);
        var codexButton = CreateButton("Codex", actionStackObject.transform, controller.OpenCodex, 86f, 27);
        var leaderboardButton = CreateButton("Leaderboard", actionStackObject.transform, controller.OpenLeaderboard, 86f, 27);
        var editDeckButton = CreateButton("Edit Deck", actionStackObject.transform, controller.OpenDeckBuilder, 86f, 27);
        resumeRankedButton.gameObject.SetActive(false);

        var navBar = CreatePanel("BottomNav", root.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 98f));
        var navBarRect = navBar.GetComponent<RectTransform>();
        navBarRect.pivot = new Vector2(0.5f, 0f);
        navBarRect.anchoredPosition = Vector2.zero;
        navBar.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
        var navLayout = navBar.AddComponent<HorizontalLayoutGroup>();
        navLayout.spacing = 8f;
        navLayout.padding = new RectOffset(10, 10, 10, 10);
        navLayout.childAlignment = TextAnchor.MiddleCenter;
        navLayout.childControlWidth = true;
        navLayout.childControlHeight = true;
        navLayout.childForceExpandWidth = true;
        navLayout.childForceExpandHeight = true;
        CreateButton("Home", navBar.transform, null, 78f, 20);
        CreateButton("Squad", navBar.transform, controller.OpenDeckBuilder, 78f, 20);
        CreateButton("Codex", navBar.transform, controller.OpenCodex, 78f, 20);
        CreateButton("Profile", navBar.transform, controller.OpenProfilePlaceholder, 78f, 20);

        var starterPromptRoot = CreatePanel("StarterPromptPanel", root.transform, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.75f), Vector2.zero);
        starterPromptRoot.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.14f, 0.96f);
        var promptLayout = starterPromptRoot.AddComponent<VerticalLayoutGroup>();
        promptLayout.spacing = 18f;
        promptLayout.padding = new RectOffset(40, 40, 44, 44);
        promptLayout.childAlignment = TextAnchor.UpperCenter;
        promptLayout.childControlWidth = true;
        promptLayout.childControlHeight = false;
        promptLayout.childForceExpandWidth = true;
        promptLayout.childForceExpandHeight = false;
        CreateLabel("StarterPromptTitle", starterPromptRoot.transform, "Starter Deck Ready", 54, 110f);
        var starterPromptLabel = CreateLabel("StarterPromptBody", starterPromptRoot.transform, "Edit your starter squad or jump straight into practice.", 30, 180f);
        CreateButton("Edit Deck", starterPromptRoot.transform, controller.OpenStarterDeckBuilder, 96f, 30);
        CreateButton("Play Bot", starterPromptRoot.transform, controller.OpenStarterBotMatch, 96f, 30);
        starterPromptRoot.SetActive(false);

        var profilePanel = CreatePanel("ProfilePlaceholderPanel", root.transform, new Vector2(0.10f, 0.27f), new Vector2(0.90f, 0.65f), Vector2.zero);
        profilePanel.GetComponent<Image>().color = new Color(0.05f, 0.08f, 0.14f, 0.96f);
        var profileLabel = CreateAnchoredLabel(
            "ProfilePanelText",
            profilePanel.transform,
            "Profile placeholder",
            26,
            new Vector2(0.08f, 0.20f),
            new Vector2(0.92f, 0.80f),
            TextAnchor.MiddleCenter);
        profileLabel.alignment = TextAnchor.MiddleCenter;
        profilePanel.SetActive(false);

        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("profileSummaryLabel").objectReferenceValue = profileSummaryLabel;
        serializedController.FindProperty("deckSummaryLabel").objectReferenceValue = deckSummaryLabel;
        serializedController.FindProperty("sceneSquadChipsContainer").objectReferenceValue = squadChipsRect;
        serializedController.FindProperty("resumeRankedRoot").objectReferenceValue = resumeRankedButton.gameObject;
        serializedController.FindProperty("resumeRankedLabel").objectReferenceValue = resumeRankedLabel;
        serializedController.FindProperty("starterPromptRoot").objectReferenceValue = starterPromptRoot;
        serializedController.FindProperty("starterPromptLabel").objectReferenceValue = starterPromptLabel;
        serializedController.FindProperty("rankedButton").objectReferenceValue = rankedButton;
        serializedController.FindProperty("practiceButton").objectReferenceValue = practiceButton;
        serializedController.FindProperty("codexButton").objectReferenceValue = codexButton;
        serializedController.FindProperty("leaderboardButton").objectReferenceValue = leaderboardButton;
        serializedController.FindProperty("editDeckButton").objectReferenceValue = editDeckButton;
        serializedController.FindProperty("homePanelBackground").objectReferenceValue = root.GetComponent<Image>();
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        SaveScene(SceneNames.Home);
    }

    private static void CreateDeckBuilderScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        var canvas = CreateCanvas();
        CreateEventSystem();

        var controller = new GameObject("DeckBuilderController").AddComponent<DeckBuilderController>();
        var root = CreatePanel("DeckBuilderPanel", canvas.transform, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.98f), Vector2.zero);
        root.GetComponent<Image>().color = new Color(0.07f, 0.12f, 0.2f, 0.95f);

        CreateAnchoredLabel("DeckBuilderTitle", root.transform, "Deck Builder", 70, new Vector2(0.08f, 0.91f), new Vector2(0.92f, 0.98f), TextAnchor.MiddleCenter);
        var phaseStepper = CreatePhaseBarWidget("DeckPhaseStepper", root.transform);
        SetAnchors(phaseStepper.transform as RectTransform, new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.90f));
        var deckSummaryLabel = CreateAnchoredLabel("DeckSummary", root.transform, "Selected 0/6", 30, new Vector2(0.08f, 0.81f), new Vector2(0.92f, 0.85f), TextAnchor.MiddleCenter);
        var statusChip = CreateStatusChipWidget("DeckStatusChip", root.transform);
        SetAnchors(statusChip.transform as RectTransform, new Vector2(0.20f, 0.76f), new Vector2(0.80f, 0.80f));
        var statusLabel = CreateAnchoredLabel("DeckStatus", root.transform, string.Empty, 22, new Vector2(0.08f, 0.76f), new Vector2(0.92f, 0.80f), TextAnchor.MiddleCenter);
        statusLabel.gameObject.SetActive(false);

        var trayPanel = CreatePanel("SelectedTrayPanel", root.transform, new Vector2(0.08f, 0.69f), new Vector2(0.92f, 0.76f), Vector2.zero);
        trayPanel.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        var selectedTrayLabel = CreateAnchoredLabel("SelectedTray", trayPanel.transform, "Selected tray", 20, new Vector2(0.06f, 0.62f), new Vector2(0.94f, 0.98f), TextAnchor.MiddleCenter);
        selectedTrayLabel.gameObject.SetActive(false);
        var selectedTrayCards = CreateSelectedTrayContainer("SelectedTrayCards", trayPanel.transform);
        SetAnchors(selectedTrayCards, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.62f));

        var emojiButtons = CreateAnchoredScrollableChoiceGrid(root.transform, 16, 2, new Vector2(0.08f, 0.22f), new Vector2(0.92f, 0.67f), out var emojiLabels);

        var footerPanel = CreatePanel("DeckFooter", root.transform, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.18f), Vector2.zero);
        footerPanel.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);
        var footerLayout = footerPanel.AddComponent<VerticalLayoutGroup>();
        footerLayout.spacing = 10f;
        footerLayout.padding = new RectOffset(0, 0, 0, 0);
        footerLayout.childAlignment = TextAnchor.MiddleCenter;
        footerLayout.childControlWidth = true;
        footerLayout.childControlHeight = false;
        footerLayout.childForceExpandWidth = true;
        footerLayout.childForceExpandHeight = false;

        var saveButton = CreateButton("Save Active Deck", footerPanel.transform, null, 86f, 31);
        var backButton = CreateButton("Back Home", footerPanel.transform, null, 76f, 27);
        var stickyPrimary = AttachStickyPrimaryAction(saveButton);

        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("deckSummaryLabel").objectReferenceValue = deckSummaryLabel;
        serializedController.FindProperty("selectedTrayLabel").objectReferenceValue = selectedTrayLabel;
        serializedController.FindProperty("selectedTrayContainer").objectReferenceValue = selectedTrayCards;
        serializedController.FindProperty("statusLabel").objectReferenceValue = statusLabel;
        serializedController.FindProperty("phaseStepper").objectReferenceValue = phaseStepper;
        serializedController.FindProperty("statusChip").objectReferenceValue = statusChip;
        serializedController.FindProperty("stickyPrimaryAction").objectReferenceValue = stickyPrimary;
        AssignObjectArray(serializedController.FindProperty("emojiButtons"), emojiButtons);
        AssignObjectArray(serializedController.FindProperty("emojiButtonLabels"), emojiLabels);
        serializedController.FindProperty("saveButton").objectReferenceValue = saveButton;
        serializedController.FindProperty("saveButtonLabel").objectReferenceValue = saveButton.GetComponentInChildren<Text>();
        serializedController.FindProperty("backButton").objectReferenceValue = backButton;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        SaveScene(SceneNames.DeckBuilder);
    }

    private static void CreateMatchScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        var canvas = CreateCanvas();
        CreateEventSystem();

        var controller = new GameObject("MatchScreenController").AddComponent<MatchScreenController>();
        var root = CreatePanel("MatchPanel", canvas.transform, new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.98f), Vector2.zero);
        root.GetComponent<Image>().color = new Color(0.07f, 0.12f, 0.2f, 0.95f);

        var scoreText = CreateAnchoredLabel("ScoreLabel", root.transform, "Ranked PvP", 60, new Vector2(0.08f, 0.92f), new Vector2(0.92f, 0.98f), TextAnchor.MiddleCenter);
        var phaseStepper = CreatePhaseBarWidget("MatchPhaseStepper", root.transform);
        SetAnchors(phaseStepper.transform as RectTransform, new Vector2(0.08f, 0.87f), new Vector2(0.92f, 0.91f));
        var whyText = CreateAnchoredLabel("WhyLabel", root.transform, "Queue", 28, new Vector2(0.08f, 0.81f), new Vector2(0.92f, 0.86f), TextAnchor.MiddleCenter);
        var statusChip = CreateStatusChipWidget("MatchStatusChip", root.transform);
        SetAnchors(statusChip.transform as RectTransform, new Vector2(0.18f, 0.76f), new Vector2(0.82f, 0.80f));

        var detailPanel = CreatePanel("MatchDetailPanel", root.transform, new Vector2(0.08f, 0.55f), new Vector2(0.92f, 0.75f), Vector2.zero);
        detailPanel.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
        var whyChainText = CreateAnchoredLabel("WhyChainLabel", detailPanel.transform, "Battle state will appear here.", 24, new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f), TextAnchor.UpperLeft);
        whyChainText.horizontalOverflow = HorizontalWrapMode.Wrap;
        whyChainText.verticalOverflow = VerticalWrapMode.Overflow;

        var decisiveStrip = CreateDecisiveMomentsStrip("DecisiveMomentStrip", root.transform);
        SetAnchors(decisiveStrip, new Vector2(0.08f, 0.50f), new Vector2(0.92f, 0.54f));

        var choiceButtons = CreateAnchoredChoiceList(root.transform, 6, new Vector2(0.08f, 0.19f), new Vector2(0.92f, 0.48f), out var choiceLabels);
        var actionButton = CreateAnchoredButton("Continue", root.transform, null, new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.15f), 34);
        var stickyPrimary = AttachStickyPrimaryAction(actionButton);

        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("scoreLabel").objectReferenceValue = scoreText;
        serializedController.FindProperty("whyLabel").objectReferenceValue = whyText;
        serializedController.FindProperty("whyChainLabel").objectReferenceValue = whyChainText;
        serializedController.FindProperty("panelBackground").objectReferenceValue = root.GetComponent<Image>();
        serializedController.FindProperty("phaseStepper").objectReferenceValue = phaseStepper;
        serializedController.FindProperty("statusChip").objectReferenceValue = statusChip;
        serializedController.FindProperty("decisiveMomentsStrip").objectReferenceValue = decisiveStrip;
        serializedController.FindProperty("stickyPrimaryAction").objectReferenceValue = stickyPrimary;
        AssignObjectArray(serializedController.FindProperty("choiceButtons"), choiceButtons);
        AssignObjectArray(serializedController.FindProperty("choiceButtonLabels"), choiceLabels);
        serializedController.FindProperty("actionButton").objectReferenceValue = actionButton;
        serializedController.FindProperty("actionButtonLabel").objectReferenceValue = actionButton.GetComponentInChildren<Text>();
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        SaveScene(SceneNames.Match);
    }

    private static Button[] CreateAnchoredChoiceList(Transform parent, int count, Vector2 anchorMin, Vector2 anchorMax, out Text[] labels)
    {
        var root = new GameObject("ChoicesList", typeof(RectTransform), typeof(VerticalLayoutGroup));
        root.transform.SetParent(parent, false);
        var rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = anchorMin;
        rootRect.anchorMax = anchorMax;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var layout = root.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var buttons = new Button[count];
        labels = new Text[count];
        for (var index = 0; index < count; index++)
        {
            buttons[index] = CreateButton($"Choice {index + 1}", root.transform, null, 78f, 28);
            labels[index] = buttons[index].GetComponentInChildren<Text>();
        }

        return buttons;
    }

    private static Button[] CreateAnchoredScrollableChoiceGrid(
        Transform parent,
        int count,
        int columns,
        Vector2 anchorMin,
        Vector2 anchorMax,
        out Text[] labels)
    {
        var scrollObject = new GameObject("ChoicesGridScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        scrollObject.transform.SetParent(parent, false);
        var scrollRectTransform = scrollObject.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = anchorMin;
        scrollRectTransform.anchorMax = anchorMax;
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = Vector2.zero;

        var scrollImage = scrollObject.GetComponent<Image>();
        scrollImage.color = new Color(1f, 1f, 1f, 0.05f);

        var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(scrollObject.transform, false);
        var viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(8f, 8f);
        viewportRect.offsetMax = new Vector2(-8f, -8f);
        viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        var contentObject = new GameObject("ChoicesGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        var contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        var grid = contentObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(380f, 86f);
        grid.spacing = new Vector2(12f, 12f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;

        var fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 28f;
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        var buttons = new Button[count];
        labels = new Text[count];
        for (var index = 0; index < count; index++)
        {
            buttons[index] = CreateButton($"Emoji {index + 1}", contentObject.transform, null, 86f, 24);
            labels[index] = buttons[index].GetComponentInChildren<Text>();
        }

        return buttons;
    }

    private static Button CreateAnchoredButton(
        string text,
        Transform parent,
        UnityAction onClick,
        Vector2 anchorMin,
        Vector2 anchorMax,
        int fontSize = 30)
    {
        var button = CreateButton(text, parent, onClick, 96f, fontSize);
        SetAnchors(button.transform as RectTransform, anchorMin, anchorMax);
        var layout = button.GetComponent<LayoutElement>();
        if (layout != null)
        {
            UnityEngine.Object.DestroyImmediate(layout);
        }

        return button;
    }

    private static Text CreateAnchoredLabel(
        string name,
        Transform parent,
        string text,
        int fontSize,
        Vector2 anchorMin,
        Vector2 anchorMax,
        TextAnchor alignment)
    {
        var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = anchorMin;
        labelRect.anchorMax = anchorMax;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var label = labelObject.GetComponent<Text>();
        label.text = text;
        label.font = GetUiFont();
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.lineSpacing = 1.1f;
        return label;
    }

    private static void SetAnchors(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void CreateCodexScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        var canvas = CreateCanvas();
        CreateEventSystem();

        var controller = new GameObject("CodexScreenController").AddComponent<CodexScreenController>();
        var root = CreateContentPanel(canvas.transform, "CodexPanel", new Vector2(920f, 1560f), 22f, 52, 52, 72, 72);
        CreateLabel("CodexTitle", root.transform, "Codex", 72, 120f);
        var summaryLabel = CreateLabel("CodexSummary", root.transform, "Loading Codex...", 34, 120f);
        var entriesLabel = CreateScrollableTextArea(
            "CodexEntriesArea",
            root.transform,
            "CodexEntries",
            "Unlocked interactions will appear here.",
            24,
            980f);
        var backButton = CreateButton("Back Home", root.transform, null, 104f, 36);

        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("summaryLabel").objectReferenceValue = summaryLabel;
        serializedController.FindProperty("entriesLabel").objectReferenceValue = entriesLabel;
        serializedController.FindProperty("backButton").objectReferenceValue = backButton;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        SaveScene(SceneNames.Codex);
    }

    private static void CreateLeaderboardScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        CreateCamera();
        var canvas = CreateCanvas();
        CreateEventSystem();

        var controller = new GameObject("LeaderboardScreenController").AddComponent<LeaderboardScreenController>();
        var root = CreateContentPanel(canvas.transform, "LeaderboardPanel", new Vector2(920f, 1560f), 22f, 52, 52, 72, 72);
        CreateLabel("LeaderboardTitle", root.transform, "Leaderboard", 72, 120f);
        var summaryLabel = CreateLabel("LeaderboardSummary", root.transform, "Loading leaderboard...", 34, 140f);
        var entriesLabel = CreateScrollableTextArea(
            "LeaderboardEntriesArea",
            root.transform,
            "LeaderboardEntries",
            "Ranked standings will appear here.",
            24,
            980f);
        var backButton = CreateButton("Back Home", root.transform, null, 104f, 36);

        var serializedController = new SerializedObject(controller);
        serializedController.FindProperty("summaryLabel").objectReferenceValue = summaryLabel;
        serializedController.FindProperty("entriesLabel").objectReferenceValue = entriesLabel;
        serializedController.FindProperty("backButton").objectReferenceValue = backButton;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        SaveScene(SceneNames.Leaderboard);
    }

    private static void SaveScene(string sceneName)
    {
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), $"{SceneFolder}/{sceneName}.unity");
    }

    private static void ApplyBuildSettings()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene($"{SceneFolder}/{SceneNames.Bootstrap}.unity", true),
            new EditorBuildSettingsScene($"{SceneFolder}/{SceneNames.Home}.unity", true),
            new EditorBuildSettingsScene($"{SceneFolder}/{SceneNames.DeckBuilder}.unity", true),
            new EditorBuildSettingsScene($"{SceneFolder}/{SceneNames.Match}.unity", true),
            new EditorBuildSettingsScene($"{SceneFolder}/{SceneNames.Codex}.unity", true),
            new EditorBuildSettingsScene($"{SceneFolder}/{SceneNames.Leaderboard}.unity", true)
        };
    }

    private static Camera CreateCamera()
    {
        var cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        var camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
        camera.orthographic = true;
        return camera;
    }

    private static Canvas CreateCanvas()
    {
        var canvasObject = new GameObject("Canvas");
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static Font GetUiFont()
    {
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static void CreateEventSystem()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static GameObject CreatePanel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        var rectTransform = panel.GetComponent<RectTransform>();
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = sizeDelta;
        panel.GetComponent<Image>().color = new Color(0.11f, 0.14f, 0.18f, 0.85f);
        return panel;
    }

    private static GameObject CreateContentPanel(
        Transform parent,
        string name,
        Vector2 sizeDelta,
        float spacing = 28f,
        int paddingLeft = 60,
        int paddingRight = 60,
        int paddingTop = 100,
        int paddingBottom = 100)
    {
        var root = CreatePanel(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), sizeDelta);
        var layout = root.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.padding = new RectOffset(paddingLeft, paddingRight, paddingTop, paddingBottom);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        root.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        return root;
    }

    private static Text CreateLabel(
        string name,
        Transform parent,
        string text,
        int fontSize,
        float preferredHeight = 120f,
        TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var labelObject = new GameObject(name, typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(parent, false);
        var layoutElement = labelObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.minHeight = preferredHeight;

        var label = labelObject.GetComponent<Text>();
        label.text = text;
        label.font = GetUiFont();
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.lineSpacing = 1.15f;
        return label;
    }

    private static Text CreateScrollableTextArea(
        string areaName,
        Transform parent,
        string textName,
        string initialText,
        int fontSize,
        float preferredHeight)
    {
        var areaObject = new GameObject(areaName, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(ScrollRect));
        areaObject.transform.SetParent(parent, false);

        var layoutElement = areaObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.minHeight = preferredHeight;

        var areaImage = areaObject.GetComponent<Image>();
        areaImage.color = new Color(0.08f, 0.1f, 0.14f, 0.45f);

        var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(areaObject.transform, false);
        var viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(16f, 16f);
        viewportRect.offsetMax = new Vector2(-16f, -16f);
        viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        var contentObject = new GameObject(textName, typeof(RectTransform), typeof(Text), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        var contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        var label = contentObject.GetComponent<Text>();
        label.text = initialText;
        label.font = GetUiFont();
        label.fontSize = fontSize;
        label.alignment = TextAnchor.UpperLeft;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.lineSpacing = 1.15f;

        var fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = areaObject.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        return label;
    }

    private static Button[] CreateChoiceList(Transform parent, int count, out Text[] labels)
    {
        var listObject = new GameObject("ChoicesList", typeof(RectTransform), typeof(LayoutElement), typeof(VerticalLayoutGroup));
        listObject.transform.SetParent(parent, false);
        var layoutElement = listObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 860f;
        layoutElement.minHeight = 860f;

        var layout = listObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.padding = new RectOffset(0, 0, 0, 0);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var buttons = new Button[count];
        labels = new Text[count];

        for (var index = 0; index < count; index++)
        {
            buttons[index] = CreateButton($"Choice {index + 1}", listObject.transform, null, 96f, 34);
            labels[index] = buttons[index].GetComponentInChildren<Text>();
        }

        return buttons;
    }

    private static Button[] CreateChoiceGrid(Transform parent, int count, int columns, out Text[] labels)
    {
        var gridObject = new GameObject("ChoicesGrid", typeof(RectTransform), typeof(LayoutElement), typeof(GridLayoutGroup));
        gridObject.transform.SetParent(parent, false);

        var layoutElement = gridObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 760f;
        layoutElement.minHeight = 760f;

        var grid = gridObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(418f, 110f);
        grid.spacing = new Vector2(16f, 16f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;

        var buttons = new Button[count];
        labels = new Text[count];

        for (var index = 0; index < count; index++)
        {
            buttons[index] = CreateButton($"Emoji {index + 1}", gridObject.transform, null, 110f, 28);
            labels[index] = buttons[index].GetComponentInChildren<Text>();
        }

        return buttons;
    }

    private static Button[] CreateScrollableChoiceGrid(Transform parent, int count, int columns, float preferredHeight, out Text[] labels)
    {
        var scrollObject = new GameObject("ChoicesGridScrollView", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(ScrollRect));
        scrollObject.transform.SetParent(parent, false);

        var layoutElement = scrollObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.minHeight = preferredHeight;
        layoutElement.flexibleHeight = 1f;

        var scrollImage = scrollObject.GetComponent<Image>();
        scrollImage.color = new Color(0.08f, 0.1f, 0.14f, 0.3f);

        var viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportObject.transform.SetParent(scrollObject.transform, false);
        var viewportRect = viewportObject.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(12f, 12f);
        viewportRect.offsetMax = new Vector2(-12f, -12f);
        viewportObject.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);

        var contentObject = new GameObject("ChoicesGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewportObject.transform, false);
        var contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        var grid = contentObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(406f, 110f);
        grid.spacing = new Vector2(16f, 16f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        grid.childAlignment = TextAnchor.UpperCenter;

        var fitter = contentObject.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollRect = scrollObject.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;
        scrollRect.viewport = viewportRect;
        scrollRect.content = contentRect;

        var buttons = new Button[count];
        labels = new Text[count];

        for (var index = 0; index < count; index++)
        {
            buttons[index] = CreateButton($"Emoji {index + 1}", contentObject.transform, null, 110f, 28);
            labels[index] = buttons[index].GetComponentInChildren<Text>();
        }

        return buttons;
    }

    private static Button CreateButton(string text, Transform parent, UnityAction onClick, float preferredHeight = 150f, int fontSize = 40)
    {
        var buttonObject = new GameObject($"{text}Button", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = new Color(0.18f, 0.29f, 0.42f, 1f);
        var layoutElement = buttonObject.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.minHeight = preferredHeight;

        var button = buttonObject.GetComponent<Button>();
        if (onClick != null)
        {
            UnityEventTools.AddPersistentListener(button.onClick, onClick);
        }

        var label = CreateLabel($"{text}Label", buttonObject.transform, text, fontSize);
        var rectTransform = label.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        return button;
    }

    private static PhaseBar CreatePhaseBarWidget(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(PhaseBar));
        root.transform.SetParent(parent, false);

        var layout = root.GetComponent<LayoutElement>();
        layout.preferredHeight = 56f;
        layout.minHeight = 56f;

        var background = root.GetComponent<Image>();
        background.color = new Color(0.12f, 0.19f, 0.31f, 0.92f);

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(root.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 6f);
        labelRect.offsetMax = new Vector2(-8f, -6f);

        var label = labelObject.GetComponent<Text>();
        label.text = "[Squad] Ban Form Result";
        label.font = GetUiFont();
        label.fontSize = 20;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;

        var phaseBar = root.GetComponent<PhaseBar>();
        var serialized = new SerializedObject(phaseBar);
        serialized.FindProperty("label").objectReferenceValue = label;
        serialized.FindProperty("background").objectReferenceValue = background;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return phaseBar;
    }

    private static StatusChip CreateStatusChipWidget(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(StatusChip));
        root.transform.SetParent(parent, false);

        var layout = root.GetComponent<LayoutElement>();
        layout.preferredHeight = 48f;
        layout.minHeight = 48f;

        var background = root.GetComponent<Image>();
        background.color = new Color(0.16f, 0.26f, 0.41f, 0.9f);

        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelObject.transform.SetParent(root.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 4f);
        labelRect.offsetMax = new Vector2(-8f, -4f);

        var label = labelObject.GetComponent<Text>();
        label.text = string.Empty;
        label.font = GetUiFont();
        label.fontSize = 18;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;

        var chip = root.GetComponent<StatusChip>();
        var serialized = new SerializedObject(chip);
        serialized.FindProperty("label").objectReferenceValue = label;
        serialized.FindProperty("background").objectReferenceValue = background;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        return chip;
    }

    private static RectTransform CreateSelectedTrayContainer(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        root.transform.SetParent(parent, false);

        var layoutElement = root.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 70f;
        layoutElement.minHeight = 70f;

        var layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        return root.GetComponent<RectTransform>();
    }

    private static RectTransform CreateDecisiveMomentsStrip(string name, Transform parent)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        root.transform.SetParent(parent, false);

        var layoutElement = root.GetComponent<LayoutElement>();
        layoutElement.preferredHeight = 56f;
        layoutElement.minHeight = 56f;

        var layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        root.SetActive(false);
        return root.GetComponent<RectTransform>();
    }

    private static StickyPrimaryAction AttachStickyPrimaryAction(Button button)
    {
        if (button == null)
        {
            return null;
        }

        var sticky = button.gameObject.GetComponent<StickyPrimaryAction>();
        if (sticky == null)
        {
            sticky = button.gameObject.AddComponent<StickyPrimaryAction>();
        }

        var motion = button.gameObject.GetComponent<UiMotionController>();
        if (motion == null)
        {
            motion = button.gameObject.AddComponent<UiMotionController>();
        }

        var serialized = new SerializedObject(sticky);
        serialized.FindProperty("button").objectReferenceValue = button;
        serialized.FindProperty("label").objectReferenceValue = button.GetComponentInChildren<Text>();
        serialized.FindProperty("background").objectReferenceValue = button.GetComponent<Image>();
        serialized.FindProperty("motionController").objectReferenceValue = motion;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return sticky;
    }

    private static void AssignObjectArray(SerializedProperty arrayProperty, UnityEngine.Object[] objects)
    {
        arrayProperty.arraySize = objects.Length;

        for (var index = 0; index < objects.Length; index++)
        {
            arrayProperty.GetArrayElementAtIndex(index).objectReferenceValue = objects[index];
        }
    }

    private static void SetStringField(UnityEngine.Object target, string propertyName, string value)
    {
        var serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).stringValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void InitializeSupabaseConfigDefaults(SupabaseProjectConfig config)
    {
        EnsureStringField(config, "projectUrl", "http://127.0.0.1:54321");
        EnsureStringField(config, "anonKey", "sb_publishable_ACJWlzQHlZjBrEguHvfOxg_3BJgxAaH", SupabaseProjectConfig.LocalPlaceholderAnonKey);
        EnsureStringField(config, "functionsPath", "/functions/v1/");
    }

    private static void EnsureStringField(UnityEngine.Object target, string propertyName, string defaultValue, string invalidSentinel = "")
    {
        var serializedObject = new SerializedObject(target);
        var property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(property.stringValue) || (!string.IsNullOrWhiteSpace(invalidSentinel) && property.stringValue == invalidSentinel))
        {
            property.stringValue = defaultValue;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
    }

    private static void SetFloatField(UnityEngine.Object target, string propertyName, float value)
    {
        var serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).floatValue = value;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetEnumField(UnityEngine.Object target, string propertyName, int enumValueIndex)
    {
        var serializedObject = new SerializedObject(target);
        serializedObject.FindProperty(propertyName).enumValueIndex = enumValueIndex;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private static void SetBattleStats(EmojiDefinition target, int hp, int attack, int speed, PreferredRow preferredRow)
    {
        var serializedObject = new SerializedObject(target);
        var battleStats = serializedObject.FindProperty("battleStats");
        battleStats.FindPropertyRelative("hp").intValue = hp;
        battleStats.FindPropertyRelative("attack").intValue = attack;
        battleStats.FindPropertyRelative("speed").intValue = speed;
        battleStats.FindPropertyRelative("preferredRow").enumValueIndex = (int)preferredRow;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
    }

    private readonly struct EmojiSeed
    {
        public EmojiSeed(EmojiId id, string displayName, EmojiRole role, string primaryVerb, string strengths, string weaknesses, string whySummary, int hp, int attack, int speed, PreferredRow preferredRow)
        {
            Id = id;
            DisplayName = displayName;
            Role = role;
            PrimaryVerb = primaryVerb;
            Strengths = strengths;
            Weaknesses = weaknesses;
            WhySummary = whySummary;
            Hp = hp;
            Attack = attack;
            Speed = speed;
            PreferredRow = preferredRow;
        }

        public EmojiId Id { get; }
        public string DisplayName { get; }
        public EmojiRole Role { get; }
        public string PrimaryVerb { get; }
        public string Strengths { get; }
        public string Weaknesses { get; }
        public string WhySummary { get; }
        public int Hp { get; }
        public int Attack { get; }
        public int Speed { get; }
        public PreferredRow PreferredRow { get; }
    }
}
