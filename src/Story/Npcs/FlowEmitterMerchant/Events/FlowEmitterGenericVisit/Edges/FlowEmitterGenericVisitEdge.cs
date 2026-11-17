
namespace FluxForge.Story.FlowEmitterGenericVisit
{
    /// <summary>通用到访的两句招呼。</summary>
    internal sealed class FlowEmitterGenericVisitEdge : StoryEdge
    {
        internal FlowEmitterGenericVisitEdge()
            : base(null, null)
        {
        }

        internal override void Execute(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterGenericVisitTexts.Greeting));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterGenericVisitTexts.MoreItems));
        }
    }
}
