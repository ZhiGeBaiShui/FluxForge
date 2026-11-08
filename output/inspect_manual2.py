import UnityPy
from pathlib import Path
base=Path(r'E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data')
e=UnityPy.load(str(base/'level1'))
for o in e.objects:
 if o.type.name=='MonoBehaviour':
  d=o.read_typetree(check_read=False)
  script=o.assets_file.objects[d['m_Script']['m_PathID']] if d['m_Script']['m_FileID']==0 else None
  if script and script.read().m_Name=='MultiPageUIManager':print(o.path_id,d,o.get_raw_data().hex())
