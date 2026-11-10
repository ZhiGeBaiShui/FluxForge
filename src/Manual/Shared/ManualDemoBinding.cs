using System;
using UnityEngine;
using UnityEngine.UI;

namespace FluxForge.Manual
{
    internal static class ManualDemoBinding
    {
        internal static void Bind(
            GameObject page,
            string demoName,
            Func<GameGridInventory, bool> run)
        {
            Transform demo = page.transform.Find(demoName);
            ManualDemoPage demoPage = demo.gameObject.AddComponent<ManualDemoPage>();
            demoPage.Configure(
                demo.Find("GridArea").GetComponent<RectTransform>(),
                demo.Find("GridArea/manual.demo.placeholder").gameObject,
                run);
            demo.Find("PlayButton").GetComponent<Button>().onClick.AddListener(demoPage.Play);
        }

    }
}
