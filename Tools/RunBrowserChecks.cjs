#!/usr/bin/env node
// Run against ServeYandexChecks.py --mock-sdk and a dedicated Chromium instance.
const Cdp=require('./BrowserCdp.cjs'),fs=require('node:fs'),assert=require('node:assert/strict');
const output=process.argv[2]||'Builds/Release083/browser';fs.mkdirSync(output,{recursive:true});let checks=0,c;
const delay=ms=>new Promise(r=>setTimeout(r,ms));
const check=(ok,label)=>{checks++;assert.ok(ok,label);console.log('PASS',label);};
async function state(command='state'){const s=await c.probe(command);assert.ok(!s.error,s.error);return s;}
async function tap(name){await c.tap(name);await delay(500);return state();}
async function until(test,label,seconds=10){const end=Date.now()+seconds*1000;while(Date.now()<end){const s=await state();if(test(s))return s;await delay(100);}throw Error('Timed out: '+label);}
(async()=>{
 c=await Cdp.connect();await c.call('Emulation.setDeviceMetricsOverride',{width:540,height:960,deviceScaleFactor:1,mobile:false});await c.call('Page.navigate',{url:'http://127.0.0.1:8093/?lang=en'});await c.wait('!!window.pickaxeInstance',180);await c.wait('window.testPlatformEvents?.some(x=>x[0]==="ready")',60);
 let s=await until(s=>s.phase==='Ready','startup',30);check(s.language==='en','SDK English selected');await state('prepare');
 check((await c.eval("testPlatformEvents.filter(x=>x[0]==='ready').length"))===1,'Game Ready once');
 await tap('open-settings');await c.screenshot(output+'/settings-en.png');s=await tap('language-ru');s=await until(s=>s.language==='ru','Russian switch');check(s.language==='ru','Russian switch through UI');
 s=await tap('language-en');s=await until(s=>s.language==='en','English switch');check(s.russian.length===0,'English settings have no untranslated labels');
 await c.eval("pickaxeInstance.SendMessage('PickaxeFall','WebProbe','prepare')");await delay(300); // close scroll modal without synthetic button commands
 await tap('tab-shop');s=await state();check(s.russian.length===0,'English shop translated');await c.screenshot(output+'/shop-en.png');
 await tap('tab-inventory');s=await state();check(s.russian.length===0,'English inventory translated');await c.screenshot(output+'/bag-en.png');
 await tap('tab-mine');await tap('main-action');s=await until(s=>s.phase==='Equipped','roulette');check(s.phase==='Equipped','real main button spins');await tap('main-action');s=await until(s=>s.phase==='Mining','drop');check(s.ticks>0,'physics advances in browser');
 await state('spawner');await tap('spawner-start');s=await state();check(s.hits===0,'spawner starts');
 // Stable target has known normalized centre in this layout; use screenshot/UI geometry
 // from the dedicated target command so tests do not rely on a hand-written coordinate.
 await c.eval("pickaxeInstance.SendMessage('PickaxeFall','WebProbe','trial-target')");await delay(200);
 await state('run');await state('long-ad');await c.wait('typeof window.testCloseAd === "function"',10);s=await state();const frozenTicks=s.ticks,after=s.afterSeconds;
 await delay(500);s=await state();check(s.frozen&&s.ticks===frozenTicks,'ad freezes mining');await c.eval('testCloseAd()');s=await until(s=>!s.frozen,'ad resume');check(s.nextAd>=480&&s.afterSeconds>=after,'180-second repeat and independent post-run counter');
 await state('finish');s=await until(s=>s.phase==='Finished','finish');await delay(700);await c.screenshot(output+'/results-en.png');check(s.rewardValue>0,'run bonus computed');const before=s.coins,value=s.rewardValue;
 await tap('rewarded-run-bonus');await c.wait('typeof window.testCloseAd === "function"',10);await c.eval('testCloseAd()');s=await until(s=>s.rewarded,'reward grant');check(s.coins===before+value,'bonus granted exactly once despite duplicate SDK callbacks');
 await tap('main-action');await c.wait('typeof window.testCloseAd === "function"',10);s=await state();check(s.adBusy,'post-run ad remains due after in-run ad');await c.eval('testCloseAd()');s=await until(s=>s.phase==='Ready','repeat');check(s.afterSeconds===0,'post-run timer resets on shown ad');
 await state('run');await state('pause');await state('save');s=await state();const coins=s.coins,ticks=s.ticks;
 await c.call('Page.reload');await c.wait('!!window.pickaxeInstance',180);await c.wait('window.testPlatformEvents?.some(x=>x[0]==="ready")',60);s=await until(s=>s.phase==='Mining','resume',30);check(s.paused&&s.ticks===ticks&&s.coins===coins,'page reload restores paused local run and profile');
 await c.call('Emulation.setDeviceMetricsOverride',{width:960,height:540,deviceScaleFactor:1,mobile:false});await delay(500);s=await state();check(s.phase==='Mining'&&s.coins===coins,'landscape resize preserves run');await c.screenshot(output+'/landscape.png');
 await c.call('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});await delay(500);await c.screenshot(output+'/mobile.png');
 fs.writeFileSync(output+'/result.json',JSON.stringify({checks,passed:true,platform:'Chromium; local SDK double; not a Yandex draft approval'},null,2));console.log('PICKAXEFALL_BROWSER_OK:',checks);
 c.close();
})().catch(async error=>{try{await c?.screenshot(output+'/failure.png');fs.writeFileSync(output+'/failure-state.json',JSON.stringify(await c?.probe(),null,2));}catch(_){}fs.writeFileSync(output+'/failure.txt',String(error.stack));console.error(error);c?.close();process.exit(1)});
