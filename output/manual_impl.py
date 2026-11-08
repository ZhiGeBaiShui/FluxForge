from pathlib import Path
p=Path('src/Events/Runtime/EventSession.cs'); s=p.read_text(encoding='utf-8-sig'); s=s.replace('            Grid = grid;','            Active.Add(grid, this);\n            Grid = grid;'); s=s.replace('        private readonly List<GameItem>', '        private static readonly Dictionary<GameGridInventory, EventSession> Active = new Dictionary<GameGridInventory, EventSession>();\n\n        internal static void Cancel(GameGridInventory grid)\n        {\n            if (Active.TryGetValue(grid, out EventSession session)) session.End();\n        }\n\n        private readonly List<GameItem>',1); s=s.replace('            _ended = true;','            _ended = true;\n            Active.Remove(Grid);'); p.write_text(s,encoding='utf-8')
p=Path('src/Events/Public/FlowEventApi.cs'); s=p.read_text(encoding='utf-8-sig').replace('        public static Action<GameGridInventory> EventCompleted;','        public static Action<GameGridInventory> EventCompleted;\n\n        public static void Cancel(GameGridInventory grid) => EventSession.Cancel(grid);'); p.write_text(s,encoding='utf-8')
p=Path('src/ItemIntegration/Manual'); p.mkdir(parents=True,exist_ok=True)
(p/'ManualAttachment.cs').write_text('''namespace FluxForge.Mod
{
    public sealed class ManualAttachment : ItemAttachment
    {
        public override string Name => "manual";
        public override void Attach(GameItem item, FlowItemDefinition def)
        {
            item.EnableTag("DOCUMENT");
            item.EnableTag("IMPORTANT_TAG");
            item.EnableTag("GUIDE_PAPER_TAG");
            item.ModifyTag("FLUXFORGE_MANUAL_ID", state => state.Enable().SetString("device_guide"));
            item.SetSpriteAndShape("Items/items_misc", "book2");
            BookHelper.InitGuide(item);
            item.canActivateSlotItemFunc = (_, _, _) => HealthData.CanRead();
            item.mayActivateSlotItemFunc = (_, _, _) => HealthData.CanRead();
            item.onActivateSlotItemFunc = (_, _, _) => DeviceManual.Open();
        }
    }
}
''',encoding='utf-8')
(p/'DeviceManual.cs').write_text('''using System;
using FluxForge.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FluxForge.Mod
{
    public sealed class DeviceManual : MonoBehaviour
    {
        private sealed class Page
        {
            internal string Title, Body;
            internal Func<GameGridInventory, bool> Demo;
            internal Page(string title, string body, Func<GameGridInventory, bool> demo = null)
            { Title = title; Body = body; Demo = demo; }
        }
        private static readonly Page[] Pages = {
            new Page("装置手册", "把背包，变成你的流体实验室。\\n\\n将液体装入发射器，组合转向、过滤、温控与冷萃装置，搭建自己的处理路线。\\n\\n这些装置可以降低酸、碱中的化学污染和重金属污染，也可以对化学产物进行少量成分微调。\\n\\n后续页面提供独立演示，演示物品不会进入你的库存。"),
            new Page("发射与回收", "将容器中的液体装入发射器，启动后一次性发射存量。发射器也能接收、暂存流源。\\n\\n转向装置改变流向，直通管主要用于美化路线。流源越出网格后消失。", FlowEventApi.RunBoundaryEmitter),
            new Page("过滤装置", "过滤能降低化学污染与重金属污染，帮助净化酸、碱等原料。\\n\\n低流速与高温提高过滤效率，但高温也会造成蒸发损失。过滤存在浓度下限。", FlowEventApi.RunFilterLoop),
            new Page("冷萃装置", "冷萃会抽取含量最低的非零液体成分并存入自身。\\n\\n可以尝试减少极少量污染，微调产物比例；它不允许直接指定目标，请先确认实际成分。", FlowEventApi.RunColdTrapLoop)
        };
        private int index;
        private TextMeshProUGUI heading, body, number;
        private Button previous, next, play;
        private RectTransform demoArea;
        private PixelWindow demoWindow;
        private GameGridInventory grid;
        private bool hasGrid;
        public static void Open()
        {
            var root = new GameObject("FluxForgeManual", typeof(RectTransform), typeof(Image), typeof(DeviceManual));
            root.transform.SetParent(PaperUIManager.Instance.panel.transform.parent, false);
            var view = root.GetComponent<DeviceManual>();
            view.Build();
        }
        private void Build()
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f);
            rect.sizeDelta = new Vector2(780,680);
            GetComponent<Image>().color = new Color(.16f,.14f,.17f,.99f);
            heading = Text("Title", new Vector2(25,-20), new Vector2(620,45), 28);
            body = Text("Body", new Vector2(25,-80), new Vector2(730,200), 19);
            number = Text("Number", new Vector2(330,-625), new Vector2(110,35), 18);
            previous = Button("上一页", new Vector2(25,-620), () => Show(index-1));
            next = Button("下一页", new Vector2(620,-620), () => Show(index+1));
            play = Button("演示", new Vector2(325,-290), Play);
            Button("关闭", new Vector2(620,-20), () => Destroy(gameObject));
            demoArea = new GameObject("DemoArea",typeof(RectTransform)).GetComponent<RectTransform>();
            demoArea.SetParent(transform,false);
            demoArea.anchorMin = demoArea.anchorMax = new Vector2(.5f,.5f);
            demoArea.anchoredPosition = new Vector2(0,-120);
            Show(0);
        }
        private TextMeshProUGUI Text(string name, Vector2 pos, Vector2 size, float fontSize)
        {
            var text = new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(transform,false);
            text.font = PaperUIManager.Instance.body.font;
            text.fontSize = fontSize;
            text.color = new Color(.93f,.87f,.73f);
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0,1);
            rect.pivot = new Vector2(0,1); rect.anchoredPosition=pos; rect.sizeDelta=size;
            return text;
        }
        private Button Button(string label, Vector2 pos, UnityEngine.Events.UnityAction action)
        {
            var text=Text(label,pos,new Vector2(130,38),20);
            text.raycastTarget=true;
            var button=text.gameObject.AddComponent<Button>(); button.targetGraphic=text;
            button.onClick.AddListener(action); return button;
        }
        private void Show(int page)
        {
            ClearDemo(); index=page;
            heading.text=Pages[index].Title; body.text=Pages[index].Body;
            number.text=$"{index+1} / {Pages.Length}";
            previous.interactable=index>0; next.interactable=index<Pages.Length-1;
            play.gameObject.SetActive(Pages[index].Demo != null);
        }
        private void Play()
        {
            ClearDemo();
            grid=new GameGridInventory(12,6).SetMachineryStyle();
            demoWindow=new PixelWindow(false); demoWindow.Attach(grid);
            grid.rectTransform.SetParent(demoArea,false);
            grid.rectTransform.localScale=Vector3.one*2;
            grid.rectTransform.anchoredPosition=Vector2.zero;
            hasGrid=true;
            Pages[index].Demo(grid);
            grid.LockInv();
            grid.handler.gameObject.AddComponent<CanvasGroup>().blocksRaycasts=false;
        }
        private void ClearDemo()
        {
            if (!hasGrid) return;
            FlowEventApi.Cancel(grid);
            FlowDriver.RetireByGrid(grid);
            Destroy(grid.handler.gameObject);
            demoWindow.Destroy();
            hasGrid=false;
        }
        private void OnDestroy() => ClearDemo();
    }
}
''',encoding='utf-8')
import json
p=Path('assets/items.json'); d=json.loads(p.read_text(encoding='utf-8-sig')); d['items'].append(dict(key='fluxforge_device_manual',name='流源装置手册',desc='介绍流源装置与液体处理方法，附带演示。',value=10,itemTypes=['DOCUMENT'],attachments=['manual'])); p.write_text(json.dumps(d,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
p=Path('src/Story/Npcs/FlowEmitterMerchant/FlowEmitterMerchantStock.cs'); s=p.read_text(encoding='utf-8-sig').replace('new StoryStockRule(null, "flow_emitter", 2, true),','new StoryStockRule(null, "fluxforge_device_manual", 1, false),\n            new StoryStockRule(null, "flow_emitter", 2, true),').replace('private static readonly StoryStockRule[] RepeatRules =\n        {','private static readonly StoryStockRule[] RepeatRules =\n        {\n            new StoryStockRule(null, "fluxforge_device_manual", 1, false),'); p.write_text(s,encoding='utf-8')
