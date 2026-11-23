#!/usr/bin/env python3
"""Inspect actual APK/AAB manifests, native alignment and release-only contents."""
import argparse,hashlib,json,re,struct,subprocess,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
ANDROID=Path('/home/maxxon/Unity/Hub/Editor/6000.4.0f1/Editor/Data/PlaybackEngines/AndroidPlayer')
p=argparse.ArgumentParser(description=__doc__);p.add_argument('artifact',type=Path);p.add_argument('--qa',action='store_true');p.add_argument('--offline',action='store_true');p.add_argument('--output',required=True,type=Path);a=p.parse_args()
java=ANDROID/'OpenJDK/bin/java';bundletool=ANDROID/'Tools/bundletool-all-1.17.2.jar';sdk=ANDROID/'SDK';artifact=a.artifact.resolve()
checks={};details={}
def run(args):return subprocess.check_output([str(x) for x in args],text=True,stderr=subprocess.STDOUT)
if artifact.suffix=='.aab':
    run([java,'-jar',bundletool,'validate','--bundle='+str(artifact)])
    manifest=run([java,'-jar',bundletool,'dump','manifest','--bundle='+str(artifact),'--module=base'])
    details['bundleConfig']=run([java,'-jar',bundletool,'dump','config','--bundle='+str(artifact)])
    checks['bundleValidated']=True
    checks['bundle16K']='PAGE_ALIGNMENT_16K' in details['bundleConfig']
else:
    env=dict(__import__('os').environ,JAVA_HOME=str(ANDROID/'OpenJDK'))
    manifest=subprocess.check_output([str(sdk/'cmdline-tools/16.0/bin/apkanalyzer'),'manifest','print',str(artifact)],env=env,text=True)
    details['signing']=run([sdk/'build-tools/36.0.0/apksigner','verify','--verbose','--print-certs',artifact])
    run([sdk/'build-tools/36.0.0/zipalign','-c','-P','16','4',artifact]);checks['zipAligned16K']=True
version_source=(ROOT/'Assets/PickaxeFall/Scripts/Data/GameVersion.cs').read_text()
version=re.search(r'Name = "([^"]+)"',version_source).group(1)
version_code=re.search(r'AndroidVersionCode = (\d+)',version_source).group(1)
checks['versionMatchesSource']=f'versionName="{version}"' in manifest and f'versionCode="{version_code}"' in manifest
checks['package']='package="com.enotikpoker.pickaxefall'+('.playqa' if a.qa else '')+'"' in manifest
if not a.offline:
    checks['target36']='targetSdkVersion="36"' in manifest
    checks['billing']='com.android.vending.BILLING' in manifest
    checks['noSystemCloudBackup']='android:allowBackup="false"' in manifest and 'android:fullBackupContent="false"' in manifest
    checks['noLocationOrAdId']=all(x not in manifest for x in ['com.google.android.gms.permission.AD_ID','ACCESS_FINE_LOCATION','ACCESS_COARSE_LOCATION'])
    checks['noAdsOrGamesAutoProvider']=all(x not in manifest for x in ['MobileAdsInitializeProvider','PlayGamesInitProvider','AppMetricaContentProvider','PreloadInfoContentProvider'])
    checks['manualAdsMetadata']='com.yandex.mobile.ads.AUTOMATIC_SDK_INITIALIZATION' in manifest and 'android:value="false"' in manifest
else:
    checks['noPlaySdk']=all(x not in manifest for x in ['com.android.vending.BILLING','com.yandex.mobile.ads','net.kidoz','com.google.android.gms.games'])
checks['noForbiddenStoragePermissions']=all(x not in manifest for x in ['MANAGE_EXTERNAL_STORAGE','READ_CONTACTS','RECORD_AUDIO','READ_MEDIA_IMAGES'])
native=[]
with zipfile.ZipFile(artifact) as z:
    content=[z.read(x) for x in z.namelist() if x.endswith(('.so','.dat','.dex','.assets'))]
    if not a.qa:
        checks['noQaOrDev']=not any(marker in v for v in content for marker in [b'RuntimeGooglePlayChecks',b'PICKAXEFALL_GOOGLE_RUNTIME_OK',b'qa-receipts',b'DeveloperAuth',b'RuntimeSmokeTest'])
    for name in z.namelist():
        if not name.endswith('.so'):continue
        data=z.read(name);assert data[:5]==b'\x7fELF\x02',(name,'expected 64-bit ELF')
        offset=struct.unpack_from('<Q',data,32)[0];size,count=struct.unpack_from('<HH',data,54)
        align=[struct.unpack_from('<Q',data,offset+i*size+48)[0] for i in range(count) if struct.unpack_from('<I',data,offset+i*size)[0]==1]
        native.append({'file':name,'loadAlignments':align})
    checks['native16K']=bool(native) and all(x>=16384 for n in native for x in n['loadAlignments'])
a.output.parent.mkdir(parents=True,exist_ok=True);details.update(artifact=str(artifact),bytes=artifact.stat().st_size,sha256=hashlib.sha256(artifact.read_bytes()).hexdigest(),checks=checks,native=native)
a.output.write_text(json.dumps(details,indent=2));a.output.with_suffix('.manifest.xml').write_text(manifest)
print(json.dumps({'artifact':str(artifact),'checks':checks},indent=2));assert all(checks.values()),'Artifact verification failed'
