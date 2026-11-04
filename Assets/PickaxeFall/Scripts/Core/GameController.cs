using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;

namespace PickaxeFall
{
    [DefaultExecutionOrder(-100)]
    public partial class GameController : MonoBehaviour
    {
        [SerializeField] GameBalance balance;
        [SerializeField] PanelSettings panelSettings;
        [SerializeField] StyleSheet interfaceStyle;
        readonly List<FallingPickaxe> tools = new();
        public GameBalance Balance => balance;
        public string ProfileDirectory { get; private set; }
        public string TestMode { get; private set; } = "";
        public RunPhase Phase { get; private set; }
        public FallingPickaxe Pickaxe => tools.Count > 0 ? tools[0] : null;
        public IReadOnlyList<FallingPickaxe> Pickaxes => tools;
        public MiningWorld World { get; private set; }
        public PhysicsScene2D PhysicsScene => gameObject.scene.GetPhysicsScene2D();
        public MiningEffects Effects { get; private set; }
        public SimulationClock Clock { get; } = new();
        public EffectRolls Rolls { get; } = new();
        public bool IsSimulation => Clock.Manual;
        public bool ReadyToPresent => initialized;
        public EconomyService Economy { get; private set; }
        public GameSettings Settings { get; private set; }
        public RunEnergy Energy { get; } = new();
        public RunBenefits Benefits { get; } = new();
        public int SelectedToolIndex { get; private set; }
        public FallingPickaxe SelectedTool => tools.Count > SelectedToolIndex ? tools[SelectedToolIndex] : null;
        public float AbilityCost(AbilityKind kind) => RunUpgrades.EnergyCost(kind == AbilityKind.Dash ? 45 : 60);
        public bool ControlsAvailable => Phase == RunPhase.Mining && !Paused && (IsSimulation || hud != null && !hud.ModalOpen);
        public RunUpgrades RunUpgrades { get; private set; }
        public ResourceInventory Inventory { get; private set; }
        public int Depth { get; private set; }
        public int BlocksBroken { get; private set; }
        public int BestDepth => Economy.Profile.bestDepth;
        public long Wallet => Economy.Profile.coins;
        public long PotentialSaleValue { get { long value = 0; foreach(var kind in ResourceInventory.Materials)value+=OreCounts[(int)kind]*Progression.SellPrices[(int)kind];return value; } }
        public int WorkbenchesUsed { get; private set; }
        public int RunToolCount { get; private set; } = 1;
        public int LivingToolCount { get { int count=0;foreach(var tool in tools)if(tool.IsAlive)count++;return count; } }
        public bool RescueUsed { get; private set; }
        public HubTab CurrentHubTab => hud.CurrentTab;
        public int Seed { get; private set; }
        public bool Paused { get; private set; }
        public bool CanTrade => Phase == RunPhase.Ready || Phase == RunPhase.Equipped || Phase == RunPhase.Finished;
        public Rect GameplayViewport => hud != null ? hud.GameplayViewport : new Rect(0.04f, 0.24f, 0.92f, 0.53f);
        public readonly long[] OreCounts = new long[ResourceInventory.SlotCount];
        public event Action Changed;
        GameHud hud;
        MineCamera cameraFollow;
        System.Random rouletteRandom;
        int selectedTier;
        bool initialized, dirty;
        float nextSave;

        public void Configure(GameBalance config, PanelSettings panel, StyleSheet style)
        { balance = config; panelSettings = panel; interfaceStyle = style; }

        void Start()
        {
            if (IsSimulation) return;
            Application.targetFrameRate = 60; Screen.orientation = ScreenOrientation.Portrait;
            Time.fixedDeltaTime = 1f / 60; Physics2D.simulationMode = SimulationMode2D.Script;
            Physics2D.gravity = new Vector2(0, balance.gravity);
            ProfileDirectory = Application.persistentDataPath;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (string arg in Environment.GetCommandLineArgs()) if (arg.StartsWith("--preview-") && arg!="--preview-exit") TestMode = arg.Substring(10);
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                using var intent = activity.Call<AndroidJavaObject>("getIntent");
                TestMode = intent.Call<string>("getStringExtra", "pickaxefall-test") ?? "";
            }
            catch (Exception e) { Debug.LogWarning("Test intent unavailable: " + e.Message); }
#endif
            if (Array.IndexOf(new[] { "smoke", "shop", "luck", "bag", "sale", "team", "mine", "settings", "controls", "abilities", "results", "benefits", "tutorial", "reset", "loot", "dungeon", "geode", "ores" }, TestMode) < 0) TestMode = "";
            bool probe = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--balance-probe" || a == "--economy-probe");
            if (probe) TestMode = "probe";
            else if (Array.Exists(Environment.GetCommandLineArgs(), a => a == "--smoke-test")) TestMode = "smoke";
            foreach(string mode in new[] {"savewrite","saveread","savefinished"})if(Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--"+mode+"-check"))TestMode=mode;
            if (TestMode.Length > 0) { Application.runInBackground=true; ProfileDirectory = Path.Combine(Application.temporaryCachePath, "verification", Guid.NewGuid().ToString("N")); }
            if(TestMode.StartsWith("save",StringComparison.Ordinal))
            {
                string id=Array.Find(Environment.GetCommandLineArgs(),a=>a.StartsWith("--resume-check-id=",StringComparison.Ordinal))?.Substring(18);
                if(!Guid.TryParseExact(id,"N",out var guid))throw new InvalidOperationException("A fresh resume-check-id GUID is required.");
                ProfileDirectory=Path.Combine(Application.temporaryCachePath,"verification","resume-"+guid.ToString("N"));
            }
#endif
            Settings = new GameSettings(TestMode.Length > 0);
            var store = new ProfileStore(ProfileDirectory, TestMode.Length > 0 ? () => new PlayerProfile() : null);
            try { Economy = new EconomyService(store.Load(), store.Save, store.Reset); }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                var errorDocument = gameObject.AddComponent<UIDocument>(); errorDocument.panelSettings = panelSettings;
                errorDocument.rootVisualElement.styleSheets.Add(interfaceStyle);
                var message = new Label("Сохранение недоступно.\n" + e.Message) { name = "save-error" };
                message.AddToClassList("fatal-error"); errorDocument.rootVisualElement.Add(message); return;
            }
            Inventory = new ResourceInventory(Economy); RunUpgrades = new RunUpgrades(Economy.Profile);
            rouletteRandom = new System.Random(Guid.NewGuid().GetHashCode());
            InitializeWorldAndTools();
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>();
            cameraFollow = cameraObject.AddComponent<MineCamera>(); cameraFollow.Initialize(this);
            var document = gameObject.AddComponent<UIDocument>(); document.panelSettings = panelSettings;
            hud = new GameHud(this, document.rootVisualElement, interfaceStyle);
            initialized = true;
            if(Economy.Profile.run!=null)RestoreRun(Economy.Profile.run);else NewRun();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TestMode == "smoke" || Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--smoke-test")) gameObject.AddComponent<RuntimeSmokeTest>();
            if(TestMode.StartsWith("save",StringComparison.Ordinal))gameObject.AddComponent<RuntimeResumeCheck>();
            else if (TestMode.Length > 0 && TestMode != "smoke" && TestMode != "probe") gameObject.AddComponent<RuntimePreview>();
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--balance-probe" || arg == "--economy-probe")) gameObject.AddComponent<RuntimeBalanceProbe>();
#endif
        }

        void InitializeWorldAndTools()
        {
            originalBalance=balance;
            Effects = new GameObject("Mining effects").AddComponent<MiningEffects>(); Effects.transform.SetParent(transform); Effects.Initialize(Settings, IsSimulation);
            World = new GameObject("Streamed mine").AddComponent<MiningWorld>(); World.transform.SetParent(transform); World.Initialize(this, balance);
            if(!IsSimulation){var lighting=new GameObject("Mine light and shadows");lighting.transform.SetParent(transform);lighting.AddComponent<MineLighting>().Initialize(this);}
            for (int i = 0; i < 5; i++)
            {
                var tool = new GameObject("Physical pickaxe " + (i + 1)).AddComponent<FallingPickaxe>();
                tool.transform.SetParent(transform); tool.Initialize(this); tools.Add(tool); tool.gameObject.SetActive(false);
            }
        }

        public void NewRun()
        {
            if (!initialized) return;
            StopAllCoroutines(); Time.timeScale = 1; Paused = false; Effects.Clear();
            foreach (var tool in tools) { tool.Stop(); tool.gameObject.SetActive(false); }
            UseOriginalBalance();Economy.Profile.run=null;
            Depth = BlocksBroken = WorkbenchesUsed = 0; Array.Clear(OreCounts, 0, OreCounts.Length);
            RunToolCount = 1; RescueUsed = false; RunUpgrades = new RunUpgrades(Economy.Profile);
            SelectedToolIndex = 0; Benefits.Reset(); Energy.Reset(RunUpgrades); Clock.Reset(); hud?.CancelGesture();
            int previousSeed = Seed;
            do { Seed = Guid.NewGuid().GetHashCode() & int.MaxValue; } while (Seed == 0 || Seed == previousSeed);
            World.ResetWorld(Seed); Pickaxe.Equip(0); Pickaxe.gameObject.SetActive(false); cameraFollow?.ResetView();
            Phase = RunPhase.Ready; hud?.SelectTab(HubTab.Mine); Notify();
        }
        public void Spin()
        {
            if (Phase != RunPhase.Ready) return;
            Phase = RunPhase.Spinning; selectedTier = balance.Roll(rouletteRandom); Notify(); StartCoroutine(SpinRoutine());
        }
        IEnumerator SpinRoutine()
        {
            const int ticks = 24;
            var animationRandom = new System.Random(Guid.NewGuid().GetHashCode());
            for (int i = 0; i < ticks; i++)
            {
                hud.ShowRouletteTier(i == ticks - 1 ? selectedTier : balance.Roll(animationRandom));
                float progress = (float)i / (ticks - 1);
                yield return new WaitForSecondsRealtime(Mathf.Lerp(0.035f, 0.20f, progress * progress));
            }
            RunUpgrades = new RunUpgrades(Economy.Profile);
            Pickaxe.Equip(selectedTier); Phase = RunPhase.Equipped; Notify();
        }
        public void Drop() => StartDrop(0);
        void StartDrop(int forcedCount)
        {
            if (Phase != RunPhase.Equipped) return;
            RunUpgrades = new RunUpgrades(Economy.Profile); Rolls.Reset(unchecked(Seed ^ 0x6AC690C5));
#if BALANCE_LAB || UNITY_EDITOR
            if (IsSimulation) { Rolls.Forced[(int)UpgradeKind.TeamChance] = forcedTeam; Rolls.Forced[(int)UpgradeKind.LastChance] = forcedRescue; }
#endif
            RunToolCount = forcedCount > 0 ? Mathf.Clamp(forcedCount, 1, 5) : RollEffect(UpgradeKind.TeamChance) ? RunUpgrades.TeamSize : 1;
            for (int i = 0; i < tools.Count; i++)
            {
                if (i >= RunToolCount) { tools[i].gameObject.SetActive(false); continue; }
                tools[i].Equip(selectedTier, i, (i - (RunToolCount - 1) * 0.5f) * 1.4f); tools[i].Launch();
            }
            Energy.Reset(RunUpgrades); SelectTool(0);
            Phase = RunPhase.Mining; World.StreamTools(tools); cameraFollow?.ResetView(); Notify();
            if (RunToolCount > 1) hud?.Toast("УДАЧА! КИРОК В ОТРЯДЕ: " + RunToolCount);
        }
        public bool RollEffect(UpgradeKind kind) => Rolls.Roll(kind, RunUpgrades.Chance(kind));
        public int PreviewDamageHalfUnits(int tier) => balance.pickaxes[tier].damage * 2 + Progression.DamageStepHalfUnits(tier) * Economy.Profile.Level(new(UpgradeKind.Damage, tier));
        public int PreviewDurability(int tier) => balance.pickaxes[tier].durability + Progression.DurabilityStep(tier) * Economy.Profile.Level(new(UpgradeKind.Durability, tier));

        void Update()
        {
            if (!initialized || IsSimulation) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { if (!hud.CloseModal()) TogglePause(); }
            if (Phase == RunPhase.Mining && !Paused)
            {
                if (Time.unscaledTime >= nextSave) SaveProgress();
            }
            hud.Tick();
        }

        void FixedUpdate() { if (initialized && !IsSimulation) SimulateMiningStep(); }
        // The player and the lab use the same native physics entry point and ordering.
        public void SimulateMiningStep(Action<GameController> policy = null)
        {
            if (Phase != RunPhase.Mining || Paused) return;
            Clock.Advance(); Energy.Regenerate(Clock.Delta, RunUpgrades); StepMiningWorld(); policy?.Invoke(this);
            foreach (var tool in tools) if (tool.IsAlive) tool.PhysicsTick();
            Physics2D.SyncTransforms(); PhysicsScene.Simulate(Clock.Delta);
        }
        public void StepMiningWorld()
        {
            if (Phase != RunPhase.Mining || Paused) return;
            foreach (var tool in tools) if (tool.IsAlive) Depth = Mathf.Max(Depth, Mathf.FloorToInt(-tool.Body.position.y));
            World.StreamTools(tools);
        }

        public bool SelectTool(int index)
        {
            if (index < 0 || index >= tools.Count || !tools[index].IsAlive) return false;
            if (SelectedToolIndex != index) hud?.CancelGesture();
            SelectedToolIndex = index;
            foreach (var tool in tools) tool.SetSelected(tool.ToolIndex == index);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Phase == RunPhase.Mining) Debug.Log($"PICKAXEFALL_SELECT_TOOL: index={index}; energy={Energy.Value}");
#endif
            return true;
        }
        public bool TryUseAbility(AbilityKind kind, Vector2 direction)
        {
            if(!AbilityUnlocked(kind))return false;
            if (!ControlsAvailable || SelectedTool == null || !SelectedTool.IsAlive || SelectedTool.AbilityActive || !Energy.CanSpend(AbilityCost(kind))) return false;
            if (kind != AbilityKind.Dash && kind != AbilityKind.Plunge) return false;
            if (kind == AbilityKind.Dash)
            {
                if (direction.sqrMagnitude < 0.5f || float.IsNaN(direction.x) || float.IsNaN(direction.y)) return false;
                direction = Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? new Vector2(Mathf.Sign(direction.x), 0) : new Vector2(0, Mathf.Sign(direction.y));
            }
            if (!SelectedTool.StartAbility(kind, direction)) return false;
            Energy.TrySpend(AbilityCost(kind)); Benefits.Abilities++; return true;
        }
        public void OnPhysicalHit(FallingPickaxe source, bool strong)
        {
            if (Phase != RunPhase.Mining || Paused) return;
            if (strong) Benefits.StrongHits++;
        }
        // Bonus TNT is detonated by the world after the mined cell has been retired.
        public bool OnBlockBroken(MineBlock block, FallingPickaxe source, DamageOrigin origin)
        {
            if (Phase != RunPhase.Mining) return false;
            if (block.Kind == BlockKind.Workbench)
            {
                if (source == null) return false;
                WorkbenchesUsed++; int steps = RollEffect(UpgradeKind.DoubleBench) ? 2 : 1;
                source.RepairAndUpgrade(steps);
                if (steps == 2) { Benefits.DoubleBenches++; Energy.Ensure(Energy.Maximum); }
                Effects.Impact(block.transform.position, new Color32(105, 239, 196, 255), 18, true);
                hud?.Toast(steps == 2 ? "ВЕРСТАК ×2 · ПОЛНЫЙ РЕМОНТ И ЭНЕРГИЯ" : "ВЕРСТАК · ПОЛНЫЙ РЕМОНТ");
            }
            else
            {
                BlocksBroken++;
                if (origin == DamageOrigin.Dash || origin == DamageOrigin.Plunge) Benefits.AbilityBlocks++;
                if (origin == DamageOrigin.Shrapnel) Benefits.ShrapnelBlocks++;
                if (ResourceInventory.IsMaterial(block.Kind))
                {
                    int quantity = Progression.IsOre(block.Kind) && RollEffect(UpgradeKind.Fortune) ? 2 : 1;
                    Benefits.BonusOre += quantity - 1;GiveResource(ResourceInventory.DropKind(block.Kind),quantity,source,block.transform.position);
                }
                else if(block.Kind==BlockKind.Chest)AwardChest(block.Column,block.Row,source);
                Effects.Impact(block.transform.position, block.BaseColor, 7);
            }
            bool tnt = (origin == DamageOrigin.Physical || origin == DamageOrigin.Dash || origin == DamageOrigin.Plunge) && source != null && Progression.IsOre(block.Kind) && RollEffect(UpgradeKind.TntFind);
            if (tnt) Benefits.BonusTnt++;
            Notify(); return tnt;
        }

        public void OnPickaxeExhausted(FallingPickaxe tool)
        {
            bool last = true;
            foreach (var other in tools) if (other != tool && other.IsAlive) { last = false; break; }
            if (last && !RescueUsed)
            {
                RescueUsed = true;
                if (RollEffect(UpgradeKind.LastChance))
                {
                    tool.Revive(); Benefits.Rescues++; Benefits.RestoredDurability += tool.Durability; Energy.Ensure(AbilityCost(AbilityKind.Plunge));
                    hud?.Toast("ПОСЛЕДНИЙ ШАНС! +50% ПРОЧНОСТИ И ЭНЕРГИЯ"); Notify(); return;
                }
            }
            tool.BreakApart();
            if (tool == SelectedTool) for (int i = 0; i < tools.Count; i++) if (SelectTool(i)) break;
            foreach(var other in tools)other.SetSelected(other.ToolIndex==SelectedToolIndex);
            if (last) FinishRun(); else { hud?.Toast("КИРКА №" + (tool.ToolIndex + 1) + " РАССЫПАЛАСЬ"); Notify(); }
        }
        public void FinishRun()
        {
            if (Phase != RunPhase.Mining) return;
            if (IsSimulation) { StepMiningWorld(); Phase = RunPhase.Finished; foreach (var tool in tools) tool.Stop(); return; }
            foreach (var tool in tools)
            {
                if (tool.IsAlive) { Depth = Mathf.Max(Depth, Mathf.FloorToInt(-tool.transform.position.y)); tool.BreakApart(); }
            }
            hud.CancelGesture();
            Phase = RunPhase.Breaking; Paused = false; Time.timeScale = 1;
            Economy.Profile.bestDepth = Mathf.Max(BestDepth, Depth); SaveProgress();
            Notify(); StartCoroutine(FinishAnimation());
        }
        IEnumerator FinishAnimation() { yield return new WaitForSeconds(0.85f); Phase = RunPhase.Finished; Notify(); }

        public bool ResetProgress()
        {
            if(IsSimulation || (!CanTrade && !Paused))return false;
            if(!Economy.TryResetProgress()){hud?.Toast(Economy.LastError);return false;}
            Settings.SetTutorialSeen(false);Settings.Save();NewRun();hud?.ProgressReset();return true;
        }
        public bool Buy(UpgradeKey key)
        {
            if (!CanTrade) return false;
            bool success = Economy.TryBuy(key);
            if (success && Phase == RunPhase.Equipped) { RunUpgrades = new RunUpgrades(Economy.Profile); Pickaxe.Equip(selectedTier); }
            hud.Toast(success ? "УЛУЧШЕНИЕ КУПЛЕНО" : Economy.LastError); Notify(); return success;
        }
        public bool Sell(BlockKind kind, long quantity)
        {
            if (!CanTrade) return false;
            bool success = Economy.TrySell(kind, quantity); hud.Toast(success ? "РЕСУРСЫ ПРОДАНЫ" : Economy.LastError); Notify(); return success;
        }
        public bool SellAll()
        {
            if (!CanTrade) return false;
            bool success = Economy.TrySellAll(); hud.Toast(success ? "РЮКЗАК ПРОДАН" : Economy.LastError); Notify(); return success;
        }
        public void TogglePause()
        {
            if (Phase != RunPhase.Mining) return;
            hud?.CancelGesture(); Paused = !Paused; Time.timeScale = Paused ? 0 : 1; Notify();
            if(Paused)SaveProgress();
        }
        public bool SelectHubTab(HubTab tab) { if (!CanTrade) return false; hud.SelectTab(tab); return true; }
        void SaveProgress()
        {
            if (IsSimulation || !initialized) return;
            try { Economy.Profile.run=Phase==RunPhase.Mining?CaptureRun():null;Economy.Save(); Settings?.Save(); dirty = false; nextSave = Time.unscaledTime + 5; }
            catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); hud?.Toast("НЕ УДАЛОСЬ СОХРАНИТЬ ПРОГРЕСС"); nextSave = Time.unscaledTime + 10; }
        }
        void OnApplicationPause(bool pause) { if(IsSimulation || !pause)return;if(Phase==RunPhase.Mining && !Paused)TogglePause();else SaveProgress(); }
        void OnApplicationFocus(bool focus){if(!focus && initialized && !IsSimulation && TestMode.Length==0 && Phase==RunPhase.Mining && !Paused)TogglePause();}
        void OnApplicationQuit(){SaveProgress();quitting=true;}
        void OnDestroy() { if (initialized && !quitting) SaveProgress(); Time.timeScale = 1; hud?.Dispose();if(restoredBalance)Destroy(restoredBalance); }
        void Notify() => Changed?.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void BeginProbeRun(int tier, int seed, int count = 0)
        {
            NewRun(); Seed = seed; World.ResetWorld(seed); selectedTier = tier;
            RunUpgrades = new RunUpgrades(Economy.Profile); Pickaxe.Equip(tier);
            Phase = RunPhase.Equipped; StartDrop(count);
        }
#endif
#if BALANCE_LAB || UNITY_EDITOR
        int forcedTeam = -1, forcedRescue = -1;
        public void InitializeSimulation(GameBalance config)
        {
            Clock.Manual = true; balance = config; Settings = new GameSettings(true,false);
            Economy = new EconomyService(new PlayerProfile()); Inventory = new ResourceInventory(Economy);
            RunUpgrades = new RunUpgrades(Economy.Profile); Physics2D.gravity = new Vector2(0, config.gravity);
            InitializeWorldAndTools(); initialized = true;
        }
        public void BeginSimulation(int tier, int seed, int[] levels, int team, int rescue)
        {
            foreach (var tool in tools) { tool.Stop(); tool.gameObject.SetActive(false); }
            Array.Clear(Economy.Profile.resources, 0, ResourceInventory.SlotCount); Array.Copy(levels, Economy.Profile.upgrades, 25);
            Economy.Profile.dashUnlocked=Economy.Profile.plungeUnlocked=true;
            Economy.Profile.Validate(); Seed = seed; Clock.Reset(); forcedTeam = team; forcedRescue = rescue;
            Depth = BlocksBroken = WorkbenchesUsed = 0; RescueUsed = false; Array.Clear(OreCounts, 0, OreCounts.Length); Benefits.Reset();
            World.ResetWorld(seed); selectedTier = tier; Phase = RunPhase.Equipped; StartDrop(0);
        }
#endif
    }
}
