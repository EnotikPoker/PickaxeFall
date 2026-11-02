using System;
using UnityEngine;

namespace PickaxeFall
{
    public sealed class GameSettings
    {
        const string Prefix = "settings.v1.";
        readonly bool temporary;
        bool dirty;
        public float Volume { get; private set; } = 0.8f;
        public int FrameRate { get; private set; } = 60;
        public bool ReducedEffects { get; private set; }
        public bool LootMessages { get; private set; }
        public GameSettings(bool temporary = false)
        {
            this.temporary = temporary;
            if (!temporary)
            {
                Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + "volume", PlayerPrefs.GetInt("soundMuted", 0) == 1 ? 0 : 0.8f));
                FrameRate = PlayerPrefs.GetInt(Prefix + "fps", 60) == 30 ? 30 : 60;
                ReducedEffects = PlayerPrefs.GetInt(Prefix + "effects", 0) == 1;
                LootMessages = PlayerPrefs.GetInt(Prefix + "lootMessages", 0) == 1;
            }
            Apply();
        }
        void Apply() { AudioListener.volume = Volume; Application.targetFrameRate = FrameRate; }
        public void SetVolume(float value) { Volume = Mathf.Clamp01(value); dirty = true; Apply(); }
        public void SetFrameRate(int value) { FrameRate = value == 30 ? 30 : 60; dirty = true; Apply(); }
        public void SetReducedEffects(bool value) { ReducedEffects = value; dirty = true; }
        public void SetLootMessages(bool value) { LootMessages = value; dirty = true; }
        public void Reset() { Volume = 0.8f; FrameRate = 60; ReducedEffects = LootMessages = false; dirty = true; Apply(); }
        public void Save()
        {
            if (!dirty || temporary) return;
            PlayerPrefs.SetFloat(Prefix + "volume", Volume); PlayerPrefs.SetInt("soundMuted", Volume < 0.001f ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "fps", FrameRate); PlayerPrefs.SetInt(Prefix + "effects", ReducedEffects ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "lootMessages", LootMessages ? 1 : 0); PlayerPrefs.Save(); dirty = false;
        }
    }
}
