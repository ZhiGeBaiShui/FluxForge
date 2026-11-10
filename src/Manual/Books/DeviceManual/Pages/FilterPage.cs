using FluxForge.Events;
using UnityEngine;

namespace FluxForge.Manual
{
    internal static class FilterPage
    {
        internal static GameObject Bind(Transform pagesRoot)
        {
            GameObject page = pagesRoot.Find("Page_filter").gameObject;
            ManualDemoBinding.Bind(page, "Demo_filter_loop", FlowEventApi.RunFilterLoop);
            return page;
        }
    }
}
