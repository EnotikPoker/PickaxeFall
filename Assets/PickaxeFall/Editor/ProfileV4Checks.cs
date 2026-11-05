using System;
using System.IO;
using UnityEngine;

namespace PickaxeFall.Editor
{
    public static class ProfileV4Checks
    {
        [Serializable] sealed class OldProfile
        {
            public int version=3,bestDepth=321;
            public long coins=900719925474099L;
            public long[] resources={11,22,33,44,55,66,3000000000L,88};
            public int[] upgrades=new int[25];
        }
        public static void Validate()
        {
            string dir=Path.Combine(Application.temporaryCachePath,"v4-migration-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
            try
            {
                string path=Path.Combine(dir,"profile-v2.json");var old=new OldProfile();old.upgrades[0]=7;old.upgrades[24]=4;
                string json=JsonUtility.ToJson(old);File.WriteAllText(path,json);
                var store=new ProfileStore(dir);var loaded=store.Load();
                if(loaded.version!=5 || loaded.coins!=old.coins || loaded.bestDepth!=old.bestDepth || loaded.upgrades[0]!=7 || loaded.upgrades[24]!=4)throw new Exception("v4 migration lost progression");
                for(int i=0;i<8;i++)if(loaded.resources[i]!=old.resources[i])throw new Exception("v4 migration lost old resource "+i);
                for(int i=8;i<loaded.resources.Length;i++)if(loaded.resources[i]!=0)throw new Exception("v4 new resource slots not empty");
                if(File.ReadAllText(path+".pre-v4.bak")!=json || store.Load().coins!=old.coins)throw new Exception("v4 backup/idempotence");
                // An unwritable migration backup must preserve the primary and refuse
                // an older valid backup, instead of rolling progression backwards.
                File.WriteAllText(path,json);File.Delete(path+".pre-v4.bak");Directory.CreateDirectory(path+".pre-v4.bak");
                File.WriteAllText(path+".bak",JsonUtility.ToJson(new PlayerProfile { coins=1 }));
                bool failed=false;try{store.Load();}catch(IOException){failed=true;}catch(UnauthorizedAccessException){failed=true;}
                if(!failed || File.ReadAllText(path)!=json)throw new Exception("v4 failed migration changed original save");
                Debug.Log("PICKAXEFALL_PROFILE_V4_OK: exact v3 resources/coins/upgrades, backup, idempotence, migration failure preserves primary.");
            }
            finally{Directory.Delete(dir,true);}
        }
    }
}
