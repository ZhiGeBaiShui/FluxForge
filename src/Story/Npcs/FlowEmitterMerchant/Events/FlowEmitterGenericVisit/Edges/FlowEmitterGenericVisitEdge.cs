using System.Threading.Tasks;

namespace FluxForge.Story.FlowEmitterGenericVisit
{
    /// <summary>通用到访的两句招呼。</summary>
    internal sealed class FlowEmitterGenericVisitEdge : StoryEdge
    {
        internal FlowEmitterGenericVisitEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(FlowEmitterGenericVisitTexts.Greeting);
            player.SayNpc(FlowEmitterGenericVisitTexts.MoreItems);
            return Task.CompletedTask;
        }
    }
}
