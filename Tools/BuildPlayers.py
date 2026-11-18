#!/usr/bin/env python3
"""Build a platform player; pass an isolated Unity project with --project."""
import argparse,os,subprocess
from pathlib import Path
p=argparse.ArgumentParser(description=__doc__);p.add_argument('target',choices=['web','web-draft','android','android-dev','linux','checks']);p.add_argument('--project',required=True,type=Path);p.add_argument('--log',required=True,type=Path);p.add_argument('--unity',type=Path,default=Path('/home/maxxon/Unity/Hub/Editor/6000.4.0f1/Editor/Unity'));a=p.parse_args()
root=Path(__file__).resolve().parent.parent;env=os.environ.copy();local=root/'Builds/Tools/ffmpeg/usr'
if (local/'bin/ffmpeg').exists():
 env['PATH']=str(local/'bin')+os.pathsep+env.get('PATH','');env['LD_LIBRARY_PATH']=str(local/'lib/x86_64-linux-gnu')+os.pathsep+env.get('LD_LIBRARY_PATH','')
env['PICKAXEFALL_PRIVATE_DIR']=str(root/'Builds/Private')
methods={'web':'PickaxeFall.Editor.YandexBuild.Production','web-draft':'PickaxeFall.Editor.YandexBuild.Draft','android':'PickaxeFall.Editor.ReleaseBuild.Public','android-dev':'PickaxeFall.Editor.ReleaseBuild.Developer','linux':'PickaxeFall.Editor.DesktopVerificationBuild.Build','checks':'PickaxeFall.Editor.PlatformReleaseChecks.Validate'}
raise SystemExit(subprocess.run([str(a.unity),'-batchmode','-nographics','-projectPath',str(a.project.resolve()),'-executeMethod',methods[a.target],'-quit','-logFile',str(a.log.resolve())],env=env).returncode)
