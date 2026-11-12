#!/usr/bin/env python3
"""Original soft interface cues; generated assets are dedicated to CC0."""
import math,wave,struct,json,hashlib
from pathlib import Path
root=Path(__file__).resolve().parents[1]
patterns={'navigate':([659],.045),'click':([440,660],.055),'sale':([523,784],.09),'skill':([523,659,784],.11),'ability':([392,523,659,1046],.12),'profile':([392,494,587,784,988,1175],.15),'construction':([330,440,554,660,880],.14),'record':([659,784,988,1319],.13)}
manifest=json.loads((root/'Docs/AudioSources.json').read_text())
for name,(notes,step) in patterns.items():
 path=root/f'Assets/PickaxeFall/Resources/Audio/Sfx/{name}/ui-{name}.wav';path.parent.mkdir(parents=True,exist_ok=True)
 rate=22050;duration=(len(notes)-1)*step+.26;data=[]
 for i in range(int(rate*duration)):
  t=i/rate;value=0
  for n,freq in enumerate(notes):
   age=t-n*step
   if 0<=age<.26:
    env=min(1,age/.008)*math.exp(-age*23)*(1-age/.26)
    value+=env*(math.sin(math.tau*freq*age)+.18*math.sin(math.tau*freq*2*age))*.44
  data.append(struct.pack('<h',round(max(-.9,min(.9,value))*32767)))
 with wave.open(str(path),'wb') as out:out.setparams((1,2,rate,0,'NONE','not compressed'));out.writeframes(b''.join(data))
 rel=str(path.relative_to(root));manifest=[x for x in manifest if x['file']!=rel];manifest.append({'file':rel,'author':'PickaxeFall','license':'CC0-1.0','source':'Tools/GenerateInterfaceAudio.py','sha256':hashlib.sha256(path.read_bytes()).hexdigest()})
(root/'Docs/AudioSources.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')
