package com.enotikpoker.pickaxefall.platform;

import android.app.Activity;
import com.unity3d.player.UnityPlayer;
import org.json.JSONObject;
import java.nio.charset.StandardCharsets;
import com.google.android.gms.games.PlayGames;
import com.google.android.gms.games.PlayGamesSdk;
import com.google.android.gms.games.SnapshotsClient;
import com.google.android.gms.games.snapshot.Snapshot;
import com.google.android.gms.games.snapshot.SnapshotMetadataChange;

/** Build-injected bridge. Every asynchronous result retains its original request and owner. */
public final class PlayBridge {
    static Activity activity; static String target, owner=""; static boolean gamesStarted,adsBlocked;
    static int adProvider; // Process lifetime: an initialized SDK cannot be safely uninitialized.
    static Snapshot snapshot; static String conflictId="", cloudRequest=""; static SnapshotsClient.SnapshotConflict conflict;
    static JSONObject config;
    public static void bind(String objectName,String configuration) {
        activity=UnityPlayer.currentActivity;target=objectName;
        try {config=new JSONObject(configuration);}catch(Exception e){config=new JSONObject();}
    }
    static void event(String method,JSONObject value){UnityPlayer.UnitySendMessage(target,method,value.toString());}
    static JSONObject json(String kind){JSONObject j=new JSONObject();put(j,"kind",kind);return j;}
    static void put(JSONObject j,String k,Object v){try{j.put(k,v);}catch(Exception ignored){}}
    static void run(Runnable action){activity.runOnUiThread(action);}
    static JSONObject cloud(String kind,String request,String account){JSONObject r=json(kind);put(r,"request",request);put(r,"owner",account);return r;}
    static boolean current(String request,String account){return request.equals(cloudRequest)&&account.equals(owner);}
    static void cloudError(String request,String account){
        if(!current(request,account))return;
        event("OnPlayCloud",cloud("error",request,account));discard();
    }
    static String text(Snapshot s)throws Exception{return new String(s.getSnapshotContents().readFully(),StandardCharsets.UTF_8);}
    static void release(Snapshot s){if(s!=null)PlayGames.getSnapshotsClient(activity).discardAndClose(s);}
    static void discard(){cloudRequest="";release(snapshot);snapshot=null;if(conflict!=null){release(conflict.getSnapshot());release(conflict.getConflictingSnapshot());conflict=null;}}
    public static void signIn(){run(()->{
        try {
            if(!gamesStarted){PlayGamesSdk.initialize(activity);gamesStarted=true;}
            PlayGames.getGamesSignInClient(activity).signIn().addOnSuccessListener(auth->{
                if(!auth.isAuthenticated()){event("OnPlayIdentity",json("unavailable"));return;}
                PlayGames.getPlayersClient(activity).getCurrentPlayer().addOnSuccessListener(player->{
                    discard();owner=player.getPlayerId();JSONObject result=json("signed-in");put(result,"owner",owner);event("OnPlayIdentity",result);
                }).addOnFailureListener(e->event("OnPlayIdentity",json("unavailable")));
            }).addOnFailureListener(e->event("OnPlayIdentity",json("unavailable")));
        }catch(Exception e){event("OnPlayIdentity",json("unavailable"));}
    });}
    public static void readCloud(String request,String slot){run(()->{
        discard();cloudRequest=request;String account=owner;
        if(account.isEmpty()){cloudError(request,account);return;}
        PlayGames.getPlayersClient(activity).getCurrentPlayer().addOnSuccessListener(player->{
            if(!current(request,account))return;
            if(!account.equals(player.getPlayerId())){cloudError(request,account);event("OnPlayIdentity",json("changed"));return;}
            PlayGames.getSnapshotsClient(activity).open(slot,true,SnapshotsClient.RESOLUTION_POLICY_MANUAL)
                .addOnSuccessListener(result->opened(request,account,result)).addOnFailureListener(e->cloudError(request,account));
        }).addOnFailureListener(e->cloudError(request,account));
    });}
    static void opened(String request,String account,SnapshotsClient.DataOrConflict<Snapshot> result){
        if(!current(request,account)){if(result.isConflict()){release(result.getConflict().getSnapshot());release(result.getConflict().getConflictingSnapshot());}else release(result.getData());return;}
        try {
            JSONObject out=cloud(result.isConflict()?"sdk-conflict":"loaded",request,account);
            if(result.isConflict()){
                conflict=result.getConflict();conflictId=conflict.getConflictId();put(out,"left",text(conflict.getSnapshot()));put(out,"right",text(conflict.getConflictingSnapshot()));
            }else{snapshot=result.getData();put(out,"text",text(snapshot));}
            event("OnPlayCloud",out);
        }catch(Exception e){cloudError(request,account);}
    }
    public static void resolveCloud(boolean first){run(()->{
        String request=cloudRequest,account=owner;
        if(conflict==null){cloudError(request,account);return;}
        Snapshot selected=first?conflict.getSnapshot():conflict.getConflictingSnapshot();conflict=null;
        PlayGames.getSnapshotsClient(activity).resolveConflict(conflictId,selected).addOnSuccessListener(r->opened(request,account,r)).addOnFailureListener(e->cloudError(request,account));
    });}
    public static void writeCloud(String request,String text){run(()->{
        String account=owner;if(snapshot==null||!request.equals(cloudRequest)){cloudError(request,account);return;}
        try{
            snapshot.getSnapshotContents().writeBytes(text.getBytes(StandardCharsets.UTF_8));Snapshot writing=snapshot;snapshot=null;
            PlayGames.getSnapshotsClient(activity).commitAndClose(writing,new SnapshotMetadataChange.Builder().setDescription("PickaxeFall progress").build())
                .addOnSuccessListener(metadata->{if(current(request,account))event("OnPlayCloud",cloud("saved",request,account));})
                .addOnFailureListener(e->cloudError(request,account));
        }catch(Exception e){cloudError(request,account);}
    });}
    public static void discardCloud(){run(()->discard());}
    public static void deleteCloud(String request,String slot){run(()->{
        discard();cloudRequest=request;String account=owner;
        PlayGames.getSnapshotsClient(activity).open(slot,false,SnapshotsClient.RESOLUTION_POLICY_MANUAL).addOnSuccessListener(result->{
            if(!current(request,account))return;
            Snapshot s=result.isConflict()?result.getConflict().getSnapshot():result.getData();
            PlayGames.getSnapshotsClient(activity).delete(s.getMetadata()).addOnSuccessListener(id->{if(current(request,account))event("OnPlayCloud",cloud("deleted",request,account));}).addOnFailureListener(e->cloudError(request,account));
        }).addOnFailureListener(e->cloudError(request,account));
    });}
    public static void achievement(String id){run(()->{
        String account=owner;PlayGames.getAchievementsClient(activity).unlockImmediate(id)
            .addOnSuccessListener(v->{JSONObject r=json("unlocked");put(r,"id",id);put(r,"owner",account);event("OnPlayAchievement",r);})
            .addOnFailureListener(e->{});
    });}
    public static void achievements(){run(()->PlayGames.getAchievementsClient(activity).getAchievementsIntent()
        .addOnSuccessListener(intent->activity.startActivityForResult(intent,4201)).addOnFailureListener(e->{}));}
    public static synchronized boolean configureAds(boolean adult,boolean consent){
        int requested=adult?1:2;if(adsBlocked||adProvider!=0&&adProvider!=requested)return false;
        adProvider=requested;run(()->{if(adult)YandexAds.configure(activity,config,consent);else FamilyAds.configure(activity,config);});return true;
    }
    public static synchronized void blockAds(){adsBlocked=true;}
    public static void showAd(String id,boolean reward,boolean adult){run(()->{
        if(adsBlocked||adProvider!=(adult?1:2)){ad(id,"close",false);return;}
        if(adult)YandexAds.show(id,reward);else FamilyAds.show(id,reward);
    });}
    static void ad(String id,String kind,boolean shown){JSONObject r=json(kind);put(r,"id",id);put(r,"shown",shown);event("OnPlatformAd",r);}
    private PlayBridge(){}
}
