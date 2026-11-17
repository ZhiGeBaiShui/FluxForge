using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace FluxForge.Mod
{
    /// <summary>在原版资源加载完成后装配模组描述和物品类型文本。</summary>
    internal static class ModMetadataLocalization
    {
        private static LoadedMod _mod;

        internal static void Initialize()
        {
            // 原版OnEnable早于LoadLocalization；资源就绪后才读取CSV。
            ModHook.OnModAssetsLoaded += OnAssetsLoaded;
        }

        internal static void Shutdown()
        {
            ModHook.OnModAssetsLoaded -= OnAssetsLoaded;
            LocalizationSettings.SelectedLocaleChanged -= UpdateTexts;
            _mod = null;
        }

        private static void OnAssetsLoaded(LoadedMod mod)
        {
            // 原版为每个启用模组广播此事件，只装配本模组的文本。
            if (mod.Manifest.ID != ItemFactory.ModId)
            {
                return;
            }
            _mod = mod;
            LocalizationSettings.SelectedLocaleChanged += UpdateTexts;
            UpdateTexts(LocalizationSettings.SelectedLocale);
        }

        private static void UpdateTexts(Locale locale)
        {
            _mod.Manifest.Description = ModHelper.GetLocalized(ItemFactory.ModId, "mod.description");
            ItemTypeLocalization.RegisterAll();
        }
    }
}
