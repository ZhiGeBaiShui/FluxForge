using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterFarewell
{
    /// <summary>第二个剧情的装载类：卖空后启动离场赠礼。</summary>
    internal sealed class FlowEmitterFarewellAssembly : NpcStoryAssembly
    {
        internal override StoryEdge Entry { get; }

        internal override StoryPhase Phase
        {
            get { return StoryPhase.SoldOut; }
        }

        internal override bool CanPlay()
        {
            return FlowEmitterMerchantProgress.HasArrivalSeen
                && !FlowEmitterMerchantProgress.HasFirstStory
                && EmporiumEntry.Instance.GetAllNonOwendItemCount() == 0;
        }

        internal FlowEmitterFarewellAssembly()
        {
            FlowEmitterFarewellEdge edge = new FlowEmitterFarewellEdge();
            edge.Connect(null, null);
            Entry = edge;
        }

        /// <summary>所有退出边最终都会触发这里。</summary>
        internal override void Complete()
        {
            FlowEmitterMerchantProgress.MarkFirstStory();
        }
    }
}
