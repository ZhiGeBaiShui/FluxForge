using System.Threading.Tasks;
using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>玩家选择都知道了后的结束边。</summary>
    internal sealed class FlowEmitterExitEdge : StoryEdge
    {
        internal FlowEmitterExitEdge(string label)
            : base(label, "fluxforge_test_water")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.Do(delegate
            {
                FlowEmitterMerchantStock.Place();
            });

            player.SayNpc(FlowEmitterDemoTexts.FirstVisitDiscount);
            player.SayNpc(FlowEmitterDemoTexts.DiscountSameFunction);
            player.SayNpc(FlowEmitterDemoTexts.DiscountOldLook);

            return Task.CompletedTask;
        }
    }
}
