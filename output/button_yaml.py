import re
from pathlib import Path
root=Path('output/manual-unity-reference/full-export/ExportedProject/Assets')
s=(root/'Scenes/InventoryScene.unity').read_text(encoding='utf-8-sig')
for id in [577,989]:
 go=re.search(r'--- !u!1 &'+str(id)+r'\n.*?(?=\n---)',s,re.S).group()
 for c in re.findall(r'component: \{fileID: (\d+)\}',go):
  d=re.search(r'--- !u!\d+ &'+c+r'\n.*?(?=\n---)',s,re.S).group()
  if 'm_OnClick:' in d:print(d[-2000:])
