using UnityEngine;

namespace FluxForge.Mod
{
    public sealed class DeviceManual : MonoBehaviour
    {
        private static readonly ManualPage[] Pages =
        {
            new OverviewManualPage(),
            new EmitterManualPage(),
            new FilterManualPage(),
            new ColdTrapManualPage()
        };
        private static DeviceManual current;
        private MultiPageUIManager manager;
        private GameObject book;

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
            for (int i = 0; i < Pages.Length; i++)
            {
                var page = new GameObject("Page" + (i + 1), typeof(RectTransform));
                page.SetActive(false);
                page.transform.SetParent(book.transform, false);
                CopyRect((RectTransform)source.transform.Find("Page2"), (RectTransform)page.transform);
                Pages[i].Build((RectTransform)page.transform);
            }
            manager.books.Add(book);
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
            current = null;
            Destroy(closing.book);
        }
    }

}
