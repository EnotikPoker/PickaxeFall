using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
namespace PickaxeFall
{
    public sealed partial class PlatformHost:MonoBehaviour,IGameTimeSource,IGameAdvertising,IPlatformLifecycle
    {
        public bool IsYandex {
            get {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
        public bool Available {get=>IsGooglePlay?Google?.AdvertisingEnabled==true:available;private set=>available=value;}bool available;
        public bool Busy {get;private set;}
        double adStarted;
        public bool ServiceStalled=>(Busy && Time.realtimeSinceStartupAsDouble-adStarted>=120) || Google?.PurchaseStalled==true;
        public bool MidRunAds {get;private set;}
        public bool Authorized {get;private set;}
        public bool Conflict {get;private set;}
        public string Language {get;private set;}="ru";
        public string CloudStatus {get;private set;}="";
        public bool Suspended=>sdkPaused||pagePaused||Conflict||Google?.Frozen==true||Google?.TransactionBusy==true;
        bool sdkPaused,pagePaused,ready,gameplay;
        GameController game;Action reward;Action<bool> closed;string adId="";bool rewardSent;
        public long UtcMilliseconds=>serverTime>0?serverTime+(long)((Time.realtimeSinceStartupAsDouble-serverAt)*1000):DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public bool IsTrusted=>serverTime>0;long serverTime;double serverAt;
        public IProfilePersistence Store {get;private set;}
        public WebAdState Ads {
            get {
                if(Google!=null)return Google.Store.Ads;
#if UNITY_WEBGL && !UNITY_EDITOR
                if(Store is BrowserProfileStore browser)return browser.Ads;
#endif
                return nativeAds;
            }
        }
        readonly WebAdState nativeAds=new();
        public IEnumerator Initialize(GameController owner,PanelSettings panel,StyleSheet style,string directory,bool temporary)
        {
            game=owner;
#if UNITY_WEBGL && !UNITY_EDITOR
            PF_Init(gameObject.name);float deadline=Time.realtimeSinceStartup+20;
            while(!booted&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!booted)throw new InvalidOperationException(L.T("Не удалось подключить платформу. Перезагрузи страницу."));
            L.SetLanguage(PlayerPrefs.GetString("settings.v1.language",Language));
            var local=ReadLocal(user);WebCheckpoint cloud=ParseCheckpoint(boot.cloud);
            if(local==null&&Authorized){var guest=ReadLocal("guest");if(guest!=null){local=guest.Copy();local.user=user;local.synced="";}}
            if(!boot.cloudRead&&Authorized&&local==null)
            {
                bool guest=false;yield return Choice(panel,style,"Облако недоступно","Повторить загрузку","Играть гостем",v=>{if(v)PF_Reload();else guest=true;});
                if(!guest)yield break;user="guest";Authorized=false;local=ReadLocal(user);cloud=null;
            }
            if(local!=null&&cloud!=null&&local.revision!=cloud.revision&&local.synced!=cloud.revision)
            {
                var left=local;var right=cloud;bool chooseLocal=true;
                yield return Choice(panel,style,"Выбери сохранение",Summary("На устройстве",left),Summary("В облаке",right),v=>chooseLocal=v);
                if(PF_Backup(JsonUtility.ToJson(left),JsonUtility.ToJson(right))!=1)throw new InvalidOperationException(L.T("Память браузера недоступна или заполнена. Предыдущее сохранение не изменено."));
                local=chooseLocal?left:right.Core();local.user=user;local.synced=right.revision;
                if(chooseLocal)local.revision=Guid.NewGuid().ToString("N");
            }
            var chosen=local??cloud?.Core()??BrowserProfileStore.New(user,UtcMilliseconds);chosen.user=user;
            if(cloud!=null&&chosen.revision==cloud.revision)chosen.synced=cloud.revision;
            WriteLocal(chosen);Store=new BrowserProfileStore(this,chosen);QueueCloud();
#else
            if(IsGooglePlay&&(!temporary||game.TestMode=="google")){Google=new GooglePlayClient(this,game,directory);Store=Google.Store;}
            else Store=new ProfileStore(directory,temporary?()=>new PlayerProfile():null);yield break;
#endif
        }
        public void GameReady()
        {
            if(ready)return;ready=true;
            Google?.Present();
#if UNITY_WEBGL && !UNITY_EDITOR
            StartCoroutine(ReadyWhenPresented());
#endif
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        IEnumerator ReadyWhenPresented()
        {
            while(!UnityEngine.Rendering.SplashScreen.isFinished)yield return null;
            yield return null; // UI Toolkit has completed its first layout.
            PF_Ready();
        }
#endif
        public void Gameplay(bool running)
        {
            if(gameplay==running)return;gameplay=running;
#if UNITY_WEBGL && !UNITY_EDITOR
            PF_Gameplay(running?1:0);
#endif
        }
        public void Reload()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            PF_Reload();
#else
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
#endif
        }
        public void Login()
        {
            if(IsGooglePlay){Google.SignIn();return;}
#if UNITY_WEBGL && !UNITY_EDITOR
            game.SaveCheckpoint();PF_Login();
#endif
        }
        public void Show(string placement,bool rewarded,Action onReward,Action<bool> onClosed)
        {
            if(Busy||!Available||!rewarded&&Google?.RemoveAds==true){onClosed(false);return;}
            Busy=true;adStarted=Time.realtimeSinceStartupAsDouble;adId=Guid.NewGuid().ToString("N");reward=onReward;closed=onClosed;rewardSent=false;game.PlatformStateChanged();
#if UNITY_WEBGL && !UNITY_EDITOR
            PF_Ad(adId,rewarded?1:0);
#else
            if(IsGooglePlay)Google.ShowAd(adId,rewarded);else CompleteAd(false);
#endif
        }
        void CompleteAd(bool shown){if(!Busy)return;Busy=false;reward=null;var callback=closed;closed=null;game.PlatformStateChanged();callback?.Invoke(shown);}
        public void ExitStalledService()
        {
            if(!ServiceStalled || !game.SaveCheckpoint())return;
            // Do not resume gameplay under an SDK advertisement that may still open.
            // A fresh process/page also discards delayed callbacks from the old request.
#if UNITY_WEBGL && !UNITY_EDITOR
            PF_Reload();
#elif UNITY_ANDROID && !UNITY_EDITOR
            using var process=new AndroidJavaClass("android.os.Process");
            process.CallStatic("killProcess",process.CallStatic<int>("myPid"));
#else
            Application.Quit();
#endif
        }
#if PICKAXEFALL_PLAY_QA && DEVELOPMENT_BUILD
        public void QaExpireAd()=>adStarted=Time.realtimeSinceStartupAsDouble-121;
#endif
        [Serializable] sealed class AdEvent {public string id,kind;public bool shown;}
        [UnityEngine.Scripting.Preserve]
        public void OnPlatformAd(string json)
        {
            var message=JsonUtility.FromJson<AdEvent>(json);if(!Busy||message.id!=adId)return;
            if(message.kind=="reward"){if(!rewardSent){rewardSent=true;reward?.Invoke();}}else if(message.kind=="close")CompleteAd(message.shown);
        }
        [Serializable] sealed class PauseEvent {public string source;public bool paused;}
        [UnityEngine.Scripting.Preserve]
        public void OnPlatformPause(string json)
        {
            var message=JsonUtility.FromJson<PauseEvent>(json);if(message.source=="sdk")sdkPaused=message.paused;else pagePaused=message.paused;
            game?.PlatformStateChanged();
        }
#if UNITY_WEBGL && !UNITY_EDITOR
        [Serializable] sealed class Boot {public bool available,authorized,cloudRead,midRunAds;public string language,user,cloud;public long serverTime;}
        Boot boot;bool booted,cloudDirty,cloudBusy;string user="guest",sending="";double nextCloud;
        [DllImport("__Internal")]static extern void PF_Init(string target);
        [DllImport("__Internal")]static extern string PF_LocalRead(string user);
        [DllImport("__Internal")]static extern int PF_LocalWrite(string user,string json,int reset);
        [DllImport("__Internal")]static extern int PF_Backup(string local,string cloud);
        [DllImport("__Internal")]static extern void PF_Ready();
        [DllImport("__Internal")]static extern void PF_Gameplay(int running);
        [DllImport("__Internal")]static extern void PF_Login();
        [DllImport("__Internal")]static extern void PF_Ad(string id,int rewarded);
        [DllImport("__Internal")]static extern void PF_Cloud(string json,string expected);
        [DllImport("__Internal")]static extern void PF_CloudRead();
        [DllImport("__Internal")]static extern void PF_Reload();
        [UnityEngine.Scripting.Preserve]
        public void OnPlatformBoot(string json)
        {
            boot=JsonUtility.FromJson<Boot>(json);Available=boot.available;Authorized=boot.authorized;MidRunAds=boot.midRunAds;Language=boot.language=="ru"?"ru":"en";
            user=Authorized?boot.user:"guest";serverTime=boot.serverTime;serverAt=Time.realtimeSinceStartupAsDouble;booted=true;
        }
        WebCheckpoint ParseCheckpoint(string text)
        {
            if(string.IsNullOrEmpty(text))return null;var checkpoint=JsonUtility.FromJson<WebCheckpoint>(text);checkpoint.Validate();return checkpoint;
        }
        WebCheckpoint ReadLocal(string id)
        {
            string json=PF_LocalRead(id);if(json=="!error")throw new InvalidOperationException(L.T("Разреши сохранение данных сайта в браузере и перезагрузи страницу."));return ParseCheckpoint(json);
        }
        public void WriteLocal(WebCheckpoint value,bool reset=false)
        {
            if(PF_LocalWrite(value.user,JsonUtility.ToJson(value),reset?1:0)!=1)throw new InvalidOperationException(L.T("Память браузера недоступна или заполнена. Предыдущее сохранение не изменено."));
        }
        public void QueueCloud(){cloudDirty=true;}
        void TickWebCloud()
        {
            if(!Authorized||Conflict||cloudBusy||Store==null||Time.realtimeSinceStartupAsDouble<nextCloud)return;
            if(!boot.cloudRead){cloudBusy=true;nextCloud=Time.realtimeSinceStartupAsDouble+30;PF_CloudRead();return;}
            if(!cloudDirty)return;
            var browser=(BrowserProfileStore)Store;var value=browser.Checkpoint.Core();
            if(value.revision==value.synced){cloudDirty=false;return;}
            cloudBusy=true;cloudDirty=false;sending=value.revision;nextCloud=Time.realtimeSinceStartupAsDouble+10;CloudStatus=L.T("Сохранение в облако…");
            PF_Cloud(JsonUtility.ToJson(value),value.synced);
        }
        [Serializable] sealed class CloudEvent {public string kind,cloud;}
        [UnityEngine.Scripting.Preserve]
        public void OnPlatformCloud(string json)
        {
            cloudBusy=false;var message=JsonUtility.FromJson<CloudEvent>(json);var store=(BrowserProfileStore)Store;
            if(message.kind=="loaded")
            {
                boot.cloudRead=true;var remote=ParseCheckpoint(message.cloud);var local=store.Checkpoint;
                if(remote!=null&&local.revision!=remote.revision&&local.synced!=remote.revision)
                {Conflict=true;game.PlatformStateChanged();StartCoroutine(RuntimeConflict(message.cloud));}
                else{if(remote!=null&&local.revision==remote.revision)store.Acknowledge(remote.revision);cloudDirty=true;}
            }
            else if(message.kind=="saved")
            {
                try{store.Acknowledge(sending);CloudStatus=L.T("Сохранено в облаке");}
                catch(Exception){cloudDirty=true;CloudStatus=L.T("Не удалось сохранить на устройстве");}
            }
            else if(message.kind=="conflict")
            {
                Conflict=true;game.PlatformStateChanged();StartCoroutine(RuntimeConflict(message.cloud));
            }
            else{cloudDirty=true;nextCloud=Time.realtimeSinceStartupAsDouble+30;CloudStatus=L.T("Нет связи с облаком. Прогресс на устройстве сохранён.");}
        }
        IEnumerator RuntimeConflict(string text)
        {
            var local=((BrowserProfileStore)Store).Checkpoint.Copy();var remote=ParseCheckpoint(text);bool keep=true;
            yield return Choice(game.PanelConfig,game.InterfaceStyle,"Выбери сохранение",Summary("На устройстве",local),Summary("В облаке",remote),v=>keep=v);
            if(PF_Backup(JsonUtility.ToJson(local),JsonUtility.ToJson(remote))!=1)
            {
                CloudStatus=L.T("Память браузера недоступна или заполнена. Предыдущее сохранение не изменено.");
                Conflict=false;cloudDirty=true;nextCloud=Time.realtimeSinceStartupAsDouble+30;game.PlatformStateChanged();yield break;
            }
            var next=keep?local:remote.Core();next.user=user;next.synced=remote.revision;if(keep)next.revision=Guid.NewGuid().ToString("N");WriteLocal(next);PF_Reload();
        }
        string Summary(string label,WebCheckpoint value)
        {
            var p=ProfileStore.Parse(value.profile);return L.T(label)+"\n"+L.F("Уровень {0} · Монеты {1}",p.profileLevel,p.coins)+"\n"+DateTimeOffset.FromUnixTimeMilliseconds(value.savedAt).UtcDateTime.ToString("yyyy-MM-dd HH:mm")+" UTC";
        }
        IEnumerator Choice(PanelSettings panel,StyleSheet style,string title,string left,string right,Action<bool> choose)
        {
            var obj=new GameObject("Save choice");var doc=obj.AddComponent<UIDocument>();doc.panelSettings=panel;doc.sortingOrder=200;
            var root=doc.rootVisualElement;root.styleSheets.Add(style);root.AddToClassList("screen");root.style.position=Position.Absolute;root.style.left=0;root.style.right=0;root.style.top=0;root.style.bottom=0;root.style.backgroundColor=new Color(0,0,0,.7f);
            var font=Resources.Load<Font>("Fonts/Monocraft");if(font)root.style.unityFontDefinition=FontDefinition.FromFont(font);
            var card=new VisualElement();card.AddToClassList("platform-choice");card.AddToClassList("play-dialog");root.Add(card);
            var heading=new Label(L.T(title));heading.AddToClassList("title");card.Add(heading);bool done=false;
            foreach(bool first in new[]{true,false}){var b=new Button(()=>{choose(first);done=true;}){text=L.T(first?left:right),name=first?"save-choice-local":"save-choice-cloud"};b.AddToClassList("stone-button");card.Add(b);}
            while(!done)yield return null;Destroy(obj);yield return null;
        }
#endif
    }
}
