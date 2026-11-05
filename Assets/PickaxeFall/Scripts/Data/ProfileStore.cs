using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace PickaxeFall
{
    public sealed class ProfileStore
    {
        readonly string path;
        readonly Func<PlayerProfile> importLegacy;
        public ProfileStore(string directory, Func<PlayerProfile> legacyImporter = null)
        { path = Path.Combine(directory, "profile-v2.json"); importLegacy = legacyImporter ?? ReadLegacy; }
        public PlayerProfile Load()
        {
            foreach (string candidate in new[] { path, path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                PlayerProfile result; bool migrated; string json;
                try { json = File.ReadAllText(candidate); result = ReadProfile(json, out migrated); }
                catch (Exception e) { Debug.LogWarning("Profile recovery: " + e.Message); continue; }
                // A migration write failure must never fall back to an older, poorer profile.
                if (migrated)
                {
                    string snapshot=path+".pre-v6.bak";
                    if(!File.Exists(snapshot))File.WriteAllText(snapshot,json);
                    Save(result);
                    // Recovery after the intentional season reset must use the new profile.
                    try{File.Copy(path,path+".bak",true);}catch(Exception e){Debug.LogWarning("Reset backup: "+e.Message);}
                }
                return result;
            }
            // Never silently replace an existing, unreadable save with an empty profile.
            if (File.Exists(path) || File.Exists(path + ".bak")) throw new IOException("Не удалось прочитать сохранение. Резервные файлы сохранены.");
            var imported = importLegacy();
            Save(imported);
            return imported;
        }
        static PlayerProfile ReadLegacy()
        {
            return new PlayerProfile {resetNotice=PlayerPrefs.HasKey("wallet") || PlayerPrefs.HasKey("bestDepth")};
        }
        static PlayerProfile ReadProfile(string json) => ReadProfile(json, out _);
        static PlayerProfile ReadProfile(string json, out bool migrated)
        {
            migrated = false;
            // Missing JSON fields must not inherit the defaults of a new, empty player.
            var profile = new PlayerProfile { version = 0, coins = -1, bestDepth = -1, resources = null, upgrades = null, profileLevel=0,lifetimeMined=null,revealed=null };
            JsonUtility.FromJsonOverwrite(json, profile);
            if(profile.version>=2 && profile.version<=5)
            {
                int slots=profile.version==2||profile.version==3?8:profile.version==4?14:ResourceInventory.SlotCount;
                int upgrades=profile.version==2?18:25;
                if(profile.coins<0 || profile.bestDepth<0 || profile.resources?.Length!=slots || profile.upgrades?.Length!=upgrades)
                    throw new InvalidOperationException("Invalid legacy profile.");
                foreach(long amount in profile.resources)if(amount<0)throw new InvalidOperationException("Invalid legacy resources.");
                foreach(int level in profile.upgrades)if(level<0||level>40)throw new InvalidOperationException("Invalid legacy upgrades.");
                migrated=true;return new PlayerProfile {resetNotice=true};
            }
            if(!string.IsNullOrEmpty(profile.activeRunJson)){
                profile.run=JsonUtility.FromJson<RunSnapshot>(profile.activeRunJson);
                if(profile.run.version==1 && profile.run.loot?.Length==14)Array.Resize(ref profile.run.loot,ResourceInventory.SlotCount);
            }
            profile.Validate(); return profile;
        }
        public void Reset(PlayerProfile empty)
        {
            Save(empty);
            // The primary is committed; recovery must not resurrect the erased progression.
            try { File.Copy(path,path+".bak",true); }
            catch(Exception e){Debug.LogWarning("Reset recovery copy: "+e.Message);}
        }
        public void Save(PlayerProfile profile)
        {
            profile.activeRunJson=profile.run==null?null:JsonUtility.ToJson(profile.run);
            profile.Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(profile, true)); stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            bool validPrimary = false;
            if (File.Exists(path))
            {
                try { ReadProfile(File.ReadAllText(path)); validPrimary = true; }
                catch (Exception) { File.Move(path, path + ".corrupt-" + DateTime.UtcNow.Ticks); }
            }
            if (validPrimary) File.Replace(temp, path, path + ".bak");
            else
            {
                // Preserve an existing recovery copy, and perform no fallible writes after committing the primary.
                if (!File.Exists(path + ".bak")) File.Copy(temp, path + ".bak");
                File.Move(temp, path);
            }
        }
    }
}
