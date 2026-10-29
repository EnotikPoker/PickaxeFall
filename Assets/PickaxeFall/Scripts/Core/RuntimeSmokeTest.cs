#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    // Runs against the real scene and physics in a Development player, never in release APKs.
    public class RuntimeSmokeTest : MonoBehaviour
    {
        GameController game;
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error) { Debug.Log("PICKAXEFALL_SMOKE_FAILED: " + message); Application.Quit(1); } }

        IEnumerator Start()
        {
            game = GetComponent<GameController>();
            yield return null;
            Check(game.Phase == RunPhase.Ready, "Scene starts ready.");
            long inventoryAtStart = game.Inventory.Total;
            Debug.Log("INVENTORY_LOADED: " + inventoryAtStart);
            int menuSeed = game.Seed;
            Check(game.SelectHubTab(HubTab.Shop) && game.CurrentHubTab == HubTab.Shop, "The shop placeholder tab opens.");
            Check(game.Inventory.Total == inventoryAtStart && game.Phase == RunPhase.Ready, "The shop placeholder does not spend items or start a run.");
            yield return Capture("07-shop");
            var shopBack = GetComponent<UIDocument>().rootVisualElement.Q<Button>("shop-back");
            Check(shopBack.worldBound.height > 25 && shopBack.worldBound.yMax <= GetComponent<UIDocument>().rootVisualElement.worldBound.yMax,
                "The shop return button is visible and has a usable height.");
            Check(game.SelectHubTab(HubTab.Inventory) && game.CurrentHubTab == HubTab.Inventory, "The inventory tab opens.");
            Check(game.SelectHubTab(HubTab.Mine) && game.Seed == menuSeed, "Navigation returns to the same expedition.");
            AssertSafeSpawn();
            int firstSeed = game.Seed, firstMap = game.World.LayoutFingerprint();
            game.World.ResetWorld(firstSeed);
            Check(game.World.LayoutFingerprint() == firstMap, "The same world seed reproduces the same terrain.");
            game.NewRun();
            Check(game.Seed != firstSeed && game.World.LayoutFingerprint() != firstMap, "A new round generates a different procedural map.");
            var font = Resources.Load<Font>("Fonts/Monocraft");
            Check(font && font.HasCharacter('Я') && font.HasCharacter('ё'), "Pixel font includes Cyrillic glyphs.");
            Check(PixelArt.BlockColor(BlockKind.Iron, true) == PixelArt.BlockColor(BlockKind.Deepstone), "Deep ore uses the surrounding deepstone color.");
            Check(PixelArt.Block(BlockKind.Iron, 1, true) != PixelArt.Block(BlockKind.Iron, 1, false), "Surface and deep ores have distinct sprites.");
            var damageObject = new GameObject("Damage overlay test");
            var damageBlock = damageObject.AddComponent<MineBlock>();
            damageBlock.Setup(game.World, BlockKind.Stone, 80, 10000, 0);
            int lastStage = 0;
            while (damageBlock.Health > 1)
            {
                damageBlock.Hit(1);
                Check(damageBlock.DamageStage > lastStage, "Each hit grows the crack overlay.");
                Check(damageObject.GetComponent<SpriteRenderer>().color == Color.white, "Damage never fades or tints the base block.");
                lastStage = damageBlock.DamageStage;
            }
            damageBlock.Setup(game.World, BlockKind.Stone, 80, 10000, 0);
            Check(damageBlock.DamageStage == 0 && !damageObject.transform.GetChild(0).GetComponent<SpriteRenderer>().enabled,
                "A reused block clears all previous cracks.");
            Destroy(damageObject);
            yield return Capture("01-roulette");
            game.Spin();
            float deadline = Time.realtimeSinceStartup + 8;
            while (game.Phase == RunPhase.Spinning && Time.realtimeSinceStartup < deadline) yield return null;
            Check(game.Phase == RunPhase.Equipped, "Roulette must equip a pickaxe.");
            Check(game.Pickaxe.Durability > 0, "Equipped pickaxe has durability.");
            AssertSafeSpawn();
            yield return Capture("02-equipped");
            game.Drop();
            Check(game.Pickaxe.Body.simulated, "Drop enables real physics.");
            Check(!game.SelectHubTab(HubTab.Shop) && game.CurrentHubTab == HubTab.Mine, "Hub navigation cannot interrupt an active fall.");
            yield return new WaitForFixedUpdate();
            yield return null;
            Check(game.Pickaxe.transform.position.y > 5 && game.Depth == 0 && game.BlocksBroken == 0, "Drop starts in the sky, not inside the ground.");
            bool sawHop = false;
            for (int i = 0; i < 60 && game.Phase == RunPhase.Mining; i++)
            {
                yield return new WaitForSeconds(0.15f);
                if (game.Pickaxe.HopCount > 0 && game.Pickaxe.Body.linearVelocity.y > 0.5f) sawHop = true;
            }
            Check(game.BlocksBroken > 0, "Physical collisions break blocks.");
            Check(game.Depth >= 2, "Mining advances below the surface.");
            Check(game.Pickaxe.Durability < game.Pickaxe.MaxDurability, "Strikes consume durability.");
            Check(game.Pickaxe.HopCount > 0 && sawHop, "Real strikes make the pickaxe hop upward.");
            Check(game.OreCounts[(int)BlockKind.Dirt] > 0, "Surface dirt is collected as a resource.");
            int earned = 0;
            foreach (BlockKind kind in Enum.GetValues(typeof(BlockKind))) earned += game.OreCounts[(int)kind] * game.Balance.Block(kind).reward;
            Check(game.Coins == earned, "Destroyed blocks award exactly the configured coins.");
            yield return Capture("03-mining");
            foreach (var kind in ResourceInventory.Materials)
            {
                MineBlock specimen = null;
                for (int row = 12; row < 25 && !specimen; row++)
                    for (int x = 8; x < 18; x++)
                    {
                        specimen = game.World.CellAt(x, row);
                        if (specimen) break;
                    }
                Check(specimen, "A real terrain cell is available for resource collection.");
                int xCell = Mathf.RoundToInt(specimen.transform.position.x), rowCell = specimen.Row;
                specimen.Setup(game.World, kind, xCell, rowCell, 0);
                int beforeRun = game.OreCounts[(int)kind], beforeInventory = game.Inventory[kind];
                specimen.Hit(int.MaxValue);
                Check(game.OreCounts[(int)kind] == beforeRun + 1 && game.Inventory[kind] == beforeInventory + 1,
                    "Mining collects exactly one item into run and persistent inventory: " + kind);
            }
            Check(!ResourceInventory.IsMaterial(BlockKind.Tnt) && !ResourceInventory.IsMaterial(BlockKind.Workbench),
                "TNT and workbenches are interactions, not collectible materials.");
            game.Inventory.Save();
            var reloaded = new ResourceInventory(); reloaded.Load();
            foreach (var kind in ResourceInventory.Materials)
                Check(reloaded[kind] == game.Inventory[kind], "Saved resource survives reload: " + kind);
            MineBlock tnt = null, chainedTnt = null, blastTarget = null;
            for (int row = 10; row < 25 && !tnt; row++)
                for (int x = 8; x < 16; x++)
                {
                    var a = game.World.CellAt(x, row); var b = game.World.CellAt(x + 1, row); var c = game.World.CellAt(x + 2, row);
                    if (!a || !b || !c) continue;
                    tnt = a; chainedTnt = b; blastTarget = c;
                    a.Setup(game.World, BlockKind.Tnt, x, row, 0);
                    b.Setup(game.World, BlockKind.Tnt, x + 1, row, 0);
                    c.Setup(game.World, BlockKind.Gold, x + 2, row, 0);
                    break;
                }
            Check(tnt && chainedTnt && blastTarget, "Explosion test finds neighboring real terrain cells.");
            Physics2D.SyncTransforms();
            int gold = game.OreCounts[(int)BlockKind.Gold];
            tnt.Hit(1);
            Check(tnt.IsBroken && chainedTnt.IsBroken && blastTarget.IsBroken, "A struck TNT triggers a chain explosion and destroys nearby blocks.");
            Check(game.OreCounts[(int)BlockKind.Gold] > gold, "Explosions award ore rewards.");
            int blastCoins = game.Coins;
            tnt.Hit(1);
            Check(game.Coins == blastCoins, "A spent TNT cannot explode or reward twice.");
            if (game.Phase == RunPhase.Mining)
            {
                game.TogglePause(); Check(Time.timeScale == 0 && game.Paused, "Pause freezes physics.");
                game.TogglePause(); Check(Time.timeScale == 1 && !game.Paused, "Resume restores physics.");
                int oldTier = game.Pickaxe.Tier;
                var benchObject = new GameObject("Test workbench"); var bench = benchObject.AddComponent<MineBlock>();
                bench.Setup(game.World, BlockKind.Workbench, 0, 10000, 1);
                game.OnBlockBroken(bench);
                Check(game.Pickaxe.Tier == Mathf.Min(oldTier + 1, game.Balance.pickaxes.Length - 1), "Workbench upgrades the pickaxe.");
                Check(game.Pickaxe.Durability == game.Pickaxe.MaxDurability, "Workbench repairs to full durability.");
                Destroy(benchObject);
            }
            int wallet = game.Wallet;
            if (game.Phase == RunPhase.Mining)
            {
                // A very short-lived tool exercises actual collision-driven exhaustion.
                int[] durability = new int[game.Balance.pickaxes.Length];
                for (int i = 0; i < durability.Length; i++)
                { durability[i] = game.Balance.pickaxes[i].durability; game.Balance.pickaxes[i].durability = 1; }
                game.Pickaxe.Equip(0); game.Pickaxe.Launch();
                deadline = Time.realtimeSinceStartup + 12;
                while (game.Phase == RunPhase.Mining && Time.realtimeSinceStartup < deadline) yield return null;
                for (int i = 0; i < durability.Length; i++) game.Balance.pickaxes[i].durability = durability[i];
                Check(game.Phase == RunPhase.Breaking && game.Pickaxe.Durability == 0, "Real strikes start the break animation at zero durability.");
                Check(!game.Pickaxe.IsVisible && !game.Pickaxe.Body.simulated, "The broken pickaxe disappears and stops colliding.");
                Check(game.Effects.LastShatterPieces > 10 && game.Effects.ActiveToolFragments > 0, "Breaking scatters fragments from the actual pickaxe sprite.");
                yield return Capture("05-pickaxe-break");
                Check(game.Wallet == wallet + game.Coins, "Run rewards persist once.");
            }
            int coins = game.Coins;
            int savedWallet = game.Wallet; game.FinishRun();
            Check(game.Wallet == savedWallet, "Duplicate finish cannot award coins twice.");
            deadline = Time.realtimeSinceStartup + 3;
            while (game.Phase == RunPhase.Breaking && Time.realtimeSinceStartup < deadline) yield return null;
            Check(game.Phase == RunPhase.Finished, "Results appear after the fragments animation.");
            yield return Capture("04-results");
            long savedResources = game.Inventory.Total;
            Check(game.SelectHubTab(HubTab.Inventory), "Inventory opens from the results screen.");
            yield return null;
            var inventoryPage = GetComponent<UIDocument>().rootVisualElement.Q<VisualElement>("inventory-page");
            Check(inventoryPage.resolvedStyle.display == DisplayStyle.Flex, "The inventory page is actually visible.");
            var inventoryBack = inventoryPage.Q<Button>("inventory-back");
            Check(inventoryBack.worldBound.height > 25 && inventoryBack.worldBound.yMax <= GetComponent<UIDocument>().rootVisualElement.worldBound.yMax,
                "The inventory return button is visible and has a usable height.");
            foreach (var kind in ResourceInventory.Materials)
                Check(inventoryPage.Q<Label>("count-" + kind).text == game.Inventory[kind].ToString(),
                    "Inventory UI displays the collected quantity: " + kind);
            yield return Capture("08-inventory");
            Check(game.SelectHubTab(HubTab.Mine) && game.Phase == RunPhase.Finished, "Returning from inventory preserves run results.");
            var lateBlock = game.World.CellAt(16, 16);
            if (lateBlock) game.OnBlockBroken(lateBlock);
            Check(game.Coins == coins && game.Wallet == savedWallet, "Late destruction cannot change a completed run's rewards.");
            Check(game.Inventory.Total == savedResources, "Late destruction cannot duplicate persistent resources.");
            game.World.Stream(1500);
            Check(game.World.ActiveBlocks <= game.Balance.worldWidth * (game.Balance.rowsAhead + game.Balance.rowsBehind + 1), "Streaming keeps active blocks bounded.");
            Check(game.World.transform.childCount <= game.Balance.worldWidth * (game.Balance.rowsAhead + game.Balance.rowsBehind + 1) + 1,
                "A large streaming jump also keeps the allocated block pool bounded.");
            // Reproduce the reported failure: both the tool and camera were deep underground.
            game.Pickaxe.transform.position = new Vector3(0, -120, 0);
            game.Pickaxe.Body.position = new Vector2(0, -120);
            Camera.main.transform.position = new Vector3(0, -120, -10);
            int previousSeed = game.Seed;
            game.NewRun();
            Check(game.Phase == RunPhase.Ready && game.Coins == 0 && game.Depth == 0 && game.BlocksBroken == 0, "Retry resets the run.");
            Check(game.Wallet == savedWallet, "Retry preserves the wallet.");
            Check(game.Inventory.Total == savedResources, "Retry preserves collected resources.");
            foreach (var kind in ResourceInventory.Materials)
                Check(game.OreCounts[(int)kind] == 0, "Retry clears only the per-run resource counter: " + kind);
            Check(game.Effects.ActiveToolFragments == 0, "Retry clears fragments left by the previous pickaxe.");
            Check(game.Seed != previousSeed, "Retry changes the world seed.");
            AssertSafeSpawn();
            game.Spin();
            deadline = Time.realtimeSinceStartup + 8;
            while (game.Phase == RunPhase.Spinning && Time.realtimeSinceStartup < deadline) yield return null;
            Check(game.Phase == RunPhase.Equipped, "Retry roulette equips a tool.");
            AssertSafeSpawn(); game.Drop();
            yield return new WaitForFixedUpdate(); yield return null;
            Check(game.Pickaxe.transform.position.y > 5 && game.Depth == 0, "Retry drop also begins above ground.");
            Debug.Log($"PICKAXEFALL_SMOKE_OK: roulette, physical mining, depth, durability, rewards, workbench, pause, bounded streaming, results and retry. Previous run: {coins} coins.");
            Application.Quit(0);
        }

        void AssertSafeSpawn()
        {
            Check(Mathf.Abs(game.Pickaxe.transform.position.y - game.Balance.spawnHeight) < 0.01f &&
                Mathf.Abs(game.Pickaxe.Body.position.y - game.Balance.spawnHeight) < 0.01f,
                "Visible and physics spawn poses agree high above the surface.");
            Check(Camera.main.transform.position.y > 0, "The camera resets to the sky at the start of a round.");
            Vector3 screen = Camera.main.WorldToViewportPoint(game.Pickaxe.transform.position);
            Check(screen.x > 0 && screen.x < 1 && screen.y > 0.2f && screen.y < 0.85f,
                "The spawn is visible below the top HUD.");
            foreach (var collider in Physics2D.OverlapBoxAll(game.Pickaxe.transform.position, Vector2.one * 2, 0))
                Check(!collider.GetComponent<MineBlock>(), "The spawn does not overlap any terrain.");
        }

        static IEnumerator Capture(string name)
        {
            // Screenshots only when running with a graphics device.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            yield return new WaitForEndOfFrame();
            string dir = Path.Combine(Environment.CurrentDirectory, "Docs/Screenshots"); Directory.CreateDirectory(dir);
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return new WaitForSecondsRealtime(0.2f);
        }
        static void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); Debug.Log("SMOKE PASS: " + message); }
    }
}
#endif
