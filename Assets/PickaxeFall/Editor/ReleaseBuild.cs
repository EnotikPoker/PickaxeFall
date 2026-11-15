using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace PickaxeFall.Editor
{
    public static class ReleaseBuild
    {
        [Serializable] sealed class PrivateAccess {public string code,salt,hash;}
        [Serializable] sealed class PublicAccess {public string salt,hash;}
        [MenuItem("PickaxeFall/Release/Build both Android APKs")]
        public static void Both(){Public();Developer();}
        [MenuItem("PickaxeFall/Release/Build public APK")]
        public static void Public()=>Build(false);
        [MenuItem("PickaxeFall/Release/Build private developer APK")]
        public static void Developer()=>Build(true);
        public static void DeveloperLinux()=>Build(true,BuildTarget.StandaloneLinux64);
        static void Build(bool developer,BuildTarget target=BuildTarget.Android)
        {
            const string auth="Assets/PickaxeFall/Resources/DeveloperAuth.json";
            if(File.Exists(auth))throw new InvalidOperationException("Remove stale generated DeveloperAuth before building.");
            if(!developer && PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Contains("PICKAXEFALL_DEVTOOLS"))throw new InvalidOperationException("Dev tools define is forbidden in public player settings.");
            ProjectTools.ValidateBalance();string product=PlayerSettings.productName,id=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string version=PlayerSettings.bundleVersion;int code=PlayerSettings.Android.bundleVersionCode;
            try {
                PlayerSettings.bundleVersion=GameVersion.Name;PlayerSettings.Android.bundleVersionCode=GameVersion.AndroidVersionCode;
                PlayerSettings.productName=developer?"PickaxeFall Dev":"PickaxeFall";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,developer?"com.enotikpoker.pickaxefall.dev":"com.enotikpoker.pickaxefall");
                if(developer)
                {
                    string directory=Environment.GetEnvironmentVariable("PICKAXEFALL_PRIVATE_DIR");
                    if(string.IsNullOrEmpty(directory))directory=Path.GetFullPath("Builds/Private");Directory.CreateDirectory(directory);string path=Path.Combine(directory,"developer-access.json");
                    PrivateAccess access;
                    if(File.Exists(path))access=JsonUtility.FromJson<PrivateAccess>(File.ReadAllText(path));
                    else {using var rng=RandomNumberGenerator.Create();var secret=new byte[16];var salt=new byte[16];rng.GetBytes(secret);rng.GetBytes(salt);access=new PrivateAccess{code=BitConverter.ToString(secret).Replace("-",""),salt=BitConverter.ToString(salt).Replace("-","")};using var sha=SHA256.Create();access.hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(access.salt+access.code))).Replace("-","").ToLowerInvariant();File.WriteAllText(path,JsonUtility.ToJson(access,true));}
                    File.WriteAllText(auth,JsonUtility.ToJson(new PublicAccess{salt=access.salt,hash=access.hash}));AssetDatabase.ImportAsset(auth,ImportAssetOptions.ForceSynchronousImport);
                }
                string output=target==BuildTarget.Android?"Builds/Android/PickaxeFall-"+GameVersion.Name+(developer?"-Dev":"")+".apk":"Builds/DevLinux/PickaxeFallDev.x86_64";Directory.CreateDirectory(Path.GetDirectoryName(output));Directory.CreateDirectory("Builds/Android");EditorUserBuildSettings.buildAppBundle=false;
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ProjectTools.ScenePath},target=target,locationPathName=output,options=BuildOptions.None,extraScriptingDefines=developer?new[]{"PICKAXEFALL_DEVTOOLS"}:Array.Empty<string>()});
                if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Android build failed: "+result.summary.result);
                Debug.Log("PICKAXEFALL_RELEASE_OK: "+output+"; seconds="+result.summary.totalTime.TotalSeconds);
            } finally {
                if(File.Exists(auth))AssetDatabase.DeleteAsset(auth);
                PlayerSettings.productName=product;PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,id);PlayerSettings.bundleVersion=version;PlayerSettings.Android.bundleVersionCode=code;
            }
        }
    }
}
