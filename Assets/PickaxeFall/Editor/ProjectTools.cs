using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall.Editor
{
    public static class ProjectTools
    {
        public const string ScenePath = "Assets/PickaxeFall/Scenes/Main.unity";
        const string DataPath = "Assets/PickaxeFall/Data/DefaultBalance.asset";
        const string PanelPath = "Assets/PickaxeFall/UI/GamePanel.asset";

        [MenuItem("PickaxeFall/Create or rebuild main scene")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/PickaxeFall/Scenes");
            Directory.CreateDirectory("Assets/PickaxeFall/Data"); AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var balance = AssetDatabase.LoadAssetAtPath<GameBalance>(DataPath);
            if (!balance) { balance = ScriptableObject.CreateInstance<GameBalance>(); AssetDatabase.CreateAsset(balance, DataPath); }
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (!panel)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1080, 1920);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = 0; AssetDatabase.CreateAsset(panel, PanelPath);
            }
            // Runtime UI Toolkit needs a theme even when all custom styles live in USS.
            if (!panel.themeStyleSheet)
            {
                const string themePath = "Assets/PickaxeFall/UI/RuntimeTheme.tss";
                if (!File.Exists(themePath)) File.WriteAllText(themePath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(themePath);
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            }
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/PickaxeFall/UI/GameHud.uss");
            var root = new GameObject("PickaxeFall");
            root.AddComponent<GameController>().Configure(balance, panel, style);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            PlayerSettings.companyName = "EnotikPoker"; PlayerSettings.productName = "PickaxeFall";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.enotikpoker.pickaxefall");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.defaultScreenWidth = 540; PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.optimizedFramePacing = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            // Keep the built-in input path for keyboard + UI Toolkit without extra packages.
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = settings.FindProperty("activeInputHandler");
            if (input != null) { input.intValue = 0; settings.ApplyModifiedPropertiesWithoutUndo(); }
            EditorSettings.serializationMode = SerializationMode.ForceText;
            Time.fixedDeltaTime = 1f / 60;
            Physics2D.gravity = new Vector2(0, balance.gravity);
            QualitySettings.vSyncCount = 0; QualitySettings.antiAliasing = 0;
            QualitySettings.shadows = ShadowQuality.Disable;
            EditorUtility.SetDirty(balance); EditorUtility.SetDirty(panel); AssetDatabase.SaveAssets();
            ValidateBalance();
            Debug.Log("PICKAXEFALL_SETUP_OK: main scene, balance, UI and Android portrait settings saved.");
        }

        [MenuItem("PickaxeFall/Validate game balance")]
        public static void ValidateBalance()
        {
            var balance = AssetDatabase.LoadAssetAtPath<GameBalance>(DataPath);
            Require(balance && balance.pickaxes.Length == 5, "Five pickaxe tiers must exist.");
            Require(balance.blocks.Length == Enum.GetValues(typeof(BlockKind)).Length, "Every block kind needs a balance entry.");
            float oreChance = 0;
            for (int i = 0; i < balance.blocks.Length; i++)
            {
                var block = balance.blocks[i];
                Require((int)block.kind == i && block.health > 0 && block.reward >= 0, "Block entries must follow enum order and contain valid values.");
                oreChance += block.oreChance;
            }
            Require(oreChance <= 1, "Ore probabilities cannot exceed 100%.");
            int[] counts = new int[balance.pickaxes.Length]; var random = new System.Random(8127);
            for (int i = 0; i < 100000; i++) counts[balance.Roll(random)]++;
            float total = 0;
            foreach (var tier in balance.pickaxes)
            {
                Require(tier.damage > 0 && tier.durability > 0 && tier.weight > 0, "Tier values must be positive.");
                total += tier.weight;
            }
            for (int i = 0; i < counts.Length; i++)
                Require(Math.Abs(counts[i] / 100000f - balance.pickaxes[i].weight / total) < 0.008f, "Roulette weight mismatch.");
            foreach (int depth in new[] { 0, 5, 15, 30, 45, 70 })
            {
                var first = new System.Random(73); var second = new System.Random(73);
                bool sawDiamond = false;
                for (int i = 0; i < 2000; i++)
                {
                    var block = balance.ChooseBlock(first, depth);
                    Require(block == balance.ChooseBlock(second, depth), "World generation must be deterministic.");
                    Require(depth >= 65 || block != BlockKind.Diamond, "Diamonds appeared too early.");
                    Require(depth >= 40 || block != BlockKind.Gold, "Gold appeared too early.");
                    Require(depth >= 22 || block != BlockKind.Iron, "Iron appeared too early.");
                    Require(depth >= 10 || block != BlockKind.Copper, "Copper appeared too early.");
                    sawDiamond |= block == BlockKind.Diamond;
                }
                if (depth == 70) Require(sawDiamond, "Deep layers must contain diamonds.");
            }
            Debug.Log("PICKAXEFALL_BALANCE_OK: 100000 roulette rolls; deterministic generation and ore depth gates.");
        }

        [MenuItem("PickaxeFall/Build Linux smoke-test player")]
        public static void BuildLinux()
        {
            ValidateBalance();
            Build(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, target = BuildTarget.StandaloneLinux64,
                locationPathName = "Builds/Linux/PickaxeFall.x86_64", options = BuildOptions.Development
            });
        }

        public static void SetupAndBuildLinux() { Setup(); BuildLinux(); }

        [MenuItem("PickaxeFall/Build Android APK")]
        public static void BuildAndroid()
        {
            Directory.CreateDirectory("Builds/Android");
            EditorUserBuildSettings.buildAppBundle = false;
            Build(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, target = BuildTarget.Android,
                locationPathName = "Builds/Android/PickaxeFall.apk", options = BuildOptions.None
            });
        }

        static void Build(BuildPlayerOptions options)
        {
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Build failed: " + report.summary.result);
            Debug.Log("PICKAXEFALL_BUILD_OK: " + report.summary.outputPath);
        }
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
