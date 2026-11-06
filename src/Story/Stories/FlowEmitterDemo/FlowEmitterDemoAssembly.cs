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
                = new StoryNode(FlowEmitterDemoTexts.Greeting);
            StoryNode root = new StoryNode(FlowEmitterDemoTexts.DevicePrompt);

            FlowEmitterGreetingEdge greeting = new FlowEmitterGreetingEdge();
            FlowEmitterAskEdge ask = new FlowEmitterAskEdge();
            FlowEmitterDemoEdge demo = new FlowEmitterDemoEdge();
            FlowEmitterExitEdge exit = new FlowEmitterExitEdge();

            greeting.Connect(null, greetingNode);
            ask.Connect(greetingNode, root);
            demo.Connect(root, root);
            exit.Connect(root, null);

            Entry = greeting;
        }
    }
}
