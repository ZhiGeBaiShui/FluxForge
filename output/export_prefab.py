from pathlib import Path
import re
s=Path('output/manual-unity-reference/full-export/ExportedProject/Assets/Scenes/InventoryScene.unity').read_text(encoding='utf-8-sig'); docs={int(m.group(2)):m.group(0) for m in re.finditer(r'--- !u!(\d+) &(\d+)\n.*?(?=\n--- !u!|\Z)',s,re.S)}
def go(id,depth=0):
 d=docs[id]; print(' '*depth,id,re.search(r'  m_Name: (.*)',d).group(1)); tids=re.findall(r'component: \{fileID: (\d+)\}',d)
 for tid in tids:
  t=docs[int(tid)]
  if t.startswith('--- !u!224'):
   children=t.split('  m_Children:')[1].split('  m_Father:')[0]
   if depth<2:
    for c in re.findall(r'fileID: (\d+)',children):go(int(re.search(r'm_GameObject: \{fileID: (\d+)',docs[int(c)]).group(1)),depth+1)
go(1167)
