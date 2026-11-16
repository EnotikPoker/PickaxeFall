#!/usr/bin/env python3
"""Inject real single/multiple Android fingers into the isolated verification APK."""
import argparse,json,os,re,subprocess,time,zipfile
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('--sdk',type=Path,required=True);p.add_argument('--jdk',type=Path,required=True)
p.add_argument('--apk',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
p.add_argument('--serial',required=True)
p.add_argument('--modes',nargs='+',choices=['single','pair','hold','cancel','edge'],default=['single','pair','hold','cancel','edge'])
a=p.parse_args();out=a.output.resolve();out.mkdir(parents=True,exist_ok=False)
adb=[str(a.sdk/'platform-tools/adb'),'-s',a.serial];package='com.enotikpoker.pickaxefall.verification'
def command(args,**kw):
 try:return subprocess.check_output(args,text=True,stderr=subprocess.STDOUT,**kw)
 except subprocess.CalledProcessError as error:raise RuntimeError(error.output.strip()) from error
def shell(*args):return command(adb+['shell']+list(args))
android=a.sdk/'platforms/android-36/android.jar';build=a.sdk/'build-tools/36.0.0';env=dict(os.environ,JAVA_HOME=str(a.jdk))
classes=out/'classes';classes.mkdir();dex=out/'dex';dex.mkdir()
subprocess.run([str(a.jdk/'bin/javac'),'--release','8','-cp',str(android),'-d',str(classes),str(Path(__file__).with_name('AndroidTouchProbe.java'))],check=True)
subprocess.run([str(build/'d8'),'--lib',str(android),'--output',str(dex),str(classes/'AndroidTouchProbe.class')],check=True,env=env)
jar=out/'touch.jar'
with zipfile.ZipFile(jar,'w') as z:z.write(dex/'classes.dex','classes.dex')
command(adb+['push',str(jar),'/data/local/tmp/pickaxefall-touch.jar'])
print(command(adb+['install','--no-streaming','-r',str(a.apk)]),flush=True)
activity=shell('cmd','package','resolve-activity','--brief','-c','android.intent.category.LAUNCHER',package).strip().splitlines()[-1]
assert activity.startswith(package+'/'),activity
results=[]
try:
 for mode in a.modes:
  expected={'single':30,'pair':30,'hold':1,'cancel':2,'edge':30}[mode]
  shell('am','force-stop',package);shell('am','start','-n',activity,'--es','pickaxefall-test','touch-edge' if mode=='edge' else 'touch')
  deadline=time.monotonic()+40;pid='';logs=''
  def read_logs():
   global pid
   found=subprocess.run(adb+['shell','pidof',package],text=True,capture_output=True)
   if found.returncode!=0 or not found.stdout.strip():return ''
   pid=found.stdout.strip().split()[0]
   return command(adb+['logcat','-d','--pid='+pid,'-s','Unity:I'])
  def target(kind):
   while time.monotonic()<deadline:
    try:
     log=read_logs();m=re.search(r'PICKAXEFALL_TOUCH_TARGET: '+kind+r'; x=(\d+); y=(\d+)',log)
     if m:return tuple(m.groups())
    except (subprocess.CalledProcessError,IndexError):pass
    time.sleep(.2)
   raise RuntimeError('No '+kind+' target in verification app')
  x,y=target('start');shell('input','tap',x,y);x,y=target('tap');time.sleep(.2)
  focus=shell('dumpsys','window');assert re.search(r'mCurrentFocus=.*'+re.escape(package),focus),'Verification app lost focus; no injection performed'
  print(shell('CLASSPATH=/data/local/tmp/pickaxefall-touch.jar','app_process','/system/bin','AndroidTouchProbe',mode,x,y),flush=True)
  time.sleep(.3);logs=read_logs();(out/(mode+'.log')).write_text(logs)
  counts=[int(n) for n in re.findall(r'PICKAXEFALL_TOUCH_COUNT: (\d+)',logs)];actual=max(counts,default=-1)
  row=dict(mode=mode,expected=expected,actual=actual,passed=actual==expected);results.append(row);print(row,flush=True)
finally:
 shell('am','force-stop',package);shell('rm','-f','/data/local/tmp/pickaxefall-touch.jar')
 (out/'results.json').write_text(json.dumps(results,indent=2))
if len(results)!=len(a.modes) or not all(r['passed'] for r in results):raise SystemExit(1)
