using System.Globalization;

namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalHighestDemandOfferEdge : StoryEdge
    {
        internal HospitalHighestDemandOfferEdge()
            : base(null, null)
        {
        }

        internal override void Execute(StorySequence player)
        {
            Append(player);
        }

        internal static void Append(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Greeting));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Offer1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Offer2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Offer3));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Offer4));
        }
    }

    internal sealed class HospitalHighestDemandDecisionEdge : StoryEdge
    {
        private readonly bool _accept;
        internal HospitalHighestDemandDecisionEdge(bool accept)
            : base(accept ? HospitalHighestDemandTexts.AcceptOption : HospitalHighestDemandTexts.DeclineOption, "question")
        {
            _accept = accept;
        }
        internal override void Execute(StorySequence player)
        {
            if (_accept)
            {
                HospitalHighestDemandState.Start();
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.AcceptResponse1));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.AcceptResponse2));
            }
            else
            {
                HospitalHighestDemandState.Decline();
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.DeclineResponse));
            }
        }
    }

    internal sealed class HospitalHighestDemandProgressEdge : StoryEdge
    {
        internal HospitalHighestDemandProgressEdge()
            : base(null, null)
        {
        }

        internal override void Execute(StorySequence player)
        {
            player.SayNpc(string.Format(
                CultureInfo.InvariantCulture,
                ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Progress),
                (HospitalHighestDemandState.Acid / 1000d).ToString("0.###", CultureInfo.InvariantCulture),
                (HospitalHighestDemandState.Base / 1000d).ToString("0.###", CultureInfo.InvariantCulture),
                (HospitalHighestDemandState.Alcohol / 1000d).ToString("0.###", CultureInfo.InvariantCulture)));
        }
    }

    internal sealed class HospitalHighestDemandCompleteEdge : StoryEdge
    {
        internal HospitalHighestDemandCompleteEdge()
            : base(null, null)
        {
        }

        internal override void Execute(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Complete));
        }
    }
}
