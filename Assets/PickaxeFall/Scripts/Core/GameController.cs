using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public class GameController : MonoBehaviour
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
        public MiningEffects Effects { get; private set; }
        public EconomyService Economy { get; private set; }
        public GameSettings Settings { get; private set; }
        public RunUpgrades RunUpgrades { get; private set; }
        public ResourceInventory Inventory { get; private set; }
        public int Depth { get; private set; }
        public int BlocksBroken { get; private set; }
        public int BestDepth => Economy.Profile.bestDepth;
        public long Wallet => Economy.Profile.coins;
        public long PotentialSaleValue { get { long value = 0; for (int i = 0; i < 8; i++) value += OreCounts[i] * Progression.SellPrices[i]; return value; } }
        public int WorkbenchesUsed { get; private set; }
        public int RunToolCount { get; private set; } = 1;
        public bool RescueUsed { get; private set; }
        public HubTab CurrentHubTab => hud.CurrentTab;
        public int Seed { get; private set; }
        public bool Paused { get; private set; }
        public bool CanTrade => Phase == RunPhase.Ready || Phase == RunPhase.Equipped || Phase == RunPhase.Finished;
        public Rect GameplayViewport => hud != null ? hud.GameplayViewport : new Rect(0.04f, 0.24f, 0.92f, 0.53f);
        public readonly long[] OreCounts = new long[Enum.GetValues(typeof(BlockKind)).Length];
        public event Action Changed;
        GameHud hud;
        MineCamera cameraFollow;
        System.Random rouletteRandom, effectRandom;
        int selectedTier;
        bool initialized, dirty;
        float nextSave;

        public void Configure(GameBalance config, PanelSettings panel, StyleSheet style)
        { balance = config; panelSettings = panel; interfaceStyle = style; }

        void Start()
        {
            Application.targetFrameRate = 60; Screen.orientation = ScreenOrientation.Portrait;
            Physics2D.gravity = new Vector2(0, balance.gravity);
            ProfileDirectory = Application.persistentDataPath;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            foreach (string arg in Environment.GetCommandLineArgs()) if (arg.StartsWith("--preview-")) TestMode = arg.Substring(10);
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
            if (Array.IndexOf(new[] { "smoke", "shop", "luck", "bag", "sale", "team", "mine", "settings" }, TestMode) < 0) TestMode = "";
            if (TestMode.Length > 0) ProfileDirectory = Path.Combine(Application.temporaryCachePath, "verification", Guid.NewGuid().ToString("N"));
#endif
            Settings = new GameSettings(TestMode.Length > 0);
            var store = new ProfileStore(ProfileDirectory, TestMode.Length > 0 ? () => new PlayerProfile() : null);
            try { Economy = new EconomyService(store.Load(), store.Save); }
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
            Effects = new GameObject("Mining effects").AddComponent<MiningEffects>(); Effects.transform.SetParent(transform); Effects.Initialize(Settings);
            World = new GameObject("Streamed mine").AddComponent<MiningWorld>(); World.transform.SetParent(transform); World.Initialize(this, balance);
            for (int i = 0; i < 5; i++)
            {
                var tool = new GameObject("Physical pickaxe " + (i + 1)).AddComponent<FallingPickaxe>();
                tool.transform.SetParent(transform); tool.Initialize(this); tools.Add(tool); tool.gameObject.SetActive(false);
            }
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>();
            cameraFollow = cameraObject.AddComponent<MineCamera>(); cameraFollow.Initialize(this);
            var document = gameObject.AddComponent<UIDocument>(); document.panelSettings = panelSettings;
            hud = new GameHud(this, document.rootVisualElement, interfaceStyle);
            initialized = true; NewRun();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (TestMode == "smoke" || Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--smoke-test")) gameObject.AddComponent<RuntimeSmokeTest>();
            if (TestMode.Length > 0 && TestMode != "smoke") gameObject.AddComponent<RuntimePreview>();
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--balance-probe" || arg == "--economy-probe")) gameObject.AddComponent<RuntimeBalanceProbe>();
#endif
        }

        public void NewRun()
        {
            if (!initialized) return;
            StopAllCoroutines(); Time.timeScale = 1; Paused = false; Effects.Clear();
            foreach (var tool in tools) { tool.Stop(); tool.gameObject.SetActive(false); }
            Depth = BlocksBroken = WorkbenchesUsed = 0; Array.Clear(OreCounts, 0, OreCounts.Length);
            RunToolCount = 1; RescueUsed = false; RunUpgrades = new RunUpgrades(Economy.Profile);
            int previousSeed = Seed;
            do { Seed = Guid.NewGuid().GetHashCode() & int.MaxValue; } while (Seed == 0 || Seed == previousSeed);
            World.ResetWorld(Seed); Pickaxe.Equip(0); Pickaxe.gameObject.SetActive(false); cameraFollow.ResetView();
            Phase = RunPhase.Ready; hud.SelectTab(HubTab.Mine); Notify();
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
            RunUpgrades = new RunUpgrades(Economy.Profile); effectRandom = new System.Random(unchecked(Seed ^ 0x6AC690C5));
            RunToolCount = forcedCount > 0 ? Mathf.Clamp(forcedCount, 1, 5) : RollEffect(UpgradeKind.TeamChance) ? RunUpgrades.TeamSize : 1;
            for (int i = 0; i < tools.Count; i++)
            {
                if (i >= RunToolCount) { tools[i].gameObject.SetActive(false); continue; }
                tools[i].Equip(selectedTier, i, (i - (RunToolCount - 1) * 0.5f) * 1.4f); tools[i].Launch();
            }
            Phase = RunPhase.Mining; World.StreamTools(tools); cameraFollow.ResetView(); Notify();
            if (RunToolCount > 1) hud.Toast("УДАЧА! КИРОК В ОТРЯДЕ: " + RunToolCount);
        }
        public bool RollEffect(UpgradeKind kind) => effectRandom != null && RunUpgrades.Chance(kind) > 0 && effectRandom.NextDouble() < RunUpgrades.Chance(kind);
        public int PreviewDamageHalfUnits(int tier) => balance.pickaxes[tier].damage * 2 + Progression.DamageStepHalfUnits(tier) * Economy.Profile.Level(new(UpgradeKind.Damage, tier));
        public int PreviewDurability(int tier) => balance.pickaxes[tier].durability + Progression.DurabilityStep(tier) * Economy.Profile.Level(new(UpgradeKind.Durability, tier));

        void Update()
        {
            if (!initialized) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { if (!hud.CloseModal()) TogglePause(); }
            if (Phase == RunPhase.Mining && !Paused)
            {
                foreach (var tool in tools) if (tool.IsAlive) Depth = Mathf.Max(Depth, Mathf.FloorToInt(-tool.transform.position.y));
                World.StreamTools(tools);
                if (dirty && Time.unscaledTime >= nextSave) SaveProgress();
            }
            hud.Tick();
        }

        // Returns whether the harvested cell should become a fresh TNT. Explosions cannot create TNT this way.
        public bool OnBlockBroken(MineBlock block, FallingPickaxe source, bool explosion)
        {
            if (Phase != RunPhase.Mining) return false;
            if (block.Kind == BlockKind.Workbench)
            {
                if (source == null) return false;
                WorkbenchesUsed++; int steps = RollEffect(UpgradeKind.DoubleBench) ? 2 : 1;
                source.RepairAndUpgrade(steps);
                Effects.Impact(block.transform.position, new Color32(105, 239, 196, 255), 18, true);
                hud.Toast(steps == 2 ? "ВЕРСТАК ×2 · УСИЛЕНИЕ И РЕМОНТ" : "ВЕРСТАК · УСИЛЕНИЕ И РЕМОНТ");
            }
            else
            {
                BlocksBroken++;
                if (ResourceInventory.IsMaterial(block.Kind))
                {
                    int quantity = Progression.IsOre(block.Kind) && RollEffect(UpgradeKind.Fortune) ? 2 : 1;
                    if (!Economy.Collect(block.Kind, quantity)) throw new InvalidOperationException(Economy.LastError);
                    OreCounts[(int)block.Kind] += quantity; dirty = true;
                    if (Settings.LootMessages) hud.Toast($"{GameBalance.BlockName(block.Kind)} +{quantity}");
                }
                Effects.Impact(block.transform.position, block.BaseColor, 7);
            }
            bool tnt = !explosion && source != null && Progression.IsOre(block.Kind) && RollEffect(UpgradeKind.TntFind);
            Notify(); return tnt;
        }

        public void OnPickaxeExhausted(FallingPickaxe tool)
        {
            bool last = true;
            foreach (var other in tools) if (other != tool && other.IsAlive) { last = false; break; }
            if (last && !RescueUsed)
            {
                RescueUsed = true;
                if (RollEffect(UpgradeKind.LastChance)) { tool.Revive(); hud.Toast("ПОСЛЕДНИЙ ШАНС! +25% ПРОЧНОСТИ"); Notify(); return; }
            }
            tool.BreakApart();
            if (last) FinishRun(); else { hud.Toast("КИРКА №" + (tool.ToolIndex + 1) + " РАССЫПАЛАСЬ"); Notify(); }
        }
        public void FinishRun()
        {
            if (Phase != RunPhase.Mining) return;
            foreach (var tool in tools)
            {
                if (tool.IsAlive) { Depth = Mathf.Max(Depth, Mathf.FloorToInt(-tool.transform.position.y)); tool.BreakApart(); }
            }
            Phase = RunPhase.Breaking; Paused = false; Time.timeScale = 1;
            Economy.Profile.bestDepth = Mathf.Max(BestDepth, Depth); SaveProgress();
            Notify(); StartCoroutine(FinishAnimation());
        }
        IEnumerator FinishAnimation() { yield return new WaitForSeconds(0.85f); Phase = RunPhase.Finished; Notify(); }

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
            Paused = !Paused; Time.timeScale = Paused ? 0 : 1; Notify();
        }
        public bool SelectHubTab(HubTab tab) { if (!CanTrade) return false; hud.SelectTab(tab); return true; }
        void SaveProgress()
        {
            try { Economy?.Save(); Settings?.Save(); dirty = false; nextSave = Time.unscaledTime + 5; }
            catch (Exception e) { Debug.LogWarning("Save failed: " + e.Message); hud?.Toast("НЕ УДАЛОСЬ СОХРАНИТЬ ПРОГРЕСС"); nextSave = Time.unscaledTime + 10; }
        }
        void OnApplicationPause(bool pause) { if (pause) SaveProgress(); if (pause && Phase == RunPhase.Mining && !Paused) TogglePause(); }
        void OnApplicationQuit() => SaveProgress();
        void OnDestroy() { if (initialized) SaveProgress(); Time.timeScale = 1; hud?.Dispose(); }
        void Notify() => Changed?.Invoke();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void BeginProbeRun(int tier, int seed, int count = 0)
        {
            NewRun(); Seed = seed; World.ResetWorld(seed); selectedTier = tier;
            RunUpgrades = new RunUpgrades(Economy.Profile); Pickaxe.Equip(tier);
            Phase = RunPhase.Equipped; StartDrop(count);
        }
#endif
    }
}
