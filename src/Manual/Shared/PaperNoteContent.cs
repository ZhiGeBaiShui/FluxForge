using UnityEngine;

namespace FluxForge.Manual
{
    /// <summary>复用原版图文便条；文字由PaperUI的loc节点解析，图片只负责装置示意。</summary>
    internal static class PaperNoteContent
    {
        private const string BookId = "fluxforge:starter_note";

        static PaperNoteContent()
        {
            // 原版在首次打开及重建后调用OnOpened，缩放重建时也会恢复发射口朝向。
            PaperBookRegistry.OnOpened(BookId, ConfigureDiagram);
        }

        internal static void Open()
        {
            PaperBookRegistry.Open(BookId);
        }

        internal static void Clear()
        {
            PaperBookRegistry.Close(BookId);
        }

        private static void ConfigureDiagram(PaperUI page)
        {
            OrientEmitter(page, "emitter", 116f, 500f, 270f);
            OrientEmitter(page, "receiver", 604f, 500f, 90f);
        }

        private static void OrientEmitter(PaperUI page, string id, float x, float y, float angle)
        {
            RectTransform rect = page.Get(id).image.rectTransform;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.localEulerAngles = new Vector3(0f, 0f, angle);
        }
    }
}
