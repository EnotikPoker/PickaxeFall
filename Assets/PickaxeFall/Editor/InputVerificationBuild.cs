using System.IO;
using UnityEditor;
using UnityEditor.Build;
namespace PickaxeFall.Editor
{
    public static class InputVerificationBuild
    {
        [MenuItem("PickaxeFall/Verification/Build Android input checks")]
        public static void Android()
        {
            var platform=NamedBuildTarget.Android;
            string id=PlayerSettings.GetApplicationIdentifier(platform),name=PlayerSettings.productName;
            try
            {
                PlayerSettings.SetApplicationIdentifier(platform,"com.enotikpoker.pickaxefall.verification");PlayerSettings.productName="PickaxeFall QA";
                ProjectTools.BuildAndroidDevelopment();
                File.Copy("Builds/Android/PickaxeFall-development.apk","Builds/Android/PickaxeFall-"+GameVersion.Name+"-QA.apk",true);
            }
            finally{PlayerSettings.SetApplicationIdentifier(platform,id);PlayerSettings.productName=name;}
        }
    }
}
