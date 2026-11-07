using FluxForge.Story.FlowEmitterDemo;
using FluxForge.Story.FlowEmitterFarewell;

namespace FluxForge.Story.FlowEmitterMerchant
{
    /// <summary>本 NPC 的显式剧情目录。新增剧情只需在这里登记装载类。</summary>
    internal static class FlowEmitterMerchantPlan
    {
        internal static NpcStoryAssembly[] CreateStories()
        {
            return new NpcStoryAssembly[]
            {
                new FlowEmitterDemoAssembly(),
                new FlowEmitterFarewellAssembly(),
            };
        }
    }
}
