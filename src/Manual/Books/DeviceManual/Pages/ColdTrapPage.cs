using FluxForge.Events;
using UnityEngine;

namespace FluxForge.Manual
{
    internal static class ColdTrapPage
    {
        internal static GameObject Bind(Transform pagesRoot)
        {
            GameObject page = pagesRoot.Find("Page_coldtrap").gameObject;
            ManualDemoBinding.Bind(page, "Demo_cold_trap_loop", FlowEventApi.RunColdTrapLoop);
            return page;
        }
    }
}
