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

        internal const string TrustPaymentPerkId
            = "HOSPITAL_TRUST_PAYMENT";

        internal const string SpecialProcurementPerkId
            = "HOSPITAL_SPECIAL_PROCUREMENT";

        internal const string ExternalExpertPerkId
            = "HOSPITAL_EXTERNAL_EXPERT";

        internal static void Register()
        {
            var table = LocalizationSettings.StringDatabase.GetTable(
                "RepPerkTable");

            Add(
                table,
                TrustedPerkId,
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.trusted.name"),
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.trusted.description"),
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.trusted.flavor"));

            Add(
                table,
                AcePerkId,
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.ace.name"),
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.ace.description"),
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.ace.flavor"));

            Add(
                table,
                TrustPaymentPerkId,
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.payment.name"),
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.payment.description"),
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.payment.flavor"));

            Add(
                table,
                SpecialProcurementPerkId,
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.procurement.name"),
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.procurement.description"),
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.procurement.flavor"));

            Add(
                table,
                TerminationPerkId,
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.termination.name"),
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.termination.description"),
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.termination.flavor"));

            Add(
                table,
                ExternalExpertPerkId,
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.expert.name"),
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.expert.description"),
                ModHelper.GetLocalized("fluxforge", "reputation.hospital.expert.flavor"));
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
