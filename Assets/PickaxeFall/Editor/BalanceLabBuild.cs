using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using PickaxeFall.BalanceLab;

namespace PickaxeFall.Editor
{
    public static class BalanceLabBuild
    {
        [MenuItem("PickaxeFall/Balance Lab/Build and Open")]
        public static void BuildAndOpen() { Build(); Process.Start(new ProcessStartInfo(PlayerPath,"-noaudio") { UseShellExecute = false }); }
        public static string PlayerPath => Path.GetFullPath("Builds/BalanceLab/PickaxeFallBalanceLab.x86_64");
        [MenuItem("PickaxeFall/Balance Lab/Build")]
        public static void Build()
        {
            ProjectTools.ValidateBalance(); LabChecks.Validate();
            string project = Path.GetFullPath(LabFiles.Arg("lab-source-project") ?? ".");
            string fingerprint = LabFiles.Fingerprint(project);
            if (LabFiles.Fingerprint(Path.GetFullPath(".")) != fingerprint) throw new InvalidOperationException("Код или баланс изолированной копии отличается от исходного проекта. Синхронизируй копию перед сборкой.");
            string directory = Path.GetFullPath("Builds/BalanceLab"); Directory.CreateDirectory(directory);
            const string scenePath = "Assets/PickaxeFall/Scenes/BalanceLab.unity";
            var setup = EditorSceneManager.GetSceneManagerSetup();
            if (Application.isBatchMode)
            {
                EditorSceneManager.OpenScene(ProjectTools.ScenePath, OpenSceneMode.Single); setup = EditorSceneManager.GetSceneManagerSetup();
            }
            else if (setup.Length == 0 || setup.Any(s => string.IsNullOrEmpty(s.path)))
            {
                throw new InvalidOperationException("Сохрани открытую сцену перед сборкой лаборатории.");
            }
            if (!File.Exists(scenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                var obj = new GameObject("Balance Lab"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(obj, scene);
                var app = obj.AddComponent<LabApp>();
                app.Configure(AssetDatabase.LoadAssetAtPath<GameBalance>("Assets/PickaxeFall/Data/DefaultBalance.asset"), AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/PickaxeFall/UI/GamePanel.asset"));
                EditorSceneManager.SaveScene(scene, scenePath); EditorSceneManager.CloseScene(scene, true);
            }
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scenePath }, target = BuildTarget.StandaloneLinux64,
                    locationPathName = PlayerPath, options = BuildOptions.None, extraScriptingDefines = new[] { "BALANCE_LAB" } });
                if (report.summary.result != BuildResult.Succeeded) throw new Exception("Balance Lab build failed: " + report.summary.result);
                var balance = AssetDatabase.LoadAssetAtPath<GameBalance>("Assets/PickaxeFall/Data/DefaultBalance.asset");
                LabFiles.Write(Path.Combine(directory, "manifest.json"), new LabManifest { version = GameVersion.Name, unity = Application.unityVersion, fingerprint = fingerprint, project = project,
                    builtUtc = DateTime.UtcNow.ToString("O"), balance = JsonUtility.ToJson(balance), roulette = Enumerable.Range(0,5).Select(i => (double)balance.RollChance(i)).ToArray() }.CaptureEconomy());
                UnityEngine.Debug.Log("PICKAXEFALL_LAB_BUILD_OK: " + PlayerPath);
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
    }
}
