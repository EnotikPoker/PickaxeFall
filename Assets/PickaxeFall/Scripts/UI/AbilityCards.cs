using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        sealed class AbilityCard
        {
            readonly SkillLock skillLock;readonly GameHud hud;readonly AbilityKind kind;
            readonly Label state,price,reason;readonly VisualElement materials,pin;readonly Button buy;
            public AbilityCard(GameHud owner,VisualElement parent,AbilityKind ability)
            {
                hud=owner;kind=ability;var card=Box(parent,"upgrade-card compact-skill");card.name="ability-"+kind;
                var body=Box(card,"skill-content");var heading=Box(body,"skill-heading");
                string title=kind==AbilityKind.Dash?L.T("Рывок · свайп"):L.T("Пробой · нажатие");
                hud.BoldText(heading,title,"upgrade-title");
                string detail=kind==AbilityKind.Dash?L.T("Свайп толкает выбранную кирку в одну из четырёх сторон и мгновенно пробивает первый блок."):L.T("Нажатие направляет выбранную кирку вниз и мгновенно пробивает блок. Отдельная прокачка увеличивает глубину.");
                hud.Button(heading,"?",()=>hud.ShowSkillDetails(title,detail),"skill-info");
                Text(body,kind==AbilityKind.Dash?L.T("Импульс в выбранную сторону."):L.T("Удар вниз с пробитием."),"skill-summary");
                state=hud.BoldText(body,"","upgrade-effect");var row=Box(body,"skill-price-row");price=hud.BoldText(row,"","upgrade-price");materials=Box(row,"recipe-resources");reason=Text(body,"","upgrade-reason");
                buy=hud.Button(body,L.T("ОТКРЫТЬ"),()=>hud.game.BuyAbility(kind),"stone-button skill-buy",UiSound.None);buy.name="unlock-"+kind;
                pin=PaperPin(card);pin.name="goal-pin-ability-"+kind;
                skillLock=new SkillLock(hud,card,kind==AbilityKind.Dash?25:26);
                CardClick(card,()=>{if(!skillLock.IsOpen)skillLock.Activate();else if(!hud.game.Economy.AbilityUnlocked(kind))hud.ToggleCardGoal(GoalKind.Ability,(int)kind);});
            }
            public void Refresh()
            {
                bool unlocked=hud.game.Economy.AbilityUnlocked(kind),can=hud.game.Economy.CanUnlock(kind,out string why)&&hud.game.CanTrade;
                skillLock.Refresh();Display(pin,!unlocked&&skillLock.IsOpen&&hud.game.Economy.Profile.goalKind==GoalKind.Ability&&hud.game.Economy.Profile.goalIndex==(int)kind);
                state.text= L.T(unlocked?L.T("Открыто навсегда"):L.T("Открывается один раз"));materials.Clear();
                buy.text= L.T(unlocked?L.T("ОТКРЫТО"):L.T("ОТКРЫТЬ"));buy.SetEnabled(can);Display(buy,skillLock.IsOpen&&!unlocked);
                buy.EnableInClassList("buy-ready",can);buy.EnableInClassList("buy-blocked",!can&&!unlocked);
                price.text= L.T("");reason.text= L.T(why??"");Display(reason,!can&&!unlocked&&skillLock.IsOpen);
                if(unlocked)return;
                var cost=Progression.AbilityCost(kind,hud.game.Economy.Config);price.text= L.T(Exact(cost.Coins)+L.T(" мон."));
                price.EnableInClassList("cost-enough",hud.game.Wallet>=cost.Coins);price.EnableInClassList("cost-short",hud.game.Wallet<cost.Coins);
                foreach(var material in ResourceInventory.Materials)
                {
                    int i=(int)material;if(cost.Resources[i]==0)continue;
                    hud.RecipeChip(materials,material,hud.game.Inventory[material],cost.Resources[i]);
                }
            }
        }
    }
}
