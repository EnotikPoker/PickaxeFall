using System;
using System.Collections.Generic;
using UnityEngine;
namespace PickaxeFall
{
    public enum UiSound { None, Navigate, Tap, Sale, Skill, Ability, Profile, Construction, Record }
    public sealed class GameAudio:MonoBehaviour
    {
        readonly Dictionary<string,AudioClip[]> clips=new();readonly Dictionary<string,float> played=new();
        readonly System.Random random=new();AudioSource[] voices;AudioSource[] songs;GameController game;
        int voice,current=-1;MapId map;float transition=1;bool suspended;
        public void Initialize(GameController owner)
        {
            game=owner;if(owner.IsSimulation)return;
            voices=new AudioSource[10];for(int i=0;i<voices.Length;i++){voices[i]=gameObject.AddComponent<AudioSource>();voices[i].playOnAwake=false;voices[i].spatialBlend=0;}
            songs=new AudioSource[2];for(int i=0;i<2;i++){songs[i]=gameObject.AddComponent<AudioSource>();songs[i].playOnAwake=false;songs[i].loop=true;songs[i].priority=200;}
            foreach(var name in new[]{"stone","sand","wood","metal","crystal","break","shatter","dash","explosion","bench","chest","reward","trial","error","click","roll","water","lava","navigate","sale","skill","ability","profile","construction","record"})clips[name]=Resources.LoadAll<AudioClip>("Audio/Sfx/"+name);
        }
        public void SetMap(MapId value)
        {
            if(songs==null || current>=0&&map==value)return;map=value;current=(current+1)%2;var next=songs[current];next.Stop();next.clip=Resources.Load<AudioClip>("Audio/Music/"+(value==MapId.Desert?"Desert":value==MapId.Nether?"Nether":"Mine"));next.volume=0;next.Play();transition=0;
        }
        public void Ui(UiSound cue)
        {
            if(cue==UiSound.None)return;
            Play(cue switch {UiSound.Navigate=>"navigate",UiSound.Tap=>"click",UiSound.Sale=>"sale",UiSound.Skill=>"skill",UiSound.Ability=>"ability",UiSound.Profile=>"profile",UiSound.Construction=>"construction",_=>"record"});
        }
        static float Gain(string name)=>name switch {"navigate"=>.12f,"click"=>.26f,"sale"=>.34f,"skill"=>.42f,"ability"=>.48f,"profile"=>.65f,"construction"=>.55f,"record"=>.55f,"stone" or "break"=>.22f,_=>.55f};
        public void Play(string name)
        {
            if(voices==null||suspended||game.Settings.EffectsVolume<=0||!clips.TryGetValue(name,out var options)||options.Length==0)return;
            float gap=name=="stone"||name=="break"||name=="sand"||name=="metal"||name=="crystal"?.08f:name=="click"?.035f:.12f;
            if(played.TryGetValue(name,out float last)&&Time.unscaledTime-last<gap)return;played[name]=Time.unscaledTime;
            bool important=name is "profile" or "construction" or "record";
            var audio=important?voices[voices.Length-1]:voices[voice++%(voices.Length-1)];audio.Stop();audio.clip=options[random.Next(options.Length)];audio.pitch=name=="reward"?1: .95f+(float)random.NextDouble()*.1f;audio.volume=game.Settings.EffectsVolume*Gain(name);audio.Play();
        }
        public void MaterialHit(BlockKind k)=>Play(k==BlockKind.Dirt||k==BlockKind.Sand?"sand":k==BlockKind.Timber||GameBalance.IsWorkbench(k)?"wood":k==BlockKind.Amethyst||k==BlockKind.AmethystCluster||k==BlockKind.SoulCrystal?"crystal":Progression.IsOre(k)?"metal":"stone");
        float ambience;
        void Update()
        {
            if(songs==null)return;if(current<0)SetMap(game.CurrentMap);
            transition=Mathf.Min(1,transition+Time.unscaledDeltaTime/2);for(int i=0;i<2;i++)songs[i].volume=game.Settings.MusicVolume*(i==current?transition:1-transition)*(game.Paused?.45f:1);if(transition>=1)songs[1-current].Stop();
            if(game.Phase==RunPhase.Mining&&!game.Paused&&Time.unscaledTime>ambience){ambience=Time.unscaledTime+1.5f;var tool=game.SelectedTool;if(tool!=null){if(game.World.Liquids.Touches(tool,BlockKind.Lava))Play("lava");else if(game.World.Liquids.Touches(tool,BlockKind.Water))Play("water");}}
        }
        void OnApplicationPause(bool pause){suspended=pause;if(songs==null)return;foreach(var s in songs)if(pause)s.Pause();else s.UnPause();if(pause)foreach(var v in voices)v.Stop();}
    }
}
