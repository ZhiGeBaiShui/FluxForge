import UnityPy
from pathlib import Path
base=Path(r'E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data')
for name in ['level0','level1','resources.assets']:
 e=UnityPy.load(str(base/name))
 for o in e.objects:
  if o.type.name=='MonoBehaviour':
   t=o.read_typetree()
   if 'books' in t and 'pageNumberText' in t: print(name,o.path_id,t)
