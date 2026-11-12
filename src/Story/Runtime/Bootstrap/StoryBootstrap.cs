using FluxForge.Story.AcidBuyer;
using FluxForge.Story.FlowEmitterMerchant;
using FluxForge.Story.Hospital;

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
            ModHook.OnGameLoadedLate += EnsureHospitalFaction;
            _host.Subscribe();
        }

        internal static void Shutdown()
        {
            ModHook.OnGameLoadedLate -= EnsureHospitalFaction;

            if (_host == null)
            {
                return;
            }

            StoryNpcRuntime.ClearTradeCompleted();
            _host.Dispose();
            _host = null;
        }

        private static void EnsureHospitalFaction()
        {
            if (HospitalContractState.Started)
            {
                HospitalReputation.EnsureFaction();
                return;
            }

            if (AcidBuyerProgress.HasHighPurityDialogueDone
                && !HospitalContractState.OfferDeclined)
            {
                HospitalContractState.QueueOffer();
            }
        }
    }
}
