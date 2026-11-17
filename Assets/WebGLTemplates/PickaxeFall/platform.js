/* PickaxeFall's platform adapter. Production SDK comes only from /sdk.js. */
(function (global) {
  'use strict';
  const prefix='pickaxefall.web.v1.';
  const state={sdk:null,player:null,target:'',send:null,ad:null,cloudBusy:false,ready:false,sdkPaused:false};
  const emit=(method,value)=>{if(state.send)state.send(state.target,method,JSON.stringify(value));};
  const key=user=>prefix+encodeURIComponent(user);
  const parse=text=>text?JSON.parse(text):null;
  const pause=(source,paused)=>emit('OnPlatformPause',{source,paused});
  const attachSdk=sdk=>{if(state.sdk)return;state.sdk=sdk;sdk.on('game_api_pause',()=>{state.sdkPaused=true;pause('sdk',true);});sdk.on('game_api_resume',()=>{state.sdkPaused=false;pause('sdk',false);});};
  global.pickaxeSdk.then(attachSdk).catch(()=>{});
  let hidden=document.hidden||!document.hasFocus();
  const visibility=()=>{hidden=document.hidden||!document.hasFocus();pause('page',hidden);};
  document.addEventListener('visibilitychange',visibility);
  global.addEventListener('blur',visibility);global.addEventListener('focus',visibility);
  global.addEventListener('pagehide',()=>pause('page',true));
  const read=user=>{
    try {
      const value=localStorage.getItem(key(user));
      if(value){try{JSON.parse(value);return value;}catch(_){const backup=localStorage.getItem(key(user)+'.bak');if(backup){JSON.parse(backup);return backup;}return '!error';}}
      const backup=localStorage.getItem(key(user)+'.bak');if(backup)JSON.parse(backup);return backup||'';
    } catch(_){return '!error';}
  };
  const write=(user,text,reset)=>{
    try {
      JSON.parse(text);const k=key(user),previous=localStorage.getItem(k);
      localStorage.setItem(k,text); // Commit point. No fallible operation after it can cancel the transaction.
      try{localStorage.setItem(k+'.bak',reset?text:(previous||text));}catch(_){}
      return 1;
    }catch(_){return 0;}
  };
  async function init(target,send) {
    state.target=target;state.send=send;
    const result={available:false,authorized:false,cloudRead:false,midRunAds:!!global.PICKAXEFALL_CONFIG?.midRunAds,language:'en',user:'guest',cloud:'',serverTime:0};
    try {
      const sdk=await global.pickaxeSdk;attachSdk(sdk);
      result.available=true;result.language=sdk.environment.i18n.lang;result.serverTime=sdk.serverTime();
      try {
        state.player=await sdk.getPlayer();result.authorized=state.player.isAuthorized();
        if(result.authorized){result.user=state.player.getUniqueID();try{const data=await state.player.getData(['pfCore']);result.cloud=data.pfCore?JSON.stringify(data.pfCore):'';result.cloudRead=true;}catch(_){}}
        else result.cloudRead=true;
      } catch(_){}
    }catch(_){}
    emit('OnPlatformBoot',result);pause('sdk',state.sdkPaused);visibility();
  }
  function ad(id,rewarded) {
    if(state.ad||!state.sdk){emit('OnPlatformAd',{id,kind:'close',shown:false});return;}
    const request=state.ad={id,rewarded,open:false,rewardedOnce:false,finished:false};
    const current=()=>state.ad===request&&!request.finished;
    const close=shown=>{if(!current())return;request.finished=true;clearTimeout(request.timer);state.ad=null;emit('OnPlatformAd',{id,kind:'close',shown:!!shown});};
    const callbacks={
      onOpen(){if(!current())return;request.open=true;clearTimeout(request.timer);},
      onRewarded(){if(current()&&rewarded&&!request.rewardedOnce){request.rewardedOnce=true;emit('OnPlatformAd',{id,kind:'reward',shown:true});}},
      onClose(shown){close(request.open||shown);},onError(){close(false);}
    };
    // Only SDK close/error callbacks release a request; timeouts must not let
    // a late advertisement open over a resumed game.
    try {
      if(rewarded)state.sdk.adv.showRewardedVideo({callbacks});
      else state.sdk.adv.showFullscreenAdv({callbacks});
    }catch(_){close(false);}
  }
  async function cloud(text,expected) {
    if(state.cloudBusy||!state.player?.isAuthorized()){emit('OnPlatformCloud',{kind:'error'});return;}
    state.cloudBusy=true;
    try {
      const value=JSON.parse(text);if(new TextEncoder().encode(JSON.stringify({pfCore:value})).length>190000)throw Error('cloud quota');
      const data=await state.player.getData(['pfCore']),remote=data.pfCore;
      if(remote&&remote.revision!==expected&&remote.revision!==value.revision){emit('OnPlatformCloud',{kind:'conflict',cloud:JSON.stringify(remote)});return;}
      await state.player.setData({pfCore:value},true);emit('OnPlatformCloud',{kind:'saved'});
    }catch(_){emit('OnPlatformCloud',{kind:'error'});}finally{state.cloudBusy=false;}
  }
  global.PickaxePlatform={init,read,write,cloud,ad,
    async readCloud(){try{const data=await state.player.getData(['pfCore']);emit('OnPlatformCloud',{kind:'loaded',cloud:data.pfCore?JSON.stringify(data.pfCore):''});}catch(_){emit('OnPlatformCloud',{kind:'error'});}},
    backup(local,remote){try{localStorage.setItem(prefix+'conflict.local',local);localStorage.setItem(prefix+'conflict.cloud',remote);return 1;}catch(_){return 0;}},
    ready(){if(!state.ready&&state.sdk){state.sdk.features.LoadingAPI.ready();state.ready=true;}},
    gameplay(running){const api=state.sdk?.features.GameplayAPI;if(api)(running?api.start():api.stop());},
    async login(){try{if(!state.sdk)return;await state.sdk.auth.openAuthDialog();global.location.reload();}catch(_){}},
    reload(){global.location.reload();}
  };
})(window);
