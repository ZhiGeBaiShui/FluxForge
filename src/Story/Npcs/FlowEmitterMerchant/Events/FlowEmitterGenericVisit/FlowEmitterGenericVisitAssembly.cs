using System.Threading.Tasks;

namespace FluxForge.Story.FlowEmitterGenericVisit
{
    /// <summary>没有专用剧情时的通用到访开场。</summary>
    internal sealed class FlowEmitterGenericVisitAssembly : NpcStoryAssembly
    {
        internal override StoryEdge Entry { get; }

        internal override StoryPhase Phase
        {
            get { return StoryPhase.GenericVisit; }
        }

        internal override bool CanPlay()
        {
            return true;
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
