using UnityEngine;
namespace PickaxeFall
{
    public sealed partial class PlatformHost
    {
        public GooglePlayClient Google {get;private set;}
        public bool IsGooglePlay {
            get {
#if PICKAXEFALL_GOOGLE_PLAY && UNITY_ANDROID && !UNITY_EDITOR
                return true;
#elif PICKAXEFALL_PLAY_QA && DEVELOPMENT_BUILD
                return game&&game.TestMode=="google";
#else
                return false;
#endif
            }
        }
        public bool HasAdvertising=>IsYandex||IsGooglePlay;
        void Update()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            TickWebCloud();
#endif
            Google?.Tick();
        }
        [UnityEngine.Scripting.Preserve]public void OnPlayIdentity(string json)=>Google?.IdentityEvent(json);
        [UnityEngine.Scripting.Preserve]public void OnPlayCloud(string json)=>Google?.CloudEventReceived(json);
        [UnityEngine.Scripting.Preserve]public void OnPlayAchievement(string json)=>Google?.AchievementEvent(json);
        void OnDestroy()=>Google?.Dispose();
    }
}
