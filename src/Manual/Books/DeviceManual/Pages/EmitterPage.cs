using FluxForge.Events;
using UnityEngine;
using UnityEngine.UI;

namespace FluxForge.Manual
{
    internal static class EmitterPage
    {
        internal static GameObject Bind(Transform pagesRoot)
        {
            GameObject page = pagesRoot.Find("Page_emitter").gameObject;
            Transform demo = page.transform.Find("Demo_boundary_emitter");
            ManualDemoPage demoPage = demo.gameObject.AddComponent<ManualDemoPage>();
            demoPage.Configure(
                demo.Find("GridArea").GetComponent<RectTransform>(),
                demo.Find("GridArea/manual.demo.placeholder").gameObject,
                FlowEventApi.RunBoundaryEmitter);
            demo.Find("PlayButton1").GetComponent<Button>()
                .onClick.AddListener(demoPage.PlayBoundaryEmitter);
            demo.Find("PlayButton2").GetComponent<Button>()
                .onClick.AddListener(demoPage.PlayTwoEmitterRelay);
            return page;
        }

    }
}
