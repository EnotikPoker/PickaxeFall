using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
namespace PickaxeFall
{
    // Russian source strings are stable keys. Serialized balance names and saved
    // identifiers stay untouched; only presentation and returned messages translate.
    public static class L
    {
        [Serializable] public sealed class Entry {public string ru,en;}
        [Serializable] public sealed class Catalog {public Entry[] entries;}
        static Dictionary<string,string> translations,originals;
        public static string Language {get;private set;}="ru";
        public static CultureInfo Culture=>CultureInfo.GetCultureInfo(Language=="ru"?"ru-RU":"en-US");
        public static event Action Changed;
        public static void SetLanguage(string language){string next=language=="ru"?"ru":"en";if(next==Language)return;if(next=="en")Ensure();Language=next;Changed?.Invoke();}
        public static Catalog Load()=>JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Localization/en").text);
        static void Ensure()
        {
            if(translations!=null)return;var catalog=Load();translations=new(StringComparer.Ordinal);originals=new(StringComparer.Ordinal);
            foreach(var e in catalog.entries){translations[e.ru]=e.en;if(!originals.ContainsKey(e.en))originals[e.en]=e.ru;translations.TryAdd(e.ru.ToUpperInvariant(),e.en.ToUpperInvariant());}
        }
        public static string T(string text)
        {
            if(string.IsNullOrEmpty(text))return text;if(Language=="ru"&&translations==null)return text;Ensure();
            if(Language=="ru")return originals.TryGetValue(text,out string ru)?ru:text;
            return translations.TryGetValue(text,out string en)?en:text;
        }
        public static string F(string format,params object[] args)=>string.Format(Culture,T(format),args);
    }
}
