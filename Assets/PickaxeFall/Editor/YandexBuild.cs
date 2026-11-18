using System;
using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace PickaxeFall.Editor
{
    public static class YandexBuild
    {
        [MenuItem("PickaxeFall/Release/Build Yandex ZIP")]
        public static void Production()=>Build(false);
        [MenuItem("PickaxeFall/Verification/Build Yandex draft checks")]
        public static void Draft()=>Build(true);
        static void Build(bool draft)
        {
            if(File.Exists("Assets/PickaxeFall/Resources/DeveloperAuth.json"))throw new InvalidOperationException("Generated developer auth must not be present in a web build.");
            if(PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.WebGL).Contains("PICKAXEFALL_DEVTOOLS"))throw new InvalidOperationException("Developer tools are forbidden in Web builds.");
            if(Application.platform==RuntimePlatform.LinuxEditor)
            {
                try{using var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ffmpeg","-version"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true});if(process==null)throw new Exception();process.WaitForExit();if(process.ExitCode!=0)throw new Exception();}
                catch(Exception){throw new InvalidOperationException("Web audio encoding needs ffmpeg on PATH. Use Tools/BuildPlayers.py or install ffmpeg.");}
            }
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL,BuildTarget.WebGL);
            foreach(string path in Directory.GetFiles("Assets/PickaxeFall/Resources/Audio","*",SearchOption.AllDirectories))if(path.EndsWith(".ogg")||path.EndsWith(".wav")||path.EndsWith(".mp3"))AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
            ProjectTools.ValidateBalance();
            string template=PlayerSettings.WebGL.template,version=PlayerSettings.bundleVersion;
            var exceptions=PlayerSettings.WebGL.exceptionSupport;var compression=PlayerSettings.WebGL.compressionFormat;bool fallback=PlayerSettings.WebGL.decompressionFallback;
            string directory="Builds/Yandex/PickaxeFall-"+GameVersion.Name+(draft?"-Draft":"");
            try
            {
                PlayerSettings.bundleVersion=GameVersion.Name;PlayerSettings.WebGL.template="PROJECT:PickaxeFall";
                PlayerSettings.WebGL.compressionFormat=WebGLCompressionFormat.Gzip;PlayerSettings.WebGL.decompressionFallback=true;
                PlayerSettings.WebGL.exceptionSupport=WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ProjectTools.ScenePath},target=BuildTarget.WebGL,locationPathName=directory,options=draft?BuildOptions.Development:BuildOptions.None});
                if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Web build failed: "+result.summary.result);
                string html=Path.Combine(directory,"index.html");File.WriteAllText(html,File.ReadAllText(html).Replace("midRunAds:false","midRunAds:"+(draft?"true":"false")));
                long size=0;foreach(string path in Directory.GetFiles(directory,"*",SearchOption.AllDirectories))
                {
                    string name=path.Substring(directory.Length+1);if(name.Contains(' ')||System.Text.RegularExpressions.Regex.IsMatch(name,"[А-Яа-яЁё]"))throw new InvalidOperationException("Invalid archive path.");size+=new FileInfo(path).Length;
                }
                if(size>100000000)throw new InvalidOperationException("Yandex archive exceeds 100 MB uncompressed.");
                string zip=directory+".zip";if(File.Exists(zip))File.Delete(zip);ZipFile.CreateFromDirectory(directory,zip,System.IO.Compression.CompressionLevel.Optimal,false);
                Debug.Log("PICKAXEFALL_YANDEX_BUILD_OK: "+zip+"; uncompressed="+size+"; seconds="+result.summary.totalTime.TotalSeconds);
            }
            finally{PlayerSettings.WebGL.template=template;PlayerSettings.bundleVersion=version;PlayerSettings.WebGL.compressionFormat=compression;PlayerSettings.WebGL.decompressionFallback=fallback;PlayerSettings.WebGL.exceptionSupport=exceptions;}
        }
    }
}
