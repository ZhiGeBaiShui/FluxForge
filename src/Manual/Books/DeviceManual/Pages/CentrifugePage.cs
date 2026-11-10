using FluxForge.Events;
using UnityEngine;

namespace FluxForge.Manual
{
    internal static class CentrifugePage
    {
        internal static GameObject Bind(Transform pagesRoot)
        {
            GameObject page = pagesRoot.Find("Page_centrifuge").gameObject;
            ManualDemoBinding.Bind(page, "Demo_centrifuge", FlowEventApi.RunCentrifuge);
            return page;
        }
    }
}
