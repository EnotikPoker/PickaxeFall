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
        public int Seed { get; private set; }
        public bool Paused { get; private set; }
        public readonly int[] OreCounts = new int[9];
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
            rouletteRandom = new System.Random();
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
#endif
        }

        public void NewRun()
        {
            StopAllCoroutines(); Time.timeScale = 1; Paused = false;
            Depth = Coins = BlocksBroken = 0; Array.Clear(OreCounts, 0, OreCounts.Length);
            Seed = rouletteRandom.Next(1, int.MaxValue);
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
            for (int i = 0; i < ticks; i++)
            {
                hud.ShowRouletteTier(i == ticks - 1 ? selectedTier : i % balance.pickaxes.Length);
                float progress = (float)i / (ticks - 1);
                yield return new WaitForSecondsRealtime(Mathf.Lerp(0.035f, 0.20f, progress * progress));
            }
            Pickaxe.Equip(selectedTier); Phase = RunPhase.Equipped; Notify();
        }

        public void Drop()
        {
            if (Phase != RunPhase.Equipped) return;
            Phase = RunPhase.Mining; Pickaxe.Launch(); Notify();
        }

        void Update()
        {
            if (!initialized) return;
            if (Input.GetKeyDown(KeyCode.Escape)) TogglePause();
            if (Phase == RunPhase.Mining && !Paused)
            {
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) Pickaxe.Nudge(-1);
                if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) Pickaxe.Nudge(1);
                Depth = Mathf.Max(Depth, Mathf.FloorToInt(-Pickaxe.transform.position.y));
                World.Stream(Depth);
            }
            hud.Tick();
        }

        public void OnBlockBroken(MineBlock block)
        {
            if (block.Kind == BlockKind.Workbench)
            {
                Pickaxe.RepairAndUpgrade();
                Effects.Impact(block.transform.position, new Color32(105, 239, 196, 255), 18, true);
                hud.Toast("ВЕРСТАК  ·  УЛУЧШЕНИЕ + ПОЛНЫЙ РЕМОНТ");
            }
            else
            {
                BlocksBroken++; Coins += balance.Block(block.Kind).reward; OreCounts[(int)block.Kind]++;
                Effects.Impact(block.transform.position, PixelArt.BlockColor(block.Kind), 7);
                if (block.Kind >= BlockKind.Coal && block.Kind <= BlockKind.Diamond)
                    hud.Toast($"{GameBalance.BlockName(block.Kind)}  +{balance.Block(block.Kind).reward}");
            }
            Notify();
        }

        public void FinishRun()
        {
            if (Phase != RunPhase.Mining) return;
            Phase = RunPhase.Finished; Pickaxe.Stop();
            BestDepth = Mathf.Max(BestDepth, Depth); Wallet += Coins;
            PlayerPrefs.SetInt("bestDepth", BestDepth); PlayerPrefs.SetInt("wallet", Wallet); PlayerPrefs.Save();
            Notify();
        }

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
