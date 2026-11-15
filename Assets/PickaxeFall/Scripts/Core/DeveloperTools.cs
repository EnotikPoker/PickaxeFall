#if PICKAXEFALL_DEVTOOLS
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace PickaxeFall
{
    public partial class GameController
    {
        [NonSerialized] public string DeveloperContactInfo="Ожидание столкновения";
        public bool DeveloperUnlocked {get;private set;}
        [NonSerialized] public bool DeveloperInfiniteWear,DeveloperInfiniteEnergy,DeveloperColliders;
        [Serializable] sealed class AccessData {public string salt,hash;}
        const string DeveloperConsent="pickaxefall.dev.consent";
        string DeveloperAccessIdentity()
        {
            var asset=Resources.Load<TextAsset>("DeveloperAuth");if(!asset)return "";
            var access=JsonUtility.FromJson<AccessData>(asset.text);
            using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes("device-consent:"+access.salt+access.hash))).Replace("-","");
        }
        void RestoreDeveloperAccess()
        {
            if(TestMode.Length>0)return;
            string identity=DeveloperAccessIdentity();DeveloperUnlocked=identity.Length>0 && PlayerPrefs.GetString(DeveloperConsent,"")==identity;
        }
        public bool DeveloperAuthenticate(string code)
        {
            var asset=Resources.Load<TextAsset>("DeveloperAuth");if(!asset)return false;
            var access=JsonUtility.FromJson<AccessData>(asset.text);using var sha=SHA256.Create();
            string actual=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(access.salt+code.Trim()))).Replace("-","").ToLowerInvariant();
            int difference=actual.Length^access.hash.Length;for(int i=0;i<Math.Min(actual.Length,access.hash.Length);i++)difference|=actual[i]^access.hash[i];
            if(difference!=0)return false;
            DeveloperUnlocked=true;
            if(TestMode.Length==0){PlayerPrefs.SetString(DeveloperConsent,DeveloperAccessIdentity());PlayerPrefs.Save();}
            Notify();return true;
        }
        public bool DeveloperEdit(Action<PlayerProfile> edit)
        {
            if(!DeveloperUnlocked||Phase==RunPhase.Mining&&!Paused)return false;
            if(!Economy.DeveloperEdit(edit))return false;
            if(Phase!=RunPhase.Mining)RunUpgrades=new RunUpgrades(Economy.Profile,balance.progression);
            Notify();return true;
        }
        public bool DeveloperRestore(bool backup)
        {
            if(!DeveloperUnlocked)return false;string directory=Path.Combine(ProfileDirectory,"DeveloperBackup");
            try {
                if(backup){new ProfileStore(directory).Save(Economy.Profile.Copy());return true;}
                if(!File.Exists(Path.Combine(directory,"profile-v2.json")))return false;var saved=new ProfileStore(directory).Load();NewRun();return DeveloperEdit(p=>{
                    p.coins=saved.coins;p.profileLevel=saved.profileLevel;p.resources=(long[])saved.resources.Clone();p.lifetimeMined=(long[])saved.lifetimeMined.Clone();p.upgrades=(int[])saved.upgrades.Clone();p.revealed=(bool[])saved.revealed.Clone();p.discovered=(bool[])saved.discovered.Clone();p.mapRecords=(int[])saved.mapRecords.Clone();p.bestDepth=saved.bestDepth;p.selectedMap=saved.selectedMap;p.bridgeStone=saved.bridgeStone;p.bridgeIron=saved.bridgeIron;p.portalObsidian=saved.portalObsidian;p.dashUnlocked=saved.dashUnlocked;p.plungeUnlocked=saved.plungeUnlocked;p.goalKind=saved.goalKind;p.goalIndex=saved.goalIndex;
                });
            }catch(Exception){return false;}
        }
        public void DeveloperRun(MapId map,int tier,int count,int seed,int depth)
        {
            if(!DeveloperUnlocked)return;
            NewRun();DeveloperEdit(p=>{p.bridgeStone=1000;p.bridgeIron=50;p.portalObsidian=10;p.selectedMap=map;});NewRun();
            Seed=Math.Max(1,seed);World.ResetWorld(Seed);selectedTier=Mathf.Clamp(tier,0,4);Phase=RunPhase.Equipped;StartDrop(count);
            if(depth>0){foreach(var tool in tools)if(tool.IsAlive){var pos=tool.Body.position;pos.y=-Mathf.Clamp(depth,0,2000)+3;tool.Body.position=pos;tool.transform.position=pos;}World.StreamTools(tools);for(int x=-6;x<=6;x++)for(int row=depth-5;row<=depth-1;row++)World.DeveloperClear(x,row);Physics2D.SyncTransforms();cameraFollow?.SnapToTools();}
            TogglePause();SaveProgress();Notify();
        }
        public void DeveloperScene(int scene)
        {
            if(!DeveloperUnlocked || TrialOpen)return;
            DeveloperRun(scene==1?MapId.Desert:MapId.Mine,4,1,8127,scene==1?4:72);
            int row=scene==1?4:72;
            if(scene!=1)
            {
                for(int x=-3;x<=3;x++)for(int y=row-3;y<=row+2;y++)World.DeveloperClear(x,y);
                if(scene==0){for(int x=-2;x<=2;x++)World.DebugSetCell(x,row+1,BlockKind.Obsidian);World.Liquids.DebugSet(0,row,BlockKind.Lava);}
                else World.DebugSetCell(0,row+1,BlockKind.Spawner);
                var pos=new Vector2(0,-row+1);Pickaxe.Body.position=pos;Pickaxe.transform.position=pos;Pickaxe.Body.linearVelocity=Vector2.down*2;
                Physics2D.SyncTransforms();World.Liquids.RefreshVisuals();cameraFollow?.SnapToTools();
            }
            SaveProgress();Notify();
        }
        public void DeveloperRefill()
        {
            if(!DeveloperUnlocked)return;foreach(var tool in tools)if(tool.IsAlive)tool.RepairAndUpgrade(0);Energy.Ensure(Energy.Maximum);Notify();
        }
    }
    public partial class MiningWorld
    {
        internal void DeveloperClear(int x,int row)
        {
            var block=CellAt(x,row);if(block&&!block.IsBroken)block.RetireTrial();
            changes[new Vector2Int(x,row)]=new CellState{kind=BlockKind.Stone,health=0,broken=true};
        }
    }
    public sealed partial class EconomyService
    {
        internal bool DeveloperEdit(Action<PlayerProfile> edit){try{var next=Profile.Copy();edit(next);return Commit(next);}catch(Exception){LastError="Проверь введённые значения.";return false;}}
    }
}
#endif
