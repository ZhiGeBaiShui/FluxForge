from pathlib import Path
import re,json,urllib.request,struct,shutil
from Crypto.Hash import MD4
base=Path('output/manual-unity-reference/full-export/ExportedProject'); src=base/'Assets'; dest=Path('output/manual-unity-reference/UnityReferenceProject'); assets=dest/'Assets'; assets.mkdir(parents=True,exist_ok=True)
s=(src/'Scenes/InventoryScene.unity').read_text(encoding='utf-8-sig');docs={int(m.group(2)):m.group(0) for m in re.finditer(r'--- !u!(\d+) &(\d+)\n.*?(?=\n--- !u!|\Z)',s,re.S)}
metas={re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig'),re.M).group(1):p for p in src.rglob('*.meta') if re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig'),re.M)}
plugin={n:re.search(r'^guid: (\w+)',(src/'Plugins'/ (n+'.dll.meta')).read_text(),re.M).group(1) for n in ['UnityEngine.UI','Unity.TextMeshPro']}
scriptmaps={}; packageguids=set()
for repo,ref,ns,dll in [('com.unity.ugui','master','UnityEngine.UI','UnityEngine.UI'),('com.unity.textmeshpro','3.0.6','TMPro','Unity.TextMeshPro')]:
 tree=json.load(urllib.request.urlopen('https://api.github.com/repos/needle-mirror/'+repo+'/git/trees/'+ref+'?recursive=1'))['tree']
 for item in tree:
  path=item['path']
  if path.endswith('.cs.meta') and '/Editor/' not in path:
   name=Path(path).name[:-8]; fid=struct.unpack('<i',MD4.new(('s\0\0\0'+ns+name).encode()).digest()[:4])[0]
   scriptmaps[(plugin[dll],fid)]=(repo,ref,path)
def remap(txt):
 def f(m):
  fid=int(m[1]);guid=m[2]; key=(guid,fid)
  if key not in scriptmaps:return m[0]
  val=scriptmaps[key]
  if isinstance(val,tuple):
   raw=urllib.request.urlopen('https://raw.githubusercontent.com/needle-mirror/'+val[0]+'/'+val[1]+'/'+val[2]).read().decode(); val=re.search(r'^guid: (\w+)',raw,re.M)[1]; scriptmaps[key]=val
  packageguids.add(val);return '{fileID: 11500000, guid: '+val+', type: 3}'
 return re.sub(r'\{fileID: (-?\d+), guid: (\w+), type: 3\}',f,txt)
keep=set(); skipped={687,1754,1427,272,410,952,557,642,1790}
def collect(goid):
 if goid in skipped:return
 keep.add(goid)
 for c in re.findall(r'component: \{fileID: (\d+)\}',docs[goid]):
  cid=int(c);d=docs[cid]
  if d.startswith('--- !u!114'):
   guid=re.search(r'm_Script: .*guid: (\w+)',d)[1]
   if guid not in plugin.values():continue
  keep.add(cid)
  if d.startswith('--- !u!224'):
   for child in re.findall(r'fileID: (\d+)',d.split('  m_Children:')[1].split('  m_Father:')[0]):collect(int(re.search(r'm_GameObject: \{fileID: (\d+)',docs[int(child)])[1]))
collect(1167)
navguid='abde1234567890123456789012345678';navid=99000001
out=[]
for id in sorted(keep):
 d=docs[id]
 if d.startswith('--- !u!1'):
  d=re.sub(r'  - component: \{fileID: (\d+)\}\n',lambda m:m[0] if int(m[1]) in keep else '',d)
  if id==1167:d=d.replace('  m_Layer:',f'  - component: {{fileID: {navid}}}\n  m_Layer:').replace('  m_IsActive: 0','  m_IsActive: 1')
 if d.startswith('--- !u!224'):
  d=re.sub(r'  - \{fileID: (\d+)\}\n',lambda m:m[0] if int(m[1]) in keep else '',d)
  if 'm_GameObject: {fileID: 1167}' in d:d=re.sub(r'  m_Father:.*','  m_Father: {fileID: 0}',d)
 if 'm_OnClick:' in d:d=d[:d.index('  m_OnClick:')]+ '  m_OnClick:\n    m_PersistentCalls:\n      m_Calls: []'
 # Scene-only object links must not leak out of the prefab.
 d=re.sub(r'\{fileID: (-?\d+)\}',lambda m:m[0] if int(m[1])==0 or int(m[1]) in keep else '{fileID: 0}',d)
 out.append(remap(d))
out.append(f'''--- !u!114 &{navid}
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 1167}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {navguid}, type: 3}}
  m_Name:
  m_EditorClassIdentifier:
''')
prefab=assets/'ChemistryManualReference.prefab';prefab.write_text('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n'+'\n'.join(out)+'\n',encoding='utf-8')
queue=list(set(re.findall(r'guid: (\w+)',prefab.read_text(encoding='utf-8'))));copied=set()
while queue:
 guid=queue.pop()
 if guid in copied or guid==navguid or guid in packageguids or guid.startswith('00000000'):continue
 if guid not in metas:raise RuntimeError('missing '+guid)
 meta=metas[guid];file=Path(str(meta)[:-5]);rel=file.relative_to(src)
 if rel.parts[0] in ['Plugins','Scripts']:raise RuntimeError('unexpected code dependency '+str(rel))
 target=assets/rel;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(meta,str(target)+'.meta')
 if file.is_file():
  if file.suffix in ['.asset','.mat','.shader','.controller','.anim']:
   try:text=remap(file.read_text(encoding='utf-8-sig'));target.write_text(text,encoding='utf-8');queue.extend(re.findall(r'guid: (\w+)',text))
   except UnicodeDecodeError:shutil.copy2(file,target)
  else:shutil.copy2(file,target)
 copied.add(guid)
print('prefab objects',len(keep),'dependencies',len(copied),'package scripts',len(packageguids))
(dest/'Packages').mkdir(exist_ok=True);(dest/'Packages/manifest.json').write_text(json.dumps({'dependencies':{'com.unity.ugui':'1.0.0','com.unity.textmeshpro':'3.0.6'}},indent=2))
(dest/'ProjectSettings').mkdir(exist_ok=True);shutil.copy2(base/'ProjectSettings/ProjectVersion.txt',dest/'ProjectSettings/ProjectVersion.txt')
(assets/'ReferenceNavigation.cs.meta').write_text('fileFormatVersion: 2\nguid: '+navguid+'\n')
Path('output/manual-unity-reference/package-script-guids.json').write_text(json.dumps(sorted(packageguids),indent=2))
