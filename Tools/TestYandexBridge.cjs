#!/usr/bin/env node
// Runs the real adapter against isolated SDK/storage doubles; never contacts Yandex.
const fs=require('node:fs'),vm=require('node:vm'),assert=require('node:assert/strict');
const source=fs.readFileSync('Assets/WebGLTemplates/PickaxeFall/platform.js','utf8');let checks=0;
const check=(condition,message)=>{checks++;assert.ok(condition,message);};
function environment(){
 const data=new Map(),events=[],handlers={},ads=[],cloud={};let failWrite=false,cloudFail=false,setCalls=0;
 const storage={getItem:k=>data.get(k)||null,setItem(k,v){if(failWrite)throw Error('quota');data.set(k,v);}};
 const player={isAuthorized:()=>true,getUniqueID:()=>'test-user',getData:async()=>{if(cloudFail)throw Error('offline');return cloud;},setData:async value=>{if(cloudFail)throw Error('offline');setCalls++;Object.assign(cloud,value);}};
 const sdk={on:(event,cb)=>handlers[event]=cb,serverTime:()=>100000,environment:{i18n:{lang:'ru'}},getPlayer:async()=>player,features:{LoadingAPI:{ready:()=>events.push(['ready'])},GameplayAPI:{start:()=>events.push(['start']),stop:()=>events.push(['stop'])}},adv:{showFullscreenAdv:({callbacks})=>ads.push(callbacks),showRewardedVideo:({callbacks})=>ads.push(callbacks)}};
 const window={pickaxeSdk:Promise.resolve(sdk),PICKAXEFALL_CONFIG:{midRunAds:false},addEventListener:(event,cb)=>handlers[event]=cb,location:{reload:()=>events.push(['reload'])}};
 const document={hidden:false,hasFocus:()=>true,addEventListener:(event,cb)=>handlers[event]=cb};
 vm.runInNewContext(source,{window,document,localStorage:storage,TextEncoder,setTimeout,clearTimeout,console});
 return {api:window.PickaxePlatform,events,ads,handlers,cloud,data,sdk,setFail:v=>failWrite=v,setCloudFail:v=>cloudFail=v,get setCalls(){return setCalls;}};
}
(async()=>{
 const e=environment();await e.api.init('game',(object,method,json)=>e.events.push([method,JSON.parse(json)]));
 check(e.events.some(([k,v])=>k==='OnPlatformBoot'&&v.authorized&&v.cloudRead&&!v.midRunAds),'bootstrap SDK, user, production gate');
 e.api.ready();e.api.ready();check(e.events.filter(([k])=>k==='ready').length===1,'ready once');
 check(e.api.write('guest','{"coins":7}',false)===1,'local commit');e.setFail(true);check(e.api.write('guest','{"coins":9}',false)===0,'quota fails');check(e.api.read('guest')==='{"coins":7}','old primary preserved');e.setFail(false);
 e.api.write('guest','{"coins":0}',true);check(e.data.get('pickaxefall.web.v1.guest.bak')==='{"coins":0}','reset backup cannot resurrect money');
 e.data.set('pickaxefall.web.v1.guest','broken');check(e.api.read('guest')==='{"coins":0}','corrupt primary uses backup');
 e.api.ad('first',true);const callbacks=e.ads.at(-1);callbacks.onOpen();callbacks.onRewarded();callbacks.onRewarded();callbacks.onClose(true);callbacks.onRewarded();callbacks.onClose(true);
 check(e.events.filter(([k,v])=>k==='OnPlatformAd'&&v.kind==='reward').length===1,'reward once, ignores callbacks after close');check(e.events.filter(([k,v])=>k==='OnPlatformAd'&&v.id==='first'&&v.kind==='close').length===1,'close once');
 e.api.ad('second',false);e.api.ad('overlap',false);check(e.ads.length===2,'single SDK request at a time');check(e.events.some(([k,v])=>k==='OnPlatformAd'&&v.id==='overlap'&&!v.shown),'overlap closed');e.ads.at(-1).onError();
 const count=e.events.length;callbacks.onClose(true);check(e.events.length===count,'late old callback rejected');
 await e.api.cloud(JSON.stringify({revision:'a',profile:'{}'}),'');check(e.cloud.pfCore.revision==='a'&&e.setCalls===1,'cloud confirmed setData');
 await e.api.cloud(JSON.stringify({revision:'b',profile:'{}'}),'stale');check(e.setCalls===1&&e.events.at(-1)[1].kind==='conflict','remote conflict never overwritten');
 await e.api.cloud(JSON.stringify({revision:'b',profile:'{}'}),'a');check(e.setCalls===2,'chosen base can advance');
 e.setCloudFail(true);await e.api.cloud(JSON.stringify({revision:'c'}),'b');check(e.events.at(-1)[1].kind==='error','offline cloud reports failure');
 e.setCloudFail(false);await e.api.cloud(JSON.stringify({revision:'c',profile:'x'.repeat(200000)}),'b');check(e.setCalls===2,'cloud size guarded');
 e.handlers.game_api_pause();check(e.events.at(-1)[1].paused,'SDK pause');e.handlers.game_api_resume();check(!e.events.at(-1)[1].paused,'SDK resume');
 const f=environment();await f.api.init('game',(o,m,j)=>f.events.push([m,JSON.parse(j)]));
 check(f.api.write('u','broken',false)===0,'invalid local JSON rejected');
 f.api.write('u','{"ok":1}',false);f.api.write('u','{"ok":2}',false);check(f.data.get('pickaxefall.web.v1.u.bak')==='{"ok":1}','previous checkpoint backed up');
 f.api.ad('no-reward',true);f.ads.at(-1).onClose(false);check(!f.events.some(([k,v])=>k==='OnPlatformAd'&&v.kind==='reward'),'closing without rewarded callback gives no reward');
 f.api.ad('sdk-error',true);const errorCallbacks=f.ads.at(-1);errorCallbacks.onError();errorCallbacks.onRewarded();check(!f.events.some(([k,v])=>k==='OnPlatformAd'&&v.kind==='reward'),'late reward after error rejected');
 f.setCloudFail(true);await f.api.readCloud();check(f.events.at(-1)[1].kind==='error','failed cloud read reported');f.setCloudFail(false);await f.api.readCloud();check(f.events.at(-1)[1].kind==='loaded','cloud read recovers');
 f.sdk.getPlayer=async()=>({isAuthorized:()=>false});await f.api.init('guest',(o,m,j)=>f.events.push([m,JSON.parse(j)]));check(f.events.some(([k,v])=>k==='OnPlatformBoot'&&!v.authorized&&v.cloudRead),'guest needs no account');
 f.setFail(true);check(f.api.backup('{}','{}')===0,'backup quota failure reported without uncaught exception');
 console.log('PICKAXEFALL_JS_BRIDGE_OK:',checks,'checks');
})().catch(error=>{console.error(error);process.exit(1);});
