#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
namespace PickaxeFall
{
    public sealed class RuntimeLocalizationChecks:MonoBehaviour
    {
        int checks;void Check(bool ok,string label){checks++;if(!ok)throw new Exception("Localization: "+label);}
        void OnEnable()=>Application.logMessageReceived+=OnLog;
        void OnDisable()=>Application.logMessageReceived-=OnLog;
        void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception)Application.Quit(1);}
        IEnumerator Start()
        {
            yield return null;var game=GetComponent<GameController>();game.Settings.SetTutorialSeen(true);game.Settings.SetTutorialHints(int.MaxValue);
            game.Economy.TrySetGoal(GoalKind.Upgrade,new UpgradeKey(UpgradeKind.Damage,0).Index);
            string profile=ProfileStore.Serialize(game.Economy.Profile);var root=GetComponent<UIDocument>().rootVisualElement;
            game.ChangeLanguage("en");yield return null;
            Check(game.Settings.Language=="en"&&L.Language=="en","English selected");Check(root.Q("rewarded-run-bonus")==null&&!game.Platform.IsYandex,"native has no Yandex advertising");
            CheckEnglish(root);
            Submit(root.Q("settings-close"));foreach(HubTab tab in Enum.GetValues(typeof(HubTab))){game.SelectHubTab(tab);yield return null;CheckEnglish(root);}
            game.SelectHubTab(HubTab.Mine);Submit(root.Q("open-settings"));Submit(root.Q("settings-tutorial"));yield return null;
            for(int i=0;i<14;i++)
            {
                CheckEnglish(root);var next=root.Q<Button>("tutorial-next");if(next==null)break;Submit(next);yield return null;
            }
            game.ChangeLanguage("ru");yield return null;Check(game.Settings.Language=="ru"&&root.Q<Button>("settings-close").text=="ГОТОВО","Russian restored");
            Check(ProfileStore.Serialize(game.Economy.Profile)==profile,"language changes preserve full profile");
            Debug.Log("PICKAXEFALL_LOCALIZATION_OK: "+checks+" checks; both languages, menus, tutorial, unchanged profile and native distribution.");Application.Quit(0);
        }
        void CheckEnglish(VisualElement root)
        {
            root.Query<Button>().ForEach(button=>Check(!System.Text.RegularExpressions.Regex.IsMatch(button.tooltip??"","[А-Яа-яЁё]"),"English tooltip: "+button.tooltip));
            root.Query<TextElement>().ForEach(text=>{if(text.text!="Русский")Check(!System.Text.RegularExpressions.Regex.IsMatch(text.text??"","[А-Яа-яЁё]"),"English text: "+text.text);});
        }
        static void Submit(VisualElement target){if(target==null)throw new Exception("Missing localization test button");using var e=NavigationSubmitEvent.GetPooled();e.target=target;target.SendEvent(e);}
    }
}
#endif
