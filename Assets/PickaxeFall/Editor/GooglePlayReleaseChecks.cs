using System;
using System.IO;
using System.Linq;
using UnityEngine;
namespace PickaxeFall.Editor
{
    public static class GooglePlayReleaseChecks
    {
        public static void Validate()
        {
            int count=0;void Check(bool value,string why){count++;if(!value)throw new InvalidOperationException("Play 0.8.4: "+why);}
            Check(PlayReleaseConfig.SaveEpoch==0,"test season stays active in 0.8.4");Check(PlayReleaseConfig.SaveSpace(0)!=PlayReleaseConfig.SaveSpace(1),"separate production space");
            Check(PlayReleaseConfig.ClassifyAge(-1)==PlayAudience.Unknown,"skip is family safe");
            for(int age=0;age<=120;age++)Check(PlayReleaseConfig.ClassifyAge(age)==(age<18?PlayAudience.Family:PlayAudience.Adult),"age route "+age);
            var config=new PlayReleaseConfig();Check(config.PublishingErrors().Length>=10,"empty test configuration cannot publish");
            var profile=new PlayerProfile();Check(PlayAchievements.Earned(profile).All(v=>!v),"new profile has no achievement");
            profile.lifetimeMined[(int)BlockKind.Stone]=10000;profile.lifetimeMined[(int)BlockKind.Emerald]=1;profile.bestDepth=250;profile.profileLevel=5;profile.bridgeStone=1000;profile.bridgeIron=50;profile.portalObsidian=10;
            Check(PlayAchievements.Earned(profile).All(v=>v),"all twelve milestones derive from durable profile");
            string directory=Path.GetFullPath("Builds/Release084/SaveChecks/"+Guid.NewGuid().ToString("N"));
            try
            {
                var store=new PlaySaveStore(directory);store.Save(profile);var original=store.Checkpoint.Copy();
                var reopened=new PlaySaveStore(directory);Check(reopened.Load().coins==0&&reopened.Load().bestDepth==250,"cold load");
                string recovery=Path.Combine(directory,"recovery.json");
                void ValidateRecovery(string text){if(!text.StartsWith("valid",StringComparison.Ordinal))throw new InvalidDataException();}
                AtomicPlayFile.Write(recovery,"valid-old",ValidateRecovery);
                AtomicPlayFile.Write(recovery,"valid-current",ValidateRecovery);
                File.WriteAllText(recovery,"corrupt");
                Check(AtomicPlayFile.Read(recovery,ValidateRecovery)=="valid-old","recover validated backup");
                AtomicPlayFile.Write(recovery,"valid-new",ValidateRecovery);
                Check(File.ReadAllText(recovery+".bak")=="valid-old","recovered backup survives next save");
                Check(Directory.GetFiles(directory,"recovery.json.corrupt-*").Length==1,"damaged bytes retained");
                File.WriteAllText(recovery,"corrupt-again");
                Check(AtomicPlayFile.Read(recovery,ValidateRecovery)=="valid-old","second corruption still recovers");
                Check(new PlaySaveStore(directory,"player-b").Load().bestDepth==0,"account isolation");
                Check(new PlaySaveStore(directory,epoch:1).Load().bestDepth==0,"first public epoch excludes test data");
                var ads=store.Ads;ads.StartRun();ads.Finish(101);store.Save(profile);var rewarded=ads.Copy();rewarded.rewarded=true;var paid=profile.Copy();paid.coins+=rewarded.rewardValue;store.CommitReward(paid,rewarded);
                var receipt=new PlaySaveStore(directory);Check(receipt.Load().coins==50&&receipt.Ads.rewarded,"reward and coins atomically persisted");
                var core=receipt.Checkpoint.Core();Check(ProfileStore.Parse(core.profile).run==null&&string.IsNullOrEmpty(ProfileStore.Parse(core.profile).activeRunJson),"cloud strips run");
                Check(string.IsNullOrEmpty(core.ads.runId)&&core.ads.rewarded&&core.ads.rewardValue==50,"cloud retains reward receipt only");
                Check(!PlaySaveStore.Conflicts(core,core.Copy()),"equal revisions do not conflict");
                var remote=core.Copy();remote.revision="remote";Check(PlaySaveStore.Conflicts(core,remote),"independent changes conflict");core.synced=remote.revision;Check(!PlaySaveStore.Conflicts(core,remote),"known remote base");
                store.Backup(store.Checkpoint,remote);Check(Directory.GetFiles(directory,"conflict-*.json",SearchOption.AllDirectories).Length==2,"both conflict copies preserved");
                store.Adopt(remote,"player-a",remote.revision);Check(store.Checkpoint.owner=="player-a"&&new PlaySaveStore(directory,"player-a").Load().coins==50,"committed account adoption");
                store.Reset(new PlayerProfile());Check(new PlaySaveStore(directory,"player-a").Load().bestDepth==0,"reset persists");
                Check(new PlaySaveStore(directory).Load().bestDepth==250,"other owner unaffected by reset");
                string blocked=Path.Combine(directory,"blocked");File.WriteAllText(blocked,"not a directory");
                var failure=new PlaySaveStore(blocked);bool rejected=false;try{failure.Save(profile);}catch(IOException){rejected=true;}
                Check(rejected&&failure.Load().bestDepth==0,"failed local commit leaves cached economy unchanged");
                var invalid=store.Checkpoint.Copy();invalid.epoch=1;bool wrong=false;try{store.Parse(JsonUtility.ToJson(invalid));}catch(Exception){wrong=true;}Check(wrong,"wrong epoch rejected");
                Check(File.ReadAllText("Assets/WebGLTemplates/PickaxeFall/platform.js").Contains("pfCoreRelease"),"web release slot prepared");
                foreach(string prefix in new[]{"Cobblestone/Cobblestone_Cobblestone_","MossyCobblestone/MossyCobblestone_MossyCobblestone_","Up_Workbench/UpgradeWorkbench_UpgradeWorkbench_"})
                    for(int i=0;i<4;i++)Check(Resources.Load<Sprite>("Blocks/World/Structures/"+prefix+i)!=null,"imported structure "+prefix+i);
                var landscape=PixelArt.Landscape();Check(landscape.texture.width==256&&landscape.texture.height==128,"updated mine landscape resolution");
                PixelArt.ReleaseGenerated(landscape);Check(landscape!=null&&landscape.texture!=null,"imported landscape not destroyed");
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
            PlatformReleaseChecks.Validate();ProjectTools.ValidateBalance();Debug.Log("PICKAXEFALL_GOOGLE_CHECKS_OK: "+count);
        }
    }
}
