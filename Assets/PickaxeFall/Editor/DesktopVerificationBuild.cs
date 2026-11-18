using UnityEditor;
namespace PickaxeFall.Editor
{
    public static class DesktopVerificationBuild
    {
        public static void Build()=>ProjectTools.BuildLinux();
        public static void PlayQa()
        {
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ProjectTools.ScenePath},target=BuildTarget.StandaloneLinux64,locationPathName="Builds/PlayQA/PickaxeFall.x86_64",options=BuildOptions.Development,extraScriptingDefines=new[]{"PICKAXEFALL_PLAY_QA"}});
            if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new System.Exception("Play QA build failed");
        }
    }
}
