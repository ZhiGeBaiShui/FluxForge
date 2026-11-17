using System.Collections.Generic;
using FluxForge.Mod;

namespace FluxForge.Story.Hospital
{
    /// <summary>
    /// 医院取货：分别执行后柜台检查与已确认货物结算，按配置品类和浓度计分。
    /// 提交时收走合格容器并累计纯试剂任务进度；结束来访时统一付款、调整声望。
    /// </summary>
    internal static class HospitalContractTrade
    {
        private const double Epsilon = 1e-9d;

        /// <summary>只读取桌面，供当前选项判断；不产生交易副作用。</summary>
        internal static HospitalContractInspection Inspect(
            IReadOnlyList<ChemicalRequest> requests)
        {
            bool hasUnderConcentration = false;
            GameInventory counter
                = EmporiumEntry.Instance.backInvinvElement;
            List<GameItem> qualified = new List<GameItem>();
            double score = 0d;
            double pureAcid = 0d;
            double pureBase = 0d;
            double pureAlcohol = 0d;

            for (int i = 0; i < counter.childItems.Count; i++)
            {
                GameItem item = counter.childItems[i];
                if (!item.IsTag(FlowTags.LiquidContainerTag)
                    || item.IsTag(FlowTags.FlowDevice))
                {
                    continue;
                }

                FlowProperties properties = FlowStock.Read(item);
                if (!HospitalSynaprestState.Completed
                    && properties.Get(FlowProps.Synaprest) > 0d)
                {
                    continue;
                }
                double total = properties.SubstanceTotal;
                if (total <= 0d)
                {
                    continue;
                }

                double bestScorePerMillilitre = 0d;
                bool accepted = false;
                bool sawRequestedChemical = false;
                for (int j = 0; j < requests.Count; j++)
                {
                    ChemicalRequest request = requests[j];
                    double active = properties.Get(request.Key);
                    if (active <= 0d)
                    {
                        continue;
                    }

                    sawRequestedChemical = true;
                    double concentration = active / total;
                    if (concentration + Epsilon
                        < request.MinimumConcentration)
                    {
                        continue;
                    }

                    double scorePerMillilitre
                        = HospitalContractScore.Score(
                            1d,
                            request.BaseScore,
                            concentration);
                    if (scorePerMillilitre > bestScorePerMillilitre)
                    {
                        bestScorePerMillilitre = scorePerMillilitre;
                    }

                    accepted = true;
                    if (active == total)
                    {
                        if (request.Key.Equals(FlowProps.Acid))
                        {
                            pureAcid += total;
                        }
                        else if (request.Key.Equals(FlowProps.Base))
                        {
                            pureBase += total;
                        }
                        else if (request.Key.Equals(FlowProps.Alcohol))
                        {
                            pureAlcohol += total;
                        }
                    }
                }

                if (!accepted)
                {
                    hasUnderConcentration |= sawRequestedChemical;
                    continue;
                }

                qualified.Add(item);
                score += total / 1000d * bestScorePerMillilitre;
            }

            return new HospitalContractInspection(
                qualified, score, hasUnderConcentration,
                pureAcid, pureBase, pureAlcohol);
        }

        /// <summary>收走本次提交刚检查过的货物；此处不支付货款、不推进周日期。</summary>
        internal static void Take(
            HospitalContractInspection inspection)
        {
            for (int i = 0; i < inspection.Qualified.Count; i++)
            {
                inspection.Qualified[i].Destroy();
            }

            if (HospitalHighestDemandState.Active)
            {
                HospitalHighestDemandState.Record(inspection);
            }

        }

        internal static int Settle(double score)
        {
            int payment = HospitalContractScore.Payment(score, HospitalReputation.Get());
            PlayerStore.Instance.ModCash(payment);
            int delta = HospitalContractScore.ReputationDelta(score, HospitalReputation.Get());
            HospitalReputation.Add(delta);

            return payment;
        }
    }
}
