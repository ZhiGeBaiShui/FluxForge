using System.Threading.Tasks;

namespace FluxForge.Story.FlowEmitterSecondVisit
{
    /// <summary>第二次到访的开场白，然后进入玩家选项。</summary>
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
            player.SayNpc(FlowEmitterSecondVisitTexts.CanDemo);
            return Task.CompletedTask;
        }
    }
}
