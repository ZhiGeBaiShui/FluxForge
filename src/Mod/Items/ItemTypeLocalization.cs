using System;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 给自定义物品类型补本地化条目，让 tooltip 里显示成「[流量装置]」而不是
    /// 「No translation found for 'type_flow_device'」。
    ///
    /// 原版的类型显示链路（实证）：
    ///   CommonTooltipHelper.CreateItemTypeTooltip
    ///     → TypeHelper.GetTypeDisplayName(type)
    ///     → LocHelper.GetLocalizedMechanic("type_" + type.ToLowerInvariant())
    /// 查的是 Unity Localization 的 Mechanic 字符串表，表来自 Addressables 外部 bundle，
    /// 没有内嵌资源可改，只能在运行时往表里补。
    ///
    /// 只改内存、只对当前语言生效。切换语言或重载表后条目会丢，
    /// 所以读档时要再调一次（AddEntry 对同 key 是覆盖，幂等）。
    /// </summary>
    public static class ItemTypeLocalization
    {
        /// <summary>注册一个物品类型的显示名。类型名不需要预先存在于任何表里。</summary>
        public static void Register(string type, string displayName)
        {
            AddEntry("type_" + type.ToLowerInvariant(), displayName);
        }

        private static void AddEntry(string key, string value)
        {
            try
            {
                UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase
                    .GetTable("Mechanic")
                    .AddEntry(key, value);
            }
            catch (Exception e)
            {
                // 表还没加载完是正常情况（启动早期），读档后会再补一次
                MelonLoader.MelonLogger.Warning(
                    "[本地化] 补充类型名失败（tooltip 会显示缺翻译提示）：" + e.Message);
            }
        }
    }
}
