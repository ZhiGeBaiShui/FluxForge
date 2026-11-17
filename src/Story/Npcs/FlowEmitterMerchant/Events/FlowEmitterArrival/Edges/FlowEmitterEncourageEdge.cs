namespace FluxForge.Story.FlowEmitterArrival
{
    internal sealed class FlowEmitterEncourageEdge : StoryEdge
    {
        internal FlowEmitterEncourageEdge() : base("story.oya.option.failed", null) { }
        internal override void Execute(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Encourage1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Encourage2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Encourage3));
            FlowEmitterArrivalDeparture.Append(player);
        }
    }
}
