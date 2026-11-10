using FluxForge.Story.AcidBuyer;
using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story
{
    /// <summary>剧情框架的组合根。</summary>
    internal static class StoryBootstrap
    {
        private static NpcStoryHost _host;

        internal static void Initialize()
        {
            _host = new NpcStoryHost();
            _host.Add(FlowEmitterMerchantStory.Create());
            _host.Add(AcidBuyerStory.Create());
            _host.Subscribe();
        }

        internal static void Shutdown()
        {
            if (_host == null)
            {
                return;
            }

            StoryNpcRuntime.ClearTradeCompleted();
            _host.Dispose();
            _host = null;
        }
    }
}
