using System;
using UnityEngine;

namespace PickaxeFall
{
    public sealed class GameSettings
    {
        const string Prefix="settings.v1.";
        readonly bool temporary, applyRuntime;
        bool dirty;
        public float Volume { get; private set; }=.8f;
        public float MusicVolume {get;private set;}=.25f;
        public float EffectsVolume {get;private set;}=.8f;
        public int FrameRate { get; private set; }=60;
        public int EffectiveFrameRate => FrameRate==0 ? Mathf.Clamp(Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value),30,360) : FrameRate;
        public bool ReducedEffects { get; private set; }
        public bool LootMessages { get; private set; }=true;
        public bool TutorialSeen { get; private set; }
        public static readonly int[] FrameRates={30,60,90,120,144,165,240,0};
        public GameSettings(bool temporary=false,bool applyRuntime=true)
        {
            this.temporary=temporary;this.applyRuntime=applyRuntime;
            if(!temporary)
            {
                MusicVolume=Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix+"music",.25f));EffectsVolume=Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix+"sfx",.8f));
                Volume=Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix+"volume",PlayerPrefs.GetInt("soundMuted",0)==1 ? 0 : .8f));
                int saved=PlayerPrefs.GetInt(Prefix+"fps",60);FrameRate=Array.IndexOf(FrameRates,saved)>=0?saved:60;
                ReducedEffects=PlayerPrefs.GetInt(Prefix+"effects",0)==1;
                LootMessages=PlayerPrefs.GetInt(Prefix+"lootPopups",1)==1;
                TutorialSeen=PlayerPrefs.GetInt(Prefix+"tutorialSeen",0)==1;
            }
            Apply();
        }
        void Apply(){if(applyRuntime){AudioListener.volume=Volume;QualitySettings.vSyncCount=0;Application.targetFrameRate=EffectiveFrameRate;}}
        public void SetVolume(float value){Volume=Mathf.Clamp01(value);dirty=true;Apply();}
        public void SetMusicVolume(float value){MusicVolume=Mathf.Clamp01(value);dirty=true;}
        public void SetEffectsVolume(float value){EffectsVolume=Mathf.Clamp01(value);dirty=true;}
        public void SetFrameRate(int value){FrameRate=Array.IndexOf(FrameRates,value)>=0?value:60;dirty=true;Apply();}
        public void SetReducedEffects(bool value){ReducedEffects=value;dirty=true;}
        public void SetLootMessages(bool value){LootMessages=value;dirty=true;}
        public void SetTutorialSeen(bool value){TutorialSeen=value;dirty=true;}
        public void Reset(){Volume=.8f;MusicVolume=.25f;EffectsVolume=.8f;FrameRate=60;ReducedEffects=false;LootMessages=true;dirty=true;Apply();}
        public void Save()
        {
            if(!dirty || temporary)return;
            PlayerPrefs.SetFloat(Prefix+"music",MusicVolume);PlayerPrefs.SetFloat(Prefix+"sfx",EffectsVolume);PlayerPrefs.SetFloat(Prefix+"volume",Volume);PlayerPrefs.SetInt("soundMuted",Volume<.001f?1:0);
            PlayerPrefs.SetInt(Prefix+"fps",FrameRate);PlayerPrefs.SetInt(Prefix+"effects",ReducedEffects?1:0);
            PlayerPrefs.SetInt(Prefix+"lootPopups",LootMessages?1:0);PlayerPrefs.SetInt(Prefix+"tutorialSeen",TutorialSeen?1:0);
            PlayerPrefs.Save();dirty=false;
        }
    }
}
