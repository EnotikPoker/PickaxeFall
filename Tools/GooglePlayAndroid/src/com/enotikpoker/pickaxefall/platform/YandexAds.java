package com.enotikpoker.pickaxefall.platform;
import android.app.Activity;
import android.os.Handler;
import android.os.Looper;
import org.json.JSONObject;
import com.yandex.mobile.ads.common.*;
import com.yandex.mobile.ads.interstitial.*;
import com.yandex.mobile.ads.rewarded.*;

final class YandexAds {
    static final Handler retries = new Handler(Looper.getMainLooper());
    static int interFailures, rewardFailures;
    static void retry(boolean reward) {
        int failures = reward ? rewardFailures++ : interFailures++;
        long delay = Math.min(60000L, 5000L << Math.min(failures, 4));
        retries.postDelayed(() -> { if (!PlayBridge.adsBlocked && activity != null && !activity.isFinishing()) load(); }, delay);
    }
    static Activity activity;static boolean initialized,initializing,loadingInter,loadingReward;static String interId,rewardId;
    static InterstitialAd inter;static RewardedAd rewarded;static InterstitialAdLoader interLoader;static RewardedAdLoader rewardLoader;
    static void configure(Activity a,JSONObject config,boolean consent){
        if(initialized||initializing)return;activity=a;
        interId=config.optString("yandexInterstitial");rewardId=config.optString("yandexRewarded");
        if(config.optBoolean("testMode")){interId="demo-interstitial-yandex";rewardId="demo-rewarded-yandex";}
        if(interId.isEmpty()||rewardId.isEmpty())return;
        MobileAds.setLocationConsent(false);MobileAds.setUserConsent(consent);MobileAds.setAgeRestrictedUser(false);MobileAds.setAppAdAnalyticsReporting(false);
        initializing=true;MobileAds.initialize(a,()->{initializing=false;initialized=true;load();});
    }
    static void load(){if(!initialized||PlayBridge.adsBlocked)return;
        if(inter==null&&!loadingInter){loadingInter=true;interLoader=new InterstitialAdLoader(activity);interLoader.setAdLoadListener(new InterstitialAdLoadListener(){
            public void onAdLoaded(InterstitialAd ad){loadingInter=false;interFailures=0;inter=ad;}public void onAdFailedToLoad(AdRequestError e){loadingInter=false;retry(false);}
        });interLoader.loadAd(new AdRequestConfiguration.Builder(interId).build());}
        if(rewarded==null&&!loadingReward){loadingReward=true;rewardLoader=new RewardedAdLoader(activity);rewardLoader.setAdLoadListener(new RewardedAdLoadListener(){
            public void onAdLoaded(RewardedAd ad){loadingReward=false;rewardFailures=0;rewarded=ad;}public void onAdFailedToLoad(AdRequestError e){loadingReward=false;retry(true);}
        });rewardLoader.loadAd(new AdRequestConfiguration.Builder(rewardId).build());}
    }
    static void show(String id,boolean reward){
        if(reward){RewardedAd ad=rewarded;rewarded=null;if(ad==null){PlayBridge.ad(id,"close",false);load();return;}
            ad.setAdEventListener(new RewardedAdEventListener(){boolean shown;
                public void onAdShown(){shown=true;}public void onAdFailedToShow(AdError e){PlayBridge.ad(id,"close",false);ad.setAdEventListener(null);load();}
                public void onAdDismissed(){PlayBridge.ad(id,"close",shown);ad.setAdEventListener(null);load();}
                public void onRewarded(Reward value){PlayBridge.ad(id,"reward",true);}public void onAdClicked(){}public void onAdImpression(ImpressionData value){}
            });ad.show(activity);
        }else{InterstitialAd ad=inter;inter=null;if(ad==null){PlayBridge.ad(id,"close",false);load();return;}
            ad.setAdEventListener(new InterstitialAdEventListener(){boolean shown;
                public void onAdShown(){shown=true;}public void onAdFailedToShow(AdError e){PlayBridge.ad(id,"close",false);ad.setAdEventListener(null);load();}
                public void onAdDismissed(){PlayBridge.ad(id,"close",shown);ad.setAdEventListener(null);load();}
                public void onAdClicked(){}public void onAdImpression(ImpressionData value){}
            });ad.show(activity);
        }
    }
}
