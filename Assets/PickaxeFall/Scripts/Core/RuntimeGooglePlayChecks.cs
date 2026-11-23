#if PICKAXEFALL_PLAY_QA && DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    // Runs the actual game, UI and service state machines. Network SDK callbacks are controlled.
    public sealed class RuntimeGooglePlayChecks:MonoBehaviour
    {
        int checks;GameController game;GooglePlayClient client;readonly List<(string method,object[] args)> calls=new();
        void Check(bool ok,string label){checks++;if(!ok)throw new Exception("Google runtime: "+label);}
        void OnEnable()=>Application.logMessageReceived+=OnLog;
        void OnDisable()=>Application.logMessageReceived-=OnLog;
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)Application.Quit(1);}
        [Serializable]sealed class Payload{public string kind,owner,request,text,id;public bool shown;}
        void Cloud(string kind,PlayCheckpoint value=null,string request=null,string owner="player-a")=>game.Platform.OnPlayCloud(JsonUtility.ToJson(new Payload{kind=kind,owner=owner,request=request??client.QaRequest,text=value==null?"":JsonUtility.ToJson(value)}));
        void Identity(string account)=>game.Platform.OnPlayIdentity(JsonUtility.ToJson(new Payload{kind="signed-in",owner=account}));
        void Ad(string kind,string id)=>game.Platform.OnPlatformAd(JsonUtility.ToJson(new Payload{kind=kind,id=id,shown=true}));
        void Submit(VisualElement element){Check(element!=null,"button exists");using var e=NavigationSubmitEvent.GetPooled();e.target=element;element.SendEvent(e);}
        void Click(string text){Button found=null;client.QaDialog.Query<Button>().ForEach(b=>{if(b.text==L.T(text))found=b;});Submit(found);}
        void Age(int age){client.QaDialog.Q<TextField>("play-age-input").value=age.ToString();Submit(client.QaDialog.Q("play-age-confirm"));}
#if UNITY_ANDROID
        [Serializable]sealed class ReceiptFixture{public string publicKey;public string[] receipts;}
        void CheckReceipts()
        {
            var fixture=JsonUtility.FromJson<ReceiptFixture>(Resources.Load<TextAsset>("GooglePlay/qa-receipts").text);
            var config=new PlayReleaseConfig{googleBillingPublicKey=fixture.publicKey};string file=System.IO.Path.Combine(game.ProfileDirectory,"qa-entitlement.json");
            using var verify=new PlayPurchases(config,file);Check(verify.QaValidate(fixture.receipts[0]),"real Unity validator accepts valid RSA signature");
            for(int i=1;i<fixture.receipts.Length;i++)Check(!verify.QaValidate(fixture.receipts[i]),"reject cancelled, refunded, pending, other product/package and empty token "+i);
            Check(!verify.QaValidate(fixture.receipts[0].Replace("test-token","tampered")),"tampered signed payload rejected");Check(!verify.QaValidate("{}"),"malformed receipt rejected");
            System.IO.Directory.CreateDirectory(game.ProfileDirectory);System.IO.File.WriteAllText(file,"{\"receipt\":"+UnityReceiptJson(fixture.receipts[0])+"}");
            using var offline=new PlayPurchases(config,file);Check(offline.RemoveAds,"verified entitlement works offline");
            System.IO.File.Copy(file,file+".bak");System.IO.File.WriteAllText(file,"broken");using var recovered=new PlayPurchases(config,file);Check(recovered.RemoveAds,"corrupted receipt primary recovers verified backup");
        }
        [Serializable]sealed class ReceiptWrapper{public string receipt;}
        string UnityReceiptJson(string value){var s=JsonUtility.ToJson(new ReceiptWrapper{receipt=value});return s.Substring(11,s.Length-12);}
#endif
        IEnumerator Start()
        {
            yield return null;game=GetComponent<GameController>();
            var root=GetComponent<UIDocument>().rootVisualElement;
#if UNITY_ANDROID
            CheckReceipts();
#endif
            client=game.Platform.Google;Check(client!=null,"Play host initialized in isolated QA player");
            game.Settings.SetTutorialSeen(true);game.Settings.SetTutorialHints(int.MaxValue);client.QaNative=(method,args)=>calls.Add((method,args));
            yield return null;
            Check(client.QaDialog.worldBound.height>100&&client.QaDialog.Q("play-service-card").worldBound.height>100,"dialog has visible full-screen layout");
            Check(client.Frozen&&game.PlatformFrozen&&Time.timeScale==0,"age dialog freezes physics");
            Check(calls.Count==0,"no SDK before age choice");var input=client.QaDialog.Q<TextField>("play-age-input");
            var picked=client.QaDialog.panel.Pick(input.worldBound.center);Check(picked!=null&&client.QaDialog.Contains(picked),"age input is above the tutorial and receives touches");input.value="-1";
            Check(!client.QaDialog.Q<Button>("play-age-confirm").enabledSelf,"invalid age cannot submit");
            Click("ПРОПУСТИТЬ");Check(!client.Frozen&&client.Audience==PlayAudience.Unknown&&!client.AdvertisingEnabled,"unknown can play without configured ads");
            Check(calls.Count==0,"unknown never initializes Yandex");
            client.Config.kidozAccountApproved=true;client.Config.kidozCloseWithinFiveSecondsConfirmed=true;client.Config.kidozPublisherId="qa";client.Config.kidozToken="qa";
            client.QaFreshAudience();client.ChooseAudience();Age(17);Check(client.Audience==PlayAudience.Family&&client.AdvertisingEnabled,"family route enabled only after configuration");
            Check(calls[^1].method=="configureAds"&&!(bool)calls[^1].args[0],"family initializes child provider only");
            client.QaFreshAudience();client.ChooseAudience();Age(18);Check(client.Frozen,"adult consent shown separately");Click("Без персонализации");
            Check(client.Adult&&client.AdvertisingEnabled&&!(bool)calls[^1].args[1],"adult can refuse personalization and continue");
            game.Spin();game.AdvanceRoulette(3);game.Drop();game.SimulateMiningStep();float beforeAd=game.Clock.Now;Vector3 position=game.Pickaxe.transform.position;
            int rewards=0,closes=0;game.Platform.Show("qa",true,()=>rewards++,shown=>closes++);var ad=calls[^1];string adId=(string)ad.args[0];
            Check(game.Platform.Busy&&game.PlatformFrozen&&Time.timeScale==0,"ad freezes play");game.SimulateMiningStep();
            Check(game.Clock.Now==beforeAd&&game.Pickaxe.transform.position==position,"an ad freezes real mining time and pickaxe physics");
            Ad("reward",adId);Ad("reward",adId);Ad("close",adId);Ad("reward",adId);Ad("close",adId);
            Check(rewards==1&&closes==1&&!game.Platform.Busy,"duplicated and stale callbacks cannot double reward");
            game.Platform.Show("qa-lost-callback",true,()=>rewards++,shown=>closes++);
            adId=(string)calls[^1].args[0];game.Platform.QaExpireAd();yield return null;
            Check(game.Platform.ServiceStalled && game.PlatformFrozen && root.Q("service-recovery-exit")!=null,"missing callback offers saved exit without resuming under an ad");
            Check(rewards==1,"watchdog never invents an advertising reward");
            Ad("close",adId);yield return null;
            Check(!game.Platform.ServiceStalled && root.Q("service-recovery").style.display==DisplayStyle.None,"late close clears recovery overlay");
            game.SimulateMiningStep();Check(game.Clock.Now>beforeAd,"mining resumes after the ad closes");game.NewRun();
            game.Platform.Ads.StartRun();game.Platform.Ads.Finish(101);game.SaveCheckpoint();long coins=game.Economy.Profile.coins;
            Check(game.RewardAvailable,"real game offers run bonus");game.ClaimRunReward();adId=(string)calls[^1].args[0];Ad("reward",adId);Ad("reward",adId);Ad("close",adId);
            Check(game.Economy.Profile.coins==coins+50&&client.Store.Ads.rewarded,"actual bonus atomically paid exactly once");
            game.ClaimRunReward();Check(game.Economy.Profile.coins==coins+50,"claim cannot be repeated after completion");
            var cold=new PlaySaveStore(game.ProfileDirectory);Check(cold.Load().coins==coins+50&&cold.Ads.rewarded,"reward survives cold load");
            client.Config.gamesApplicationId="1";client.SignIn();Check(calls[^1].method=="signIn","sign-in is explicit");Identity("player-a");Cloud("loaded");
            Check(client.Store.Checkpoint.owner=="player-a"&&game.Economy.Profile.coins==coins+50,"guest adoption preserves actual economy");
            Check(calls[^1].method=="writeCloud","cloud writes after local adoption");Cloud("saved");
            var remote=client.Store.Checkpoint.Core();remote.revision="other-device";var profile=ProfileStore.Parse(remote.profile);profile.coins=999;remote.profile=ProfileStore.Serialize(profile);
            client.SignIn();Identity("player-a");game.Economy.Profile.coins+=7;Cloud("loaded",remote);Check(client.Frozen&&game.PlatformFrozen,"conflict requires explicit choice");
            Check(game.Economy.Profile.coins==coins+57,"remote cannot silently replace coins earned during a cloud request");
            client.QaLongDecision();
            Button localButton=null;client.QaDialog.Query<Button>().ForEach(b=>{if(b.text.StartsWith(L.T("На устройстве"),StringComparison.Ordinal))localButton=b;});Submit(localButton);
            client.Tick();Check(calls[^1].method=="writeCloud","a long decision gets a fresh network deadline");
            Check(game.Economy.Profile.coins==coins+57&&client.Store.Load().coins==coins+57,"local choice keeps unsaved progress earned while the cloud request was pending");Cloud("saved");
            remote.revision="third-device";client.SignIn();Identity("player-a");Cloud("loaded",remote);
            Button cloudButton=null;client.QaDialog.Query<Button>().ForEach(b=>{if(b.text.StartsWith(L.T("В облаке"),StringComparison.Ordinal))cloudButton=b;});Submit(cloudButton);
            Check(!client.Frozen&&game.Economy.Profile.coins==999&&client.Store.Load().coins==999,"cloud choice persists before live economy changes");Cloud("saved");
            remote.revision="cancelled-device";client.SignIn();Identity("player-a");Cloud("loaded",remote);
            string storage=System.IO.Path.Combine(game.ProfileDirectory,"google",PlayReleaseConfig.SaveSpace(PlayReleaseConfig.SaveEpoch)),backup=storage+"-qa-backup";
            System.IO.Directory.Move(storage,backup);System.IO.File.WriteAllText(storage,"QA injected IO failure");
            try
            {
                localButton=null;client.QaDialog.Query<Button>().ForEach(b=>{if(b.text.StartsWith(L.T("На устройстве"),StringComparison.Ordinal))localButton=b;});Submit(localButton);
                Check(client.Frozen&&game.Economy.Profile.coins==999,"failed conflict backup cannot replace progress");
                Click("ПОКА БЕЗ ОБЛАКА");Check(!client.Frozen&&!client.SignedIn,"offline exit is available when disk writes fail");
            }
            finally{System.IO.File.Delete(storage);System.IO.Directory.Move(backup,storage);}
            string stale=client.QaRequest;client.SignIn();Identity("player-b");Cloud("saved",request:stale);Check(client.Store.Checkpoint.owner=="player-a","stale callback ignored after account change");
            Cloud("loaded",owner:"player-b");Check(client.Store.Checkpoint.owner=="player-b"&&game.Economy.Profile.coins==0,"new account receives fresh profile, no currency merge");Cloud("saved",owner:"player-b");
            client.DeleteCloud();Check(client.Frozen,"delete requires confirmation");Click("ОТМЕНА");Check(client.SignedIn,"cancel does not delete");
            client.DeleteCloud();Click("УДАЛИТЬ");Cloud("deleted",owner:"player-a");Check(client.SignedIn,"wrong-owner deletion ignored");Cloud("deleted",owner:"player-b");
            Check(!client.SignedIn&&client.Store.Load().coins==0,"cloud deletion keeps device save and disconnects");
            game.ChangeLanguage("en");yield return null;
            root.Query<TextElement>().ForEach(t=>Check(t.text=="Русский"||!System.Text.RegularExpressions.Regex.IsMatch(t.text??"","[А-Яа-яЁё]"),"English Play settings: "+t.text));
            client.ChooseAudience();yield return null;client.QaDialog.Query<TextElement>().ForEach(t=>Check(!System.Text.RegularExpressions.Regex.IsMatch(t.text??"","[А-Яа-яЁё]"),"English age screen"));Age(16);
            Check(!client.AdvertisingEnabled&&calls[^1].method=="blockAds","changing age disables all ads until process restart");Click("ПРОДОЛЖИТЬ");
            Debug.Log("PICKAXEFALL_GOOGLE_RUNTIME_OK: "+checks+" checks; real UI, age, ad rewards, cloud conflicts, account isolation, deletion and English.");Application.Quit(0);
        }
    }
}
#endif
