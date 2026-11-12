using System.Threading.Tasks;
using FluxForge.Mod;

namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractOfferGreetingEdge : StoryEdge
    {
        internal HospitalContractOfferGreetingEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(HospitalContractOfferTexts.Greeting1);
            player.SayNpc(HospitalContractOfferTexts.Greeting2);
            player.SayNpc(HospitalContractOfferTexts.Greeting3);
            player.SayNpc(HospitalContractOfferTexts.Greeting4);
            return Task.CompletedTask;
        }
    }

    internal sealed class HospitalContractOfferAskEdge : StoryEdge
    {
        internal HospitalContractOfferAskEdge()
            : base(HospitalContractOfferTexts.AskOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(HospitalContractOfferTexts.AskLine1);
            player.SayNpc(HospitalContractOfferTexts.AskLine2);
            player.SayNpc(HospitalContractOfferTexts.AskLine3);
            return Task.CompletedTask;
        }
    }

    internal sealed class HospitalContractOfferAcceptEdge : StoryEdge
    {
        internal HospitalContractOfferAcceptEdge()
            : base(HospitalContractOfferTexts.AcceptOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            HospitalContractState.Start(StoreStation.GetDayCounter());
            HospitalReputation.EnsureFaction();
            player.SayNpc(HospitalContractOfferTexts.AcceptLine1);
            player.SayNpc(HospitalContractOfferTexts.AcceptLine2);
            player.SayNpc(HospitalContractOfferTexts.AcceptLine3);
            return Task.CompletedTask;
        }
    }

    internal sealed class HospitalContractOfferDeclineEdge : StoryEdge
    {
        internal HospitalContractOfferDeclineEdge()
            : base(HospitalContractOfferTexts.DeclineOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            HospitalContractState.DeclineOffer();
            player.SayNpc(HospitalContractOfferTexts.DeclineLine);
            return Task.CompletedTask;
        }
    }

    internal sealed class HospitalContractOfferNoInterestEdge : StoryEdge
    {
        internal HospitalContractOfferNoInterestEdge()
            : base(HospitalContractOfferTexts.NoInterestOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            HospitalContractState.DeclineOffer();
            player.SayNpc(HospitalContractOfferTexts.NoInterestLine1);
            player.SayNpc(HospitalContractOfferTexts.NoInterestLine2);
            return Task.CompletedTask;
        }
    }
}
