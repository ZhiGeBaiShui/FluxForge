using System.Threading.Tasks;
using FluxForge.Events;
using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>净化装置演示：事件在后台播放，NPC 同时继续说明。</summary>
    internal sealed class FlowEmitterFilterEdge : StoryEdge
    {
        internal FlowEmitterFilterEdge()
            : base(FlowEmitterDemoTexts.Filter, "flow_filter")
        {
        }

        internal override bool RequiresEmptyGrid
        {
            get { return true; }
        }

        internal override bool LocksGrid
        {
            get { return true; }
        }

        internal override string GridBusyMessage
        {
            get { return FlowEmitterDemoTexts.GridBusy; }
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(FlowEmitterDemoTexts.FilterIntro);
            player.SayNpc(FlowEmitterDemoTexts.FilterIntroLimit);
            player.SayNpc(FlowEmitterDemoTexts.FilterManual);
            player.SayNpc(FlowEmitterDemoTexts.FilterManualAction);

            player.StartFlow(
                FlowEventApi.RunFilterLoop,
                FlowEmitterMerchantProgress.MarkFilterDemo);

            player.SayNpc(FlowEmitterDemoTexts.FilterTemperature);
            player.SayNpc(FlowEmitterDemoTexts.FilterTemperatureLimit);
            player.SayNpc(FlowEmitterDemoTexts.FilterLoss);
            player.SayNpc(FlowEmitterDemoTexts.FilterLossDetail);
            player.SayNpc(FlowEmitterDemoTexts.FilterLossControl);
            player.SayNpc(FlowEmitterDemoTexts.FilterSimple);
            player.SayNpc(FlowEmitterDemoTexts.FilterSimpleAction);
            player.SayNpc(FlowEmitterDemoTexts.FilterSpeed);
            player.SayNpc(FlowEmitterDemoTexts.FilterSpeedUnknown);

            if (!FlowEmitterMerchantProgress.HasSoundHintSeen)
            {
                player.SayNpc(FlowEmitterDemoTexts.DeviceSoundHint);
                player.SayNpc(FlowEmitterDemoTexts.NoSoundHint);
                player.Do(delegate
                {
                    FlowEmitterMerchantProgress.MarkSoundHintSeen();
                });
            }

            player.SayNpc(FlowEmitterDemoTexts.RepeatPrompt);

            return Task.CompletedTask;
        }
    }
}
