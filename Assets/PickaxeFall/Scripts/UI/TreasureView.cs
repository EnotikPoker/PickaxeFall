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
            SetText(trialLabel,"");Display(trialLabel,false);
        }
        void AudioCredits()
        {
            var card=OpenModal(L.T("Звук и музыка"));Text(card,L.T("Звуковые эффекты: Kenney и artisticdude · CC0.\nИнтерфейс: PickaxeFall · CC0.\n\nМузыка: Kevin MacLeod (incompetech.com)\nDreams Become Real\nEastern Thought\nOssuary 5 — Rest\n\nCreative Commons Attribution 4.0. Громкость и переходы адаптированы для игры."),"description");
            Text(card,"https://creativecommons.org/licenses/by/4.0/","footnote");
            if(game.Platform==null||!game.Platform.IsYandex)Button(card,L.T("ЛИЦЕНЗИЯ CC BY 4.0"),()=>Application.OpenURL("https://creativecommons.org/licenses/by/4.0/"),"stone-button compact-button");
            if(game.Platform==null||!game.Platform.IsYandex)Button(card,L.T("САЙТ АВТОРА"),()=>Application.OpenURL("https://incompetech.com/"),"stone-button compact-button");Button(card,L.T("НАЗАД"),DrawSettings,"stone-button compact-button");
        }
    }
}
