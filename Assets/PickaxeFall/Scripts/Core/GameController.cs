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
        public GameBalance ShopBalance=>originalBalance?originalBalance:balance;
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
        public float AbilityCost(AbilityKind kind) => RunUpgrades.EnergyCost(kind == AbilityKind.Dash ? RunUpgrades.Config.dashCost : RunUpgrades.Config.plungeCost);
        public bool ControlsAvailable => Phase == RunPhase.Mining && !WorldFrozen && (IsSimulation || hud != null && !hud.ModalOpen);
        public RunUpgrades RunUpgrades { get; private set; }
        public ResourceInventory Inventory { get; private set; }
        public int Depth { get; private set; }
        public int BlocksBroken { get; private set; }
        public int BestDepth => Economy.Profile.mapRecords[(int)CurrentMap];
        public long Wallet => Economy.Profile.coins;
        public long PotentialSaleValue { get { long value = 0; foreach(var kind in ResourceInventory.Materials)value+=OreCounts[(int)kind]*RunUpgrades.Config.sellPrices[(int)kind];return value; } }
        public int WorkbenchesUsed { get; private set; }
        public int RunToolCount { get; private set; } = 1;
        public int LivingToolCount { get { int count=0;foreach(var tool in tools)if(tool.IsAlive)count++;return count; } }
        public bool RescueUsed { get; private set; }
        public HubTab CurrentHubTab => hud.CurrentTab;
        public int Seed { get; private set; }
        public bool Paused { get; private set; }
        public bool SystemSuspended { get; private set; }
        public bool TrialOpen => World != null && World.ActiveTrial != null;
        public bool WorldFrozen => Paused || SystemSuspended || TrialOpen || PlatformFrozen;
        public float SessionSeconds => Clock.Now + Benefits.TrialSeconds;
        void RefreshTimeScale() { if(!IsSimulation)Time.timeScale=WorldFrozen?0:1; }
        internal void TrialChanged() { trialFrameDelta=0;hud?.CancelUiPresses(); RefreshTimeScale(); Notify(); SaveProgress(); }
        public void SetApplicationSuspended(bool value)
        {
            if(!initialized || IsSimulation)return;
            hud?.CancelUiPresses();SystemSuspended=value;if(!value){skipTrialFrame=true;Platform?.Google?.PurchaseTickOnResume();}RefreshTimeScale();if(value)SaveProgress();
        }

        public bool CanTrade => !PlatformFrozen && (Phase == RunPhase.Ready || Phase == RunPhase.Equipped || Phase == RunPhase.Finished);
        public Rect GameplayViewport => hud != null ? hud.GameplayViewport : new Rect(0.04f, 0.24f, 0.92f, 0.53f);
        public readonly long[] OreCounts = new long[ResourceInventory.SlotCount];
        public event Action Changed;
        GameHud hud;
        MineCamera cameraFollow;
        System.Random rouletteRandom;
        int selectedTier;
        bool initialized, dirty, skipTrialFrame;
        float nextSave,lastMinedTime,trialFrameDelta,nextWebSave;

        public void Configure(GameBalance config, PanelSettings panel, StyleSheet style)
        { balance = config; panelSettings = panel; interfaceStyle = style; }

        IEnumerator Start()
        {
            if (IsSimulation) yield break;
            Application.runInBackground=true; Application.targetFrameRate = 60; Screen.orientation = ScreenOrientation.Portrait;
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
            if (Array.IndexOf(new[] { "touch", "touch-edge", "smoke", "shop", "luck", "bag", "sale", "team", "mine", "settings", "controls", "abilities", "results", "benefits", "tutorial", "reset", "loot", "dungeon", "geode", "ores", "liquids", "benches", "profile", "bridge", "portal", "locks", "surplus", "mapmine", "pinned", "desert", "nether", "temple", "fortress", "mineshaft", "treasure", "google" }, TestMode) < 0) TestMode = "";
            bool probe = Array.Exists(Environment.GetCommandLineArgs(), a => a == "--balance-probe" || a == "--economy-probe");
            if (probe) TestMode = "probe";
#if PICKAXEFALL_PLAY_QA
            if(Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--google-check"))TestMode="google";
#endif
            if(Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--localization-check"))TestMode="localization";
            else if(Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--visual-checks"))TestMode="visual";
            else if(Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--input-checks"))TestMode="input";
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
            Platform=gameObject.AddComponent<PlatformHost>();
            var bootstrap=Platform.Initialize(this,panelSettings,interfaceStyle,ProfileDirectory,TestMode.Length>0);
            while(true)
            {
                bool move;try{move=bootstrap.MoveNext();}catch(Exception e){ShowStartupError(e.Message);yield break;}
                if(!move)break;yield return bootstrap.Current;
            }
            Settings = new GameSettings(TestMode.Length > 0);
            var store = Platform.Store;
            try { Economy = new EconomyService(store.Load(), store.Save, store.Reset, balance.progression); }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                var errorDocument = gameObject.AddComponent<UIDocument>(); errorDocument.panelSettings = panelSettings;
                errorDocument.rootVisualElement.styleSheets.Add(interfaceStyle);
                var message = new Label(L.T("Сохранение недоступно.\n") + L.T("Не удалось прочитать сохранение. Резервные файлы сохранены.")) { name = "save-error" };
                message.AddToClassList("fatal-error"); errorDocument.rootVisualElement.Add(message); yield break;
            }
            if(Economy.Profile.resetNotice){Settings.SetTutorialSeen(false);Settings.Save();}
            Economy.ReserveSteps=Settings.ReserveSteps;
            Inventory = new ResourceInventory(Economy); RunUpgrades = new RunUpgrades(Economy.Profile,balance.progression);
            rouletteRandom = new System.Random(Guid.NewGuid().GetHashCode());
            InitializeWorldAndTools();
            Audio=gameObject.AddComponent<GameAudio>();Audio.Initialize(this);
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>();
            cameraFollow = cameraObject.AddComponent<MineCamera>(); cameraFollow.Initialize(this);
            var document = gameObject.AddComponent<UIDocument>(); document.panelSettings = panelSettings;
#if PICKAXEFALL_DEVTOOLS
            RestoreDeveloperAccess();
#endif
            hud = new GameHud(this, document.rootVisualElement, interfaceStyle);
            initialized = true;
#if PICKAXEFALL_DEVTOOLS
            gameObject.AddComponent<DeveloperDiagnostics>().Initialize(this);
#endif
            if(Economy.Profile.run!=null)RestoreRun(Economy.Profile.run);else NewRun();
            Platform.GameReady();PlatformStateChanged();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TestMode == "smoke" || Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--smoke-test")) gameObject.AddComponent<RuntimeSmokeTest>();
            if(TestMode.StartsWith("touch",StringComparison.Ordinal))gameObject.AddComponent<RuntimeTouchProbe>();
            if(TestMode=="input")gameObject.AddComponent<RuntimeInputLightingChecks>();
            if(TestMode=="localization")gameObject.AddComponent<RuntimeLocalizationChecks>();
            if(TestMode=="visual")gameObject.AddComponent<RuntimeVisualChecks>();
#if PICKAXEFALL_PLAY_QA
            if(TestMode=="google")gameObject.AddComponent<RuntimeGooglePlayChecks>();
#endif
            if(TestMode.StartsWith("save",StringComparison.Ordinal))gameObject.AddComponent<RuntimeResumeCheck>();
            else if (TestMode.Length > 0 && TestMode != "smoke" && TestMode != "probe" && TestMode!="input" && TestMode!="localization" && TestMode!="visual" && TestMode!="google" && !TestMode.StartsWith("touch",StringComparison.Ordinal)) gameObject.AddComponent<RuntimePreview>();
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
            UseOriginalBalance();ApplySelectedMap();World.RestoreTrials(null);Economy.Profile.run=null;
            BenchVisits.Clear(); Depth = BlocksBroken = WorkbenchesUsed = 0; Array.Clear(OreCounts, 0, OreCounts.Length);
            RunToolCount = 1; RescueUsed = false; RunUpgrades = new RunUpgrades(Economy.Profile,balance.progression);
            SelectedToolIndex = 0; Benefits.Reset(); Energy.Reset(RunUpgrades); Clock.Reset();lastMinedTime=0; hud?.CancelUiPresses();
            int previousSeed = Seed;
            do { Seed = Guid.NewGuid().GetHashCode() & int.MaxValue; } while (Seed == 0 || Seed == previousSeed);
            World.ResetWorld(Seed); Pickaxe.Equip(0); Pickaxe.gameObject.SetActive(false); Audio?.SetMap(CurrentMap);cameraFollow?.SetMap();cameraFollow?.ResetView();
            Phase = RunPhase.Ready; RefreshTimeScale(); hud?.SelectTab(HubTab.Mine); Notify();
        }
        const float SpinDuration=2.2f;
        float spinElapsed;int spinFrame=-1;
        System.Random spinPresentation;
        public void Spin()
        {
            if(Phase!=RunPhase.Ready)return;
            hud?.CancelUiPresses();selectedTier=balance.Roll(rouletteRandom);
            spinElapsed=0;spinFrame=-1;spinPresentation=new System.Random(Guid.NewGuid().GetHashCode());
            Phase=RunPhase.Spinning;Notify();AdvanceRoulette(0);
        }
        internal void AdvanceRoulette(float seconds)
        {
            if(Phase!=RunPhase.Spinning || SystemSuspended || seconds<0 || float.IsNaN(seconds)||float.IsInfinity(seconds))return;
            spinElapsed+=seconds;
            if(spinElapsed>=SpinDuration){CompleteRoulette();return;}
            int frame=Mathf.Min(22,Mathf.FloorToInt(Mathf.Sqrt(spinElapsed/SpinDuration)*23));
            if(frame==spinFrame)return;spinFrame=frame;Audio?.Play("roll");
            hud?.ShowRouletteTier(balance.Roll(spinPresentation));
        }
        void CompleteRoulette()
        {
            if(Phase!=RunPhase.Spinning)return;
            // Always present the original draw, including recovery from interruption.
            RunUpgrades=new RunUpgrades(Economy.Profile,balance.progression);Pickaxe.Equip(selectedTier);
            Phase=RunPhase.Equipped;hud?.CancelUiPresses();Audio?.Play("reward");Notify();
        }
        public void Drop() => StartDrop(0);
        void StartDrop(int forcedCount)
        {
            if (Phase != RunPhase.Equipped) return;
            RunUpgrades = new RunUpgrades(Economy.Profile,balance.progression); Rolls.Reset(unchecked(Seed ^ 0x6AC690C5));
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
            if(!IsSimulation&&Platform!=null&&Platform.HasAdvertising)Platform.Ads.StartRun();
            Phase = RunPhase.Mining; World.StreamTools(tools); cameraFollow?.ResetView(); Notify();
            if (RunToolCount > 1) hud?.Toast(L.T("УДАЧА! КИРОК В ОТРЯДЕ: ") + RunToolCount);
        }
        public bool RollEffect(UpgradeKind kind) => Rolls.Roll(kind, RunUpgrades.Chance(kind));
        public int PreviewDamageHalfUnits(int tier) => ShopBalance.pickaxes[tier].damage * 2 + Progression.DamageStepHalfUnits(tier,Economy.Config) * Economy.Profile.Level(new(UpgradeKind.Damage, tier));
        public int PreviewDurability(int tier) => ShopBalance.pickaxes[tier].durability + Progression.DurabilityStep(tier,Economy.Config) * Economy.Profile.Level(new(UpgradeKind.Durability, tier));

        void Update()
        {
            if (!initialized || IsSimulation) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { if (!hud.CloseModal()) TogglePause(); }
            if (Phase == RunPhase.Mining && !Paused)
            {
                if (Time.unscaledTime >= nextSave || Platform!=null&&Platform.IsYandex&&dirty&&Time.unscaledTime>=nextWebSave) SaveProgress();
            }
            #if PICKAXEFALL_DEVTOOLS
            if(DeveloperInfiniteEnergy&&Phase==RunPhase.Mining)Energy.Ensure(Energy.Maximum);
#endif
            trialFrameDelta=0;
            if(skipTrialFrame)skipTrialFrame=false;
            else
            {
                if(!PlatformFrozen)AdvanceRoulette(Time.unscaledDeltaTime);
                if(!Paused&&!SystemSuspended&&!PlatformFrozen)trialFrameDelta=Time.unscaledDeltaTime;
            }
            TickPlatform();hud.Tick();
        }

        void LateUpdate()
        {
            // Complete this frame's input before deciding that the tap timer expired.
            if(initialized&&!IsSimulation&&!Paused&&!SystemSuspended&&!PlatformFrozen&&trialFrameDelta>0)World.AdvanceTrial(trialFrameDelta);
            trialFrameDelta=0;
        }
        void FixedUpdate() { if (initialized && !IsSimulation) SimulateMiningStep(); }
        // The player and the lab use the same native physics entry point and ordering.
        public void SimulateMiningStep(Action<GameController> policy = null)
        {
            if (Phase != RunPhase.Mining || WorldFrozen) return;
            Clock.Advance(); if(!IsSimulation&&Platform!=null&&Platform.HasAdvertising)Platform.Ads.Advance(Clock.Delta);if(Clock.Now-lastMinedTime>15)Benefits.SlowMiningSeconds+=Clock.Delta; Energy.Regenerate(Clock.Delta, RunUpgrades); StepMiningWorld(); World.Liquids.Tick(); World.TickTrials(); policy?.Invoke(this);
            foreach (var tool in tools) if (tool.IsAlive) tool.PhysicsTick();
            if(WorldFrozen)return;
            Physics2D.SyncTransforms(); PhysicsScene.Simulate(Clock.Delta);
        }
        public void StepMiningWorld()
        {
            if (Phase != RunPhase.Mining || WorldFrozen) return;
            foreach (var tool in tools) if (tool.IsAlive) Depth = Mathf.Max(Depth, Mathf.FloorToInt(-tool.Body.position.y));
            World.StreamTools(tools);
        }

        public bool SelectTool(int index)
        {
            if (index < 0 || index >= tools.Count || !tools[index].IsAlive) return false;
            if (SelectedToolIndex != index) hud?.CancelUiPresses();
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
            Audio?.Play("dash");Benefits.EnergySpent+=AbilityCost(kind);Energy.TrySpend(AbilityCost(kind)); Benefits.Abilities++; return true;
        }
        public void OnPhysicalHit(FallingPickaxe source, bool strong)
        {
            if (Phase != RunPhase.Mining || WorldFrozen) return;
            if (strong) Benefits.StrongHits++;
        }
        // Bonus TNT is detonated by the world after the mined cell has been retired.
        public bool OnBlockBroken(MineBlock block, FallingPickaxe source, DamageOrigin origin)
        {
            if (Phase != RunPhase.Mining) return false;
            Audio?.Play(GameBalance.IsWorkbench(block.Kind)?"bench":"break");
            if (GameBalance.IsWorkbench(block.Kind))
            {
                if (source == null) return false;
                WorkbenchesUsed++;
                bool doubled=RollEffect(UpgradeKind.DoubleBench),upgrading=block.Kind==BlockKind.UpgradeWorkbench;
                int steps=balance.rulesVersion<2?(doubled?2:1):upgrading?(doubled?2:1):doubled?1:0;
                int before=source.Durability,tierBefore=source.Tier,maximumBefore=source.MaxDurability;
                source.RepairAndUpgrade(steps);
                if(doubled){Benefits.DoubleBenches++;if(balance.rulesVersion<2 || source.Tier==balance.pickaxes.Length-1)Energy.Ensure(Energy.Maximum);}
                if(upgrading)Benefits.UpgradeBenches++;
                RecordBench(source,block,before,maximumBefore,tierBefore);
                Effects.Impact(block.transform.position,upgrading?new Color32(81,233,228,255):new Color32(105,239,196,255),18,true);
                hud?.Toast((upgrading?L.T("УЛУЧШАЮЩИЙ ВЕРСТАК"):L.T("ВЕРСТАК"))+(doubled?" ×2":"")+L.T(" · ПОЛНЫЙ РЕМОНТ")+(source.Tier>tierBefore?L.T(" И ПОВЫШЕНИЕ"):""));
            }
            else
            {
                BlocksBroken++;lastMinedTime=Clock.Now;
                if (origin == DamageOrigin.Dash || origin == DamageOrigin.Plunge) Benefits.AbilityBlocks++;
                if (origin == DamageOrigin.Shrapnel) Benefits.ShrapnelBlocks++;
                if (ResourceInventory.IsMaterial(block.Kind))
                {
                    Economy.RecordMined(block.Kind);
                    int quantity = Progression.IsOre(block.Kind) && RollEffect(UpgradeKind.Fortune) ? 2 : 1;
                    if((block.Kind==BlockKind.Dirt||block.Kind==BlockKind.Sand) && origin is DamageOrigin.Physical or DamageOrigin.Dash or DamageOrigin.Plunge && RollEffect(UpgradeKind.EarthDigger)){quantity++;Benefits.BonusEarth++;}
                    Benefits.BonusOre += Progression.IsOre(block.Kind)?quantity-1:0;GiveResource(ResourceInventory.DropKind(block.Kind),quantity,source,block.transform.position);
                }
                else if(block.Kind==BlockKind.Chest || block.Kind==BlockKind.MinecartChest)AwardChest(block.Column,block.Row,source);
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
                    hud?.Toast(L.T("ПОСЛЕДНИЙ ШАНС! +50% ПРОЧНОСТИ И ЭНЕРГИЯ")); Notify(); return;
                }
            }
            tool.BreakApart();
            if (tool == SelectedTool) for (int i = 0; i < tools.Count; i++) if (SelectTool(i)) break;
            foreach(var other in tools)other.SetSelected(other.ToolIndex==SelectedToolIndex);
            if (last) FinishRun(); else { hud?.Toast(L.T("КИРКА №") + (tool.ToolIndex + 1) + L.T(" РАССЫПАЛАСЬ")); Notify(); }
        }
        public void FinishRun()
        {
            if (Phase != RunPhase.Mining) return;
            if (IsSimulation) { StepMiningWorld(); Phase = RunPhase.Finished; foreach (var tool in tools) tool.Stop(); return; }
            foreach (var tool in tools)
            {
                if (tool.IsAlive) { Depth = Mathf.Max(Depth, Mathf.FloorToInt(-tool.transform.position.y)); tool.BreakApart(); }
            }
            hud.CancelUiPresses();
            Phase = RunPhase.Breaking; Paused = false; RefreshTimeScale();
            if(Platform!=null&&Platform.HasAdvertising)Platform.Ads.Finish(PotentialSaleValue);
            Economy.Profile.mapRecords[(int)CurrentMap]=Mathf.Max(BestDepth,Depth);if(CurrentMap==MapId.Mine)Economy.Profile.bestDepth=Economy.Profile.mapRecords[0]; SaveProgress();
            Notify(); StartCoroutine(FinishAnimation());
        }
        IEnumerator FinishAnimation() { yield return new WaitForSeconds(0.85f); Phase = RunPhase.Finished; SaveProgress();Notify(); }

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
            if (success && Phase == RunPhase.Equipped) { RunUpgrades = new RunUpgrades(Economy.Profile,balance.progression); Pickaxe.Equip(selectedTier); }
            if(success)Audio?.Ui(UiSound.Skill);else Audio?.Play("error");hud.Toast(success ? L.T("УЛУЧШЕНИЕ КУПЛЕНО") : Economy.LastError); Notify(); return success;
        }
        public bool Sell(BlockKind kind, long quantity)
        {
            if (!CanTrade) return false;
            bool success = Economy.TrySell(kind, quantity); if(success)Audio?.Ui(UiSound.Sale);else Audio?.Play("error");hud.Toast(success ? L.T("РЕСУРСЫ ПРОДАНЫ") : Economy.LastError); Notify(); return success;
        }
        public bool SellAll()
        {
            if (!CanTrade) return false;
            bool success = Economy.TrySellAll(); if(success)Audio?.Ui(UiSound.Sale);else Audio?.Play("error");hud.Toast(success ? L.T("РЮКЗАК ПРОДАН") : Economy.LastError); Notify(); return success;
        }
        public void TogglePause()
        {
            if (Phase != RunPhase.Mining || PlatformFrozen) return;
            hud?.CancelUiPresses(); Paused = !Paused; RefreshTimeScale(); Notify();
            if(Paused)SaveProgress();
        }
        public bool SelectHubTab(HubTab tab) { if (!CanTrade) return false; hud.SelectTab(tab); return true; }
        bool SaveProgress()
        {
            if (IsSimulation || !initialized) return false;
            try { Economy.Profile.run=Phase==RunPhase.Mining?CaptureRun():null;Economy.Save(); Settings?.Save(); dirty = false;nextWebSave=0; nextSave = Time.unscaledTime + 5; return true; }
            catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); hud?.Toast(L.T("НЕ УДАЛОСЬ СОХРАНИТЬ ПРОГРЕСС")); nextSave = Time.unscaledTime + 10;nextWebSave=nextSave; return false; }
        }
        void ShowStartupError(string error)
        {
            var doc=gameObject.AddComponent<UIDocument>();doc.panelSettings=panelSettings;doc.rootVisualElement.styleSheets.Add(interfaceStyle);
            var label=new Label(L.T("Сохранение недоступно.\n")+error);label.AddToClassList("fatal-error");doc.rootVisualElement.Add(label);
            var retry=new Button(()=>Platform.Reload()){text=L.T("Повторить загрузку")};retry.AddToClassList("stone-button");doc.rootVisualElement.Add(retry);
        }
        void OnApplicationPause(bool pause) => SetApplicationSuspended(pause);
        void OnApplicationFocus(bool focus)
        {
            if(focus && initialized && !IsSimulation)Platform?.Google?.PurchaseTickOnResume();
            if(Platform!=null&&Platform.IsYandex){SetApplicationSuspended(!focus);return;}
            if(!focus && initialized && !IsSimulation){hud?.CancelUiPresses();SaveProgress();}
        }
        void OnApplicationQuit(){SaveProgress();quitting=true;}
        void OnDestroy() { if (initialized && !quitting) SaveProgress(); Time.timeScale = 1; hud?.Dispose();if(restoredBalance)Destroy(restoredBalance); }
        void Notify(){dirty=true;Changed?.Invoke();}

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void BeginProbeRun(int tier, int seed, int count = 0)
        {
            NewRun(); Seed = seed; World.ResetWorld(seed); selectedTier = tier;
            RunUpgrades = new RunUpgrades(Economy.Profile,balance.progression); Pickaxe.Equip(tier);
            Phase = RunPhase.Equipped; StartDrop(count);
        }
#endif
#if BALANCE_LAB || UNITY_EDITOR
        int forcedTeam = -1, forcedRescue = -1;
        public void InitializeSimulation(GameBalance config)
        {
            Clock.Manual = true; balance = config; Settings = new GameSettings(true,false);
            Economy = new EconomyService(new PlayerProfile(),config:config.progression); Inventory = new ResourceInventory(Economy);
            RunUpgrades = new RunUpgrades(Economy.Profile,balance.progression); Physics2D.gravity = new Vector2(0, config.gravity);
            InitializeWorldAndTools(); initialized = true;
        }
        public void BeginSimulation(int tier, int seed, int[] levels, int team, int rescue,bool dashUnlocked=true,bool plungeUnlocked=true)
        {
            UseOriginalBalance();World.SetBalance(balance);Paused=false;lastMinedTime=0;
            foreach (var tool in tools) { tool.Stop(); tool.gameObject.SetActive(false); }
            Array.Clear(Economy.Profile.resources, 0, ResourceInventory.SlotCount); Array.Copy(levels, Economy.Profile.upgrades, Progression.KeyCount);
            Economy.Profile.dashUnlocked=dashUnlocked;Economy.Profile.plungeUnlocked=plungeUnlocked;
            Economy.Profile.Validate(); Seed = seed; Clock.Reset();lastMinedTime=0; forcedTeam = team; forcedRescue = rescue;
            BenchVisits.Clear(); Depth = BlocksBroken = WorkbenchesUsed = 0; RescueUsed = false; Array.Clear(OreCounts, 0, OreCounts.Length); Benefits.Reset();
            World.RestoreTrials(null);World.ResetWorld(seed); selectedTier = tier; Phase = RunPhase.Equipped; StartDrop(0);
        }
#endif
    }
}
