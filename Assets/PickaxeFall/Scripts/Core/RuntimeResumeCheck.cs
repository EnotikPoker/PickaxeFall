#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PickaxeFall
{
    // Three separate player processes use a fresh GUID-owned cache profile:
    // write -> restore/continue/finish -> confirm finished run cannot resurrect.
    public sealed class RuntimeResumeCheck:MonoBehaviour
    {
        [Serializable] sealed class Expected {public PlayerProfile profile;public RunSnapshot run;public bool[] rolls;public int layout;}
        void OnEnable()=>Application.logMessageReceived+=OnLog;
        void OnDisable()=>Application.logMessageReceived-=OnLog;
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error || type==LogType.Exception)Application.Quit(1);}
        static void Check(bool value,string message){if(!value)throw new Exception("Cold resume audit: "+message);}
        IEnumerator Start()
        {
            yield return null;var game=GetComponent<GameController>();string file=Path.Combine(game.ProfileDirectory,"expected.json");
            if(game.TestMode=="savewrite")
            {
                var p=game.Economy.Profile;p.dashUnlocked=true;p.plungeUnlocked=false;p.coins=4321;p.profileLevel=3;p.revealed[25]=true;p.bridgeStone=123;p.bridgeIron=4;p.portalObsidian=6;p.goalKind=GoalKind.Portal;
                p.upgrades[new UpgradeKey(UpgradeKind.Damage,4).Index]=4;p.upgrades[new UpgradeKey(UpgradeKind.Durability,4).Index]=3;
                p.upgrades[new UpgradeKey(UpgradeKind.Damage,3).Index]=2;p.upgrades[new UpgradeKey(UpgradeKind.Durability,3).Index]=2;
                game.BeginProbeRun(0,712319,2);game.SimulateMiningStep();
                var tool=game.Pickaxes[0];tool.RepairAndUpgrade(4);
                Check(tool.DamageHalfUnits==84 && tool.MaxDurability==game.PreviewDurability(4),"upgraded diamond uses purchased diamond levels");
                var worn=tool.Capture();worn.durability=11;tool.Restore(worn);tool.RepairAndUpgrade();
                Check(tool.Durability==game.PreviewDurability(4) && tool.DamageHalfUnits==84,"diamond-to-diamond full repair preserves purchases");
                game.Pickaxes[1].RepairAndUpgrade(3);
                Check(game.Pickaxes[1].DamageHalfUnits==28 && game.Pickaxes[1].MaxDurability==game.PreviewDurability(3),"iron uses iron levels without stacking wood/diamond");
                tool.DebugPlace(new Vector2(0,-99),new Vector2(1,-2),23);game.Pickaxes[1].DebugPlace(new Vector2(3,-102),new Vector2(-1,-3),-18);
                game.StepMiningWorld();
                var chest=game.World.DebugSetCell(-10,100,BlockKind.Chest);chest.DestroyBlock(tool);
                var stone=game.World.DebugSetCell(7,105,BlockKind.Stone);stone.HitHalfUnits(1,tool);
                foreach(var kind in new[] {BlockKind.Emerald,BlockKind.Lapis,BlockKind.AmethystCluster,BlockKind.MossyCobblestone})game.World.DebugSetCell(-15+(int)kind%5,110,kind).DestroyBlock(tool);
                for(int y=100;y<=102;y++)for(int x=9;x<=13;x++)game.World.DebugSetCell(x,y,BlockKind.Stone);
                for(int x=10;x<=12;x++)game.World.DebugSetCell(x,101,BlockKind.Redstone);
                game.World.ActivateRedstone(10,101,tool);

            Check(game.World.CellAt(12,101).IsLit,"redstone propagates through directly adjacent cells");
                // An isolated contact triggers a connected vein and its adjacent TNT.
                for(int x=-5;x<=-3;x++)game.World.DebugSetCell(x,105,BlockKind.Redstone);
                var tnt=game.World.DebugSetCell(-2,105,BlockKind.Tnt);game.World.ActivateRedstone(-5,105,tool);
                Check(tnt.IsBroken,"redstone ignites adjacent TNT");
                Check(game.TryUseAbility(AbilityKind.Dash,Vector2.right) && tool.AbilityActive,"save in the middle of an active dash");
                game.World.DebugSetCell(15,104,BlockKind.Stone).DestroyBlock(null);
                game.World.Liquids.DebugSet(15,104,BlockKind.Water,5);
                game.TogglePause();var saved=new ProfileStore(game.ProfileDirectory).Load();
                Check(saved.run!=null && saved.run.tools.Count(t=>t.alive)==2 && !saved.plungeUnlocked,"run, team and independent unlock stored together");
                var rng=new EffectRolls();rng.Restore(saved.run.rollStates,saved.run.attempts,saved.run.successes);var expected=new bool[64];for(int i=0;i<64;i++)expected[i]=rng.Roll(UpgradeKind.Fortune,.37);
                File.WriteAllText(file,JsonUtility.ToJson(new Expected {profile=saved,run=saved.run,rolls=expected,layout=game.World.LayoutFingerprint()}));
                Debug.Log("PICKAXEFALL_COLD_SAVE_WRITE_OK: "+game.ProfileDirectory);Application.Quit(0);yield break;
            }
            if(game.TestMode=="savefinished")
            {
                Check(game.Phase==RunPhase.Ready && game.Economy.Profile.run==null,"completed run cannot resurrect");
                Check(game.Economy.Profile.dashUnlocked && !game.Economy.Profile.plungeUnlocked && game.Wallet==4321,"progress survives completion and another startup");
                Debug.Log("PICKAXEFALL_COLD_SAVE_FINISHED_OK");Application.Quit(0);yield break;
            }
            var data=JsonUtility.FromJson<Expected>(File.ReadAllText(file));var s=data.run;
            Check(game.Phase==RunPhase.Mining && game.Paused && game.Clock.Ticks==s.ticks,"cold startup restores paused timeline");
            Check(game.Wallet==data.profile.coins && game.Economy.Profile.resources.SequenceEqual(data.profile.resources) && game.OreCounts.SequenceEqual(s.loot),"bank and run loot restore exactly, no double awards");
            Check(game.Economy.Profile.profileLevel==3&&game.Economy.Profile.revealed[25]&&game.Economy.Profile.bridgeStone==123&&game.Economy.Profile.bridgeIron==4&&game.Economy.Profile.portalObsidian==6&&game.Economy.Profile.goalKind==GoalKind.Portal&&game.Economy.Profile.lifetimeMined.SequenceEqual(data.profile.lifetimeMined),"profile level, mining history, locks, construction and goal restore exactly");
            Check(game.World.LayoutFingerprint()==data.layout,"mined and partially damaged cells restore exactly");
            Check(game.World.CellAt(-10,100)==null && game.World.CellAt(7,105).DamageStage>0,"chest stays looted, stone retains cracks");
            Check(game.World.Liquids.Amount(15,104)==5,"finite liquid volume restores exactly");
            Check(game.World.CellAt(12,101).IsLit,"redstone light timer restores");
            Check(Mathf.Abs(game.Energy.Value-s.energy)<.001f && game.Pickaxes[0].DamageHalfUnits==84 && game.Pickaxes[0].MaxDurability==game.PreviewDurability(4),"energy and purchased tool bonuses restore");
            for(int i=0;i<2;i++)
            {
                var tool=game.Pickaxes[i];var expected=s.tools[i];
                Check(tool.Tier==expected.tier && tool.Durability==expected.durability && tool.Body.position==expected.position && tool.Body.linearVelocity==expected.velocity && tool.Body.rotation==expected.rotation,"tool pose, velocity, type and durability "+i);
            }
            Check(game.Pickaxes[0].AbilityActive,"unfinished dash duration and remaining penetration restore");
            for(int i=0;i<64;i++)Check(game.Rolls.Roll(UpgradeKind.Fortune,.37)==data.rolls[i],"effect RNG continues exactly "+i);
            int ticks=game.Clock.Ticks;float energy=game.Energy.Value;game.SimulateMiningStep();Check(game.Clock.Ticks==ticks && game.Energy.Value==energy,"paused restore cannot advance physics/energy");
            Check(!game.TryUseAbility(AbilityKind.Dash,Vector2.down),"paused action cannot spend energy");
            game.TogglePause();game.SimulateMiningStep();Check(game.Clock.Ticks==ticks+1,"restored run continues");
            game.FinishRun();yield return new WaitForSeconds(1);
            Check(new ProfileStore(game.ProfileDirectory).Load().run==null,"finishing clears snapshot atomically");
            Debug.Log("PICKAXEFALL_COLD_SAVE_READ_OK: resources, poses, tool upgrades, damage, chest, redstone, RNG, pause, continuation and finish.");Application.Quit(0);
        }
    }
}
#endif
