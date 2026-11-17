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
            try{amounts=game.Economy.SurplusQuote(out value);checked{_=game.Wallet+value;}}catch(OverflowException){Toast(L.T("Слишком большая сумма."));return;}
            if(value<=0)return;
            var card=OpenModal(L.T("Продать лишнее?"));
            Text(card,L.T("Цель: ")+game.Economy.GoalTitle+L.T("\nЗапас: ")+(game.Economy.Profile.goalKind==GoalKind.Upgrade?(game.Economy.ReserveSteps==0?L.T("до максимума"):game.Economy.ReserveSteps+L.T(" ступ.")):L.T("оставшаяся стоимость цели"))+L.T("\nВыручка: ")+Exact(value)+L.T(" монет"),"description");
            BoldText(card,L.T("Останется в рюкзаке"),"section-label");
            foreach(var kind in ResourceInventory.Materials){long keep=game.Inventory[kind]-amounts[(int)kind];if(keep>0)RecipeLine(card,kind,keep,0);}
            Button(card,L.T("ПРОДАТЬ ЛИШНЕЕ"),()=>{
                if(Time.unscaledTime<nextTrade)return;nextTrade=Time.unscaledTime+.25f;
                CommitProfile(game.Economy.TrySellSurplus,()=>{CloseModal();Toast(L.T("ЛИШНЕЕ ПРОДАНО"));},UiSound.Sale);
            },"stone-button primary").name="confirm-sell-surplus";
            Button(card,L.T("ОТМЕНА"),()=>CloseModal(),"stone-button secondary");
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
                if(!title){BoldText(card,L.T("Будут проданы материалы цели: ")+game.Economy.GoalTitle,"description cost-short");title=true;}
                RecipeLine(card,kind,affected,0);
            }
        }
        void ConfirmTargetSale(BlockKind kind,long count)
        {
            long value;
            try{value=checked(count*game.Economy.Config.sellPrices[(int)kind]);checked{_=game.Wallet+value;}}
            catch(OverflowException){Toast(L.T("Слишком большая сумма."));return;}
            long reserved=game.Economy.Reserved(kind);
            if(count<=game.Inventory[kind]-reserved){if(game.Sell(kind,count))CloseModal();return;}
            var card=OpenModal(L.T("Продать материалы цели?"));
            Text(card,L.T("Продать ")+Exact(count)+" × "+GameBalance.BlockName(kind)+L.T(" за ")+Exact(value)+L.T(" монет?"),"description");
            ReservationWarning(card,kind,count);
            Button(card,L.T("ВСЁ РАВНО ПРОДАТЬ"),()=>{
                if(Time.unscaledTime<nextTrade)return;nextTrade=Time.unscaledTime+.25f;
                if(game.Sell(kind,count))CloseModal();
            },"stone-button primary buy-blocked").name="confirm-sell-reserved";
            Button(card,L.T("ОТМЕНА"),()=>OpenSale(kind),"stone-button secondary");
        }
    }
}
