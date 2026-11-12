using UnityEngine.Localization.Settings;

namespace FluxForge.Story.Hospital
{
    /// <summary>医院自定义声望 perk 的运行时本地化。</summary>
    internal static class HospitalReputationTexts
    {
        internal const string PerkId
            = "HOSPITAL_CONTRACT_TERMINATED";

        internal const string PerkName
            = "终止合同";

        internal const string PerkDescription
            = "医院不相信你稳定供货的能力，选择终止合同。";

        internal const string PerkFlavor
            = "似乎有一位特殊的客人，仍然会来造访。";

        internal static void Register()
        {
            var table = LocalizationSettings.StringDatabase.GetTable(
                "RepPerkTable");
            table.AddEntry(
                "rep_perk_" + PerkId + "_name",
                PerkName);
            table.AddEntry(
                "rep_perk_" + PerkId + "_desc",
                PerkDescription);
            table.AddEntry(
                "rep_perk_" + PerkId + "_flavor",
                PerkFlavor);
        }
    }
}
