import UnityPy
from pathlib import Path
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
base=Path(r'E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data')
g=TypeTreeGenerator('2021.3.0f1');g.load_local_dll_folder(str(base/'Managed'))
for name in ['level0','level1','resources.assets']:
 e=UnityPy.load(str(base/name))
 for o in e.objects:
  if o.type.name=='MonoBehaviour':
   d=o.read(check_read=False)
   sc=d.m_Script.read()
   if sc.m_Name=='MultiPageUIManager':
    tree=g.get_nodes_up(sc.m_AssemblyName,sc.m_ClassName)
    print(name,o.path_id,o.read_typetree(tree))
