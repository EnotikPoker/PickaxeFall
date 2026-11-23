using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    public partial class GameController
    {
        public PlatformHost Platform {get;private set;}
        public PanelSettings PanelConfig=>panelSettings;
        public StyleSheet InterfaceStyle=>interfaceStyle;
        bool adCountdown;string verifiedRewardId="";
        public bool PlatformFrozen=>adCountdown||Platform!=null&&(Platform.Busy||Platform.Suspended);
        public bool RewardAvailable=>Platform!=null&&Platform.HasAdvertising&&(Phase==RunPhase.Finished||Phase==RunPhase.Ready)&&!Platform.Ads.rewarded&&Platform.Ads.rewardValue>0;
        public void PlatformStateChanged()
        {
            if(!initialized)return;hud?.CancelUiPresses();RefreshTimeScale();Audio?.SetPlatformSuspended(PlatformFrozen);
            Platform?.Gameplay(Phase==RunPhase.Mining&&!WorldFrozen);if(PlatformFrozen)SaveProgress();
        }
        public bool SaveCheckpoint()=>SaveProgress();
        public void ApplyPlatformProfile(PlayerProfile profile)
        {
            var run=profile.run;Economy.ReplaceSaved(profile);NewRun();if(run!=null)RestoreRun(run);SaveProgress();
        }
        public void ChangeLanguage(string language)
        {
            Settings.SetLanguage(language);Settings.Save();hud?.Dispose();var document=GetComponent<UIDocument>();document.rootVisualElement.Clear();
            hud=new GameHud(this,document.rootVisualElement,interfaceStyle);hud.OpenSettings();Notify();
        }
        public void RepeatRun()
        {
            if(Platform==null||!Platform.HasAdvertising||!Platform.Ads.AfterDue){NewRun();return;}
            SaveProgress();Platform.Show("after-run",false,null,shown=>{Platform.Ads.AfterResult(shown);SaveProgress();NewRun();});
        }
        public void ClaimRunReward()
        {
            if(!RewardAvailable||Platform.Busy)return;
            string id=Platform.Ads.rewardId;
            if(verifiedRewardId==id){GrantRunReward(id);return;}
            Platform.Show("run-bonus",true,()=>GrantRunReward(id),shown=>{if(!shown)hud?.Toast(L.T("Нет доступной рекламы. Попробуй позже."));Notify();});
        }
        void GrantRunReward(string id)
        {
            if(Platform.Store is not IRewardProfileStore store||Platform.Ads.rewarded||Platform.Ads.rewardId!=id)return;
            verifiedRewardId=id;var next=Economy.Profile.Copy();var ads=Platform.Ads.Copy();
            try{next.coins=checked(next.coins+ads.rewardValue);ads.rewarded=true;store.CommitReward(next,ads);Economy.ReplaceSaved(next);hud?.Toast(L.T("Награда получена"));}
            catch(Exception){hud?.Toast(L.T("Не удалось выдать награду. Попробуй ещё раз."));}
            Notify();
        }
        void TickPlatform()
        {
            if(Platform==null||!Platform.HasAdvertising)return;
            Audio?.SetPlatformSuspended(PlatformFrozen);
            Platform.Gameplay(Phase==RunPhase.Mining&&!WorldFrozen);
            if(Platform.Available&&Platform.MidRunAds&&Platform.Ads.RunDue&&Phase==RunPhase.Mining&&!WorldFrozen&&!hud.ModalOpen)
            {
                foreach(var tool in tools)if(tool.IsAlive&&tool.AbilityActive)return;
                StartCoroutine(MidRunAdvertising());
            }
        }
        IEnumerator MidRunAdvertising()
        {
            adCountdown=true;PlatformStateChanged();SaveProgress();
            float elapsed=0;
            while(elapsed<2)
            {
                hud.AdvertisingCountdown(Mathf.CeilToInt(2-elapsed));
                yield return null;if(!SystemSuspended&&!Platform.Suspended)elapsed+=Time.unscaledDeltaTime;
            }
            hud.AdvertisingCountdown(0);
            Platform.Show("long-run",false,null,shown=>{Platform.Ads.RunResult();adCountdown=false;PlatformStateChanged();SaveProgress();});
        }
    }
    public sealed partial class GameHud
    {
        Label adCountdownLabel;Button rewardButton;
        VisualElement serviceRecovery;
        void TickServiceRecovery()
        {
            bool stalled=game.Platform?.ServiceStalled==true;
            if(stalled && serviceRecovery==null)
            {
                serviceRecovery=Box(root,"pause-overlay service-recovery");serviceRecovery.name="service-recovery";
                var card=Box(serviceRecovery,"modal-card");
                BoldText(card,L.T("Сервис не отвечает"),"title");
                Text(card,L.T("Перезапусти игру. Перед этим сохраним прогресс."),"description");
                Button(card,L.T(game.Platform.IsYandex?"ПЕРЕЗАГРУЗИТЬ":"СОХРАНИТЬ И ЗАКРЫТЬ"),game.Platform.ExitStalledService,"stone-button primary").name="service-recovery-exit";
            }
            if(serviceRecovery==null)return;
            Display(serviceRecovery,stalled);
            if(stalled)serviceRecovery.BringToFront();
        }
        void BuildPlatformControls()
        {
            if(game.Platform==null||!game.Platform.HasAdvertising)return;
            rewardButton=Button(mineView,"",game.ClaimRunReward,"stone-button primary");rewardButton.name="rewarded-run-bonus";
        }
        void TickPlatformControls()
        {
            TickServiceRecovery();
            TickGoogleControls();
            if(rewardButton==null)return;if(cloudStatusLabel!=null)cloudStatusLabel.text=L.T(game.Platform.CloudStatus);window.SetEnabled(!game.PlatformFrozen);
            Display(rewardButton,game.RewardAvailable);rewardButton.text=L.F("Реклама: +{0} монет",game.Platform.Ads.rewardValue);rewardButton.SetEnabled(game.Platform.Available&&!game.Platform.Busy);
        }
        public void AdvertisingCountdown(int value)
        {
            if(adCountdownLabel==null){adCountdownLabel=BoldText(root,"","ad-countdown");adCountdownLabel.name="ad-countdown";adCountdownLabel.pickingMode=PickingMode.Position;}
            Display(adCountdownLabel,value>0);adCountdownLabel.text=L.F("Реклама через {0}",value);adCountdownLabel.BringToFront();
        }
    }
}
