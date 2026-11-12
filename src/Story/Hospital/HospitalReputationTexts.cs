using UnityEngine.Localization.Settings;

namespace FluxForge.Story.Hospital
{
    /// <summary>医院自定义声望 perk 的运行时本地化。</summary>
    internal static class HospitalReputationTexts
    {
        internal const string TrustedPerkId
            = "HOSPITAL_TRUSTED_SUPPLIER";

        internal const string AcePerkId
            = "HOSPITAL_ACE_SUPPLIER";

        internal const string TerminationPerkId
            = "HOSPITAL_CONTRACT_TERMINATED";

        internal static void Register()
        {
            var table = LocalizationSettings.StringDatabase.GetTable(
                "RepPerkTable");

            Add(
                table,
                TrustedPerkId,
                "初步信任",
                "医院认为你比其他供货商更值得信任，薇拉每周会额外带1瓶原料给你。",
                "有个很奇怪的事情——你并没有听说过医院会提供原料给经销商们。");

            Add(
                table,
                AcePerkId,
                "王牌供应商",
                "你成为了医院最大的供货商之一，现在薇拉每周会带4瓶原料给你了。",
                "你听说给你提供原料，是薇拉在医院中的提议，她的身份看起来不简单。");

            Add(
                table,
                TerminationPerkId,
                "终止合同",
                "医院不相信你稳定供货的能力，选择终止合同。",
                "似乎有一位特殊的客人，仍然会来造访。");
        }

        private static void Add(
            UnityEngine.Localization.Tables.StringTable table,
            string perkId,
            string name,
            string description,
            string flavor)
        {
            table.AddEntry(
                "rep_perk_" + perkId + "_name",
                name);
            table.AddEntry(
                "rep_perk_" + perkId + "_desc",
                description);
            table.AddEntry(
                "rep_perk_" + perkId + "_flavor",
                flavor);
        }
    }
}
