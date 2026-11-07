namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>
    /// 流源发射机剧情 A 的装配类。
    /// 只创建边并设置每条边的起点和终点。
    /// </summary>
    internal sealed class FlowEmitterDemoAssembly
    {
        internal StoryEdge Entry { get; }

        internal FlowEmitterDemoAssembly()
        {
            StoryNode greetingNode
                = new StoryNode(FlowEmitterDemoTexts.DevicePrompt);
            StoryNode root = new StoryNode(FlowEmitterDemoTexts.CanDemo);
            StoryNode repeatNode
                = new StoryNode(FlowEmitterDemoTexts.RepeatPrompt);

            FlowEmitterGreetingEdge greeting = new FlowEmitterGreetingEdge();
            FlowEmitterAskEdge ask = new FlowEmitterAskEdge();
            FlowEmitterDemoEdge demo = new FlowEmitterDemoEdge();
            FlowEmitterFilterEdge filter = new FlowEmitterFilterEdge();
            FlowEmitterExitEdge exit = new FlowEmitterExitEdge();

            greeting.Connect(null, greetingNode);
            ask.Connect(greetingNode, root);
            demo.Connect(root, repeatNode);
            filter.Connect(root, repeatNode);
            exit.Connect(root, null);

            FlowEmitterDemoEdge demoAgain = new FlowEmitterDemoEdge();
            FlowEmitterFilterEdge filterAgain = new FlowEmitterFilterEdge();
            FlowEmitterExitEdge exitAgain = new FlowEmitterExitEdge();

            demoAgain.Connect(repeatNode, repeatNode);
            filterAgain.Connect(repeatNode, repeatNode);
            exitAgain.Connect(repeatNode, null);

            Entry = greeting;
        }
    }
}
