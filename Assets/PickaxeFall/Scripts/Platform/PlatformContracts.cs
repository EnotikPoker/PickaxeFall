using System;
using UnityEngine;
namespace PickaxeFall
{
    public interface IProfilePersistence { PlayerProfile Load(); void Save(PlayerProfile profile); void Reset(PlayerProfile profile); }
    public interface IGameTimeSource { long UtcMilliseconds {get;} bool IsTrusted {get;} }
    public interface IGameAdvertising { bool Available {get;} bool Busy {get;} void Show(string placement,bool rewarded,Action onReward,Action<bool> onClosed); }
    public interface IPlatformLifecycle { void GameReady(); void Gameplay(bool running); }
    public sealed class DeviceGameTime:IGameTimeSource
    {
        public long UtcMilliseconds=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();public bool IsTrusted=>false;
    }
    [Serializable] public sealed class WebAdState
    {
        public double afterSeconds,runSeconds,nextRunAd=300;
        public string runId="",rewardId="";public long rewardValue;public bool rewarded,afterAttempted;
        public void StartRun(){runId=Guid.NewGuid().ToString("N");runSeconds=0;nextRunAd=300;afterAttempted=false;}
        public void Advance(double seconds){if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<=0)return;afterSeconds+=seconds;runSeconds+=seconds;}
        public bool AfterDue=>afterSeconds>=120&&!afterAttempted;
        public bool RunDue=>runSeconds>=nextRunAd;
        public void AfterResult(bool shown){afterAttempted=true;if(shown)afterSeconds=0;}
        public void RunResult(){nextRunAd=runSeconds+180;}
        public void Finish(long value){rewardId=runId;rewardValue=Math.Max(0,value)/2;rewarded=false;}
        public WebAdState Copy()=>JsonUtility.FromJson<WebAdState>(JsonUtility.ToJson(this));
        public void Validate()
        {
            if(double.IsNaN(afterSeconds)||double.IsInfinity(afterSeconds)||afterSeconds<0||double.IsNaN(runSeconds)||double.IsInfinity(runSeconds)||runSeconds<0||double.IsNaN(nextRunAd)||double.IsInfinity(nextRunAd)||nextRunAd<300||rewardValue<0)throw new InvalidOperationException("Invalid advertising checkpoint.");
        }
    }
    [Serializable] public sealed class WebCheckpoint
    {
        public int version=1;public string user="",revision="",synced="",profile="";public long savedAt;public WebAdState ads=new();
        public WebCheckpoint Copy()=>JsonUtility.FromJson<WebCheckpoint>(JsonUtility.ToJson(this));
        public void Validate(){if(version!=1||string.IsNullOrEmpty(profile)||string.IsNullOrEmpty(revision)||ads==null)throw new InvalidOperationException("Invalid browser checkpoint.");ProfileStore.Parse(profile);ads.Validate();}
        public WebCheckpoint Core()
        {
            var copy=Copy();var p=ProfileStore.Parse(copy.profile);p.run=null;p.activeRunJson=null;copy.profile=ProfileStore.Serialize(p);
            copy.ads.runId="";copy.ads.runSeconds=0;copy.ads.nextRunAd=300;return copy;
        }
    }
}
