using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterSecondVisit
{
    /// <summary>第二次到访：介绍冷萃装置、演示、循环选项。</summary>
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
            StoryNode choiceNode = new StoryNode();
            StoryNode repeatNode = new StoryNode();

            FlowEmitterSecondVisitGreetingEdge greeting
                = new FlowEmitterSecondVisitGreetingEdge();
            FlowEmitterSecondVisitDemoEdge demo
                = new FlowEmitterSecondVisitDemoEdge();
            FlowEmitterSecondVisitExitEdge skip
                = new FlowEmitterSecondVisitExitEdge(
                    FlowEmitterSecondVisitTexts.OptionSkip);

            greeting.Connect(null, choiceNode);
            demo.Connect(choiceNode, repeatNode);
            skip.Connect(choiceNode, null);

            FlowEmitterSecondVisitDemoEdge demoAgain
                = new FlowEmitterSecondVisitDemoEdge(
                    FlowEmitterSecondVisitTexts.OptionAgain);
            FlowEmitterSecondVisitExitEdge exit
                = new FlowEmitterSecondVisitExitEdge(
                    FlowEmitterSecondVisitTexts.OptionDone);

            demoAgain.Connect(repeatNode, repeatNode);
            exit.Connect(repeatNode, null);

            Entry = greeting;
        }
    }
}
