from pathlib import Path
p=Path('src/ItemIntegration/Manual/DeviceManual.cs'); s=p.read_text(encoding='utf-8'); s=s.replace('            current = view;','            var drag = root.AddComponent<UIDragHandler>();\n            drag.id = "fluxforge_device_manual";\n            drag.moveFrontObjects = new List<GameObject> { root };\n            current = view;'); a=s.index('            var text=Text(label,pos'); b=s.index('\n        private void Show',a); s=s[:a]+'''            var target = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            target.transform.SetParent(transform, false);
            var rect = (RectTransform)target.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(130, 38);
            var image = target.GetComponent<Image>();
            image.color = new Color(.35f, .28f, .22f, .25f);
            image.raycastTarget = true;
            var button = target.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            var text = Text(label, Vector2.zero, new Vector2(130, 38), 20);
            text.transform.SetParent(target.transform, false);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }
''' + s[b:]; p.write_text(s,encoding='utf-8')
