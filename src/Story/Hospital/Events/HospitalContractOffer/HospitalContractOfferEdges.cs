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
        private const string PriceBody =
            "每周目标：300 分\n"
            + "小于90%：不接收\n"
            + "90–98.9%：权值 1.0\n"
            + "99%：权值 1.0\n"
            + "99% 以上：每 0.1 个百分点 +0.1\n"
            + "100%：权值 3.0\n"
            + "\n"
            + "酸：基础值1分\n"
            + "碱：基础值1分\n"
            + "酒精：基础值0.8分\n"
            + "\n"
            + "得分规则：每 mL 化学品基础分 × 浓度权值\n"
            + "\n"
            + "结算：0.1 信用点 / 分";

        internal HospitalContractOfferAskEdge()
            : base(HospitalContractOfferTexts.AskOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(HospitalContractOfferTexts.AskLine1);
            player.Do(delegate
            {
                PlayerStore.Instance.AddDirectSellingItemToTable(
                    CreatePriceNote(),
                    true);
            });
            player.SayNpc(HospitalContractOfferTexts.AskLine2);
            player.SayNpc(HospitalContractOfferTexts.AskLine3);
            player.SayNpc(HospitalContractOfferTexts.AskLine4);
            return Task.CompletedTask;
        }

        private static GameItem CreatePriceNote()
        {
            GameItem note = PreBuiltItemHelper.CreateHandnote(
                PriceBody,
                "上层区中心医院采购价目",
                "薇拉",
                2);
            note.name = "上层区中心医院采购价目";
            note.shortDescription = "医院合同的结算方式。";
            note.unitValue = 0L;
            return note;
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
