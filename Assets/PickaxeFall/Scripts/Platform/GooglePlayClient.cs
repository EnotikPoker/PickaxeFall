using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    public sealed class GooglePlayClient:IGameCloud,IGameAchievements
    {
        readonly PlayPreferences prefs;readonly PlatformHost host;readonly GameController game;readonly string directory;
        public PlayReleaseConfig Config {get;}
        public PlaySaveStore Store {get;}
        public PlayAudience Audience {get;private set;}
        public bool Adult=>Audience==PlayAudience.Adult;
        public bool SignedIn {get;private set;}
        public bool Frozen {get;private set;}
        public bool AdvertisingEnabled {get;private set;}
        public bool TransactionBusy=>purchases?.Busy??false;
        public bool RemoveAds=>purchases?.RemoveAds??false;
        public string Price=>purchases?.Price??"";
        public string Status {get;private set;}="Прогресс сохраняется на устройстве";
        public string PurchaseStatus=>purchases?.Status??"Магазин пока недоступен";
        public bool CanPurchase=>purchases?.Ready??false;
        readonly HashSet<string> acknowledged=new();
        PlayPurchases purchases;UIDocument dialog;bool dirty=true,cloudBusy,loggingIn,deleting;
        double nextCloud,cloudDeadline,nextAchievements;string owner="",request="",sending="";
#if PICKAXEFALL_GOOGLE_PLAY && UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaClass bridge;
#endif
        public GooglePlayClient(PlatformHost host,GameController game,string directory)
        {
            this.host=host;this.game=game;this.directory=directory;prefs=new PlayPreferences(game.TestMode.Length>0);
            var resource=Resources.Load<TextAsset>("GooglePlay/config");Config=resource?JsonUtility.FromJson<PlayReleaseConfig>(resource.text):new PlayReleaseConfig();
            Audience=(PlayAudience)prefs.GetInt("play.audience",0);if(!Enum.IsDefined(typeof(PlayAudience),Audience))Audience=PlayAudience.Unknown;
            Store=new PlaySaveStore(directory,prefs.GetString("play.owner."+PlayReleaseConfig.SaveSpace(PlayReleaseConfig.SaveEpoch),"guest"));
            Store.Changed+=()=>dirty=true;
#if PICKAXEFALL_GOOGLE_PLAY && UNITY_ANDROID && !UNITY_EDITOR
            bridge=new AndroidJavaClass("com.enotikpoker.pickaxefall.platform.PlayBridge");bridge.CallStatic("bind",host.gameObject.name,JsonUtility.ToJson(Config));
#endif
            purchases=new PlayPurchases(Config,Path.Combine(directory,"google-entitlement.json"));purchases.Changed+=game.PlatformStateChanged;
        }
#if PICKAXEFALL_PLAY_QA && DEVELOPMENT_BUILD
        public Action<string,object[]> QaNative;
        public VisualElement QaDialog=>dialog?dialog.rootVisualElement:null;
        public string QaRequest=>request;
        public void QaFreshAudience(){prefs.SetInt("play.audienceChosen",0);Audience=PlayAudience.Unknown;AdvertisingEnabled=false;}
        public void QaLongDecision()=>cloudDeadline=-1;
#endif
        void Native(string method,params object[] args)
        {
#if PICKAXEFALL_PLAY_QA && DEVELOPMENT_BUILD
            if(QaNative!=null){QaNative(method,args);return;}
#endif
#if PICKAXEFALL_GOOGLE_PLAY && UNITY_ANDROID && !UNITY_EDITOR
            try{bridge.CallStatic(method,args);}catch(Exception)
            {
                Status="Сервис временно недоступен";cloudBusy=false;loggingIn=false;
                if(method=="showAd")host.OnPlatformAd(JsonUtility.ToJson(new NativeAd{id=(string)args[0],kind="close"}));
            }
#endif
        }
        string Slot=>"pickaxefall_"+PlayReleaseConfig.SaveSpace(PlayReleaseConfig.SaveEpoch);
        string OwnerKey=>"play.owner."+PlayReleaseConfig.SaveSpace(PlayReleaseConfig.SaveEpoch);
        public void Present()
        {
            if(prefs.GetInt("play.audienceChosen",0)!=1){ChooseAudience();return;}
            if(Adult&&prefs.GetInt("play.consentChosen",0)!=1){ChooseConsent();return;}
            ConfigureAds();purchases.Start();
        }
        void ConfigureAds()
        {
            bool consent=prefs.GetInt("play.personalized",0)==1;
            AdvertisingEnabled=Adult?(Config.testMode||!string.IsNullOrEmpty(Config.yandexInterstitial)):
                Config.kidozAccountApproved&&Config.kidozCloseWithinFiveSecondsConfirmed&&!string.IsNullOrEmpty(Config.kidozPublisherId);
#if PICKAXEFALL_PLAY_QA && DEVELOPMENT_BUILD
            if(QaNative!=null){if(AdvertisingEnabled)Native("configureAds",Adult,consent);return;}
#endif
#if PICKAXEFALL_GOOGLE_PLAY && UNITY_ANDROID && !UNITY_EDITOR
            if(AdvertisingEnabled)try{AdvertisingEnabled=bridge.CallStatic<bool>("configureAds",Adult,consent);}catch(Exception){AdvertisingEnabled=false;}
#endif
        }
        public void ShowAd(string id,bool reward)
        {
            if(!AdvertisingEnabled||!reward&&RemoveAds){host.OnPlatformAd(JsonUtility.ToJson(new NativeAd{id=id,kind="close"}));return;}
            Native("showAd",id,reward,Adult);
        }
        [Serializable] sealed class NativeAd{public string id,kind;public bool shown;}
        public void SignIn()
        {
            if(loggingIn||cloudBusy||Frozen||!game.CanTrade)return;
            if(string.IsNullOrEmpty(Config.gamesApplicationId)){Status="Play Games подключится после настройки приложения";return;}
            game.SaveCheckpoint();loggingIn=true;cloudDeadline=Time.realtimeSinceStartupAsDouble+30;Native("signIn");
        }
        [Serializable] sealed class Identity{public string kind,owner;}
        public void IdentityEvent(string text)
        {
            loggingIn=false;var e=JsonUtility.FromJson<Identity>(text);
            if(e.kind!="signed-in"||string.IsNullOrEmpty(e.owner))
            {SignedIn=false;acknowledged.Clear();Status=e.kind=="changed"?"Аккаунт Google изменился. Подключи Play Games заново.":"Не удалось войти. Прогресс на устройстве сохранён.";return;}
            owner=e.owner;SignedIn=true;acknowledged.Clear();ReadCloud();
        }
        void ReadCloud()
        {
            if(!SignedIn||cloudBusy)return;
            cloudBusy=true;request=Guid.NewGuid().ToString("N");cloudDeadline=Time.realtimeSinceStartupAsDouble+30;Native("readCloud",request,Slot);
        }
        public void Tick()
        {
            purchases?.Tick();double now=Time.realtimeSinceStartupAsDouble;
            if(loggingIn&&now>cloudDeadline){loggingIn=false;Status="Не удалось войти. Прогресс на устройстве сохранён.";}
            if(cloudBusy&&!Frozen&&now>cloudDeadline){cloudBusy=false;request="";Native("discardCloud");nextCloud=now+30;Status="Нет связи с облаком. Прогресс на устройстве сохранён.";}
            if(!SignedIn||Frozen||cloudBusy||now<nextCloud)return;
            if(dirty){nextCloud=now+10;ReadCloud();}
            if(now>=nextAchievements){nextAchievements=now+30;Synchronize(game.Economy.Profile);}
        }
        [Serializable] sealed class CloudEvent{public string kind,request,owner,text,left,right;}
        public void CloudEventReceived(string json)
        {
            var e=JsonUtility.FromJson<CloudEvent>(json);
            if(e.kind=="deleted"&&deleting&&e.request==request&&e.owner==owner){deleting=false;cloudBusy=false;SignedIn=false;owner="";Status="Облачное сохранение удалено";return;}
            if(!SignedIn||e.request!=request||e.owner!=owner)return;
            if(e.kind=="error"){cloudBusy=false;deleting=false;Status="Нет связи с облаком. Прогресс на устройстве сохранён.";nextCloud=Time.realtimeSinceStartupAsDouble+30;return;}
            try
            {
                if(e.kind=="saved")
                {Store.Acknowledge(sending);cloudBusy=false;dirty=Store.Checkpoint.revision!=sending;Status="Сохранено в облаке";return;}
                if(e.kind=="sdk-conflict")
                {
                    var left=ParseRemote(e.left);var right=ParseRemote(e.right);
                    ChooseSave(left??PlayCheckpoint.New(owner,PlayReleaseConfig.SaveEpoch),right??PlayCheckpoint.New(owner,PlayReleaseConfig.SaveEpoch),first=>Native("resolveCloud",first));return;
                }
                if(e.kind!="loaded")return;
                var remote=ParseRemote(e.text);
                // Mining may have changed the live economy while this network read was pending.
                // Commit it before capturing the local choice; a write failure must abort sync.
                game.SaveCheckpoint();Store.Save(game.Economy.Profile);
                bool differentAccount=Store.Checkpoint.owner!="guest"&&Store.Checkpoint.owner!=owner;
                var local=differentAccount?(Store.Read(owner)??PlayCheckpoint.New(owner,PlayReleaseConfig.SaveEpoch)):Store.Checkpoint.Copy();
                if(PlaySaveStore.Conflicts(local,remote))
                {ChooseSave(local,remote,keep=>Accept(keep?local:remote.Core(),remote?.revision??""));return;}
                // An empty account never receives another signed-in account's resources.
                Accept(local,remote?.revision??"");
            }
            catch(Exception){cloudBusy=false;nextCloud=Time.realtimeSinceStartupAsDouble+30;Status="Не удалось прочитать облако. Локальное сохранение не изменено.";Native("discardCloud");CloseDialog();}
        }
        PlayCheckpoint ParseRemote(string text)
        {
            if(string.IsNullOrEmpty(text))return null;var value=Store.Parse(text);if(value.owner!=owner)throw new InvalidDataException("Wrong cloud owner");return value;
        }
        void Accept(PlayCheckpoint chosen,string remoteRevision)
        {
            bool replace=Store.Checkpoint.owner!=owner||chosen.revision!=Store.Checkpoint.revision;
            // Commit ownership before changing the live economy. File failure keeps old profile.
            if(replace)Store.Adopt(chosen,owner,remoteRevision);else if(!string.IsNullOrEmpty(remoteRevision))Store.Acknowledge(remoteRevision);
            prefs.SetString(OwnerKey,owner);prefs.Save();
            if(replace)game.ApplyPlatformProfile(Store.Load());
            var next=Store.Checkpoint.Core();sending=next.revision;dirty=false;Status="Сохранение в облако…";
            Native("writeCloud",request,JsonUtility.ToJson(next));
        }
        string Summary(PlayCheckpoint c)
        {
            var p=ProfileStore.Parse(c.profile);return L.F("Уровень {0} · Монеты {1}",p.profileLevel,p.coins)+"\n"+DateTimeOffset.FromUnixTimeMilliseconds(c.savedAt).UtcDateTime.ToString("yyyy-MM-dd HH:mm")+" UTC";
        }
        void ChooseSave(PlayCheckpoint left,PlayCheckpoint right,Action<bool> chosen)
        {
            var card=Dialog("Выбери сохранение");Label(card,"Сохранения не складываются. При загрузке другой копии текущий спуск завершится.");
            var error=new UnityEngine.UIElements.Label();error.AddToClassList("description");
            ChoiceButton(card,L.T("На устройстве")+"\n"+Summary(left),()=>Decide(true));ChoiceButton(card,L.T("В облаке")+"\n"+Summary(right),()=>Decide(false));
            card.Add(error);
            ChoiceButton(card,L.T("ПОКА БЕЗ ОБЛАКА"),()=>{SignedIn=false;cloudBusy=false;Native("discardCloud");CloseDialog();});
            void Decide(bool first)
            {
                try{game.SaveCheckpoint();Store.Backup(left,right);cloudDeadline=Time.realtimeSinceStartupAsDouble+30;CloseDialog();chosen(first);}
                catch(Exception){Status="Не удалось сохранить резервную копию. Прогресс не изменён.";error.text=L.T(Status);}
            }
        }
        public void DeleteCloud()
        {
            if(!SignedIn||cloudBusy)return;var card=Dialog("Удалить облачное сохранение?");Label(card,"Прогресс на этом устройстве останется. Синхронизация остановится до следующего входа.");
            ChoiceButton(card,L.T("УДАЛИТЬ"),()=>{CloseDialog();deleting=true;cloudBusy=true;cloudDeadline=Time.realtimeSinceStartupAsDouble+30;request=Guid.NewGuid().ToString("N");Native("deleteCloud",request,Slot);});ChoiceButton(card,L.T("ОТМЕНА"),CloseDialog);
        }
        public void Synchronize(PlayerProfile profile)
        {
            if(!SignedIn)return;var earned=PlayAchievements.Earned(profile);
            for(int i=0;i<earned.Length;i++)if(earned[i]&&Config.achievementIds!=null&&i<Config.achievementIds.Length)
            {var id=Config.achievementIds[i];if(!string.IsNullOrEmpty(id)&&!acknowledged.Contains(id))Native("achievement",id);}
        }
        public void AchievementEvent(string text){var e=JsonUtility.FromJson<AchievementMessage>(text);if(SignedIn&&e.kind=="unlocked"&&e.owner==owner)acknowledged.Add(e.id);}
        [Serializable] sealed class AchievementMessage{public string kind,id,owner;}
        public void Show(){if(SignedIn)Native("achievements");}
        public void Buy()=>purchases?.Buy();public void RestorePurchases()=>purchases?.Restore();
        public void PurchaseTickOnResume()=>purchases?.Restore();
        public void Dispose(){purchases?.Dispose();if(dialog)UnityEngine.Object.Destroy(dialog.gameObject);}
        VisualElement Dialog(string title)
        {
            if(dialog)UnityEngine.Object.Destroy(dialog.gameObject);Frozen=true;game.PlatformStateChanged();
            var obj=new GameObject("Play service dialog");dialog=obj.AddComponent<UIDocument>();dialog.panelSettings=game.PanelConfig;dialog.sortingOrder=220;
            var root=dialog.rootVisualElement;root.styleSheets.Add(game.InterfaceStyle);root.AddToClassList("screen");root.pickingMode=PickingMode.Position;
            root.style.position=Position.Absolute;root.style.left=0;root.style.right=0;root.style.top=0;root.style.bottom=0;root.style.backgroundColor=new Color(0,0,0,.7f);
            var font=Resources.Load<Font>("Fonts/Monocraft");if(font)root.style.unityFontDefinition=FontDefinition.FromFont(font);
            var card=new ScrollView(){name="play-service-card"};card.AddToClassList("platform-choice");card.AddToClassList("play-dialog");root.Add(card);
            card.style.bottom=Length.Percent(10);
            var heading=new UnityEngine.UIElements.Label(L.T(title));heading.AddToClassList("title");card.Add(heading);return card;
        }
        void CloseDialog(){if(dialog)UnityEngine.Object.Destroy(dialog.gameObject);dialog=null;Frozen=false;game.PlatformStateChanged();}
        void Label(VisualElement card,string text){var label=new UnityEngine.UIElements.Label(L.T(text));label.AddToClassList("description");card.Add(label);}
        void ChoiceButton(VisualElement card,string text,Action action){var button=new Button(action){text=L.T(text)};button.AddToClassList("stone-button");card.Add(button);}
        public void ChooseAudience()
        {
            var card=Dialog("Сколько тебе лет?");Label(card,"Возраст нужен, чтобы выбрать подходящую рекламу. Сохраним только возрастную группу на этом устройстве.");
            var age=new TextField(){maxLength=3,name="play-age-input",keyboardType=TouchScreenKeyboardType.NumberPad};age.AddToClassList("quantity-input");card.Add(age);
            var next=new Button(()=>{if(int.TryParse(age.value,out int n)&&n>=0&&n<=120)SetAudience(PlayReleaseConfig.ClassifyAge(n));}){text=L.T("ПРОДОЛЖИТЬ"),name="play-age-confirm"};next.AddToClassList("stone-button");next.SetEnabled(false);card.Add(next);
            age.RegisterValueChangedCallback(e=>next.SetEnabled(int.TryParse(e.newValue,out int n)&&n>=0&&n<=120));
            ChoiceButton(card,L.T("ПРОПУСТИТЬ"),()=>SetAudience(PlayAudience.Unknown));
        }
        void SetAudience(PlayAudience selected)
        {
            bool already=prefs.GetInt("play.audienceChosen",0)==1;
            prefs.SetInt("play.audience",(int)selected);prefs.SetInt("play.audienceChosen",1);prefs.SetInt("play.personalized",0);prefs.SetInt("play.consentChosen",0);prefs.Save();
            // Never switch providers in a live process: SDKs cannot reliably uninitialize.
            if(already)
            {
                game.SaveCheckpoint();AdvertisingEnabled=false;Native("blockAds");
                var restart=Dialog("Настройки сохранены");Label(restart,"Новый выбор начнёт действовать после полного перезапуска игры. До этого реклама отключена.");
                ChoiceButton(restart,L.T("ПРОДОЛЖИТЬ"),CloseDialog);ChoiceButton(restart,L.T("ЗАКРЫТЬ ИГРУ"),()=>Application.Quit());return;
            }
            Audience=selected;
            if(Adult)ChooseConsent();else FinishConsent(false);
        }
        void ChooseConsent()
        {
            var card=Dialog("Реклама и конфиденциальность");Label(card,"Яндекс может использовать данные для подбора рекламы. Разрешение необязательно: ты сможешь играть при любом выборе.");
                if(Uri.TryCreate(Config.privacyUrl,UriKind.Absolute,out _))ChoiceButton(card,L.T("Политика конфиденциальности"),()=>Application.OpenURL(Config.privacyUrl));
                ChoiceButton(card,L.T("Разрешить"),()=>FinishConsent(true));ChoiceButton(card,L.T("Без персонализации"),()=>FinishConsent(false));
        }
        void FinishConsent(bool value){prefs.SetInt("play.consentChosen",1);prefs.SetInt("play.personalized",value?1:0);prefs.Save();CloseDialog();ConfigureAds();purchases.Start();}
    }
}
