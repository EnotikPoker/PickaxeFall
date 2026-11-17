#!/usr/bin/env node
const Cdp=require('./BrowserCdp.cjs'),fs=require('node:fs'),assert=require('node:assert/strict');const out=process.argv[2];fs.mkdirSync(out,{recursive:true});let c,checks=0;const delay=ms=>new Promise(r=>setTimeout(r,ms));const check=(ok,text)=>{checks++;assert.ok(ok,text);console.log('PASS',text)};
(async()=>{
 c=await Cdp.connect();await c.call('Emulation.setDeviceMetricsOverride',{width:390,height:844,deviceScaleFactor:1,mobile:true});await c.call('Emulation.setTouchEmulationEnabled',{enabled:true,maxTouchPoints:5});
 await c.call('Page.navigate',{url:'http://127.0.0.1:8093/?lang=en'});await c.wait('!!window.pickaxeInstance',180);await c.wait('testPlatformEvents.some(x=>x[0]==="ready")',45);await c.probe('prepare');
 for(const mode of ['single','pair','hold']){
  await c.probe('spawner');await c.tap('spawner-start');await delay(200);let s=await c.probe(),target=s.buttons.find(x=>x.name==='spawner-target');assert.ok(target,'spawner target');
  const rect=await c.eval("(()=>{const r=document.querySelector('canvas').getBoundingClientRect();return {x:r.x,y:r.y,width:r.width,height:r.height}})()");const x=rect.x+target.x*rect.width,y=rect.y+target.y*rect.height;
  const touch=async(type,points)=>{await c.call('Input.dispatchTouchEvent',{type,touchPoints:points});await delay(70);};const a={x,y,id:0,radiusX:5,radiusY:5},b={x:x+16,y,id:1,radiusX:5,radiusY:5};
  if(mode==='single')for(let i=0;i<10;i++){await touch('touchStart',[a]);await touch('touchEnd',[]);}
  if(mode==='pair')for(let i=0;i<5;i++){await touch('touchStart',[a,b]);await touch('touchEnd',[]);}
  if(mode==='hold'){await touch('touchStart',[a]);for(let i=0;i<5;i++)await touch('touchMove',[{...a,x:x+i}]);await touch('touchEnd',[]);}
  s=await c.probe();check(s.hits===(mode==='hold'?1:10),mode+' touches counted once; actual '+s.hits);await c.screenshot(out+'/'+mode+'.png');
 }
 fs.writeFileSync(out+'/touch-result.json',JSON.stringify({passed:true,checks,input:'real Chromium touch events; local SDK double'},null,2));console.log('PICKAXEFALL_WEB_TOUCH_OK:',checks);c.close();
})().catch(async error=>{try{await c?.screenshot(out+'/touch-failure.png')}catch(_){}console.error(error);c?.close();process.exit(1)});
