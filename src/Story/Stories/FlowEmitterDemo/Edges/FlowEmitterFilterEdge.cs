using System.Threading.Tasks;
using FluxForge.Events;

namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>净化装置演示：事件在后台播放，NPC 同时继续说明。</summary>
    internal sealed class FlowEmitterFilterEdge : StoryEdge
    {
        internal FlowEmitterFilterEdge()
            : base(FlowEmitterDemoTexts.Filter, "flow_filter")
        {
        }

        internal override async Task Execute(StoryPlayer player)
        {
            Task filter = player.StartFlow(FlowEventApi.RunFilterLoop);

            player.SayNpc(FlowEmitterDemoTexts.FilterIntro);
            player.SayNpc(FlowEmitterDemoTexts.FilterIntroLimit);
            player.SayNpc(FlowEmitterDemoTexts.FilterManual);
            player.SayNpc(FlowEmitterDemoTexts.FilterManualAction);
            player.SayNpc(FlowEmitterDemoTexts.FilterTemperature);
            player.SayNpc(FlowEmitterDemoTexts.FilterTemperatureLimit);
            player.SayNpc(FlowEmitterDemoTexts.FilterLoss);
            player.SayNpc(FlowEmitterDemoTexts.FilterLossDetail);
            player.SayNpc(FlowEmitterDemoTexts.FilterLossControl);
            player.SayNpc(FlowEmitterDemoTexts.FilterSimple);
            player.SayNpc(FlowEmitterDemoTexts.FilterSimpleAction);
            player.SayNpc(FlowEmitterDemoTexts.FilterSpeed);
            player.SayNpc(FlowEmitterDemoTexts.FilterSpeedUnknown);

            await filter;
        }
    }
}
