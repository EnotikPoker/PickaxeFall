using System;
using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    public sealed partial class GameHud
    {
        Label googleCloudLabel,googlePurchaseLabel;Button googleBuy;
        void DrawGoogleSettings(VisualElement card)
        {
            var client=game.Platform?.Google;if(client==null)return;
            BoldText(card,L.T("Google Play"),"section-label");
            Text(card,L.T("Играть можно без входа. Play Games сохраняет прогресс между устройствами; текущий спуск остаётся на телефоне."),"footnote");
            var login=Button(card,L.T(client.SignedIn?"Проверить облако":"Подключить Play Games"),client.SignIn,"stone-button compact-button");login.name="google-login";login.SetEnabled(game.CanTrade);
            googleCloudLabel=Text(card,L.T(client.Status),"footnote");googleCloudLabel.name="google-cloud-status";
            Button(card,L.T("ДОСТИЖЕНИЯ"),OpenAchievements,"stone-button compact-button").name="settings-achievements";
            googleBuy=Button(card,"",client.Buy,"stone-button compact-button");googleBuy.name="google-buy-remove-ads";
            Text(card,L.T("Постоянная покупка убирает принудительную рекламу. Добровольные просмотры за бонус остаются."),"footnote");
            Button(card,L.T("Восстановить покупку"),client.RestorePurchases,"stone-button compact-button").name="google-restore-purchases";
            googlePurchaseLabel=Text(card,L.T(client.PurchaseStatus),"footnote");googlePurchaseLabel.name="google-purchase-status";
            Button(card,L.T("Возраст и реклама"),()=>{CloseModal();client.ChooseAudience();},"stone-button compact-button").name="google-privacy-options";
            if(Uri.TryCreate(client.Config.privacyUrl,UriKind.Absolute,out var privacy)&&privacy.Scheme=="https")
                Button(card,L.T("Политика конфиденциальности"),()=>Application.OpenURL(privacy.AbsoluteUri),"stone-button compact-button");
            else Text(card,L.T("Тестовая версия. Публичная политика конфиденциальности ещё не настроена."),"footnote");
            if(client.SignedIn)Button(card,L.T("Удалить облачное сохранение"),client.DeleteCloud,"stone-button compact-button").name="google-delete-cloud";
            if(client.Config.testMode)Text(card,L.T("ТЕСТ GOOGLE PLAY · реальные платежи и детская реклама требуют настройки аккаунтов"),"footnote");
            TickGoogleControls();
        }
        void TickGoogleControls()
        {
            var client=game.Platform?.Google;if(client==null)return;
            if(googleCloudLabel!=null)googleCloudLabel.text=L.T(client.Status);
            if(googlePurchaseLabel!=null)googlePurchaseLabel.text=L.T(client.PurchaseStatus);
            if(googleBuy!=null){googleBuy.text=client.RemoveAds?L.T("Реклама отключена"):string.IsNullOrEmpty(client.Price)?L.T("Убрать рекламу · цена недоступна"):L.T("Убрать рекламу · ")+client.Price;googleBuy.SetEnabled(game.CanTrade&&client.CanPurchase&&!client.RemoveAds);}
        }
        void OpenAchievements()
        {
            var card=OpenModal(L.T("ДОСТИЖЕНИЯ"));var earned=PlayAchievements.Earned(game.Economy.Profile);
            for(int i=0;i<earned.Length;i++)
            {
                var row=Box(card,"achievement-row");BoldText(row,L.T(PlayAchievements.Titles[i]),"section-label");
                Text(row,L.T(earned[i]?"Выполнено":"Ещё впереди"),earned[i]?"cost-enough":"footnote");
            }
            var client=game.Platform?.Google;
            if(client?.SignedIn==true)Button(card,L.T("Открыть в Play Games"),client.Show,"stone-button compact-button");
            else Text(card,L.T("После подключения Play Games выполненные достижения отправятся автоматически."),"footnote");
            Button(card,L.T("ЗАКРЫТЬ"),()=>CloseModal(),"stone-button primary");
        }
    }
}
