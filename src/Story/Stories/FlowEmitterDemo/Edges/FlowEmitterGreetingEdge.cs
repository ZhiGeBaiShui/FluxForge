using System.Threading.Tasks;

namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>入口边：NPC 开场白，然后抵达根节点。</summary>
    internal sealed class FlowEmitterGreetingEdge : StoryEdge
    {
        internal FlowEmitterGreetingEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StoryPlayer player)
        {
            player.SayNpc(FlowEmitterDemoTexts.Greeting);
            player.SayNpc(FlowEmitterDemoTexts.GreetingAsk);
            return Task.CompletedTask;
        }
    }
}
