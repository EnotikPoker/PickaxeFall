using System;
using System.Collections.Generic;
using UnityEngine;
namespace PickaxeFall
{
    public enum PlayAudience { Unknown, Family, Adult }
    public interface IGameCloud { bool SignedIn {get;} string Status {get;} void SignIn(); void DeleteCloud(); }
    public interface IGameAchievements { void Synchronize(PlayerProfile profile); void Show(); }
    public interface IGamePurchases { bool RemoveAds {get;} string Price {get;} void Buy(); void Restore(); }
    public interface IRewardProfileStore:IProfilePersistence { void CommitReward(PlayerProfile profile,WebAdState ads); }

    [Serializable] public sealed class PlayReleaseConfig
    {
        public bool testMode=true, kidozAccountApproved, kidozCloseWithinFiveSecondsConfirmed;
        public string developerName="",supportEmail="",privacyUrl="",deletionUrl="",appAdsUrl="";
        public string gamesApplicationId="",googleBillingPublicKey="";
        public string yandexInterstitial="",yandexRewarded="",kidozPublisherId="",kidozToken="";
        public string[] achievementIds=new string[12];
        public const string Product="remove_ads";
        // 0.8.x is a test season. Change only together with the first public release.
        public const int SaveEpoch=0;
        public static string SaveSpace(int epoch)=>epoch==0?"test-v1":"release-v"+epoch;
        public static PlayAudience ClassifyAge(int age)=>age<0?PlayAudience.Unknown:age<18?PlayAudience.Family:PlayAudience.Adult;
        public string[] PublishingErrors()
        {
            var errors=new List<string>();if(testMode)errors.Add("testMode must be false");
            if(string.IsNullOrWhiteSpace(developerName)||string.IsNullOrWhiteSpace(supportEmail)||!supportEmail.Contains("@"))errors.Add("Developer name and support email are required");
            foreach(var pair in new[]{("privacyUrl",privacyUrl),("deletionUrl",deletionUrl),("appAdsUrl",appAdsUrl)})
                if(!Uri.TryCreate(pair.Item2,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.Host.EndsWith("example.com"))errors.Add(pair.Item1+" must be a real HTTPS URL");
            if(string.IsNullOrEmpty(gamesApplicationId)||!long.TryParse(gamesApplicationId,out _))errors.Add("Play Games application ID is required");
            try{var key=Convert.FromBase64String(googleBillingPublicKey);if(key.Length<256||key[0]!=0x30)throw new FormatException();}
            catch(Exception){errors.Add("Valid Google Play licensing public key is required");}
            foreach(string id in new[]{yandexInterstitial,yandexRewarded})if(string.IsNullOrWhiteSpace(id)||id.StartsWith("demo",StringComparison.OrdinalIgnoreCase))errors.Add("Real Yandex ad unit IDs are required");
            if(string.IsNullOrWhiteSpace(kidozPublisherId)||string.IsNullOrWhiteSpace(kidozToken)||!kidozAccountApproved)errors.Add("Approved Kidoz account and payout route are required");
            if(!kidozCloseWithinFiveSecondsConfirmed)errors.Add("Family formats must be confirmed closeable within five seconds");
            var unique=new HashSet<string>();if(achievementIds==null||achievementIds.Length!=12)errors.Add("12 achievement IDs are required");
            else foreach(string id in achievementIds)if(string.IsNullOrEmpty(id)||!unique.Add(id))errors.Add("Achievement IDs must be nonempty and unique");
            return errors.ToArray();
        }
    }
    public static class PlayAchievements
    {
        public static readonly string[] Titles={"Первый блок","Сотня блоков","Тысяча блоков","Десять тысяч блоков","Глубина 25 м","Глубина 100 м","Глубина 250 м","Дорога в пустыню","Врата в ад","Третья ступень","Пятая ступень","Первый изумруд"};
        public static bool[] Earned(PlayerProfile profile)
        {
            long blocks=0;foreach(long count in profile.lifetimeMined)blocks=blocks>long.MaxValue-count?long.MaxValue:blocks+count;
            return new[]{blocks>=1,blocks>=100,blocks>=1000,blocks>=10000,profile.bestDepth>=25,profile.bestDepth>=100,profile.bestDepth>=250,
                profile.bridgeStone>=1000&&profile.bridgeIron>=50,profile.portalObsidian>=10,profile.profileLevel>=3,profile.profileLevel>=5,profile.lifetimeMined[(int)BlockKind.Emerald]>0};
        }
    }
}
