using System.Threading.Tasks;

namespace FluxForge.Story.FlowEmitterGenericVisit
{
    /// <summary>通用到访的两句招呼。</summary>
    internal sealed class FlowEmitterGenericVisitEdge : StoryEdge
    {
        internal FlowEmitterGenericVisitEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc("你好，我又来了。");
            player.SayNpc("这次多带了些小玩意儿，看看有没有你喜欢的？");
            return Task.CompletedTask;
        }
    }
}
