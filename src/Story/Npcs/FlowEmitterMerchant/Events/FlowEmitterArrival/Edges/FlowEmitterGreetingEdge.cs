using System.Threading.Tasks;
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

        internal override Task Execute(StorySequence player)
        {
            if (FlowEmitterMerchantProgress.VisitCount > 1)
            {
                player.SayNpc(FlowEmitterArrivalTexts.RetryInvitation);
                return Task.CompletedTask;
            }

            player.SayNpc(FlowEmitterArrivalTexts.Greeting1);
            player.SayNpc(FlowEmitterArrivalTexts.Greeting2);
            player.SayNpc(FlowEmitterArrivalTexts.Purpose1);
            player.SayNpc(FlowEmitterArrivalTexts.Purpose2);
            player.SayNpc(FlowEmitterArrivalTexts.Invitation1);
            player.SayNpc(FlowEmitterArrivalTexts.Invitation2);

            return Task.CompletedTask;
        }
    }
}

