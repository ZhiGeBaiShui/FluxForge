using System.Globalization;

namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalSynaprestSubmitEdge : StoryEdge
    {
        private readonly HospitalPickupSession _session;
        private readonly HospitalPickupMenu _menu;
        internal HospitalSynaprestSubmitEdge(HospitalPickupSession session, HospitalPickupMenu menu)
            : base(HospitalSynaprestTexts.SubmitOption, "question") { _session = session; _menu = menu; }
        internal override StoryNode ResolveNext() { return _menu.Current; }
        internal override void Execute(StorySequence player)
        {
            bool first = HospitalSynaprestState.Volume == 0d;
            bool sawSynaprest;
            double volume = HospitalSynaprestTrade.Submit(_session, out sawSynaprest);
            if (volume == 0d)
            {
                player.SayNpc(EmporiumEntry.Instance.backInvinvElement.childItems.Count == 0
                    ? ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Empty)
                    : sawSynaprest ? ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Impure) : ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Missing));
                return;
            }

            HospitalSynaprestState.Record(volume);
            if (first)
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.First1));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.First2));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.First3));
            }
            if (first && HospitalSynaprestState.Completed)
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.AllAtOnce));
            }
            else
            {
                player.SayNpc(string.Format(CultureInfo.InvariantCulture,
                    first ? ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.FirstProgress) : ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Progress),
                    (HospitalSynaprestState.Volume / 1000d).ToString("0.###", CultureInfo.InvariantCulture)));
            }
            if (HospitalSynaprestState.Completed)
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Complete1));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Complete2));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Complete3));
            }
            player.SayNpc(HospitalSynaprestState.Completed
                ? ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.CompleteContinue) : ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Continue));
        }
    }

}
