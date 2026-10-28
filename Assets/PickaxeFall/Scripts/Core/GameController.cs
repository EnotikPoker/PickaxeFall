using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public class GameController : MonoBehaviour
    {
        [SerializeField] GameBalance balance;
        [SerializeField] PanelSettings panelSettings;
        [SerializeField] StyleSheet interfaceStyle;
        public GameBalance Balance => balance;
        public RunPhase Phase { get; private set; }
        public FallingPickaxe Pickaxe { get; private set; }
        public MiningWorld World { get; private set; }
        public MiningEffects Effects { get; private set; }
        public int Depth { get; private set; }
        public int Coins { get; private set; }
        public int BlocksBroken { get; private set; }
        public int BestDepth { get; private set; }
        public int Wallet { get; private set; }
        public int WorkbenchesUsed { get; private set; }
        public int Seed { get; private set; }
        public bool Paused { get; private set; }
        public readonly int[] OreCounts = new int[Enum.GetValues(typeof(BlockKind)).Length];
        public event Action Changed;
        GameHud hud;
        MineCamera cameraFollow;
        System.Random rouletteRandom;
        int selectedTier;
        bool initialized;

        public void Configure(GameBalance config, PanelSettings panel, StyleSheet style)
        { balance = config; panelSettings = panel; interfaceStyle = style; }

        void Start()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Physics2D.gravity = new Vector2(0, balance.gravity);
            BestDepth = PlayerPrefs.GetInt("bestDepth", 0); Wallet = PlayerPrefs.GetInt("wallet", 0);
            rouletteRandom = new System.Random(Guid.NewGuid().GetHashCode());
            Effects = new GameObject("Mining effects").AddComponent<MiningEffects>(); Effects.transform.SetParent(transform); Effects.Initialize();
            World = new GameObject("Streamed mine").AddComponent<MiningWorld>(); World.transform.SetParent(transform); World.Initialize(this, balance);
            Pickaxe = new GameObject("Physical pickaxe").AddComponent<FallingPickaxe>(); Pickaxe.transform.SetParent(transform); Pickaxe.Initialize(this);
            var cameraObject = new GameObject("Main Camera"); cameraObject.tag = "MainCamera";
            cameraObject.AddComponent<Camera>(); cameraObject.AddComponent<AudioListener>();
            cameraFollow = cameraObject.AddComponent<MineCamera>(); cameraFollow.Initialize(this);
            var document = gameObject.AddComponent<UIDocument>(); document.panelSettings = panelSettings;
            hud = new GameHud(this, document.rootVisualElement, interfaceStyle);
            initialized = true; NewRun();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--smoke-test"))
                gameObject.AddComponent<RuntimeSmokeTest>();
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "--balance-probe"))
                gameObject.AddComponent<RuntimeBalanceProbe>();
#endif
        }

        public void NewRun()
        {
            StopAllCoroutines(); Time.timeScale = 1; Paused = false; Effects.Clear();
            Depth = Coins = BlocksBroken = 0; Array.Clear(OreCounts, 0, OreCounts.Length);
            WorkbenchesUsed = 0;
            int previousSeed = Seed;
            do { Seed = Guid.NewGuid().GetHashCode() & int.MaxValue; } while (Seed == 0 || Seed == previousSeed);
            World.ResetWorld(Seed); Pickaxe.Equip(0); Pickaxe.gameObject.SetActive(false); cameraFollow.ResetView();
            Phase = RunPhase.Ready; Notify();
        }

        public void Spin()
        {
            if (Phase != RunPhase.Ready) return;
            Phase = RunPhase.Spinning;
            selectedTier = balance.Roll(rouletteRandom); Notify(); StartCoroutine(SpinRoutine());
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
            Pickaxe.Equip(selectedTier); Phase = RunPhase.Equipped; Notify();
        }

        public void Drop()
        {
            if (Phase != RunPhase.Equipped) return;
            cameraFollow.ResetView();
            Pickaxe.Launch(); Phase = RunPhase.Mining; Notify();
        }

        void Update()
        {
            if (!initialized) return;
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (Phase == RunPhase.Mining && !Paused)
            {
                Depth = Mathf.Max(Depth, Mathf.FloorToInt(-Pickaxe.transform.position.y));
                World.Stream(Depth);
            }
            hud.Tick();
        }

        public void OnBlockBroken(MineBlock block)
        {
            if (Phase != RunPhase.Mining) return;
            if (block.Kind == BlockKind.Workbench)
            {
                WorkbenchesUsed++;
                Pickaxe.RepairAndUpgrade();
                Effects.Impact(block.transform.position, new Color32(105, 239, 196, 255), 18, true);
                hud.Toast("ВЕРСТАК  ·  УЛУЧШЕНИЕ + ПОЛНЫЙ РЕМОНТ");
            }
            else
            {
                BlocksBroken++; Coins += balance.Block(block.Kind).reward; OreCounts[(int)block.Kind]++;
                Effects.Impact(block.transform.position, block.BaseColor, 7);
                if (block.Kind >= BlockKind.Coal && block.Kind <= BlockKind.Diamond)
                    hud.Toast($"{GameBalance.BlockName(block.Kind)}  +{balance.Block(block.Kind).reward}");
                if (block.Kind == BlockKind.Tnt) hud.Toast("TNT! ЦЕПНАЯ РЕАКЦИЯ");
            }
            Notify();
        }

        public void FinishRun()
        {
            if (Phase != RunPhase.Mining) return;
            Phase = RunPhase.Breaking; Paused = false; Time.timeScale = 1;
            Depth = Mathf.Max(Depth, Mathf.FloorToInt(-Pickaxe.transform.position.y));
            Pickaxe.BreakApart(); hud.Toast("КИРКА РАССЫПАЛАСЬ");
            BestDepth = Mathf.Max(BestDepth, Depth); Wallet += Coins;
            PlayerPrefs.SetInt("bestDepth", BestDepth); PlayerPrefs.SetInt("wallet", Wallet); PlayerPrefs.Save();
            Notify(); StartCoroutine(FinishAnimation());
        }

        IEnumerator FinishAnimation()
        {
            yield return new WaitForSeconds(0.85f);
            Phase = RunPhase.Finished; Notify();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void BeginProbeRun(int tier, int seed)
        {
            NewRun(); Seed = seed; World.ResetWorld(seed); Pickaxe.Equip(tier);
            Phase = RunPhase.Equipped; Drop();
        }
#endif

        public void TogglePause()
        {
            if (Phase != RunPhase.Mining) return;
            Paused = !Paused; Time.timeScale = Paused ? 0 : 1; Notify();
        }

        void OnApplicationPause(bool pause) { if (pause && Phase == RunPhase.Mining && !Paused) TogglePause(); }
        void OnDestroy() { Time.timeScale = 1; hud?.Dispose(); }
        void Notify() => Changed?.Invoke();
    }
}
