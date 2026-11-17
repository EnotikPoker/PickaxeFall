#!/usr/bin/env python3
"""Firefox compatibility smoke on the real Web player with an isolated SDK double."""
import argparse,json,subprocess,time,urllib.request
from pathlib import Path
from playwright.sync_api import sync_playwright
p=argparse.ArgumentParser();p.add_argument('directory',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--port',type=int,default=8094);a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True);checks=[];errors=[]
def check(ok,label):
 assert ok,label
 checks.append(label);print('PASS',label,flush=True)
server=subprocess.Popen(['python3','Tools/ServeYandexChecks.py',str(a.directory),'--port',str(a.port),'--mock-sdk'],stdout=(a.output/'server.log').open('w'),stderr=subprocess.STDOUT)
try:
 for attempt in range(50):
  try:urllib.request.urlopen(f'http://127.0.0.1:{a.port}/',timeout=1);break
  except Exception:time.sleep(.1)
 with sync_playwright() as pw:
  browser=pw.firefox.launch(headless=True,firefox_user_prefs={'webgl.force-enabled':True,'webgl.disabled':False,'network.proxy.type':0})
  page=browser.new_page(viewport={'width':540,'height':960});page.on('pageerror',lambda e:errors.append(str(e)))
  page.goto(f'http://127.0.0.1:{a.port}/?lang=ru',wait_until='domcontentloaded')
  page.wait_for_function('!!window.pickaxeInstance',timeout=180000);page.wait_for_function('window.testPlatformEvents?.some(x=>x[0]==="ready")',timeout=60000)
  def probe(command='state'):
   page.evaluate('(command)=>{window.pickaxeProbe=null;pickaxeInstance.SendMessage("PickaxeFall","WebProbe",command)}',command);page.wait_for_function('!!window.pickaxeProbe',timeout=20000);state=page.evaluate('window.pickaxeProbe');assert not state['error'],state['error'];return state
  def click(name):
   state=probe();b=next(x for x in state['buttons'] if x['name']==name);r=page.locator('canvas').bounding_box();page.mouse.click(r['x']+b['x']*r['width'],r['y']+b['y']*r['height'],delay=120);page.wait_for_timeout(300)
  state=probe();check(state['language']=='ru' and state['phase']=='Ready','Firefox starts Russian game')
  probe('prepare');click('main-action');page.wait_for_timeout(2500);check(probe()['phase']=='Equipped','Firefox roulette accepts mouse input');click('main-action');page.wait_for_timeout(700);check(probe()['ticks']>0,'Firefox native WebAssembly physics runs')
  page.keyboard.press('Escape');page.wait_for_timeout(300);state=probe();ticks=state['ticks'];page.wait_for_timeout(400);check(state['paused'] and probe()['ticks']==ticks,'Firefox keyboard pause freezes physics');probe('save')
  page.reload(wait_until='domcontentloaded');page.wait_for_function('!!window.pickaxeInstance',timeout=180000);page.wait_for_function('window.testPlatformEvents?.some(x=>x[0]==="ready")',timeout=60000);state=probe();check(state['phase']=='Mining' and state['paused'] and state['coins']==100000,'Firefox reload restores local run')
  page.screenshot(path=str(a.output/'firefox.png'));check(not errors,'No uncaught JavaScript exceptions')
  (a.output/'result.json').write_text(json.dumps({'passed':True,'version':browser.version,'checks':checks,'errors':errors,'sdk':'local double'},ensure_ascii=False,indent=2));browser.close()
finally:
 server.terminate();server.wait(timeout=5)
