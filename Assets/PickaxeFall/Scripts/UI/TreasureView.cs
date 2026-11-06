using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        VisualElement treasurePanel;Label trialLabel;float treasureUntil;
        readonly Dictionary<BlockKind,int> treasure=new();
        public void ShowTreasure(LootStack[] loot,string heading)
        {
            if(Time.unscaledTime>treasureUntil)treasure.Clear();
            foreach(var stack in loot){treasure.TryGetValue(stack.kind,out int old);treasure[stack.kind]=old+stack.count;}
            treasureUntil=Time.unscaledTime+4;
            if(treasurePanel==null){treasurePanel=Box(root,"treasure-panel inset");treasurePanel.name="treasure-panel";treasurePanel.pickingMode=PickingMode.Ignore;}
            treasurePanel.Clear();BoldText(treasurePanel,heading,"treasure-heading");
            foreach(var pair in treasure){var row=Box(treasurePanel,"treasure-row");Icon(row,PixelArt.ResourceIcon(pair.Key),"recipe-icon");BoldText(row,"+"+pair.Value+" "+GameBalance.BlockName(pair.Key),"treasure-text");}
            Display(treasurePanel,true);
        }
        void TickTreasure()
        {
            if(treasurePanel!=null){float left=treasureUntil-Time.unscaledTime;Display(treasurePanel,left>0);treasurePanel.style.opacity=Mathf.Clamp01(left*2);}
            if(trialLabel==null){trialLabel=BoldText(sceneInfo,"","trial-status");trialLabel.name="trial-status";}
            string text="";foreach(var trial in game.World.Trials)if(!trial.done){text="СПАВНЕР "+new string('●',trial.hits)+new string('○',4-trial.hits)+" · "+Mathf.CeilToInt(trial.until-game.Clock.Now)+" С";break;}
            SetText(trialLabel,text);Display(trialLabel,text.Length>0 && game.Phase==RunPhase.Mining);
        }
        void AudioCredits()
        {
            var card=OpenModal("Звук и музыка");Text(card,"Звуковые эффекты: Kenney и artisticdude · CC0.\n\nМузыка: Kevin MacLeod (incompetech.com)\nDreams Become Real\nEastern Thought\nOssuary 5 — Rest\n\nCreative Commons Attribution 4.0. Громкость и переходы адаптированы для игры.","description");
            Button(card,"ЛИЦЕНЗИЯ CC BY 4.0",()=>Application.OpenURL("https://creativecommons.org/licenses/by/4.0/"),"stone-button compact-button");
            Button(card,"САЙТ АВТОРА",()=>Application.OpenURL("https://incompetech.com/"),"stone-button compact-button");Button(card,"НАЗАД",DrawSettings,"stone-button compact-button");
        }
    }
}
