#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public static class RuntimeActionChecks
    {
        static void ClearArea(GameController game)
        {
            for (int row = 0; row <= 7; row++) for (int x = -3; x <= 3; x++) game.World.DebugSetCell(x, row, BlockKind.Stone).DestroyBlock(null);
        }
        public static IEnumerator Run(GameController game, VisualElement root, Action<bool,string> check)
        {
            Array.Clear(game.Economy.Profile.upgrades, 0, game.Economy.Profile.upgrades.Length);
            game.BeginProbeRun(0, 8127, 2);
            check(game.Energy.Value == 60 && root.Q("run-energy") != null, "energy UI starts full");
            check(game.SelectTool(1) && game.SelectedToolIndex == 1 && game.Energy.Value == 60, "selecting another tool keeps shared energy");
            var other = game.Pickaxes[0]; Vector2 oldOtherVelocity = other.Body.linearVelocity;
            check(game.TryUseAbility(AbilityKind.Dash, Vector2.right) && game.Energy.Value == 15 && game.SelectedTool.Body.linearVelocity.x > 9.9f && other.Body.linearVelocity == oldOtherVelocity, "only selected tool dashes and pays once");
            game.TogglePause(); game.SimulateMiningStep(); check(!game.TryUseAbility(AbilityKind.Plunge, Vector2.down) && game.Energy.Value == 15, "pause cannot spend or regenerate energy"); game.TogglePause();
            game.Energy.TrySpend(15); game.SelectTool(0);
            check(!game.TryUseAbility(AbilityKind.Plunge, Vector2.down) && game.Energy.Value == 0, "no free energy on selection or rejected action");
            game.OnPhysicalHit(game.Pickaxes[1], false); check(game.Energy.Value == 0, "unselected hits cannot charge");
            for(int i=0;i<1000;i++)game.OnPhysicalHit(game.SelectedTool, false);
            check(game.Energy.Value == 0, "even a thousand selected hits cannot charge energy");
            game.SimulateMiningStep();
            check(Mathf.Abs(game.Energy.Value-2f/60)<.0001f,"shared regeneration happens once per simulation step for entire team");
            game.SelectedTool.DebugExhaust(); check(game.SelectedToolIndex == 1 && game.Phase == RunPhase.Mining, "broken selected tool switches to survivor");
            game.BeginProbeRun(0, 8127, 1);
            game.SelectedTool.DebugPlace(new Vector2(game.Balance.worldWidth / 2 - 1, 60), Vector2.zero); game.World.StreamTools(game.Pickaxes);
            check(game.TryUseAbility(AbilityKind.Dash, Vector2.right), "sky dash accepted");
            yield return new WaitForSeconds(0.3f);
            check(game.SelectedTool.Body.position.x < game.Balance.worldWidth / 2 + 0.5f, "high upward travel cannot escape above side walls");
            game.BeginProbeRun(0, 8127, 1); ClearArea(game);
            var tool = game.SelectedTool; tool.DebugPlace(new Vector2(0, 0.9f), Vector2.zero);
            var first = game.World.DebugSetCell(0, 0, BlockKind.Stone); var second = game.World.DebugSetCell(0, 1, BlockKind.Stone); var third = game.World.DebugSetCell(0, 2, BlockKind.Stone);
            check(game.TryUseAbility(AbilityKind.Plunge, Vector2.down), "plunge accepted");
            yield return new WaitForSeconds(0.25f);
            check(first.IsBroken && !second.IsBroken && !third.IsBroken && game.Benefits.AbilityBlocks == 1, "base plunge penetrates exactly one block");
            check(second.HealthHalfUnits < 12 && tool.Body.linearVelocity.y > 0, "next intact block gives a physical impact and rebound");
            check(tool.Durability == tool.MaxDurability - tool.TotalStrikes, "each ability block costs one normal wear");
            check(Mathf.Abs(game.Energy.Value - 2*game.Clock.Now) < 0.01f, "abilities and physical hits cannot bypass time-based regeneration");
            game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.PlungeDepth).Index] = 3;
            game.BeginProbeRun(0, 8127, 1); ClearArea(game); tool = game.SelectedTool;
            for (int row = 0; row <= 5; row++) game.World.DebugSetCell(0, row, BlockKind.Stone);
            tool.DebugPlace(new Vector2(0, 0.9f), Vector2.zero);
            check(game.TryUseAbility(AbilityKind.Plunge, Vector2.down), "upgraded plunge accepted");
            yield return new WaitForSeconds(0.45f);
            check(game.Benefits.AbilityBlocks == 4 && game.World.CellAt(0, 4) != null, "maximum plunge penetrates exactly four without tunnelling through fifth");
            game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.PlungeDepth).Index] = 0;
            foreach (var direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
            {
                game.BeginProbeRun(0, 8127, 1); ClearArea(game); tool = game.SelectedTool;
                int x = (int)direction.x, row = 3 - (int)direction.y;
                first = game.World.DebugSetCell(x, row, BlockKind.Diamond);
                tool.DebugPlace(new Vector2(0, -3.5f), Vector2.zero);
                check(game.TryUseAbility(AbilityKind.Dash, direction), "dash direction accepted " + direction);
                yield return new WaitForSeconds(0.12f);
                check(first.IsBroken && game.Benefits.AbilityBlocks == 1, "dash instantly penetrates ore " + direction);
                check(Vector2.Dot(tool.Body.linearVelocity, direction) > 4, "no bounce from penetrated block " + direction);
            }
            game.BeginProbeRun(0, 8127, 1); ClearArea(game); tool = game.SelectedTool;
            first = game.World.DebugSetCell(0, 3, BlockKind.Stone); second = game.World.DebugSetCell(2, 3, BlockKind.Stone);
            float start = Time.time; tool.DebugPlace(new Vector2(0, -2.3f), Vector2.down * 8);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            check(first.HealthHalfUnits < 12, "first physical contact damages immediately");
            tool.DebugPlace(new Vector2(2, -2.3f), Vector2.down * 8);
            for (int i = 0; i < 3; i++) yield return new WaitForFixedUpdate();
            check(second.HealthHalfUnits < 12 && Time.time - start < 0.35f, "new contact damages before old cooldown would expire");
            game.BeginProbeRun(0, 8127, 1); ClearArea(game); tool = game.SelectedTool;
            first = game.World.DebugSetCell(0, 3, BlockKind.Stone); tool.DebugPlace(new Vector2(0, -2.65f), Vector2.zero); tool.Body.gravityScale = 0;
            yield return new WaitForSeconds(0.3f);
            check(first.HealthHalfUnits == 12, "resting and weak contact cannot mine"); tool.Body.gravityScale = 1;
            game.BeginProbeRun(0, 8127, 1); tool = game.SelectedTool;
            tool.DebugPlace(new Vector2(0, 7), Vector2.zero); game.TryUseAbility(AbilityKind.Plunge, Vector2.down);
            int beforeDurability = tool.Durability;
            var bench = game.World.DebugSetCell(-10, 10, BlockKind.Workbench); bench.DestroyBlock(tool);
            check(tool.Durability == tool.MaxDurability && tool.MaxDurability > beforeDurability, "workbench fully restores upgraded maximum");
            game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.DoubleBench).Index] = 25;
            int doubleSeed = RuntimeSmokeTest.EffectSeed(UpgradeKind.DoubleBench,0.25);
            game.BeginProbeRun(4, doubleSeed, 1); game.Energy.TrySpend(game.Energy.Maximum); tool = game.SelectedTool;
            bench = game.World.DebugSetCell(-10, 10, BlockKind.Workbench); bench.DestroyBlock(tool);
            check(tool.Durability == tool.MaxDurability && game.Energy.Value == game.Energy.Maximum && game.Benefits.DoubleBenches == 1, "double workbench helps maximum-tier tool with full energy");
            game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.DoubleBench).Index] = 0;
            game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.LastChance).Index] = 40;
            int rescueSeed = RuntimeSmokeTest.EffectSeed(UpgradeKind.LastChance,0.2);
            game.BeginProbeRun(0, rescueSeed, 1); game.Energy.TrySpend(game.Energy.Maximum); tool = game.SelectedTool; tool.DebugExhaust();
            check(tool.IsAlive && tool.Durability == Mathf.CeilToInt(tool.MaxDurability * 0.5f) && game.Energy.CanSpend(game.AbilityCost(AbilityKind.Plunge)), "rescue restores half durability and at least one plunge");
            game.BeginProbeRun(0,8127,1);tool=game.SelectedTool;
            var neighbors=new[] { game.World.DebugSetCell(-1,10,BlockKind.Stone),game.World.DebugSetCell(1,10,BlockKind.Stone),game.World.DebugSetCell(0,9,BlockKind.Stone),game.World.DebugSetCell(0,11,BlockKind.Stone) };
            game.World.DamageShrapnelNeighbour(0,10,4,tool,1);
            int damaged=0,totalDamage=0;foreach(var block in neighbors){int loss=12-block.HealthHalfUnits;if(loss>0)damaged++;totalDamage+=loss;}
            check(damaged==1 && totalDamage==4,"shrapnel damages exactly one adjacent material, with no area multiplication");
            game.FinishRun(); yield return new WaitForSeconds(1.5f);
            check(root.Q<Button>("show-benefits") != null && root.Q("mine-page").worldBound.yMax <= root.worldBound.yMax, "results fit and expose benefit details");
        }
    }
}
#endif
