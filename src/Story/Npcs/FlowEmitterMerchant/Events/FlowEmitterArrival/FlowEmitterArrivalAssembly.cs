using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterArrival
{
    /// <summary>
    /// 首次到访剧情：只负责见面、介绍货品和上架首批库存。
    /// 不再演示任何装置，也不依赖事件层。
    /// </summary>
    internal sealed class FlowEmitterArrivalAssembly : NpcStoryAssembly
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

        internal FlowEmitterArrivalAssembly()
        {
            FlowEmitterGreetingEdge greeting = new FlowEmitterGreetingEdge();
            greeting.Connect(null, null);
            Entry = greeting;
        }
    }
}

