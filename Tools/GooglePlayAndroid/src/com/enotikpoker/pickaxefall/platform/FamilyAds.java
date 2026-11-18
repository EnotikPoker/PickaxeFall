package com.enotikpoker.pickaxefall.platform;
import android.app.Activity;
import org.json.JSONObject;
import java.util.IdentityHashMap;
import net.kidoz.sdk.*;
import net.kidoz.ads.fullscreen.interstial.*;
import net.kidoz.ads.fullscreen.rewarded.*;

final class FamilyAds {
    static Activity activity;static boolean initialized,initializing,loadingInter,loadingReward;static KidozInterstitialAd inter;static KidozRewardedAd rewarded;
    static final class Request {final String id;boolean shown;Request(String id){this.id=id;}}
    static final IdentityHashMap<Object,Request> requests=new IdentityHashMap<>();
    static void shown(Object ad){Request r=requests.get(ad);if(r!=null)r.shown=true;}
    static void close(Object ad){Request r=requests.remove(ad);if(r!=null)PlayBridge.ad(r.id,"close",r.shown);load();}
    static void configure(Activity a,JSONObject config){
        if(initialized||initializing)return;activity=a;
        String publisher=config.optString("kidozPublisherId"),token=config.optString("kidozToken");
        // Never use an arbitrary publisher account, even in a test player.
        if(publisher.isEmpty()||token.isEmpty()||!config.optBoolean("kidozCloseWithinFiveSecondsConfirmed"))return;
        initializing=true;Kidoz.initialize(a,publisher,token,new KidozInitializationListener(){
            public void onInitSuccess(){initializing=false;initialized=true;load();}public void onInitError(KidozError e){initializing=false;}
        });
    }
    static void load(){if(!initialized||PlayBridge.adsBlocked)return;
        if(inter==null&&!loadingInter){loadingInter=true;KidozInterstitialAd.load(activity,new KidozInterstitialAdCallback(){
            public void onAdLoaded(KidozInterstitialAd ad){loadingInter=false;inter=ad;}public void onAdFailedToLoad(KidozError e){loadingInter=false;}
            public void onAdShown(KidozInterstitialAd ad){shown(ad);}public void onAdImpression(KidozInterstitialAd ad){}
            public void onAdFailedToShow(KidozInterstitialAd ad,KidozError e){close(ad);}
            public void onAdClosed(KidozInterstitialAd ad){close(ad);}
        });}
        if(rewarded==null&&!loadingReward){loadingReward=true;KidozRewardedAd.load(activity,new KidozRewardedAdCallback(){
            public void onAdLoaded(KidozRewardedAd ad){loadingReward=false;rewarded=ad;}public void onAdFailedToLoad(KidozError e){loadingReward=false;}
            public void onAdShown(KidozRewardedAd ad){shown(ad);}public void onAdImpression(KidozRewardedAd ad){}
            public void onAdFailedToShow(KidozRewardedAd ad,KidozError e){close(ad);}
            public void onRewardReceived(KidozRewardedAd ad){Request r=requests.get(ad);if(r!=null)PlayBridge.ad(r.id,"reward",true);}
            public void onAdClosed(KidozRewardedAd ad){close(ad);}
        });}
    }
    static void show(String id,boolean reward){
        if(reward&&rewarded!=null){KidozRewardedAd ad=rewarded;rewarded=null;requests.put(ad,new Request(id));ad.show();}
        else if(!reward&&inter!=null){KidozInterstitialAd ad=inter;inter=null;requests.put(ad,new Request(id));ad.show();}
        else{PlayBridge.ad(id,"close",false);load();}
    }
}
