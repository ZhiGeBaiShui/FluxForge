using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterGenericVisit
{
    /// <summary>没有专用剧情时播放通用开场，正常结束时按进度上货。</summary>
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
