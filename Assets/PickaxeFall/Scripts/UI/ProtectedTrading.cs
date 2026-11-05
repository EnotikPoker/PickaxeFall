using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        void OpenSellSurplus()
        {
            if(!game.CanTrade || game.Economy.Profile.goalKind==GoalKind.None)return;
            long[] amounts;long value;
            try{amounts=game.Economy.SurplusQuote(out value);checked{_=game.Wallet+value;}}catch(OverflowException){Toast("Слишком большая сумма.");return;}
            if(value<=0)return;
            var card=OpenModal("Продать лишнее?");
            Text(card,"Цель: "+game.Economy.GoalTitle+"\nВыручка: "+Exact(value)+" монет","description");
            BoldText(card,"Останется в рюкзаке","section-label");
            foreach(var kind in ResourceInventory.Materials){long keep=game.Inventory[kind]-amounts[(int)kind];if(keep>0)RecipeLine(card,kind,keep,0);}
            Button(card,"ПРОДАТЬ ЛИШНЕЕ",()=>{
                if(Time.unscaledTime<nextTrade)return;nextTrade=Time.unscaledTime+.25f;
                CommitProfile(game.Economy.TrySellSurplus,()=>{CloseModal();Toast("ЛИШНЕЕ ПРОДАНО · РЕСУРСЫ ЦЕЛИ СОХРАНЕНЫ");});
            },"stone-button primary").name="confirm-sell-surplus";
            Button(card,"ОТМЕНА",()=>CloseModal(),"stone-button secondary");
        }
        void ReservationWarning(VisualElement card,BlockKind? selected,long quantity)
        {
            if(game.Economy.Profile.goalKind==GoalKind.None)return;
            bool title=false;
            foreach(var kind in ResourceInventory.Materials)
            {
                if(selected.HasValue&&selected.Value!=kind)continue;
                long sold=selected.HasValue?quantity:game.Inventory[kind];
                long affected=Math.Max(0,Math.Min(game.Economy.Reserved(kind),sold-(game.Inventory[kind]-game.Economy.Reserved(kind))));
                if(affected==0)continue;
                if(!title){BoldText(card,"Будут проданы материалы цели: "+game.Economy.GoalTitle,"description cost-short");title=true;}
                RecipeLine(card,kind,affected,0);
            }
        }
        void ConfirmTargetSale(BlockKind kind,long count)
        {
            long value;
            try{value=checked(count*game.Economy.Config.sellPrices[(int)kind]);checked{_=game.Wallet+value;}}
            catch(OverflowException){Toast("Слишком большая сумма.");return;}
            long reserved=game.Economy.Reserved(kind);
            if(count<=game.Inventory[kind]-reserved){if(game.Sell(kind,count))CloseModal();return;}
            var card=OpenModal("Продать материалы цели?");
            Text(card,"Продать "+Exact(count)+" × "+GameBalance.BlockName(kind)+" за "+Exact(value)+" монет?","description");
            ReservationWarning(card,kind,count);
            Button(card,"ВСЁ РАВНО ПРОДАТЬ",()=>{
                if(Time.unscaledTime<nextTrade)return;nextTrade=Time.unscaledTime+.25f;
                if(game.Sell(kind,count))CloseModal();
            },"stone-button primary buy-blocked").name="confirm-sell-reserved";
            Button(card,"ОТМЕНА",()=>OpenSale(kind),"stone-button secondary");
        }
    }
}
