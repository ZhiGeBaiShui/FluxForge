using System.Threading.Tasks;
using FluxForge.Events;
using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterSecondVisit
{
    /// <summary>“可以”分支：播放冷萃装置事件，再说明自动循环。</summary>
    internal sealed class FlowEmitterSecondVisitDemoEdge : StoryEdge
    {
        internal FlowEmitterSecondVisitDemoEdge()
            : this(FlowEmitterSecondVisitTexts.OptionOk)
        {
        }

        internal FlowEmitterSecondVisitDemoEdge(string label)
            : base(label, "flow_cold_trap")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(FlowEmitterSecondVisitTexts.DemoIntro);

            player.StartFlow(
                FlowEventApi.RunColdTrapLoop,
                FlowEmitterMerchantProgress.MarkColdTrapDemo);

            player.SayNpc(FlowEmitterSecondVisitTexts.AfterDemo);
            player.SayNpc(FlowEmitterSecondVisitTexts.LoopExplain);
            player.SayNpc(FlowEmitterSecondVisitTexts.LoopBenefit);
            player.SayNpc(FlowEmitterSecondVisitTexts.RepeatPrompt);

            return Task.CompletedTask;
        }
    }
}
