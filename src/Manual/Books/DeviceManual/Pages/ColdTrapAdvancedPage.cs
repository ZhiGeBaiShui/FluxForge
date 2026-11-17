using FluxForge.Events;
using UnityEngine;

namespace FluxForge.Manual
{
    internal static class ColdTrapAdvancedPage
    {
        internal static GameObject Bind(Transform pagesRoot)
        {
            GameObject page = pagesRoot.Find("Page_coldtrap_manual").gameObject;
            ManualDemoBinding.Bind(page, "Demo_coldtrap_manual_advanced",
                FlowEventApi.RunColdTrapAdvancedLoop, 11, 5);
            return page;
        }

        internal static GameObject BindAutomation(Transform pagesRoot)
        {
            GameObject page = pagesRoot.Find("Page_coldtrap_auto").gameObject;
            ManualDemoBinding.Bind(page, "Demo_coldtrap_auto_advanced",
                FlowEventApi.RunColdTrapAutomation, 9, 5);
            return page;
        }
    }
}
