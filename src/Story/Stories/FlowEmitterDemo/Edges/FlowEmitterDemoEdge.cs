using System.Threading.Tasks;
using FluxForge.Events;

namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>玩家选择流源发射机后的自动演示，完成后回到根节点。</summary>
    internal sealed class FlowEmitterDemoEdge : StoryEdge
    {
        internal FlowEmitterDemoEdge()
            : base(FlowEmitterDemoTexts.Emitter, "flow_emitter")
        {
        }

        internal override async Task Execute(StoryPlayer player)
        {
            player.SayNpc(FlowEmitterDemoTexts.EmitterIntro);
            await player.RunFlow(FlowEventApi.RunBoundaryEmitter);

            player.SayNpc(FlowEmitterDemoTexts.RelayIntro);
            await player.RunFlow(FlowEventApi.RunTwoEmitterRelay);
        }
    }
}
