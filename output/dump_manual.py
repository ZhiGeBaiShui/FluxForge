exec(open('output/inspect_manual4.py',encoding='utf-8-sig').read().split('for name in')[0])
e=UnityPy.load(str(base/'level1')); f=next(x for x in e.files.values() if hasattr(x,'objects'))
def dump(goid,indent=''):
 go=f.objects[goid].read();print(indent+str(goid)+' '+go.m_Name)
 tr=None
 for c in go.m_Component:
  o=c.component.deref()
  if o.type.name=='RectTransform':tr=o.read()
  elif o.type.name=='MonoBehaviour':
   d=o.read(check_read=False);sc=d.m_Script.read();print(indent+' '+str(o.path_id)+' '+sc.m_Name)
   if sc.m_Name in ['TextMeshProUGUI','TMPLocalizer','LocalizeStringEvent']:
    t=o.read_typetree(g.get_nodes_up(sc.m_AssemblyName, (sc.m_Namespace+'.' if sc.m_Namespace else '')+sc.m_ClassName));print(indent+'  '+str({k:v for k,v in t.items() if k in ['m_text','StringReference','fontRole']}))
 if tr:
  for child in tr.m_Children:dump(child.read().m_GameObject.path_id,indent+'  ')
dump(1238)

