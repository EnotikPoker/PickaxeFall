using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace PickaxeFall
{
    [Serializable] public sealed class PlayCheckpoint
    {
        public int version=1,epoch=PlayReleaseConfig.SaveEpoch;
        public string owner="guest",revision="",synced="",profile="";
        public long savedAt;
        public WebAdState ads=new();
        public PlayCheckpoint Copy()=>JsonUtility.FromJson<PlayCheckpoint>(JsonUtility.ToJson(this));
        public void Validate(int expectedEpoch)
        {
            if(version!=1||epoch!=expectedEpoch||string.IsNullOrEmpty(owner)||string.IsNullOrEmpty(revision)||savedAt<0||ads==null)throw new InvalidDataException("Invalid Play checkpoint");
            ProfileStore.Parse(profile);ads.Validate();
        }
        public PlayCheckpoint Core()
        {
            var next=Copy();var p=ProfileStore.Parse(profile);p.run=null;p.activeRunJson=null;next.profile=ProfileStore.Serialize(p);
            next.ads.runId="";next.ads.runSeconds=0;next.ads.nextRunAd=300;return next;
        }
        public static PlayCheckpoint New(string owner,int epoch)=>new(){owner=owner,epoch=epoch,revision=Guid.NewGuid().ToString("N"),profile=ProfileStore.Serialize(new PlayerProfile()),savedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()};
    }
    public static class AtomicPlayFile
    {
        public static string Read(string path,Action<string> validate)
        {
            foreach(var candidate in new[]{path,path+".bak"})if(File.Exists(candidate))
            {try{var text=File.ReadAllText(candidate);validate(text);return text;}catch(Exception){}}
            if(File.Exists(path)||File.Exists(path+".bak"))throw new IOException("Unreadable local data; recovery copies preserved");return null;
        }
        public static void Write(string path,string text,Action<string> validate,bool reset=false)
        {
            validate(text);Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+".tmp";
            using(var s=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){var b=Encoding.UTF8.GetBytes(text);s.Write(b,0,b.Length);s.Flush(true);}
            if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);
            if(reset)try{File.Copy(path,path+".bak",true);}catch(IOException){}
        }
    }
    // Profile, local run and reward receipt share one atomic commit. Cloud has no run.
    public sealed class PlaySaveStore:IRewardProfileStore
    {
        readonly string directory;readonly int epoch;string path,savedAds;
        public PlayCheckpoint Checkpoint {get;private set;}
        public event Action Changed;
        public WebAdState Ads=>Checkpoint.ads;
        public PlaySaveStore(string directory,string owner="guest",int epoch=PlayReleaseConfig.SaveEpoch)
        {
            this.directory=Path.Combine(directory,"google",PlayReleaseConfig.SaveSpace(epoch));this.epoch=epoch;
            path=PathFor(owner);Checkpoint=Read(owner)??PlayCheckpoint.New(owner,epoch);savedAds=JsonUtility.ToJson(Ads);
        }
        string PathFor(string owner)
        {
            using var sha=SHA256.Create();return Path.Combine(directory,BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(owner))).Replace("-","")+".json");
        }
        public PlayCheckpoint Parse(string text){var c=JsonUtility.FromJson<PlayCheckpoint>(text);c.Validate(epoch);return c;}
        public PlayCheckpoint Read(string owner)
        {
            var text=AtomicPlayFile.Read(PathFor(owner),s=>Parse(s));if(text==null)return null;var c=Parse(text);if(c.owner!=owner)throw new InvalidDataException("Wrong local profile owner");return c;
        }
        public PlayerProfile Load()=>ProfileStore.Parse(Checkpoint.profile);
        public void Save(PlayerProfile profile)=>Commit(profile,Ads);
        public void Reset(PlayerProfile empty)=>Commit(empty,new WebAdState(),true);
        public void CommitReward(PlayerProfile profile,WebAdState ads)=>Commit(profile,ads);
        void Commit(PlayerProfile profile,WebAdState ads,bool reset=false)
        {
            var next=Checkpoint.Copy();next.profile=ProfileStore.Serialize(profile);next.ads=ads.Copy();next.ads.Validate();
            if(!reset&&next.profile==Checkpoint.profile&&JsonUtility.ToJson(next.ads)==savedAds)return;
            var previousAds=JsonUtility.FromJson<WebAdState>(savedAds);
            if(reset||next.Core().profile!=Checkpoint.Core().profile||next.ads.rewardId!=previousAds.rewardId||next.ads.rewarded!=previousAds.rewarded)next.revision=Guid.NewGuid().ToString("N");
            next.savedAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();Write(path,next,reset);Checkpoint=next;savedAds=JsonUtility.ToJson(Ads);Changed?.Invoke();
        }
        void Write(string target,PlayCheckpoint c,bool reset=false)=>AtomicPlayFile.Write(target,JsonUtility.ToJson(c),s=>Parse(s),reset);
        public void Adopt(PlayCheckpoint candidate,string owner,string remoteRevision)
        {
            var next=candidate.Copy();next.owner=owner;next.synced=remoteRevision??"";if(next.revision!=remoteRevision)next.revision=Guid.NewGuid().ToString("N");
            string target=PathFor(owner);Write(target,next);path=target;Checkpoint=next;savedAds=JsonUtility.ToJson(Ads);Changed?.Invoke();
        }
        public void Acknowledge(string revision){var c=Checkpoint.Copy();c.synced=revision;Write(path,c);Checkpoint=c;}
        public void Backup(PlayCheckpoint local,PlayCheckpoint remote)
        {
            string stamp=Guid.NewGuid().ToString("N");Write(Path.Combine(directory,"conflict-"+stamp+"-local.json"),local);Write(Path.Combine(directory,"conflict-"+stamp+"-cloud.json"),remote);
        }
        public static bool Conflicts(PlayCheckpoint local,PlayCheckpoint remote)=>remote!=null&&local.revision!=remote.revision&&local.synced!=remote.revision;
    }
}
