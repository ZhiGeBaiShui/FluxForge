using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterSecondVisit
{
    /// <summary>第二次到访：介绍新货，并在手册说明后赠送手册。</summary>
    internal sealed class FlowEmitterSecondVisitGreetingEdge : StoryEdge
    {
        internal FlowEmitterSecondVisitGreetingEdge()
            : base(null, null)
        {
        }

        internal override void Execute(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterSecondVisitTexts.Greeting));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterSecondVisitTexts.NewDevice));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterSecondVisitTexts.ColdTrap));
            player.Do(FlowEmitterMerchantStock.GiveManual);
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterSecondVisitTexts.ExitLine));
        }
    }
}
