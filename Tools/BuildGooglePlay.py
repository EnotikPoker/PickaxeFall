#!/usr/bin/env python3
"""Build isolated Play artifacts with pinned SDKs; never installs SDKs in the working project."""
import argparse,hashlib,json,os,secrets,shutil,subprocess,tarfile,urllib.request
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent
UNITY=Path('/home/maxxon/Unity/Hub/Editor/6000.4.0f1/Editor/Unity')
def keys():
    private=ROOT/'Builds/Private/googleplay';private.mkdir(parents=True,exist_ok=True);private.chmod(0o700);path=private/'signing.json'
    if path.exists():return path
    password=secrets.token_urlsafe(32);env=os.environ.copy();env['PICKAXEFALL_KEY_PASSWORD']=password
    values={'appKeystore':str(private/'app.keystore'),'uploadKeystore':str(private/'upload.keystore'),'storePassword':password,'keyPassword':password,'appAlias':'pickaxefall','uploadAlias':'upload'}
    keytool=UNITY.parent/'Data/PlaybackEngines/AndroidPlayer/OpenJDK/bin/keytool'
    for file,alias in [(values['appKeystore'],values['appAlias']),(values['uploadKeystore'],values['uploadAlias'])]:
        if Path(file).exists():raise SystemExit('Unrecorded signing key exists; inspect private directory before continuing')
        subprocess.run([str(keytool),'-genkeypair','-keystore',file,'-alias',alias,'-keyalg','RSA','-keysize','3072','-validity','10000','-dname','CN=PickaxeFall App Signing','-storepass:env','PICKAXEFALL_KEY_PASSWORD','-keypass:env','PICKAXEFALL_KEY_PASSWORD','-storetype','JKS'],env=env,check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL);Path(file).chmod(0o600)
    path.write_text(json.dumps(values,indent=2));path.chmod(0o600);return path

def main():
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('target',choices=['test-aab','test-apk','production-aab','preflight','device-qa']);p.add_argument('--project',type=Path,default=ROOT/'Builds/ValidationProject');p.add_argument('--config',type=Path);p.add_argument('--log',type=Path,default=ROOT/'Builds/Release084/build-google.log');a=p.parse_args()
    project=a.project.resolve()
    if project==ROOT or ROOT not in project.parents:raise SystemExit('Use an isolated project below the repository')
    lock=json.loads((ROOT/'Tools/GooglePlayDependencies.json').read_text());deps=ROOT/'Builds/Dependencies';deps.mkdir(parents=True,exist_ok=True)
    package=deps/('iap-'+lock['unityIapVersion']+'.tgz')
    if not package.exists():package.write_bytes(urllib.request.urlopen(lock['unityIapUrl'],timeout=60).read())
    if hashlib.sha256(package.read_bytes()).hexdigest()!=lock['unityIapSha256']:raise SystemExit('Unity IAP checksum mismatch')
    extracted=deps/('iap-'+lock['unityIapVersion'])
    if not (extracted/'package/package.json').exists():
        with tarfile.open(package) as tar:tar.extractall(extracted,filter='data')
    manifest=project/'Packages/manifest.json';old_manifest=manifest.read_bytes();package_lock=project/'Packages/packages-lock.json';old_lock=package_lock.read_bytes() if package_lock.exists() else None
    resource=project/'Assets/PickaxeFall/Resources/GooglePlay/config.json';old_config=resource.read_bytes()
    plugin=project/'Assets/Plugins/Android/PickaxePlay.androidlib'
    if plugin.exists():raise SystemExit('Stale generated Play plugin: inspect it before rebuilding')
    try:
        m=json.loads(old_manifest);m['dependencies']['com.unity.purchasing']='file:'+str(extracted/'package');manifest.write_text(json.dumps(m,indent=2)+'\n')
        config=json.loads(a.config.read_text()) if a.config else json.loads(old_config)
        if a.target.startswith('test-') or a.target=='device-qa':config['testMode']=True
        resource.write_text(json.dumps(config,indent=2)+'\n');
        if a.target=='device-qa':
            from CreateIapFixture import create
            create(resource.parent/'qa-receipts.json')
        shutil.copytree(ROOT/'Tools/GooglePlayAndroid',plugin)
        strings=plugin/'res/values';strings.mkdir(parents=True);app_id=config.get('gamesApplicationId') or '0'
        if not str(app_id).isdigit():raise SystemExit('gamesApplicationId must be numeric')
        (strings/'games.xml').write_text('<resources><string name="games_app_id" translatable="false">'+str(app_id)+'</string></resources>')
        methods={'test-aab':'TestBundle','test-apk':'TestApk','production-aab':'Production','preflight':'ValidateConfiguration','device-qa':'DeviceQa'}
        env=os.environ.copy();env['PICKAXEFALL_PLAY_SIGNING']=str(keys());a.log.parent.mkdir(parents=True,exist_ok=True)
        result=subprocess.run([str(UNITY),'-batchmode','-nographics','-projectPath',str(project),'-executeMethod','PickaxeFall.Editor.GooglePlayBuild.'+methods[a.target],'-quit','-logFile',str(a.log.resolve())],env=env,start_new_session=True)
        if result.returncode:raise SystemExit(result.returncode)
        if a.target!='preflight':
            output=ROOT/'Builds/GooglePlay';output.mkdir(parents=True,exist_ok=True)
            for artifact in (project/'Builds/GooglePlay').glob('*'):shutil.copy2(artifact,output/artifact.name)
    finally:
        manifest.write_bytes(old_manifest);resource.write_bytes(old_config)
        (resource.parent/'qa-receipts.json').unlink(missing_ok=True)
        (resource.parent/'qa-receipts.json.meta').unlink(missing_ok=True)
        if old_lock is not None:package_lock.write_bytes(old_lock)
        elif package_lock.exists():package_lock.unlink()
        if plugin.exists():shutil.rmtree(plugin)
        Path(str(plugin)+'.meta').unlink(missing_ok=True)
if __name__=='__main__':main()
