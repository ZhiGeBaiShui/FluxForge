using System.Threading.Tasks;

namespace FluxForge.Story.FlowEmitterSecondVisit
{
    /// <summary>第二次到访的离场/跳过分支。</summary>
    internal sealed class FlowEmitterSecondVisitExitEdge : StoryEdge
    {
        internal FlowEmitterSecondVisitExitEdge(string label)
            : base(label, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(FlowEmitterSecondVisitTexts.ExitLine);
            return Task.CompletedTask;
        }
    }
}
