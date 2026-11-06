using System.Threading.Tasks;

namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>玩家询问装置用途后，NPC 表示可以演示，然后进入装置选择。</summary>
    internal sealed class FlowEmitterAskEdge : StoryEdge
    {
        internal FlowEmitterAskEdge()
            : base(FlowEmitterDemoTexts.Question, "question")
        {
        }

        internal override Task Execute(StoryPlayer player)
        {
            player.SayNpc(FlowEmitterDemoTexts.CanDemo);
            return Task.CompletedTask;
        }
    }
}
