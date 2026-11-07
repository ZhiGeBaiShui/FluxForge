using System.Threading.Tasks;
using FluxForge.Events;
using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>玩家选择流源发射机后的自动演示，完成后回到根节点。</summary>
    internal sealed class FlowEmitterDemoEdge : StoryEdge
    {
        internal FlowEmitterDemoEdge()
            : base(FlowEmitterDemoTexts.Emitter, "flow_emitter")
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
            player.SayNpc(FlowEmitterDemoTexts.EmitterIntro);
            player.SayNpc(FlowEmitterDemoTexts.EmitterFlowName);

            player.StartFlow(FlowEventApi.RunBoundaryEmitter);

            player.SayNpc(FlowEmitterDemoTexts.RelayIntro);

            player.StartFlow(
                FlowEventApi.RunTwoEmitterRelay,
                FlowEmitterMerchantProgress.MarkEmitterDemo);

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
