namespace FluxForge.Story.FlowEmitterArrival
{
    internal sealed class FlowEmitterStarterGiftEdge : StoryEdge
    {
        internal FlowEmitterStarterGiftEdge() : base("story.oya.option.accept", null) { }
        internal override void Execute(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Gift1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Gift2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Gift3));
            player.Do(FlowEmitterArrivalMaterials.GiveStarterKit);
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Instructions1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Instructions2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Instructions3));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Instructions4));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Goal1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Goal2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Goal3));
        }
    }
}
