using System;
using System.IO;
using UnityEngine;

namespace PickaxeFall.Editor
{
    public static class ProfileV5Checks
    {
        [Serializable] sealed class OldProfile
        {public int version=4,bestDepth=123;public long coins=1234567890123;public long[] resources=new long[14];public int[] upgrades=new int[25];public bool dashUnlocked=true,plungeUnlocked=true;public string activeRunJson;}
        public static void Validate()
        {
            string dir=Path.Combine(Application.temporaryCachePath,"v5-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                var old=new OldProfile();old.resources[13]=6000000000;old.upgrades[24]=4;
                var rng=new EffectRolls();rng.Reset(8127);var saved=new RunSnapshot {version=1,seed=8127,toolCount=1,levels=new int[25],loot=new long[14],rollStates=rng.Capture(),attempts=new long[17],successes=new long[17],cells=Array.Empty<CellSnapshot>(),benefits=new RunBenefits(),tools=new ToolSnapshot[5],balanceJson="{}"};
                for(int i=0;i<5;i++)saved.tools[i]=new ToolSnapshot {index=i,alive=i==0,durability=i==0?10:0};saved.loot[13]=23;old.activeRunJson=JsonUtility.ToJson(saved);
                string json=JsonUtility.ToJson(old),path=Path.Combine(dir,"profile-v2.json");File.WriteAllText(path,json);
                var store=new ProfileStore(dir);var p=store.Load();
                if(p.version!=5 || p.coins!=old.coins || p.resources.Length!=21 || p.resources[13]!=old.resources[13] || p.resources[20]!=0 || !p.dashUnlocked || !p.plungeUnlocked || p.upgrades[24]!=4 || p.run.version!=1 || p.run.loot.Length!=21 || p.run.loot[13]!=23 || File.ReadAllText(path+".pre-v5.bak")!=json)throw new Exception("v5 migration changed old bank, unlocks, levels or run");
                if(store.Load().run.loot[13]!=23)throw new Exception("v5 migration not idempotent");
                File.WriteAllText(path,json);File.Delete(path+".pre-v5.bak");Directory.CreateDirectory(path+".pre-v5.bak");File.WriteAllText(path+".bak",JsonUtility.ToJson(new PlayerProfile {coins=1}));
                bool failed=false;try{store.Load();}catch(IOException){failed=true;}catch(UnauthorizedAccessException){failed=true;}
                if(!failed || File.ReadAllText(path)!=json)throw new Exception("failed v5 migration must preserve primary");
                Debug.Log("PICKAXEFALL_PROFILE_V5_OK: v4 bank/unlocks/25 levels and v1 run extended exactly; original backup, idempotence and write-failure protection.");
            }
            finally{Directory.Delete(dir,true);}
        }
    }
}
