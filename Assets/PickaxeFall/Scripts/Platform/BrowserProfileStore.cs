#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using UnityEngine;
namespace PickaxeFall
{
    // Browser localStorage commits are synchronous and atomic per key. Cloud follows
    // only after local success. A quota failure leaves the old checkpoint untouched.
    public sealed class BrowserProfileStore:IRewardProfileStore
    {
        readonly PlatformHost host;string savedAds;
        public WebCheckpoint Checkpoint {get;private set;}
        public WebAdState Ads=>Checkpoint.ads;
        public BrowserProfileStore(PlatformHost host,WebCheckpoint checkpoint){this.host=host;Checkpoint=checkpoint;savedAds=JsonUtility.ToJson(checkpoint.ads);}
        public PlayerProfile Load()=>ProfileStore.Parse(Checkpoint.profile);
        public void Save(PlayerProfile profile)=>Commit(profile,Ads);
        public void Reset(PlayerProfile profile)=>Commit(profile,new WebAdState(),true);
        public void CommitReward(PlayerProfile profile,WebAdState ads)=>Commit(profile,ads);
        public void Commit(PlayerProfile profile,WebAdState ads,bool reset=false)
        {
            var next=Checkpoint.Copy();next.profile=ProfileStore.Serialize(profile);next.ads=ads.Copy();next.ads.Validate();
            // No revision churn when an unchanged menu merely asks to save.
            if(!reset&&next.profile==Checkpoint.profile && JsonUtility.ToJson(next.ads)==savedAds)return;
            next.revision=Guid.NewGuid().ToString("N");next.savedAt=host.UtcMilliseconds;
            host.WriteLocal(next,reset);Checkpoint=next;savedAds=JsonUtility.ToJson(next.ads);host.QueueCloud();
        }
        public void Adopt(WebCheckpoint checkpoint){host.WriteLocal(checkpoint);Checkpoint=checkpoint;host.QueueCloud();}
        public void Acknowledge(string revision)
        {
            var next=Checkpoint.Copy();next.synced=revision;host.WriteLocal(next);Checkpoint=next;
        }
        public static WebCheckpoint New(string user,long now)=>new(){user=user,revision=Guid.NewGuid().ToString("N"),savedAt=now,profile=ProfileStore.Serialize(new PlayerProfile())};
    }
}
#endif
