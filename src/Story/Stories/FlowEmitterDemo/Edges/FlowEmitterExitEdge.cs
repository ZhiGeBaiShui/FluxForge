using System.Threading.Tasks;

namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>玩家选择都知道了后的结束边。</summary>
    internal sealed class FlowEmitterExitEdge : StoryEdge
    {
        internal FlowEmitterExitEdge()
            : base(FlowEmitterDemoTexts.NoDemo, "fluxforge_test_water")
        {
        }

        internal override string IntroLine
        {
            get { return FlowEmitterDemoTexts.Farewell; }
        }

        internal override Task Execute(StoryPlayer player)
        {
            return Task.CompletedTask;
        }
    }
}
