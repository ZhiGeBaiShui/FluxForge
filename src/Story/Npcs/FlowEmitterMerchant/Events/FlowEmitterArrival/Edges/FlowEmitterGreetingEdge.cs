using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterArrival
{
    /// <summary>首次到访：介绍用途并邀请玩家尝试。</summary>
    internal sealed class FlowEmitterGreetingEdge : StoryEdge
    {
        internal FlowEmitterGreetingEdge()
            : base(null, null)
        {
        }

        internal override void Execute(StorySequence player)
        {
            if (FlowEmitterMerchantProgress.VisitCount > 1)
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.RetryInvitation));
                return;
            }

            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Greeting1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Greeting2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Purpose1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Purpose2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Invitation1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Invitation2));

        }
    }
}

