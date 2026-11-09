using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterSecondVisit
{
    /// <summary>第二次到访：介绍新货，不再演示冷萃装置。</summary>
    internal sealed class FlowEmitterSecondVisitAssembly : NpcStoryAssembly
    {
        internal override StoryEdge Entry { get; }

        internal override StoryPhase Phase
        {
            get { return StoryPhase.SecondVisit; }
        }

        internal override bool CanPlay()
        {
            return FlowEmitterMerchantProgress.HasFirstStory
                && !FlowEmitterMerchantProgress.HasSecondVisit;
        }

        internal override void Complete()
        {
            FlowEmitterMerchantProgress.MarkSecondVisit();
        }

        internal FlowEmitterSecondVisitAssembly()
        {
            FlowEmitterSecondVisitGreetingEdge greeting
                = new FlowEmitterSecondVisitGreetingEdge();

            greeting.Connect(null, null);
            Entry = greeting;
        }
    }
}
