using FluxForge.Story.AcidBuyerArrival;
using FluxForge.Story.AcidBuyerRepeatVisit;

namespace FluxForge.Story.AcidBuyer
{
    /// <summary>薇拉的显式剧情目录。</summary>
    internal static class AcidBuyerPlan
    {
        private static readonly NpcStoryAssembly[] Stories =
        {
            new AcidBuyerArrivalAssembly(),
            new AcidBuyerRepeatVisitAssembly(),
        };

        internal static NpcStoryAssembly[] CreateStories()
        {
            return Stories;
        }
    }
}
