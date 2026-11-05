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
                    // Preserve both historical v2->v3 backup semantics and the exact
                    // original file before expanding resources to v4. Failures here
                    // abort migration, never fall back to an older poorer backup.
                    if(JsonUtility.FromJson<PlayerProfile>(json).version==2)
                    {
                        string v3=path+".pre-v3.bak";
                        if(!File.Exists(v3))File.WriteAllText(v3,json);
                    }
                    string snapshot = path + ".pre-v4.bak";
                    if (JsonUtility.FromJson<PlayerProfile>(json).version<4 && !File.Exists(snapshot)) File.WriteAllText(snapshot, json);
                    string v5=path+".pre-v5.bak";if(!File.Exists(v5))File.WriteAllText(v5,json);
                    Save(result);
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
            var profile = new PlayerProfile { coins = Math.Max(0, PlayerPrefs.GetInt("wallet", 0)), bestDepth = Math.Max(0, PlayerPrefs.GetInt("bestDepth", 0)) };
            foreach (var kind in ResourceInventory.Materials) profile.resources[(int)kind] = Math.Max(0, PlayerPrefs.GetInt("inventory.v1." + kind, 0));
            return profile;
        }
        static PlayerProfile ReadProfile(string json) => ReadProfile(json, out _);
        static PlayerProfile ReadProfile(string json, out bool migrated)
        {
            migrated = false;
            // Missing JSON fields must not inherit the defaults of a new, empty player.
            var profile = new PlayerProfile { version = 0, coins = -1, bestDepth = -1, resources = null, upgrades = null };
            JsonUtility.FromJsonOverwrite(json, profile);
            if (profile.version == 2)
            {
                if (profile.upgrades == null || profile.upgrades.Length != 18) throw new InvalidOperationException("Invalid v2 upgrades.");
                var expanded = new int[Progression.Keys.Length]; Array.Copy(profile.upgrades, expanded, 18);
                profile.upgrades = expanded; profile.version = 3; migrated = true;
            }
            if(profile.version==3)
            {
                if(profile.resources==null || profile.resources.Length!=8 || profile.upgrades==null || profile.upgrades.Length!=25)
                    throw new InvalidOperationException("Invalid v3 profile.");
                var expanded=new long[14];
                Array.Copy(profile.resources,expanded,8);
                profile.resources=expanded;profile.version=4;migrated=true;
                profile.run=null;profile.activeRunJson=null;profile.dashUnlocked=profile.plungeUnlocked=false;
            }
            if(profile.version==4)
            {
                if(profile.resources==null || profile.resources.Length!=14)throw new InvalidOperationException("Invalid v4 profile.");
                Array.Resize(ref profile.resources,ResourceInventory.SlotCount);profile.version=5;migrated=true;
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
