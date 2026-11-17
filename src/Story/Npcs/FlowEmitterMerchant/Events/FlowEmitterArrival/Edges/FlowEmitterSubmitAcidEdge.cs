namespace FluxForge.Story.FlowEmitterArrival
{
    internal sealed class FlowEmitterSubmitAcidEdge : StoryEdge
    {
        internal bool Passed { get; private set; }
        internal FlowEmitterSubmitAcidEdge() : base("story.oya.option.submit", null) { }
        internal override void Execute(StorySequence player)
        {
            AcidSubmission result = FlowEmitterArrivalMaterials.CheckAcid();
            Passed = result == AcidSubmission.Passed;
            if (!Passed)
            {
                if (result == AcidSubmission.Missing)
                {
                    player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Missing1));
                    player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Missing2));
                }
                else
                {
                    player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.TooDilute1));
                    player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.TooDilute2));
                }
                return;
            }
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Success1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Success2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Success3));
            FlowEmitterArrivalDeparture.Append(player);
        }
    }
}
