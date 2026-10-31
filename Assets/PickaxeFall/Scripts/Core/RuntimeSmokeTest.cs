#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public class RuntimeSmokeTest : MonoBehaviour
    {
        int assertions;
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string message, string stack, LogType type) { if (type == LogType.Exception || type == LogType.Error) Application.Quit(1); }
        void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception("Smoke: " + message); }
        IEnumerator Start()
        {
            yield return null;
            var game = GetComponent<GameController>(); var root = GetComponent<UIDocument>().rootVisualElement;
            Check(game.Phase == RunPhase.Ready && game.Pickaxes.Count == 5, "ready state and tool pool");
            Check(root.Q<Button>("main-action").resolvedStyle.fontSize >= 48, "large primary label");
            Check(game.SelectHubTab(HubTab.Shop) && root.Q("upgrade-Damage-0") != null, "shop opens with per-tier upgrades");
            Check(game.SelectHubTab(HubTab.Inventory) && root.Q<Button>("sell-bag") != null, "bag exposes sales");
            game.SelectHubTab(HubTab.Mine);
            game.Economy.Profile.coins = 100000; Array.Fill(game.Economy.Profile.resources, 1000L);
            Check(game.Buy(new(UpgradeKind.Damage)) && game.PreviewDamageHalfUnits(0) == 3, "half-unit upgrade applies only to wood");
            Check(game.Buy(new(UpgradeKind.Durability)) && game.PreviewDurability(0) == 30 && game.PreviewDamageHalfUnits(1) == 4, "durability and tier isolation");
            game.BeginProbeRun(0, 8127, 1); long wallet = game.Wallet;
            Check(game.Pickaxe.transform.position.y > 5 && game.Depth == 0, "spawn in sky");
            Check(!game.Buy(new(UpgradeKind.Damage)) && !game.Sell(BlockKind.Dirt, 1), "trade blocked during mining");
            var cell = game.World.DebugSetCell(-10, 5, BlockKind.Stone);
            cell.HitHalfUnits(1, game.Pickaxe);
            Check(cell.HealthHalfUnits == 11 && cell.DamageStage > 0 && cell.GetComponent<SpriteRenderer>().color.a == 1, "fractional hit produces cracks without transparency");
            game.World.Stream(2000); game.World.Stream(5); cell = game.World.CellAt(-10, 5);
            Check(cell && cell.HealthHalfUnits == 11, "partial damage survives streaming");
            cell.DestroyBlock(game.Pickaxe); long stones = game.Inventory[BlockKind.Stone];
            game.World.Stream(2000); game.World.Stream(5);
            Check(game.World.CellAt(-10, 5) == null && game.Inventory[BlockKind.Stone] == stones, "mined cells cannot respawn or duplicate loot");
            Check(game.Wallet == wallet, "mining gives resources without coins");
            float start = Time.time, hopStartY = 0, hopPeak = 0; bool sawHop = false;
            while (Time.time - start < 7 && game.Phase == RunPhase.Mining)
            {
                if (!sawHop && game.Pickaxe.HopCount > 0) { sawHop = true; hopStartY = game.Pickaxe.transform.position.y; }
                if (sawHop) hopPeak = Mathf.Max(hopPeak, game.Pickaxe.transform.position.y - hopStartY);
                yield return null;
            }
            Check(game.Pickaxe.HopCount > 0 && game.Pickaxe.TotalStrikes > 0 && hopPeak > 0.45f, "visible physical hop after hits");
            game.FinishRun(); yield return new WaitForSeconds(1);
            Check(game.Phase == RunPhase.Finished && game.Wallet == wallet, "finish does not credit unsold blocks");
            var saved = new ProfileStore(game.ProfileDirectory).Load();
            Check(saved.coins == wallet && saved.resources[1] == game.Inventory[BlockKind.Stone], "run resources saved with wallet");
            Check(game.SelectHubTab(HubTab.Inventory), "results allow selling");
            long dirt = game.Inventory[BlockKind.Dirt]; Check(game.Sell(BlockKind.Dirt, 1) && game.Wallet == wallet + 1 && game.Inventory[BlockKind.Dirt] == dirt - 1, "live sale updates wallet and inventory");
            // Controlled RNG seeds exercise rare mechanics without changing their production probabilities.
            game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.TntFind).Index] = 20;
            int rareSeed = 1; while (new System.Random(unchecked(rareSeed ^ 0x6AC690C5)).NextDouble() >= 0.1) rareSeed++;
            game.BeginProbeRun(0, rareSeed, 1);
            for (int row = 3; row <= 7; row++) for (int x = -12; x <= -8; x++) game.World.DebugSetCell(x, row, BlockKind.Stone);
            var ore = game.World.DebugSetCell(-9, 5, BlockKind.Coal);
            var protectedBench = game.World.DebugSetCell(-10, 6, BlockKind.Workbench);
            var created = game.World.CellAt(-10, 5); long beforeStone = game.Inventory[BlockKind.Stone], beforeCoal = game.Inventory[BlockKind.Coal];
            created.DestroyBlock(game.Pickaxe);
            Check(created.Kind == BlockKind.Tnt && !created.IsBroken && game.Inventory[BlockKind.Stone] == beforeStone + 1, "direct mining awards resource and leaves TNT in the same cell");
            Physics2D.SyncTransforms(); created.DestroyBlock(game.Pickaxe);
            Check(game.Inventory[BlockKind.Coal] == beforeCoal + 1 && !protectedBench.IsBroken, "explosion grants ore and protects workbenches");
            bool recursiveTnt = false;
            for (int row = 3; row <= 7; row++) for (int x = -12; x <= -8; x++)
            { var remaining = game.World.CellAt(x, row); if (remaining && remaining.Kind == BlockKind.Tnt) recursiveTnt = true; }
            Check(!recursiveTnt && !created.DestroyBlock(game.Pickaxe), "explosions cannot create bonus TNT or trigger the spent cell twice");
            game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.TntFind).Index] = 0;
            game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.DoubleBench).Index] = 20;
            game.BeginProbeRun(0, rareSeed, 5);
            Check(game.RunToolCount == 5, "five independent tools spawn");
            for (int i = 0; i < 5; i++)
            {
                var tool = game.Pickaxes[i]; Check(tool.IsAlive && tool.OriginalTier == 0 && tool.transform.position.y > 5, "group spawn above terrain " + i);
                for (int j = 0; j < i; j++) Check(Vector2.Distance(tool.Body.position, game.Pickaxes[j].Body.position) > 1.1f, "separated group spawn");
            }
            var bench = game.World.DebugSetCell(-10, 13, BlockKind.Workbench); int before = game.Pickaxes[2].DamageHalfUnits;
            bench.DestroyBlock(game.Pickaxes[2]);
            Check(game.Pickaxes[2].Tier == 2 && game.Pickaxes[2].OriginalTier == 0 && game.Pickaxes[2].DamageHalfUnits > before && game.Pickaxes[0].Tier == 0, "workbench targets hitting tool and retains original upgrades");
            game.Pickaxes[0].DebugExhaust(); Check(game.Phase == RunPhase.Mining && game.Pickaxes[1].IsAlive, "one broken tool cannot end a group run");
            for (int i = 1; i < 5; i++)
            {
                var tool = game.Pickaxes[i]; tool.Stop(); tool.Body.interpolation = RigidbodyInterpolation2D.None;
                tool.transform.position = new Vector3((i - 2) * 3, -i * 28, 0); tool.Body.position = tool.transform.position;
            }
            yield return new WaitForSeconds(1.5f);
            Check(game.World.LoadedRows <= 5 * (game.Balance.rowsAhead + game.Balance.rowsBehind + 2), "streaming stays bounded for dispersed tools");
            Rect visible = game.GameplayViewport;
            for (int i = 1; i < 5; i++)
            {
                Vector3 point = Camera.main.WorldToViewportPoint(game.Pickaxes[i].transform.position);
                Check(point.x > visible.xMin - 0.03f && point.x < visible.xMax + 0.03f && point.y > visible.yMin - 0.03f && point.y < visible.yMax + 0.03f, "camera fits living tool " + i);
            }
            game.FinishRun(); yield return new WaitForSeconds(1);
            Array.Clear(game.Economy.Profile.upgrades, 0, 18); game.Economy.Profile.upgrades[new UpgradeKey(UpgradeKind.LastChance).Index] = 10;
            int rescueSeed = 1; while (new System.Random(unchecked(rescueSeed ^ 0x6AC690C5)).NextDouble() >= 0.05) rescueSeed++;
            game.BeginProbeRun(0, rescueSeed, 1); game.Pickaxe.DebugExhaust();
            Check(game.Phase == RunPhase.Mining && game.RescueUsed && game.Pickaxe.Durability == 7, "last tool recovers exactly rounded 25 percent");
            game.Pickaxe.DebugExhaust(); Check(game.Phase == RunPhase.Breaking, "rescue cannot repeat");
            yield return new WaitForSeconds(1); game.NewRun();
            Check(game.Depth == 0 && game.Pickaxe.transform.position.y > 5, "retry resets both physics and camera spawn");
            Debug.Log("PICKAXEFALL_SMOKE_OK: " + assertions + " checks; trading, half damage, opaque cracks, stronger hops, group lifecycle, streaming, camera, workbench and rescue.");
            Application.Quit(0);
        }
    }
}
#endif
