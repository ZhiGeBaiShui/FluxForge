using System.Threading.Tasks;
using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterArrival
{
    /// <summary>首次到访：开场、介绍货品、上架首批库存，然后结束剧情。</summary>
    internal sealed class FlowEmitterGreetingEdge : StoryEdge
    {
        internal FlowEmitterGreetingEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(FlowEmitterArrivalTexts.Greeting);
            player.SayNpc(FlowEmitterArrivalTexts.GreetingAsk);
            player.SayNpc(FlowEmitterArrivalTexts.DevicePrompt);

            player.Do(delegate
            {
                FlowEmitterMerchantStock.Place();
            });

            player.SayNpc(FlowEmitterArrivalTexts.FirstVisitDiscount);
            player.SayNpc(FlowEmitterArrivalTexts.DiscountSameFunction);
            player.SayNpc(FlowEmitterArrivalTexts.DiscountOldLook);

            return Task.CompletedTask;
        }
    }
}

