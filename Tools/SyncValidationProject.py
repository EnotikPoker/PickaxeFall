#!/usr/bin/env python3
"""Synchronize game source into an isolated Unity project, keeping editor caches."""
from pathlib import Path
import argparse,shutil
p=argparse.ArgumentParser();p.add_argument('--project',type=Path,default=Path('Builds/ValidationProject'));a=p.parse_args()
root=Path(__file__).resolve().parent.parent;dest=a.project.resolve()
if dest==root or root not in dest.parents:raise SystemExit('Use an isolated directory below the repository')
# Unity may leave UnityLockfile behind after a compile failure. On Linux check
# the actual editor process rather than treating a stale file as an active lock.
if Path('/proc').exists():
    for entry in Path('/proc').glob('[0-9]*'):
        try:
            args=(entry/'cmdline').read_bytes().decode(errors='replace').split('\0')
            if Path(args[0]).name not in ('Unity','Unity.exe') or '-projectPath' not in args:continue
            if Path(args[args.index('-projectPath')+1]).resolve()==dest:
                raise SystemExit('Close the isolated Unity Editor before synchronizing')
        except (OSError,IndexError):pass
elif (dest/'Temp/UnityLockfile').exists():
    raise SystemExit('Close the isolated Unity Editor before synchronizing')
for part in ['Assets/PickaxeFall','Assets/Plugins','Assets/WebGLTemplates','Packages','ProjectSettings']:
    source=root/part
    if source.exists():
        target=dest/part
        if part.startswith('Assets/') and target.exists():
            for old in sorted(target.rglob('*'),key=lambda p:len(p.parts),reverse=True):
                original=source/old.relative_to(target)
                if old.is_file() and not original.exists():old.unlink()
                elif old.is_dir() and not original.exists() and not any(old.iterdir()):old.rmdir()
        shutil.copytree(source,target,dirs_exist_ok=True)
print('Synchronized',dest)
