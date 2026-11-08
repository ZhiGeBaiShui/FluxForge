from pathlib import Path
import re,json
p=Path('src/ItemIntegration/Manual/DeviceManual.cs');s=p.read_text(encoding='utf-8'); matches=re.findall(r'new Page\("([^"]+)", "((?:[^"\\]|\\.)*)"',s); data={str(i):{'title':t,'body':b.replace('\\n','\n')} for i,(t,b) in enumerate(matches)}
Path('assets/manual_zh.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
s=s.replace('using System;','using System;\nusing System.Collections.Generic;\nusing Newtonsoft.Json;')
s=s.replace('        private static readonly Page[] Pages', '        private sealed class PageText { public string title; public string body; }\n        private static readonly Dictionary<string, PageText> Texts = JsonConvert.DeserializeObject<Dictionary<string, PageText>>(EmbeddedResources.ReadText("FluxForge.assets.", "manual_zh.json"));\n        private static readonly Page[] Pages')
for i,(t,b) in enumerate(matches):s=s.replace('new Page("'+t+'", "'+b+'"','new Page(Texts["'+str(i)+'"].title, Texts["'+str(i)+'"].body')
s=s.replace('GetComponent<Image>().color = new Color(.16f,.14f,.17f,.99f);','GetComponent<Image>().sprite = PaperUIManager.Instance.paperBack1;\n            GetComponent<Image>().color = Color.white;').replace('new Color(.93f,.87f,.73f)','new Color(.18f,.15f,.16f)')
s=s.replace('Pages[index].Demo(grid);','if (!Pages[index].Demo(grid)) throw new InvalidOperationException("手册演示启动失败：" + Pages[index].Title);')
p.write_text(s,encoding='utf-8')
Path('docs/manual').mkdir(exist_ok=True)
Path('docs/manual/README.md').write_text('''# 装置手册（基础版）

物品 ID：`fluxforge_device_manual`。奥雅首次库存和重复到访库存各上架一本；已有存档等待下次到访。

激活手册打开四页：概览、发射与回收、过滤、冷萃。后三页有演示按钮，重复点击重新播放。上一页、下一页与关闭都会取消演示并清理独立网格；演示不绑定玩家物品，不进入玩家存档。

文字位于 `assets/manual_zh.json`，通过资源嵌入；页面顺序和可选演示绑定位于 `DeviceManual`。基础版全部页面可见，尚未实现动态解锁。

界面复用原版纸张和字体。新增 `manual` attachment 使用原版书本图标与阅读条件。

必须游戏内验证纸张缩放、按钮点击、网格位置、演示动画、关闭/翻页清理及退出存档清理。构建通过不代表已完成这些运行时验证。
''',encoding='utf-8')

