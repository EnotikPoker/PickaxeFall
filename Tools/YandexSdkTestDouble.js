/* Local tests only. This file is deliberately outside Assets and the game ZIP. */
(function(){
 const handlers={},log=window.testPlatformEvents=[],options=window.testPlatformOptions={authorized:new URLSearchParams(location.search).get("auth")==="1"||localStorage.getItem("pf-test-auth")==="1",offline:false,ad:'success'};
 const stored=()=>JSON.parse(localStorage.getItem('pf-test-cloud')||'{}');
 const player={isAuthorized:()=>options.authorized,getUniqueID:()=>'local-test-account',getData:async()=>{if(options.offline)throw Error('test offline');return stored();},setData:async data=>{if(options.offline)throw Error('test offline');localStorage.setItem('pf-test-cloud',JSON.stringify(data));log.push(['save']);}};
 const ad=(callbacks,rewarded)=>{
  log.push(['request',rewarded?'rewarded':'interstitial']);
  if(options.ad==='unavailable'){callbacks.onError?.({message:'test unavailable'});return;}
  callbacks.onOpen?.();handlers.game_api_pause?.();
  window.testCloseAd=()=>{if(rewarded){callbacks.onRewarded?.();callbacks.onRewarded?.();}callbacks.onClose?.(true);handlers.game_api_resume?.();callbacks.onClose?.(true);window.testCloseAd=null;};
 };
 window.YaGames={init:async()=>({environment:{i18n:{lang:new URLSearchParams(location.search).get('lang')||'en'}},serverTime:()=>Date.now(),on:(event,cb)=>handlers[event]=cb,off:event=>delete handlers[event],getPlayer:async()=>player,auth:{openAuthDialog:async()=>{options.authorized=true;localStorage.setItem("pf-test-auth","1");}},features:{LoadingAPI:{ready:()=>log.push(['ready'])},GameplayAPI:{start:()=>log.push(['start']),stop:()=>log.push(['stop'])}},adv:{showFullscreenAdv:({callbacks})=>ad(callbacks,false),showRewardedVideo:({callbacks})=>ad(callbacks,true)}})};
 window.testPlatformHandlers=handlers;
})();
