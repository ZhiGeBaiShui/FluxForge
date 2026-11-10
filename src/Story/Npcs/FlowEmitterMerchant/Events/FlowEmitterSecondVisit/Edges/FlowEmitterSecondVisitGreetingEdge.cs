using System.Threading.Tasks;
using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterSecondVisit
{
    /// <summary>第二次到访的纯对话开场。</summary>
    internal sealed class FlowEmitterSecondVisitGreetingEdge : StoryEdge
    {
        internal FlowEmitterSecondVisitGreetingEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(FlowEmitterSecondVisitTexts.Greeting);
            player.SayNpc(FlowEmitterSecondVisitTexts.NewDevice);
            player.SayNpc(FlowEmitterSecondVisitTexts.ColdTrap);
            player.Do(FlowEmitterMerchantStock.GiveManual);
            player.SayNpc(FlowEmitterSecondVisitTexts.ExitLine);
            return Task.CompletedTask;
        }
    }
}
