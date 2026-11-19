using System.Collections.Generic;
using UnityEngine;
namespace PickaxeFall
{
    // Verification never reads or changes the player's real preferences.
    sealed class PlayPreferences
    {
        readonly Dictionary<string,string> temporary;
        public PlayPreferences(bool isolated){if(isolated)temporary=new();}
        public int GetInt(string key,int fallback=0)=>temporary==null?PlayerPrefs.GetInt(key,fallback):temporary.TryGetValue(key,out var s)&&int.TryParse(s,out int n)?n:fallback;
        public string GetString(string key,string fallback)=>temporary==null?PlayerPrefs.GetString(key,fallback):temporary.TryGetValue(key,out var s)?s:fallback;
        public void SetInt(string key,int n){if(temporary==null)PlayerPrefs.SetInt(key,n);else temporary[key]=n.ToString();}
        public void SetString(string key,string value){if(temporary==null)PlayerPrefs.SetString(key,value);else temporary[key]=value;}
        public void Save(){if(temporary==null)PlayerPrefs.Save();}
    }
}
