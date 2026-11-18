#!/usr/bin/env python3
"""Start an isolated browser and mock host, run real Unity Web checks, then stop both."""
import argparse,os,subprocess,time,urllib.request
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('directory',type=Path);p.add_argument('--output',type=Path,required=True);p.add_argument('--suite',choices=['main','cloud','touch','full'],default='main');p.add_argument('--chromium',type=Path,default=Path.home()/'.cache/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-linux64/chrome-headless-shell');a=p.parse_args();out=a.output.resolve();out.mkdir(parents=True,exist_ok=True)
processes=[]
try:
 for command,name in [(['python3','Tools/ServeYandexChecks.py',str(a.directory),'--mock-sdk'],'server'),([str(a.chromium),'--no-sandbox','--disable-dev-shm-usage','--enable-unsafe-swiftshader','--remote-debugging-port=9223','--user-data-dir='+str(out/'chromium-profile'),'about:blank'],'chromium')]:
  log=(out/(name+'.log')).open('w');processes.append(subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT,start_new_session=True))
 for i in range(50):
  try:urllib.request.urlopen('http://127.0.0.1:9223/json/list',timeout=1);break
  except Exception:time.sleep(.1)
 suites=['main','cloud','touch'] if a.suite=='full' else [a.suite]
 for suite in suites:
  result=subprocess.run(['node',{'main':'Tools/RunBrowserChecks.cjs','cloud':'Tools/RunCloudBrowserChecks.cjs','touch':'Tools/RunTouchBrowserChecks.cjs'}[suite],str(out)],timeout=540)
  if result.returncode:raise SystemExit(result.returncode)

finally:
 import signal
 for process in processes:
  try:os.killpg(process.pid,signal.SIGTERM);process.wait(timeout=5)
  except (ProcessLookupError,subprocess.TimeoutExpired):pass
