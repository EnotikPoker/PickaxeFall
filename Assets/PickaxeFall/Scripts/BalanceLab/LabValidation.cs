#if BALANCE_LAB || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PickaxeFall.BalanceLab
{
    public static class LabValidation
    {
        static int count;
        static void Check(bool condition, string message) { count++; if (!condition) throw new Exception("Balance Lab: "+message); }
        public static void Validate()
        {
            count = 0;AuditContractChecks.Validate(); var quick = new LabPlan(); var scenarios = LabPlanner.Scenarios(quick);
            Check(scenarios.Count == 520,"quick matrix with all future reachable tool upgrades");
            Check(scenarios.Select(s=>s.id).Distinct().Count()==scenarios.Count,"unique scenario ids");
            Check(scenarios.All(s=>s.point==0 && s.background==0 && s.order<=2),"quick scope");
            Check(LabPlanner.Jobs(quick,scenarios).Count()==4160,"quick eight-map count");
            var full = new LabPlan { mode=1, samples=32 }; var pairs = LabPlanner.Scenarios(full);
            Check(pairs.Count==19800,"pair matrix for 25/23/21/19/17 reachable keys");
            var triples = LabPlanner.Scenarios(new LabPlan { mode=2 });
            Check(triples.Count==163560,"triple matrix");
            Check(triples.Count(s=>s.order==3)==143760,"reachable-key triple count × 3 × 2 × 2");
            foreach (var s in scenarios)
            {
                Check(s.levels.Length==Progression.KeyCount,"profile length");
                Check(Progression.Keys.Where(k=>k.Kind<=UpgradeKind.Durability && k.Tier<s.tier).All(k=>s.levels[k.Index]==0),"unreachable lower type excluded");
            }
            Check(LabPlanner.Level(UpgradeKind.TeamSize,1)==2 && LabPlanner.Level(UpgradeKind.Fortune,1)==13,"ceil middle levels");
            Check(scenarios.Any(s=>s.tier==0 && s.mask==(1<<4)),"diamond damage sampled for a wooden start that can reach diamond");
            var rolls = new EffectRolls(); rolls.Reset(8127); bool[] expected = Enumerable.Range(0,100).Select(_=>rolls.Roll(UpgradeKind.Fortune,.5)).ToArray();
            rolls.Reset(8127); bool[] actual = Enumerable.Range(0,100).Select(_=> { rolls.Roll(UpgradeKind.Careful,.5); return rolls.Roll(UpgradeKind.Fortune,.5); }).ToArray();
            Check(expected.SequenceEqual(actual),"independent random streams");
            rolls.Reset(8127); rolls.Forced[(int)UpgradeKind.LastChance]=1; Check(rolls.Roll(UpgradeKind.LastChance,.005),"forced diagnostic branch");
            Check(!rolls.Roll(UpgradeKind.TeamChance,0),"zero chance remains impossible");
            string path = Path.Combine(Application.temporaryCachePath,"lab-validation-"+Guid.NewGuid().ToString("N"));
            try
            {
                var plan = new LabPlan { samples=8, tiers=new[] {true,false,false,false,false}, active=false };
                var manifest = new LabManifest { schema=1,version="check",unity=Application.unityVersion,fingerprint="test-fingerprint",roulette=new[] {.45,.3,.15,.08,.02} }.CaptureEconomy();
                using var session = LabSession.Create(path,plan,manifest);
                var baseline = session.Scenarios.First(s=>s.order==0); var damage = session.Scenarios.First(s=>s.mask==1);
                for (int i=0;i<8;i++)
                {
                    var a = new LabResult { id=baseline.id+"-"+i,scenario=baseline.id,seed=i,seconds=52,value=60,depth=10 };
                    var b = new LabResult { id=damage.id+"-"+i,scenario=damage.id,seed=i,seconds=52,value=120,depth=20 };
                    session.Results.Add(a.id,a); session.Results.Add(b.id,b);
                    File.AppendAllText(Path.Combine(path,"results.jsonl"),JsonUtility.ToJson(a)+"\n"+JsonUtility.ToJson(b)+"\n");
                }
                var analysis = new LabAnalysis(session); var summary = analysis.Get(damage);
                Check(Math.Abs(summary.Rate-120)<1e-8,"eight second overhead included");
                Check(Math.Abs(analysis.Delta(summary)-60)<1e-8,"paired delta");
                var ci=analysis.Interval(summary); Check(ci.low==60 && ci.high==60 && ci.count==8,"paired bootstrap");
                Check(analysis.Cost(summary).coins==30 && analysis.Cost(summary).total==46,"resource opportunity cost");
                analysis.Export(path); Check(File.Exists(Path.Combine(path,"report.html")) && File.Exists(Path.Combine(path,"summary.csv")),"standalone report and CSV");
                File.AppendAllText(Path.Combine(path,"results.jsonl"),"{incomplete");
                using var loaded=LabSession.Load(path); Check(loaded.Results.Count==16,"interrupted journal recovery without duplication");
                Check(LabComparison.Describe(session,loaded,0,0,0,false).Contains("Δ монет/мин 0"),"version comparison on common seeds");
                var customPlan=new LabPlan { mode=3, active=false };
                customPlan.customLevels[0]=1;customPlan.customLevels[new UpgradeKey(UpgradeKind.Durability,4).Index]=2;
                using(var custom=LabSession.Create(Path.Combine(path,"custom"),customPlan,manifest))
                {
                    foreach(var scenario in custom.Scenarios.Where(x=>x.mask!=0))
                    {
                        var row=new LabResult { id=scenario.id+"-42",scenario=scenario.id,seed=42,seconds=10,value=10+scenario.tier };
                        custom.Results.Add(row.id,row);
                    }
                    var weighted=new LabAnalysis(custom).View(-1,0,0,false).First(x=>x.scenario.mask!=0);
                    Check(weighted.Count==1 && Math.Abs(weighted.rows[0].value-10.92)<1e-9,"custom multi-tier weighted profile");
                }
                var savedPrice=manifest.prices.First(p=>p.key==0 && p.level==1);savedPrice.coins+=100;
                Check(new LabAnalysis(session).Cost(new LabAnalysis(session).Get(damage)).coins==130,"history uses captured costs, not current economy");savedPrice.coins-=100;
                var savedPrices=manifest.prices;manifest.prices=null;
                Check(double.IsNaN(new LabAnalysis(session).Cost(new LabAnalysis(session).Get(damage)).total),"legacy history does not invent old upgrade costs");manifest.prices=savedPrices;
                loaded.Plan.diagnostic=true; Check(LabComparison.Describe(session,loaded,0,0,0,false).Contains("несовместимы"),"diagnostic reports cannot masquerade as real comparisons");
                using(var worlds=LabSession.Create(Path.Combine(path,"worlds"),new LabPlan{mode=6,samples=1,seconds=900,maps=new[]{true,true,true}},manifest))
                {
                    var campaign=worlds.Scenarios.First(x=>x.hourly);var row=new LabResult{id="world-export-test",scenario=campaign.id,seed=1,profileLevelTimes=new double[]{0,-1,-1,-1,-1},profileSkillsBlocked=new double[4],profileMiningBlocked=new double[4],profileCoinsBlocked=new double[4],finalProfileLevel=1};worlds.Results.Add(row.id,row);
                    var physical=worlds.Scenarios.First(x=>!x.hourly);var empty=new LabResult{id="physical-export-test",scenario=physical.id,seed=1,profileLevelTimes=Array.Empty<double>(),profileSkillsBlocked=Array.Empty<double>(),profileMiningBlocked=Array.Empty<double>(),profileCoinsBlocked=Array.Empty<double>()};worlds.Results.Add(empty.id,empty);
                    new LabAnalysis(worlds).Export(worlds.DirectoryPath);
                    Check(File.Exists(Path.Combine(worlds.DirectoryPath,"profile-levels.csv"))&&File.Exists(Path.Combine(worlds.DirectoryPath,"profile-maps.csv")),"world audit exports campaign tables");
                    Check(File.ReadAllText(Path.Combine(worlds.DirectoryPath,"report.html")).Contains("Уровни профиля"),"world audit includes profile HTML section");
                }
            }
            finally { if (Directory.Exists(path)) Directory.Delete(path,true); }
            Debug.Log("PICKAXEFALL_LAB_MODEL_OK: "+count+" checks; matrix, streams, costs, bootstrap, recovery and export.");
        }
        public static void PhysicsChecks(GameBalance balance)
        {
            var old=Physics2D.simulationMode; Physics2D.simulationMode=SimulationMode2D.Script;
            try
            {
                var s=new LabScenario { id="repeatability",tier=0,levels=new int[Progression.KeyCount] };
                var job=new LabJob { scenario=s,seed=8127,limit=600 };
                var a=LabWorker.Execute(balance,job); LabWorker.Execute(balance,new LabJob { scenario=s,seed=19073,limit=600 }); var b=LabWorker.Execute(balance,job);
                var consecutive=LabWorker.Execute(balance,job);
                Check(a.seconds==consecutive.seconds && a.resources.SequenceEqual(consecutive.resources),"consecutive identical maps exactly match");
                Check(a.seconds==b.seconds && a.depth==b.depth && a.resources.SequenceEqual(b.resources),"repeat after different map exactly matches");
                Check(a.value>0 && a.strikes==balance.pickaxes[0].durability,"real collisions mine and exhaust wood durability");
                Check(a.censored==0 && a.branches==1 && a.tools==1,"natural baseline end");
                s.levels[new UpgradeKey(UpgradeKind.TeamChance).Index]=1;
                var team=LabWorker.Execute(balance,job); Check(team.branches==2 && Math.Abs(team.tools-1.005)<1e-9,"weighted team probability, not forced team metrics");
                s.levels[new UpgradeKey(UpgradeKind.LastChance).Index]=1;
                var both=LabWorker.Execute(balance,job); Check(both.branches==4,"joint event strata");
                s.active=true; var active=LabWorker.Execute(balance,job); Check(active.abilities>0,"active policy uses actual game abilities");
                var censored=LabWorker.Execute(balance,new LabJob { scenario=s,seed=8127,limit=1 }); Check(censored.censored==1 && Math.Abs(censored.seconds-1)<1e-6,"exact physical tick horizon");
                Debug.Log("PICKAXEFALL_LAB_PHYSICS_OK: repeatable fixed-step physics; weighted rare effects; active policy; censoring.");
            }
            finally { Physics2D.simulationMode=old; }
        }
        public static IEnumerator ParityChecks(GameBalance balance)
        {
            var previousMode=Physics2D.simulationMode; float oldScale=Time.timeScale;
            GameObject root=null; Scene scene=default;
            try
            {
                Time.fixedDeltaTime=1f/60; Application.targetFrameRate=-1;
                for (int fixture=0;fixture<4;fixture++)
                {
                    Physics2D.simulationMode=SimulationMode2D.Script;
                    var scenario=new LabScenario { id="parity-"+fixture,tier=fixture<2?0:fixture==2?1:4,active=fixture==1||fixture==3,levels=new int[Progression.KeyCount] };
                    if(fixture==2)scenario.levels[new UpgradeKey(UpgradeKind.Careful).Index]=40;
                    if(fixture==3) { scenario.levels[new UpgradeKey(UpgradeKind.EnergyCharge).Index]=20;scenario.levels[new UpgradeKey(UpgradeKind.PlungeDepth).Index]=3; }
                    var job=new LabJob { scenario=scenario,seed=8127,limit=600 };
                    var manual=LabWorker.Execute(balance,job);
                    scene=LabWorker.NewWorld(); root=new GameObject("Automatic fixed-step parity");SceneManager.MoveGameObjectToScene(root,scene); var game=root.AddComponent<GameController>(); game.InitializeSimulation(balance); game.BeginSimulation(scenario.tier,8127,scenario.levels,0,0);
                    foreach(var tool in game.Pickaxes) tool.Body.interpolation=RigidbodyInterpolation2D.Interpolate;
                    var driver=root.AddComponent<LabParityDriver>();driver.game=game;driver.policy=scenario.active?new LabPolicy():null;
                    Physics2D.simulationMode=SimulationMode2D.Script; Time.timeScale=20;
                    while(game.Phase==RunPhase.Mining && game.Clock.Ticks<36000)yield return null;
                    driver.enabled=false;
                    Debug.Log($"LAB_PARITY_SAMPLE: fixture={fixture}; manual={manual.seconds}/{manual.depth}/{manual.value}; auto={game.Clock.Now}/{game.Depth}/{game.PotentialSaleValue}; autoResources={string.Join(",",game.OreCounts)}; manualResources={string.Join(",",manual.resources)}");
                    Check(game.Clock.Now==manual.seconds && game.Depth==manual.depth && game.PotentialSaleValue==manual.value && Enumerable.Range(0,ResourceInventory.SlotCount).All(i=>game.OreCounts[i]==manual.resources[i]),"batch / rendered-frame physics parity fixture "+fixture);
                    UnityEngine.Object.DestroyImmediate(root);root=null;LabWorker.Unload(scene);yield return LabWorker.FlushWorlds();
                }
                Debug.Log("PICKAXEFALL_LAB_PARITY_OK: four exact batch / rendered-frame fixed-step comparisons, including rotation, upgrades and active abilities.");
            }
            finally { if(root){UnityEngine.Object.DestroyImmediate(root);LabWorker.Unload(scene);}Physics2D.simulationMode=previousMode;Time.timeScale=oldScale; }
        }
    }
    [DefaultExecutionOrder(100)]
    public sealed class LabParityDriver : MonoBehaviour
    {
        public GameController game;
        public LabPolicy policy;
        void FixedUpdate() { if(game.Phase==RunPhase.Mining && game.Clock.Ticks<36000)game.SimulateMiningStep(policy == null ? null : policy.Tick); }
    }
}
#endif
