#!/usr/bin/env python3
"""Validate the distributable Yandex archive without executing any game code."""
import argparse,gzip,hashlib,json,re,zipfile
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('archive',type=Path);p.add_argument('--draft',action='store_true');p.add_argument('--output',type=Path,required=True);a=p.parse_args()
with zipfile.ZipFile(a.archive) as z:
 names=z.namelist();html=z.read('index.html').decode();bridge=z.read('platform.js').decode();total=sum(x.file_size for x in z.infolist())
 checks={'indexAtRoot':'index.html' in names,'under100MB':total<=100000000,'portablePaths':all(not re.search(r'[\sА-Яа-яЁё]',n) and not n.startswith('/') and '..' not in Path(n).parts for n in names),'relativeSdk':"s.src='/sdk.js'" in html and 'games.s3.yandex.net' not in html,'midRunGate':('midRunAds:true' if a.draft else 'midRunAds:false') in html,'noTestSdk':not any('TestDouble' in n or 'sdk.js'==n or n.endswith('.cjs') for n in names),'noDevAuth':not any('DeveloperAuth' in n for n in names),'onlySdkAds':'showFullscreenAdv' in bridge and 'showRewardedVideo' in bridge,'noAutomaticAdInterval':'setInterval(' not in bridge,'noExternalAdNetwork':all(x not in html+bridge for x in ['googlesyndication','unityads','admob']),'noUnresolvedTemplate':'{{{' not in html}
 if not a.draft:
  contents=[]
  for n in names:
   value=z.read(n)
   if value.startswith(b'\x1f\x8b'):value=gzip.decompress(value)
   contents.append(value)
  checks['noDevelopmentWatermark']=not any(b'Development Build' in value for value in contents if b'function' in value[:300])
  checks['noQaCode']=not any(b'RuntimeWebProbe' in value or b'RuntimeSmokeTest' in value or b'pickaxeProbe' in value for value in contents)

 proof={'archive':str(a.archive),'bytes':a.archive.stat().st_size,'uncompressedBytes':total,'sha256':hashlib.sha256(a.archive.read_bytes()).hexdigest(),'checks':checks}
 a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(proof,indent=2));print(json.dumps(proof,indent=2));assert all(checks.values())
