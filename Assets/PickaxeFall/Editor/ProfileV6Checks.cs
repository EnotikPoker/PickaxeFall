using System;
using System.IO;
using UnityEngine;

namespace PickaxeFall.Editor
{
    public static class ProfileV6Checks
    {
        static int checks;
        static void Check(bool ok,string name){checks++;if(!ok)throw new Exception("Profile 0.6: "+name);}
        public static bool[] OpenLocks(){var flags=new bool[27];Array.Fill(flags,true);return flags;}
        public static void LegacyReset(int version)
        {
            string dir=Path.Combine(Application.temporaryCachePath,"profile-reset-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try{
                var old=new PlayerProfile {version=version,coins=900719925474099L,bestDepth=123,dashUnlocked=true,plungeUnlocked=true,
                    resources=new long[version<4?8:version==4?14:21],upgrades=new int[version==2?18:25],activeRunJson="old run discarded"};
                old.resources[6]=3000000000L;old.upgrades[0]=7;
                string path=Path.Combine(dir,"profile-v2.json"),json=JsonUtility.ToJson(old);File.WriteAllText(path,json);
                var store=new ProfileStore(dir);var p=store.Load();
                Check(p.version==7&&p.profileLevel==1&&p.coins==0&&p.bestDepth==0&&p.resetNotice&&!p.dashUnlocked&&!p.plungeUnlocked&&p.run==null&&p.activeRunJson==null,"legacy reset v"+version);
                Check(Array.TrueForAll(p.resources,x=>x==0)&&Array.TrueForAll(p.upgrades,x=>x==0)&&Array.TrueForAll(p.lifetimeMined,x=>x==0),"empty season v"+version);
                Check(File.ReadAllText(path+".pre-v6.bak")==json,"exact old backup v"+version);
                p.coins=321;p.profileLevel=2;p.lifetimeMined[1]=3000000000L;p.revealed[25]=true;p.bridgeStone=123;p.bridgeIron=4;p.portalObsidian=3;p.goalKind=GoalKind.Portal;store.Save(p);
                var again=store.Load();Check(again.coins==321&&again.profileLevel==2&&again.lifetimeMined[1]==3000000000L&&again.revealed[25]&&again.bridgeStone==123&&again.bridgeIron==4&&again.portalObsidian==3&&again.goalKind==GoalKind.Portal,"new season round trip, no second reset");
                File.WriteAllText(path,json);File.Delete(path+".pre-v6.bak");Directory.CreateDirectory(path+".pre-v6.bak");
                bool failed=false;try{store.Load();}catch(IOException){failed=true;}catch(UnauthorizedAccessException){failed=true;}
                Check(failed&&File.ReadAllText(path)==json,"failed reset preserves primary");
            }finally{Directory.Delete(dir,true);}
        }
        static PlayerProfile Rich(int level=5)
        {
            var p=new PlayerProfile {coins=100000000,profileLevel=level,revealed=OpenLocks()};
            foreach(var kind in ResourceInventory.Materials)p.resources[(int)kind]=100000;
            return p;
        }
        public static void Validate()
        {
            checks=0;LegacyReset(5);
            int[] required={1,1,1,1,1,1,1,1,1,1,2,4,5,5,2,4,3,4,2,3,5,2,3,4,5,2,3};
            for(int i=0;i<27;i++)
            {
                Check(ProfileRules.RequiredLevel(i)==required[i],"skill mapping "+i);
                if(i<10)continue;
                var p=Rich(required[i]-1);var e=new EconomyService(p);
                Check(!e.SkillAvailable(i,out _)&&!e.TryReveal(i),"level cannot bypass revealed flag "+i);
                bool bought=i<25?e.TryBuy(ProfileRules.Key(i)):e.TryUnlock((AbilityKind)(i-25));
                Check(!bought&&e.Profile.coins==p.coins,"direct purchase gated "+i);
                e.Profile.profileLevel=required[i];e.Profile.revealed[i]=false;
                Check(!e.SkillAvailable(i,out _)&&e.TryReveal(i)&&e.Profile.coins==p.coins&&e.SkillAvailable(i,out _)&&!e.TryReveal(i),"free one-time reveal "+i);
            }
            for(int level=1;level<5;level++)
            {
                var p=Rich(level);p.lifetimeMined=(long[])ProfileRules.MiningRequirements[level-1].Clone();
                foreach(var r in ProfileRules.Skills[level-1])p.upgrades[new UpgradeKey(r.Kind,r.AnyTier?2:0).Index]=r.Level;
                var e=new EconomyService(p);Check(e.CanLevelUp(out _),"all requirements "+level);
                foreach(var r in ProfileRules.Skills[level-1])
                {
                    var missing=p.Copy();missing.upgrades[new UpgradeKey(r.Kind,r.AnyTier?2:0).Index]--;
                    Check(!new EconomyService(missing).CanLevelUp(out _),"each skill required "+level+" "+r.Kind);
                    Check(ProfileRules.RequiredLevel(new UpgradeKey(r.Kind).Index)<=level,"no circular dependency");
                }
                foreach(var kind in ResourceInventory.Materials)if(p.lifetimeMined[(int)kind]>0){var missing=p.Copy();missing.lifetimeMined[(int)kind]--;Check(!new EconomyService(missing).CanLevelUp(out _),"each material required "+level+" "+kind);}
                var poor=p.Copy();poor.coins=ProfileRules.LevelCoins[level-1]-1;Check(!new EconomyService(poor).TryLevelUp(),"coin requirement");
                long[] materials=(long[])p.resources.Clone();long coins=p.coins;
                Check(e.TryLevelUp()&&e.Profile.profileLevel==level+1&&e.Profile.coins==coins-ProfileRules.LevelCoins[level-1],"level debit");
                for(int i=0;i<materials.Length;i++)Check(e.Profile.resources[i]==materials[i]&&e.Profile.lifetimeMined[i]==p.lifetimeMined[i],"no resource debit");
                var failure=new EconomyService(p,_=>throw new IOException());Check(!failure.TryLevelUp()&&failure.Profile.profileLevel==level&&failure.Profile.coins==coins,"level save rollback");
            }
            var split=Rich(1);split.upgrades[0]=2;split.upgrades[1]=1;
            Check(!ProfileRules.Skills[0][0].Met(split),"personal levels do not sum");
            var economy=new EconomyService(new PlayerProfile());economy.Collect(BlockKind.Diamond,10);Check(economy.Profile.lifetimeMined[6]==0,"loot is not lifetime mining");
            economy.RecordMined(BlockKind.Diamond);Check(economy.Profile.lifetimeMined[6]==1,"one actual block");
            Array.Clear(economy.Profile.resources,0,economy.Profile.resources.Length);economy.Profile.resources[1]=1200;economy.Profile.resources[4]=70;
            economy.TrySetGoal(GoalKind.Bridge);economy.SurplusQuote(out long quote);
            Check(quote==200*2+20*22&&economy.TrySellSurplus()&&economy.Profile.resources[1]==1000&&economy.Profile.resources[4]==50,"construction reservation");
            Check(economy.TryContribute(GoalKind.Bridge)&&ProfileRules.Complete(economy.Profile,GoalKind.Bridge)&&economy.Profile.goalKind==GoalKind.None&&!economy.TryContribute(GoalKind.Bridge),"complete bridge once");
            economy.Profile.resources[20]=4;Check(economy.TryContribute(GoalKind.Portal)&&economy.Profile.portalObsidian==4&&economy.Profile.resources[20]==0,"partial portal");
            economy.Profile.resources[20]=20;Check(economy.TryContribute(GoalKind.Portal)&&economy.Profile.portalObsidian==10&&economy.Profile.resources[20]==14,"portal caps at ten");
            var failed=new EconomyService(Rich(),_=>throw new IOException());
            Check(!failed.TryContribute(GoalKind.Bridge)&&failed.Profile.bridgeStone==0&&failed.Profile.resources[1]==100000,"construction rollback");
            failed.Profile.goalKind=GoalKind.Bridge;Check(!failed.TrySellSurplus()&&failed.Profile.coins==100000000&&failed.Profile.resources[1]==100000,"surplus rollback");
            Check(!failed.TryReveal(10),"duplicate reveal rejected");
            failed.Profile.revealed[10]=false;Check(!failed.TryReveal(10)&&!failed.Profile.revealed[10],"reveal rollback");
            var target=new EconomyService(Rich());target.TrySetGoal(GoalKind.Upgrade,0);var cost=target.GoalCost();
            Check(target.TryBuy(new(UpgradeKind.Damage))&&target.Profile.goalKind==GoalKind.Upgrade&&target.GoalCost().Coins>cost.Coins,"goal advances to next recipe");
            target.Profile.upgrades[0]=19;Check(target.TryBuy(new(UpgradeKind.Damage))&&target.Profile.goalKind==GoalKind.None,"max clears goal");
            target.Profile.goalKind=GoalKind.Ability;target.Profile.goalIndex=0;Check(target.TryUnlock(AbilityKind.Dash)&&target.Profile.goalKind==GoalKind.None,"ability goal clears");
            Check(target.TryResetProgress()&&target.Profile.profileLevel==1&&target.Profile.bridgeStone==0&&target.Profile.portalObsidian==0&&target.Profile.goalKind==GoalKind.None,"manual reset clears all new fields");
            Debug.Log("PICKAXEFALL_PROFILE_V6_OK: "+checks+" checks.");
        }
    }
}
