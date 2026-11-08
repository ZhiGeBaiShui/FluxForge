from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
import UnityPy
from pathlib import Path
base=Path(r'E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data')
g=TypeTreeGenerator('2021.3.0f1'); g.load_local_dll_folder(str(base/'Managed'))
for name in ['level0','level1','resources.assets']:
 e=UnityPy.load(str(base/name));e.typetree_generator=g
 for o in e.objects:
  if o.type.name=='MonoBehaviour':
   t=o.read_typetree()
   if 'books' in t and 'pageNumberText' in t: print(name,o.path_id,t)
