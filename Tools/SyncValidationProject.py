#!/usr/bin/env python3
"""Synchronize game source into an isolated Unity project, keeping editor caches."""
from pathlib import Path
import argparse,shutil,uuid
p=argparse.ArgumentParser();p.add_argument('--project',type=Path,default=Path('Builds/ValidationProject'));a=p.parse_args()
root=Path(__file__).resolve().parent.parent;dest=a.project.resolve()
if dest==root or root not in dest.parents:raise SystemExit('Use an isolated directory below the repository')
for part in ['Assets/PickaxeFall','Assets/Plugins','Assets/WebGLTemplates','Packages','ProjectSettings']:
    source=root/part
    if source.exists():shutil.copytree(source,dest/part,dirs_exist_ok=True)
print('Synchronized',dest)
