import urllib.request,json,re
from urllib.parse import quote
ref='1.0.0/Unity-2021.1.17f1'; repo='needle-mirror/com.unity.ugui'
tree=json.load(urllib.request.urlopen('https://api.github.com/repos/'+repo+'/git/trees/'+quote(ref,safe='')+'?recursive=1'))['tree']
for item in tree:
 if item['path'].endswith(('Image.cs.meta','Button.cs.meta','VerticalLayoutGroup.cs.meta')):
  data=urllib.request.urlopen('https://raw.githubusercontent.com/'+repo+'/'+ref+'/'+item['path']).read().decode();print(item['path'],re.search(r'guid: (\w+)',data)[1])
