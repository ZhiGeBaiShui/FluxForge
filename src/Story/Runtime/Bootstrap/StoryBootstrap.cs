using FluxForge.Story.AcidBuyer;
using FluxForge.Story.FlowEmitterMerchant;
using FluxForge.Story.Hospital;
using FluxForge.Story.Portraits;

namespace FluxForge.Story
{
    /// <summary>剧情框架的组合根。</summary>
    internal static class StoryBootstrap
    {
        private static NpcStoryHost _host;
        private static HospitalProcurement _procurement;

        internal static void Initialize()
        {
            _host = new NpcStoryHost();
            _host.Add(FlowEmitterMerchantStory.Create());
            _host.Add(AcidBuyerStory.Create());
            ModHook.OnGameLoadedLate += AcidBuyerStory.RestoreAfterLoad;
            ModHook.OnGameLoadedLate += NpcPortraitRegistry.Apply;
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged += RefreshHospitalTexts;
            _host.Subscribe();
            HospitalProcurementClients.Register();
            _procurement = new HospitalProcurement(HospitalProcurementClients.Identifiers);
            _procurement.Subscribe();
        }

        internal static void Shutdown()
        {
            _procurement.Dispose();
            _procurement = null;
            HospitalProcurementClients.Unregister();
            ModHook.OnGameLoadedLate -= AcidBuyerStory.RestoreAfterLoad;
            ModHook.OnGameLoadedLate -= NpcPortraitRegistry.Apply;
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged -= RefreshHospitalTexts;

            StoryNpcRuntime.Shutdown();
            _host.Dispose();
            NpcPortraitRegistry.Shutdown();
            _host = null;
        }

        private static void RefreshHospitalTexts(UnityEngine.Localization.Locale locale)
        {
            // The hospital faction does not exist until the contract is signed.
            StoreReputation reputation = StoreReputation.GetStoreReputation(HospitalReputation.FactionId);
            if (reputation == null) return;
            reputation.factionDisplay = HospitalReputation.DisplayName;
            HospitalReputationTexts.Register();
        }
    }
}
