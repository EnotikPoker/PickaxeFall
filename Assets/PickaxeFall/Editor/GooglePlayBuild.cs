using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace PickaxeFall.Editor
{
    public static class GooglePlayBuild
    {
        [Serializable]sealed class Signing{public string appKeystore,uploadKeystore,storePassword,keyPassword,appAlias="pickaxefall",uploadAlias="upload";}
        [MenuItem("PickaxeFall/Release/Build Google Play test AAB")]
        public static void TestBundle()=>Build(true,false);
        [MenuItem("PickaxeFall/Release/Build Google Play test APK")]
        public static void TestApk()=>Build(false,false);
        public static void DeviceQa()=>Build(false,false,true);
        [MenuItem("PickaxeFall/Release/Build Google Play publication AAB")]
        public static void Production()=>Build(true,true);
        public static void ValidateConfiguration()
        {
            var config=JsonUtility.FromJson<PlayReleaseConfig>(File.ReadAllText("Assets/PickaxeFall/Resources/GooglePlay/config.json"));
            var errors=config.PublishingErrors();if(errors.Length!=0)throw new InvalidOperationException("Google Play publication blocked:\n"+string.Join("\n",errors));
        }
        static void Build(bool bundle,bool production,bool qa=false)
        {
            if(production)ValidateConfiguration();
            if(!Directory.Exists("Assets/Plugins/Android/PickaxePlay.androidlib")||!File.Exists("Packages/manifest.json")||!File.ReadAllText("Packages/manifest.json").Contains("com.unity.purchasing"))
                throw new InvalidOperationException("Prepare the isolated Play project with Tools/BuildGooglePlay.py first.");
            if(File.Exists("Assets/PickaxeFall/Resources/DeveloperAuth.json")||PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android).Contains("PICKAXEFALL_DEVTOOLS"))throw new InvalidOperationException("Developer access is forbidden in Play builds");
            string keyFile=Environment.GetEnvironmentVariable("PICKAXEFALL_PLAY_SIGNING");if(string.IsNullOrEmpty(keyFile)||!File.Exists(keyFile))throw new InvalidOperationException("Private signing configuration required");
            var keys=JsonUtility.FromJson<Signing>(File.ReadAllText(keyFile));
            string version=PlayerSettings.bundleVersion,name=PlayerSettings.productName,id=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            string store=PlayerSettings.Android.keystoreName,alias=PlayerSettings.Android.keyaliasName,storePass=PlayerSettings.Android.keystorePass,keyPass=PlayerSettings.Android.keyaliasPass;
            int code=PlayerSettings.Android.bundleVersionCode;bool custom=PlayerSettings.Android.useCustomKeystore,oldBundle=EditorUserBuildSettings.buildAppBundle;
            var target=PlayerSettings.Android.targetSdkVersion;var backend=PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);var arch=PlayerSettings.Android.targetArchitectures;
            try
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android,BuildTarget.Android);
                PlayerSettings.bundleVersion=GameVersion.Name;PlayerSettings.Android.bundleVersionCode=GameVersion.AndroidVersionCode;
                PlayerSettings.productName=qa?"PickaxeFall Play QA":production?"PickaxeFall":"PickaxeFall Test";PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.enotikpoker.pickaxefall"+(qa?".playqa":""));
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;PlayerSettings.Android.targetSdkVersion=(AndroidSdkVersions)36;
                PlayerSettings.Android.useCustomKeystore=true;PlayerSettings.Android.keystoreName=bundle?keys.uploadKeystore:keys.appKeystore;
                PlayerSettings.Android.keyaliasName=bundle?keys.uploadAlias:keys.appAlias;PlayerSettings.Android.keystorePass=keys.storePassword;PlayerSettings.Android.keyaliasPass=keys.keyPassword;
                EditorUserBuildSettings.buildAppBundle=bundle;
                GooglePlayReleaseChecks.Validate();
                string output="Builds/GooglePlay/PickaxeFall-"+GameVersion.Name+(qa?"-QA":production?"":"-Test")+(bundle?".aab":".apk");Directory.CreateDirectory(Path.GetDirectoryName(output));
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ProjectTools.ScenePath},target=BuildTarget.Android,locationPathName=output,options=qa?BuildOptions.Development:BuildOptions.None,extraScriptingDefines=qa?new[]{"PICKAXEFALL_GOOGLE_PLAY","PICKAXEFALL_PLAY_QA"}:new[]{"PICKAXEFALL_GOOGLE_PLAY"}});
                if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Play build failed: "+result.summary.result);
                Debug.Log("PICKAXEFALL_GOOGLE_BUILD_OK: "+output+"; seconds="+result.summary.totalTime.TotalSeconds);
            }
            finally
            {
                PlayerSettings.bundleVersion=version;PlayerSettings.Android.bundleVersionCode=code;PlayerSettings.productName=name;PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,id);
                PlayerSettings.Android.keystoreName=store;PlayerSettings.Android.keyaliasName=alias;PlayerSettings.Android.keystorePass=storePass;PlayerSettings.Android.keyaliasPass=keyPass;PlayerSettings.Android.useCustomKeystore=custom;
                PlayerSettings.Android.targetSdkVersion=target;PlayerSettings.Android.targetArchitectures=arch;PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,backend);EditorUserBuildSettings.buildAppBundle=oldBundle;
            }
        }
    }
}
