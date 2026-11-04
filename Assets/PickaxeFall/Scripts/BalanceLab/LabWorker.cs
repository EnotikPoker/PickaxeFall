#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.SceneManagement;
using System.IO;
using UnityEngine;

namespace PickaxeFall.BalanceLab
{
    public sealed class LabPolicy
    {
        float next = 2;
        public void Tick(GameController game)
        {
            if (game.Clock.Now < next || game.SelectedTool == null || !game.SelectedTool.IsAlive) return;
            Vector2 position = game.SelectedTool.Body.position, direction = Vector2.down;
            int centerX = Mathf.RoundToInt(position.x), centerRow = Mathf.FloorToInt(-position.y), best = 0;
            for (int row = centerRow - 3; row <= centerRow + 3; row++) for (int x = centerX - 3; x <= centerX + 3; x++)
            {
                var cell = game.World.CellAt(x, row); if (!cell) continue;
                Vector2 delta = (Vector2)cell.transform.position - position;
                if (delta.magnitude < .5f || delta.magnitude > 3.2f || Mathf.Abs(delta.x) <= Mathf.Abs(delta.y)) continue;
                int score = cell.Kind == BlockKind.Workbench ? 100 : Progression.IsOre(cell.Kind) ? 10 + (int)cell.Kind * 4 : 0;
                if (score > best) { best = score; direction = new Vector2(Mathf.Sign(delta.x), 0); }
            }
            bool used = game.TryUseAbility(best > 0 ? AbilityKind.Dash : AbilityKind.Plunge, direction);
            next = game.Clock.Now + (used ? 10 : .2f);
        }
    }
    public static class LabWorker
    {
        static readonly List<AsyncOperation> unloading = new();
        public static Scene NewWorld() => SceneManager.CreateScene("Lab physics "+Guid.NewGuid().ToString("N"),new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        public static void Unload(Scene scene) { unloading.Add(SceneManager.UnloadSceneAsync(scene)); }
        public static IEnumerator FlushWorlds()
        {
            while(unloading.Any(op=>!op.isDone)) yield return null;
            unloading.Clear();
        }
        public static LabResult Execute(GameBalance balance, LabJob job, Func<bool> cancelled = null)
        {
            var watch = Stopwatch.StartNew();
            var result = new LabResult { id = job.Id, scenario = job.scenario.id, seed = job.seed };
            var profile = new PlayerProfile { upgrades = (int[])job.scenario.levels.Clone() }; profile.Validate();
            var upgrades = new RunUpgrades(profile);
            double teamChance = upgrades.Chance(UpgradeKind.TeamChance), rescueChance = upgrades.Chance(UpgradeKind.LastChance);
            if (job.diagnostic) { teamChance = teamChance > 0 ? 1 : 0; rescueChance = rescueChance > 0 ? 1 : 0; }
            for (int team = 0; team <= (teamChance > 0 ? 1 : 0); team++)
            for (int rescue = 0; rescue <= (rescueChance > 0 ? 1 : 0); rescue++)
            {
                double weight = (team == 1 ? teamChance : 1 - teamChance) * (rescue == 1 ? rescueChance : 1 - rescueChance);
                if (weight <= 0) continue;
                if (cancelled?.Invoke() == true) throw new OperationCanceledException();
                var scene = NewWorld(); var root = new GameObject("Isolated balance simulation");
                SceneManager.MoveGameObjectToScene(root,scene);
                try
                {
                    var game = root.AddComponent<GameController>(); game.InitializeSimulation(balance);
                    game.BeginSimulation(job.scenario.tier, job.seed, job.scenario.levels, team, rescue);
                    if (job.diagnostic) for (int i = 0; i < 17; i++) if (upgrades.Chance((UpgradeKind)i) > 0) game.Rolls.Forced[i] = 1;
                    var policy = new LabPolicy(); int maxTicks = job.limit * 60;
                    while (game.Phase == RunPhase.Mining && game.Clock.Ticks < maxTicks)
                    {
                        if (game.Clock.Ticks % 120 == 0 && cancelled?.Invoke() == true) throw new OperationCanceledException();
                        Step(game, job.scenario.active ? policy : null);
                    }
                    result.branches++; result.seconds += weight * game.Clock.Now; result.depth += weight * game.Depth;
                    result.value += weight * game.PotentialSaleValue; result.blocks += weight * game.BlocksBroken;
                    result.benches += weight * game.WorkbenchesUsed; result.tools += weight * game.RunToolCount;
                    result.abilities += weight * game.Benefits.Abilities;
                    if (game.Phase == RunPhase.Mining) result.censored += weight;
                    foreach (var tool in game.Pickaxes) result.strikes += weight * tool.TotalStrikes;
                    for (int i = 0; i < ResourceInventory.SlotCount; i++) result.resources[i] += weight * game.OreCounts[i];
                    for (int i = 0; i < 17; i++) { result.attempts[i] += weight * game.Rolls.Attempts[i]; result.successes[i] += weight * game.Rolls.Successes[i]; }
                }
                finally { UnityEngine.Object.DestroyImmediate(root); Unload(scene); }
            }
            result.wallSeconds = watch.Elapsed.TotalSeconds;
            return result;
        }
        public static void Step(GameController game, LabPolicy policy = null)
        {
            game.SimulateMiningStep(policy == null ? null : policy.Tick);
        }
        public static IEnumerator Serve(GameBalance balance, string cancelFile, string responseFile)
        {
            Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
            Physics2D.simulationMode = SimulationMode2D.Script; Physics2D.gravity = new Vector2(0, balance.gravity);
            Time.fixedDeltaTime = 1f / 60;
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                LabJob job = null;
                try
                {
                    job = JsonUtility.FromJson<LabJob>(line);
                    var result = Execute(balance, job, () => File.Exists(cancelFile));
                    LabFiles.Write(responseFile,result);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception e)
                {
                    LabFiles.Write(responseFile,new LabResult { id = job?.Id, scenario = job?.scenario.id, seed = job?.seed ?? 0, error = e.ToString() });
                }
                yield return FlushWorlds();
            }
            yield return FlushWorlds(); Application.Quit();
        }
    }
}
#endif
