using UnityEngine;

namespace FluxForge.Manual
{
    internal static class OverviewPage
    {
        internal static GameObject Bind(Transform pagesRoot)
        {
            GameObject page = pagesRoot.Find("Page_overview").gameObject;
            return page;
        }
    }
}
