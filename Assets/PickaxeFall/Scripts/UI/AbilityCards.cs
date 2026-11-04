using UnityEngine.UIElements;

namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        sealed class AbilityCard
        {
            readonly GameHud hud;readonly AbilityKind kind;readonly Label state,price,reason;readonly VisualElement materials;readonly Button buy;
            public AbilityCard(GameHud owner,VisualElement parent,AbilityKind ability)
            {
                hud=owner;kind=ability;var card=Box(parent,"upgrade-card inset");card.name="ability-"+kind;
                hud.BoldText(card,kind==AbilityKind.Dash?"РЫВОК · СВАЙП":"ПРОБОЙ · НАЖАТИЕ","upgrade-title");
                Text(card,kind==AbilityKind.Dash?"Толкает выбранную кирку по одной из четырёх осей и пробивает первый блок. Базовая цена — "+Progression.Number(hud.game.Economy.Config.dashCost)+" энергии.":"Направляет выбранную кирку вниз и пробивает блок. Глубина увеличивается отдельной прокачкой. Базовая цена — "+Progression.Number(hud.game.Economy.Config.plungeCost)+" энергии.","description");
                state=hud.BoldText(card,"","upgrade-effect");price=Text(card,"","upgrade-price");materials=Box(card,"recipe-resources");reason=Text(card,"","upgrade-reason");
                buy=hud.Button(card,"ОТКРЫТЬ",()=>hud.game.BuyAbility(kind),"stone-button secondary");buy.name="unlock-"+kind;
            }
            public void Refresh()
            {
                bool unlocked=hud.game.Economy.AbilityUnlocked(kind),can=hud.game.Economy.CanUnlock(kind,out string why)&&hud.game.CanTrade;
                state.text=unlocked?"Открыто навсегда":"Нужно открыть один раз";materials.Clear();
                buy.text=unlocked?"ОТКРЫТО":"ОТКРЫТЬ";buy.SetEnabled(can);buy.EnableInClassList("buy-ready",can);buy.EnableInClassList("buy-blocked",!can&&!unlocked);
                price.text="";reason.text=unlocked?"Уровни прокачки и открытие сохраняются.":can?"После покупки приём будет доступен в каждом спуске.":why;
                if(unlocked)return;
                var cost=Progression.AbilityCost(kind,hud.game.Economy.Config);price.text=$"Цена: {Exact(cost.Coins)} монет · есть {Short(hud.game.Wallet)}";
                price.EnableInClassList("cost-enough",hud.game.Wallet>=cost.Coins);price.EnableInClassList("cost-short",hud.game.Wallet<cost.Coins);
                foreach(var material in ResourceInventory.Materials)
                {
                    int i=(int)material;if(cost.Resources[i]==0)continue;var box=Box(materials,"recipe-item");Icon(box,PixelArt.ResourceIcon(material),"recipe-icon");
                    hud.BoldText(box,$"{Short(hud.game.Inventory[material])} / {Exact(cost.Resources[i])}",hud.game.Inventory[material]>=cost.Resources[i]?"recipe-amount cost-enough":"recipe-amount cost-short");
                }
            }
        }
    }
}
