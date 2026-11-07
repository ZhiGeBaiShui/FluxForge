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
            player.SayNpc("我多带了一些“小玩意”，看看有没有你想要的吧。");
            return Task.CompletedTask;
        }
    }
}
