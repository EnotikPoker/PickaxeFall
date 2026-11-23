#!/usr/bin/env python3
"""Prepare static Google Play privacy/deletion pages; never publishes or invents contacts."""
import argparse,html,json,re,shutil
from pathlib import Path
ROOT=Path(__file__).resolve().parent.parent

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--config',type=Path);parser.add_argument('--app-ads',type=Path)
    parser.add_argument('--output',required=True,type=Path);parser.add_argument('--draft',action='store_true');args=parser.parse_args()
    config=json.loads((args.config or ROOT/'Assets/PickaxeFall/Resources/GooglePlay/config.json').read_text())
    if not args.draft:
        if not config.get('developerName','').strip() or not re.fullmatch(r'[^\s@]+@[^\s@]+\.[^\s@]+',config.get('supportEmail','')):
            raise SystemExit('Real developer name and support email required; use --draft for review')
        for key in ['privacyUrl','deletionUrl','appAdsUrl']:
            if not config.get(key,'').startswith('https://'):raise SystemExit('Real HTTPS URL required: '+key)
        if not args.app_ads or not args.app_ads.is_file() or not args.app_ads.read_text().strip():raise SystemExit('app-ads.txt from your provider dashboards required')
    args.output.mkdir(parents=True,exist_ok=True)
    name=html.escape(config.get('developerName') or '[ИМЯ РАЗРАБОТЧИКА / DEVELOPER NAME]')
    email=html.escape(config.get('supportEmail') or '[ПОЧТА ПОДДЕРЖКИ / SUPPORT EMAIL]')
    for language in ['ru','en']:
        for page in ['privacy','deletion']:
            template=(ROOT/f'Docs/GooglePlay/{page}-{language}.html').read_text()
            content=template.replace('{{DEVELOPER}}',name).replace('{{EMAIL}}',email)
            banner='<p role="alert"><strong>ЧЕРНОВИК / DRAFT — контакты и работа SDK требуют проверки. Не использовать как публичную политику.</strong></p>' if args.draft else ''
            content=content.replace('{{DRAFT}}',banner).replace('{{ROBOTS}}','noindex,nofollow' if args.draft else 'index,follow')
            (args.output/f'{page}-{language}.html').write_text(content)
    if args.app_ads:shutil.copyfile(args.app_ads,args.output/'app-ads.txt')
    print('Prepared '+str(args.output)+(' (DRAFT, not for publication)' if args.draft else '; review before publishing'))
if __name__=='__main__':main()
