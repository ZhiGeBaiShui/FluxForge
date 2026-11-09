using FluxForge.Story.FlowEmitterArrival;
using FluxForge.Story.FlowEmitterFarewell;
using FluxForge.Story.FlowEmitterGenericVisit;
using FluxForge.Story.FlowEmitterSecondVisit;

namespace FluxForge.Story.FlowEmitterMerchant
{
    /// <summary>本 NPC 的显式剧情目录。新增剧情只需在这里登记装载类。</summary>
    internal static class FlowEmitterMerchantPlan
    {
        private static readonly NpcStoryAssembly[] Stories =
        {
            new FlowEmitterArrivalAssembly(),
            new FlowEmitterSecondVisitAssembly(),
            new FlowEmitterGenericVisitAssembly(),
            new FlowEmitterFarewellAssembly(),
        };

        internal static NpcStoryAssembly[] CreateStories()
        {
            return Stories;
        }
    }
}

