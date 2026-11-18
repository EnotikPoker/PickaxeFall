#!/usr/bin/env node
const Cdp=require('./BrowserCdp.cjs'),fs=require('node:fs'),assert=require('node:assert/strict');const out=process.argv[2];fs.mkdirSync(out,{recursive:true});let c,checks=0;const delay=ms=>new Promise(r=>setTimeout(r,ms));const check=(ok,text)=>{checks++;assert.ok(ok,text);console.log('PASS',text)};
async function load(url){await c.call('Page.navigate',{url});await c.wait('!!window.pickaxeInstance',180);await c.wait('window.testPlatformEvents?.some(x=>x[0]==="ready")',45);return c.probe();}
(async()=>{
 c=await Cdp.connect();await c.call('Emulation.setDeviceMetricsOverride',{width:540,height:960,deviceScaleFactor:1,mobile:false});
 await load('http://127.0.0.1:8093/?lang=en');await c.probe('prepare');await c.probe('save');
 // Seed a cloud record with different, valid progress. Sign-in deliberately presents a choice.
 await c.eval(`(()=>{const guest=JSON.parse(localStorage.getItem('pickaxefall.web.v1.guest'));const remote=JSON.parse(JSON.stringify(guest));remote.user='local-test-account';remote.revision='remote-conflict';remote.synced='remote-conflict';const profile=JSON.parse(remote.profile);profile.activeRunJson=null;profile.coins=77;remote.profile=JSON.stringify(profile);remote.ads.runId='';localStorage.setItem('pf-test-cloud',JSON.stringify({pfCore:remote}));})()`);
 await c.call('Page.navigate',{url:'http://127.0.0.1:8093/?lang=en&auth=1'});await c.wait('!!window.pickaxeInstance',180);let s;
 for(let i=0;i<100;i++){s=await c.probe();if(s.buttons.some(b=>b.name==='save-choice-local'))break;await delay(100);}
 check(s.phase==='Loading'&&s.buttons.some(b=>b.name==='save-choice-cloud'),'sign-in conflict blocks startup until a choice');
 await c.screenshot(out+'/conflict.png');await c.tap('save-choice-cloud');await c.wait('testPlatformEvents.some(x=>x[0]==="ready")',20);s=await c.probe();check(s.coins===77&&s.phase==='Ready','chosen cloud progress loads without local run');
 check(await c.eval("!!localStorage.getItem('pickaxefall.web.v1.conflict.local')&&!!localStorage.getItem('pickaxefall.web.v1.conflict.cloud')"),'both conflict copies preserved');
 await c.probe('prepare');await c.probe('save');await c.wait("testPlatformEvents.some(x=>x[0]==='save')",20);
 check(await c.eval("!JSON.parse(localStorage.getItem('pf-test-cloud')).pfCore.profile.includes('balanceJson')"),'cloud payload excludes large mine snapshot');
 // An offline server cannot roll back or clear the synchronously committed browser profile.
 await c.eval('testPlatformOptions.offline=true');await c.probe('run');await c.probe('finish');await delay(1200);s=await c.probe();await c.probe('save');check(s.coins===100000,'cloud outage does not lose local coins');
 await c.eval('testPlatformOptions.offline=false');await c.wait("testPlatformEvents.filter(x=>x[0]==='save').length>=2",45);check(true,'cloud retries after reconnect');
 await c.tap('tab-inventory');await delay(300);await c.tap('sell-Stone');await delay(300);const beforeTrade=(await c.probe()).coins;
 await c.eval("window.originalStorageWrite=Storage.prototype.setItem;Storage.prototype.setItem=function(k,v){if(k.startsWith('pickaxefall.web.v1.'))throw Error('test quota');return originalStorageWrite.call(this,k,v)}");
 await c.tap('sale-confirm');await delay(300);s=await c.probe();check(s.coins===beforeTrade,'failed local transaction rolls back sale');
 await c.eval('Storage.prototype.setItem=window.originalStorageWrite');await c.tap('sale-confirm');await delay(300);s=await c.probe();check(s.coins===beforeTrade+2,'sale succeeds once after quota recovery');
 fs.writeFileSync(out+'/cloud-result.json',JSON.stringify({passed:true,checks,sdk:'local double'},null,2));console.log('PICKAXEFALL_CLOUD_BROWSER_OK:',checks);c.close();
})().catch(async error=>{try{await c?.screenshot(out+'/cloud-failure.png')}catch(_){}console.error(error);c?.close();process.exit(1)});
