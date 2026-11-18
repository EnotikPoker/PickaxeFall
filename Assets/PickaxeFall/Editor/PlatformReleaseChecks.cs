using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
namespace PickaxeFall.Editor
{
    public static class PlatformReleaseChecks
    {
        public static void Validate()
        {
            int checks=0;void Check(bool ok,string name){checks++;if(!ok)throw new InvalidOperationException("Platform 0.8.3: "+name);}
            var state=new WebAdState();state.StartRun();state.Advance(119.99);Check(!state.AfterDue&&!state.RunDue,"no early ad");state.Advance(.01);Check(state.AfterDue&&!state.RunDue,"120 threshold");state.Advance(180);Check(state.RunDue,"300 threshold");
            double after=state.afterSeconds;state.RunResult();Check(state.afterSeconds==after&&state.nextRunAd==480,"independent after timer and 180 repeat");state.Advance(179);Check(!state.RunDue,"repeat waits");state.Advance(1);Check(state.RunDue,"repeat due");state.AfterResult(false);Check(state.afterSeconds==480&&!state.AfterDue,"one attempt per transition");state.StartRun();Check(state.AfterDue&&state.runSeconds==0,"next descent retains post-run debt");state.AfterResult(true);Check(state.afterSeconds==0,"confirmed post-run ad resets only its own timer");
            state.Advance(double.NaN);state.Advance(double.PositiveInfinity);state.Advance(-1);state.Validate();Check(state.afterSeconds==0,"invalid delta rejected");state.Finish(101);Check(state.rewardValue==50&&!state.rewarded,"bonus rounds down");
            var copy=state.Copy();copy.rewarded=true;Check(!state.rewarded,"receipt independent copy");
            var envelope=new WebCheckpoint{user="test",revision="local",profile=ProfileStore.Serialize(new PlayerProfile()),ads=state};envelope.Validate();var core=envelope.Core();Check(ProfileStore.Parse(core.profile).run==null&&string.IsNullOrEmpty(core.ads.runId),"cloud excludes run");
            string old=L.Language;L.SetLanguage("ru");Check(L.T("Не хватает монет")=="Не хватает монет","Russian default");L.SetLanguage("en");Check(L.T("Не хватает монет")=="Not enough coins","English errors");Check(L.F("Урон {0} · Прочность {1}",2,64)=="Damage 2 · Durability 64","formatted text");L.SetLanguage(old);
            var keys=new HashSet<string>();foreach(var entry in L.Load().entries)
            {
                Check(keys.Add(entry.ru),"unique key: "+entry.ru);Check(!string.IsNullOrWhiteSpace(entry.en),"English present");
                var ru=Regex.Matches(entry.ru,@"\{\d+(?:[^}]*)\}").Select(x=>x.Value).OrderBy(x=>x).ToArray();var en=Regex.Matches(entry.en,@"\{\d+(?:[^}]*)\}").Select(x=>x.Value).OrderBy(x=>x).ToArray();Check(ru.SequenceEqual(en),"format placeholders");
                Check(!Regex.IsMatch(entry.en,"[А-Яа-яЁё]")||entry.en=="Русский","no Russian left in English");
            }
            string html=File.ReadAllText("Assets/WebGLTemplates/PickaxeFall/index.html");Check(html.Contains("s.src='/sdk.js'")&&!html.Contains("games.s3.yandex.net"),"official relative SDK");Check(html.Contains("midRunAds:false"),"production timed ads disabled");
            Debug.Log("PICKAXEFALL_PLATFORM_CHECKS_OK: "+checks);
        }
    }
}
