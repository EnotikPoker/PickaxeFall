#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;

namespace PickaxeFall
{
    public static class RuntimeProgressionChecks
    {
        public static IEnumerator Run(GameController game,Action<bool,string> check)
        {
            Array.Clear(game.Economy.Profile.upgrades,0,25);game.BeginProbeRun(1,8127,1);var tool=game.SelectedTool;
            var worn=tool.Capture();worn.durability=3;tool.Restore(worn);
            game.World.DebugSetCell(-10,10,BlockKind.Workbench).DestroyBlock(tool);
            check(tool.Tier==1 && tool.Durability==tool.MaxDurability,"ordinary bench fully repairs, no tier increase");
            game.World.DebugSetCell(-10,11,BlockKind.UpgradeWorkbench).DestroyBlock(tool);
            check(tool.Tier==2 && tool.Durability==tool.MaxDurability,"rare bench repairs and upgrades");
            var protectedBench=game.World.DebugSetCell(-10,12,BlockKind.UpgradeWorkbench);
            check(!protectedBench.DestroyBlock(tool,DamageOrigin.Explosion),"upgrading bench protected from blast");
            game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.DoubleBench).Index]=25;
            int seed=RuntimeSmokeTest.EffectSeed(UpgradeKind.DoubleBench,.25);game.BeginProbeRun(0,seed,1);tool=game.SelectedTool;
            game.World.DebugSetCell(-10,10,BlockKind.Workbench).DestroyBlock(tool);check(tool.Tier==1,"double ordinary bench adds one tier");
            game.BeginProbeRun(0,seed,1);tool=game.SelectedTool;game.World.DebugSetCell(-10,10,BlockKind.UpgradeWorkbench).DestroyBlock(tool);check(tool.Tier==2,"double rare bench adds two tiers");
            Array.Clear(game.Economy.Profile.upgrades,0,25);game.BeginProbeRun(3,8127,1);tool=game.SelectedTool;
            // A sealed chamber isolates the test from generated lakes and contact wear.
            for(int row=7;row<=14;row++)for(int x=-4;x<=4;x++)game.World.DebugSetCell(x,row,BlockKind.Stone).DestroyBlock(null);
            for(int x=-4;x<=4;x++)game.World.DebugSetCell(x,14,BlockKind.Stone);
            for(int row=7;row<14;row++){game.World.DebugSetCell(-4,row,BlockKind.Stone);game.World.DebugSetCell(4,row,BlockKind.Stone);}
            tool.DebugPlace(new Vector2(0,-10.5f),Vector2.zero);tool.Body.gravityScale=0;
            game.World.Liquids.DebugSet(0,10,BlockKind.Water,8);int dry=tool.DamageHalfUnits;tool.PhysicsTick();
            check(tool.InWater && tool.DamageHalfUnits==Mathf.RoundToInt(dry*.7f),"water reduces current damage by thirty percent");
            game.World.Liquids.DebugSet(0,10,BlockKind.Water,0);tool.PhysicsTick();check(!tool.InWater&&tool.DamageHalfUnits==dry,"damage restores after leaving water");
            game.World.Liquids.DebugSet(0,10,BlockKind.Lava,8);int before=tool.Durability;
            for(int i=0;i<60;i++)tool.PhysicsTick();check(tool.Durability==before-8,"lava spends eight durability per second, independent of collisions");
            game.World.Liquids.DebugSet(0,10,BlockKind.Lava,0);
            game.World.Liquids.DebugSet(2,10,BlockKind.Lava,8);game.World.Liquids.DebugSet(1,10,BlockKind.Water,8);
            while(game.Clock.Ticks%6!=0)game.Clock.Advance();game.World.Liquids.Tick();
            var obsidian=game.World.CellAt(2,10);check(obsidian&&obsidian.Kind==BlockKind.Obsidian&&game.World.Liquids.Amount(2,10)==0,"water and lava create solid obsidian");
            long resource=game.Inventory[BlockKind.Obsidian];obsidian.DestroyBlock(tool);check(game.Inventory[BlockKind.Obsidian]==resource+1,"obsidian is collected as its own resource");
            game.World.Liquids.DebugSet(0,8,BlockKind.Water,8);
            int volume=0;foreach(var cell in game.World.Liquids.Capture())volume+=cell.amount;
            for(int step=0;step<4;step++){for(int i=0;i<6;i++)game.Clock.Advance();game.World.Liquids.Tick();}
            int afterVolume=0;foreach(var cell in game.World.Liquids.Capture())afterVolume+=cell.amount;
            check(afterVolume==volume && game.World.Liquids.Amount(0,8)<8,"flow moves a finite volume downward without creating water");
            var saved=game.CaptureRun();saved.version=1;saved.liquids=null;
            var legacy=UnityEngine.Object.Instantiate(game.Balance);legacy.rulesVersion=1;legacy.progression=ProgressionBalance.Legacy();Array.Resize(ref legacy.blocks,19);
            int[] oldWear={40,72,108,200,480};for(int i=0;i<5;i++)legacy.pickaxes[i].durability=oldWear[i];saved.balanceJson=JsonUtility.ToJson(legacy);UnityEngine.Object.DestroyImmediate(legacy);
            game.RestoreRun(saved);check(game.Paused && game.Balance.rulesVersion==1 && game.RunUpgrades.EnergyPerSecond==2 && game.AbilityCost(AbilityKind.Plunge)==60,"old run retains old rules and energy");
            game.TogglePause();tool=game.SelectedTool;game.World.DebugSetCell(-10,10,BlockKind.Workbench).DestroyBlock(tool);
            check(tool.Tier==4 && tool.Durability==480,"legacy ordinary bench still upgrades and fully repairs");
            check(game.PreviewDurability(4)==game.ShopBalance.pickaxes[4].durability && game.PreviewDurability(4)!=tool.MaxDurability,"shop previews new balance while old run keeps its frozen stats");
            game.World.DebugSetCell(-10,11,BlockKind.Chest).DestroyBlock(tool);
            check(game.CaptureRun().version==2 && game.CaptureRun().balanceJson.Contains("rulesVersion"),"resaving old run freezes legacy rules in new snapshot");
            tool.Body.gravityScale=1;game.FinishRun();yield return new WaitForSeconds(1);game.NewRun();check(game.Balance.rulesVersion==2 && game.RunUpgrades.EnergyPerSecond==3,"next run returns to new rules");
        }
    }
}
#endif
