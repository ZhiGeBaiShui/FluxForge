using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FluxForge.Manual
{
    /// <summary>在原版通用便条窗口中显示图片正文，窗口交互全部交给原版。</summary>
    internal static class PaperNoteContent
    {
        private static GameObject content;
        private static Image background;
        private static Sprite savedSprite;
        private static Color savedColor;

        internal static void Open(string imageName)
        {
            PaperUIManager manager = PaperUIManager.Instance;
            manager.OpenUI("", "", "");
            background = manager.panelImage;
            savedSprite = background.sprite;
            savedColor = background.color;
            background.sprite = null;
            background.color = new Color(0.9f, 0.85f, 0.75f, 1f);

            content = new GameObject("FluxForgeNoteContent", typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)content.transform;
            rect.SetParent(background.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16f, 16f);
            rect.offsetMax = new Vector2(-16f, -40f);
            Image image = content.GetComponent<Image>();
            image.sprite = PaperAssets.FromMod("fluxforge", imageName);
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        internal static void Clear()
        {
            if (content == null) return;
            content.SetActive(false);
            UnityEngine.Object.Destroy(content);
            content = null;
            background.sprite = savedSprite;
            background.color = savedColor;
            background = null;
            savedSprite = null;
        }
    }

    [HarmonyPatch(typeof(PaperUIManager), nameof(PaperUIManager.OpenUI))]
    internal static class PaperNoteClearsBeforeOpen
    {
        private static void Prefix() => PaperNoteContent.Clear();
    }

    [HarmonyPatch(typeof(PaperUIManager), nameof(PaperUIManager.CloseUI))]
    internal static class PaperNoteClearsBeforeClose
    {
        private static void Prefix() => PaperNoteContent.Clear();
    }
}
