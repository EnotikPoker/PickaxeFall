#!/usr/bin/env python3
"""Start the production player in Chromium; no development hooks are used."""
import argparse,json,subprocess,time,urllib.request
from pathlib import Path
from playwright.sync_api import sync_playwright
p=argparse.ArgumentParser();p.add_argument('directory',type=Path);p.add_argument('--output',type=Path,required=True);a=p.parse_args();a.output.mkdir(parents=True,exist_ok=True);errors=[]
server=subprocess.Popen(['python3','Tools/ServeYandexChecks.py',str(a.directory),'--port','8095','--mock-sdk'],stdout=(a.output/'server.log').open('w'),stderr=subprocess.STDOUT)
try:
 for i in range(50):
  try:urllib.request.urlopen('http://127.0.0.1:8095/',timeout=1);break
  except Exception:time.sleep(.1)
 with sync_playwright() as pw:
  browser=pw.chromium.launch(executable_path=str(Path.home()/'.cache/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-linux64/chrome-headless-shell'),headless=True,args=['--no-sandbox','--enable-unsafe-swiftshader'])
  page=browser.new_page(viewport={'width':540,'height':960});page.on('pageerror',lambda e:errors.append(str(e)))
  page.goto('http://127.0.0.1:8095/?lang=en',wait_until='domcontentloaded');page.wait_for_function('!!window.pickaxeInstance',timeout=180000);page.wait_for_function('window.testPlatformEvents?.some(x=>x[0]==="ready")',timeout=60000)
  checks={'productionReady':True,'noQaGlobal':page.evaluate('typeof window.pickaxeProbe === "undefined"'),'publicMidRunAdsDisabled':page.evaluate('PICKAXEFALL_CONFIG.midRunAds===false'),'guestSaved':page.evaluate('!!localStorage.getItem("pickaxefall.web.v1.guest")'),'noExceptions':not errors}
  page.screenshot(path=str(a.output/'production-en.png'));page.reload(wait_until='domcontentloaded');page.wait_for_function('window.testPlatformEvents?.some(x=>x[0]==="ready")',timeout=120000);checks['productionReload']=True;checks['noExceptions']=not errors
  (a.output/'result.json').write_text(json.dumps({'checks':checks,'errors':errors,'sdk':'local double'},indent=2));print(checks,flush=True);assert all(checks.values());browser.close()
finally:
 server.terminate();server.wait(timeout=5)
