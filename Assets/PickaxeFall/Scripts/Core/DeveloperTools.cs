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
        public bool DeveloperAuthenticate(string code)
        {
            var asset=Resources.Load<TextAsset>("DeveloperAuth");if(!asset)return false;
            var access=JsonUtility.FromJson<AccessData>(asset.text);using var sha=SHA256.Create();
            string actual=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(access.salt+code.Trim()))).Replace("-","").ToLowerInvariant();
            int difference=actual.Length^access.hash.Length;for(int i=0;i<Math.Min(actual.Length,access.hash.Length);i++)difference|=actual[i]^access.hash[i];
            DeveloperUnlocked=difference==0;return DeveloperUnlocked;
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
