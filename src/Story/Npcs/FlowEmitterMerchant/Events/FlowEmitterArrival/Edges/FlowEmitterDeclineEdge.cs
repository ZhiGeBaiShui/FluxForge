namespace FluxForge.Story.FlowEmitterArrival
{
    internal sealed class FlowEmitterDeclineEdge : StoryEdge
    {
        internal FlowEmitterDeclineEdge() : base("story.oya.option.decline", null) { }
        internal override void Execute(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Decline));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.DeclineFarewell1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.DeclineFarewell2));
        }
    }
}
