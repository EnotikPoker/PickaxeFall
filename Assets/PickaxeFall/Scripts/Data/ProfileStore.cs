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
                try { return ReadProfile(File.ReadAllText(candidate)); }
                catch (Exception e) { Debug.LogWarning("Profile recovery: " + e.Message); }
            }
            // Never silently replace an existing, unreadable save with an empty profile.
            if (File.Exists(path) || File.Exists(path + ".bak")) throw new IOException("Не удалось прочитать сохранение. Резервные файлы сохранены.");
            var migrated = importLegacy();
            Save(migrated);
            return migrated;
        }
        static PlayerProfile ReadLegacy()
        {
            var profile = new PlayerProfile { coins = Math.Max(0, PlayerPrefs.GetInt("wallet", 0)), bestDepth = Math.Max(0, PlayerPrefs.GetInt("bestDepth", 0)) };
            foreach (var kind in ResourceInventory.Materials) profile.resources[(int)kind] = Math.Max(0, PlayerPrefs.GetInt("inventory.v1." + kind, 0));
            return profile;
        }
        static PlayerProfile ReadProfile(string json)
        {
            // Missing JSON fields must not inherit the defaults of a new, empty player.
            var profile = new PlayerProfile { version = 0, coins = -1, bestDepth = -1, resources = null, upgrades = null };
            JsonUtility.FromJsonOverwrite(json, profile); profile.Validate(); return profile;
        }
        public void Save(PlayerProfile profile)
        {
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
