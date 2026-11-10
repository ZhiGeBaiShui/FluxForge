using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterGenericVisit
{
    /// <summary>没有专用剧情时的通用到访开场。</summary>
    internal sealed class FlowEmitterGenericVisitAssembly : StoryAssembly
    {
        internal override StoryEdge Entry { get; }

        internal override void Complete()
        {
            FlowEmitterMerchantStock.Refresh();
        }

        internal FlowEmitterGenericVisitAssembly()
        {
            FlowEmitterGenericVisitEdge edge
                = new FlowEmitterGenericVisitEdge();
            edge.Connect(null, null);
            Entry = edge;
        }
    }
}
