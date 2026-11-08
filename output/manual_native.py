from pathlib import Path
p=Path('src/ItemIntegration/Manual/DeviceManual.cs'); s=p.read_text(encoding='utf-8'); a=s.index('        private static DeviceManual current;'); s=s[:a]+'''        private static DeviceManual current;
        private MultiPageUIManager manager;
        private GameObject book;
        private ManualDemoPage[] pages;

        public static void Open()
        {
            if (current == null) CreateBook();
            current.manager.OpenUI(current.manager.books.IndexOf(current.book));
            UIFamilyLayer.BringPanelsToFront();
        }

        private static void CreateBook()
        {
            var manager = MultiPageUIManager.Instance;
            var source = manager.books[3];
            var book = new GameObject("FluxForgeDeviceGuide", typeof(RectTransform));
            book.SetActive(false);
            book.transform.SetParent(source.transform.parent, false);
            CopyRect((RectTransform)source.transform, (RectTransform)book.transform);
            var view = book.AddComponent<DeviceManual>();
            current = view;
            view.manager = manager;
            view.book = book;
            view.pages = new ManualDemoPage[Pages.Length];
            for (int i = 0; i < Pages.Length; i++)
                view.pages[i] = view.CreatePage(source.transform.Find("Page2").gameObject, Pages[i]);
            manager.books.Add(book);
        }

        private ManualDemoPage CreatePage(GameObject template, Page definition)
        {
            var page = Instantiate(template, book.transform, false);
            page.name = "FluxForge_" + definition.Title;
            page.SetActive(false);
            foreach (var localizer in page.GetComponentsInChildren<TMPLocalizer>(true))
                localizer.ChangeFontOnly = true;
            foreach (var localized in page.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true))
            {
                localized.enabled = false;
                localized.OnUpdateString.RemoveAllListeners();
            }
            page.transform.Find("Title (3)").GetComponent<TextMeshProUGUI>().text = "流源装置手册";
            page.transform.Find("Title (4)").GetComponent<TextMeshProUGUI>().text = definition.Title;
            var section = page.transform.Find("Section1");
            section.Find("SectionDivider (7)/Text (TMP) (2)").GetComponent<TextMeshProUGUI>().text = "使用说明";
            var body = section.Find("Text (TMP) (2)").GetComponent<TextMeshProUGUI>();
            body.text = definition.Body;
            body.enableAutoSizing = true;
            body.fontSizeMin = 10;
            foreach (Transform child in section)
                if (child.name.StartsWith("Image")) child.gameObject.SetActive(false);
            foreach (Transform child in page.transform)
                if (child.name != "Title (3)" && child.name != "Title (4)" && child.name != "Section1" && child.name != "Section2")
                    child.gameObject.SetActive(false);
            var demoArea = (RectTransform)page.transform.Find("Section2");
            foreach (Transform child in demoArea) child.gameObject.SetActive(false);
            var demo = page.AddComponent<ManualDemoPage>();
            demo.Configure(demoArea, definition.Demo);
            if (definition.Demo != null)
            {
                var buttonRoot = Instantiate(manager.buttonRight, demoArea, false);
                buttonRoot.name = "FluxForgeDemoButton";
                var button = buttonRoot.GetComponent<UnityEngine.UI.Button>();
                button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
                button.onClick.AddListener(demo.Play);
                var rect = (RectTransform)buttonRoot.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
                rect.anchoredPosition = Vector2.zero;
                var caption = Instantiate(section.Find("SectionDivider (7)/Text (TMP) (2)").gameObject, buttonRoot.transform, false);
                caption.GetComponent<TextMeshProUGUI>().text = "演示";
                caption.GetComponent<TextMeshProUGUI>().raycastTarget = false;
                buttonRoot.SetActive(true);
            }
            return demo;
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin; target.anchorMax = source.anchorMax;
            target.pivot = source.pivot; target.sizeDelta = source.sizeDelta;
            target.anchoredPosition = source.anchoredPosition;
            target.localScale = source.localScale;
        }

        public static void CloseCurrent()
        {
            if (current == null) return;
            var closing = current;
            if (closing.book.activeSelf) closing.manager.CloseUI();
            closing.manager.books.Remove(closing.book);
            foreach (var page in closing.pages) page.ClearDemo();
            current = null;
            Destroy(closing.book);
        }
    }

    public sealed class ManualDemoPage : MonoBehaviour
    {
        private RectTransform area;
        private Func<GameGridInventory, bool> run;
        private GameGridInventory grid;
        private PixelWindow window;
        private bool hasGrid;
        internal void Configure(RectTransform area, Func<GameGridInventory, bool> run)
        { this.area = area; this.run = run; }
        public void Play()
        {
            ClearDemo();
            grid = new GameGridInventory(12,6).SetMachineryStyle();
            window = new PixelWindow(false); window.Attach(grid);
            grid.rectTransform.SetParent(area, false);
            window.handler.gameObject.SetActive(false);
            grid.rectTransform.anchorMin = grid.rectTransform.anchorMax = new Vector2(.5f,.5f);
            grid.rectTransform.anchoredPosition = Vector2.zero;
            hasGrid = true;
            if (!run(grid)) throw new InvalidOperationException("手册演示启动失败");
            grid.LockInv();
            grid.handler.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
        }
        internal void ClearDemo()
        {
            if (!hasGrid) return;
            FlowEventApi.Cancel(grid);
            FlowDriver.RetireByGrid(grid);
            Destroy(grid.handler.gameObject);
            window.Destroy();
            hasGrid = false;
        }
        private void OnDisable() => ClearDemo();
        private void OnDestroy() => ClearDemo();
    }
}
''';p.write_text(s,encoding='utf-8')
