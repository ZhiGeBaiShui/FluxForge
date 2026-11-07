using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>
    /// 流源发射机剧情 A 的装配类。
    /// 只创建边并设置每条边的起点和终点。
    /// </summary>
    internal sealed class FlowEmitterDemoAssembly : NpcStoryAssembly
    {
        internal override StoryEdge Entry { get; }

        internal override StoryPhase Phase
        {
            get { return StoryPhase.Arrival; }
        }

        internal override bool CanPlay()
        {
            return !FlowEmitterMerchantProgress.HasArrivalSeen;
        }

        internal override void Complete()
        {
            FlowEmitterMerchantProgress.MarkArrivalSeen();
            FlowEmitterMerchantProgress.MarkFirstStory();
        }

        internal FlowEmitterDemoAssembly()
        {
            StoryNode greetingNode = new StoryNode();
            StoryNode root = new StoryNode();
            StoryNode repeatNode = new StoryNode();

            FlowEmitterGreetingEdge greeting = new FlowEmitterGreetingEdge();
            FlowEmitterAskEdge ask = new FlowEmitterAskEdge();
            FlowEmitterDemoEdge demo = new FlowEmitterDemoEdge();
            FlowEmitterFilterEdge filter = new FlowEmitterFilterEdge();
            FlowEmitterExitEdge exit = new FlowEmitterExitEdge(
                FlowEmitterDemoTexts.NoDemo);

            greeting.Connect(null, greetingNode);
            ask.Connect(greetingNode, root);
            demo.Connect(root, repeatNode);
            filter.Connect(root, repeatNode);
            exit.Connect(root, null);

            FlowEmitterDemoEdge demoAgain = new FlowEmitterDemoEdge();
            FlowEmitterFilterEdge filterAgain = new FlowEmitterFilterEdge();
            FlowEmitterExitEdge exitAgain = new FlowEmitterExitEdge(
                FlowEmitterDemoTexts.AllKnown);

            demoAgain.Connect(repeatNode, repeatNode);
            filterAgain.Connect(repeatNode, repeatNode);
            exitAgain.Connect(repeatNode, null);

            Entry = greeting;
        }
    }
}
