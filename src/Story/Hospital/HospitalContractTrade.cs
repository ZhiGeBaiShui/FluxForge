using System.Collections.Generic;
using FluxForge.Mod;

namespace FluxForge.Story.Hospital
{
    /// <summary>
    /// 医院取货：扫描后柜台，按配置的 FlowPropertyKey 和浓度计分，
    /// 收走所有合格液体，按分数付款并结算医院声望。
    /// </summary>
    internal static class HospitalContractTrade
    {
        private const double Epsilon = 1e-9d;

        internal static HospitalContractSettlement TrySettle(
            IReadOnlyList<ChemicalRequest> requests)
        {
            GameInventory counter
                = EmporiumEntry.Instance.backInvinvElement;
            List<GameItem> qualified = new List<GameItem>();
            double score = 0d;
            bool hasHighPurity = false;

            for (int i = 0; i < counter.childItems.Count; i++)
            {
                GameItem item = counter.childItems[i];
                if (!item.IsTag(FlowTags.LiquidContainerTag)
                    || item.IsTag(FlowTags.FlowDevice))
                {
                    continue;
                }

                FlowProperties properties = FlowStock.Read(item);
                double total = properties.SubstanceTotal;
                if (total <= 0d)
                {
                    continue;
                }

                double bestScorePerMillilitre = 0d;
                bool accepted = false;
                for (int j = 0; j < requests.Count; j++)
                {
                    ChemicalRequest request = requests[j];
                    double active = properties.Get(request.Key);
                    if (active <= 0d)
                    {
                        continue;
                    }

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
                    if (concentration + Epsilon
                        >= HospitalContractScore.HighPurityThreshold)
                    {
                        hasHighPurity = true;
                    }
                }

                if (!accepted)
                {
                    continue;
                }

                qualified.Add(item);
                score += total / 1000d * bestScorePerMillilitre;
            }

            int payment = HospitalContractScore.Payment(score);
            for (int i = 0; i < qualified.Count; i++)
            {
                qualified[i].Destroy();
            }

            PlayerStore.Instance.ModCash(payment);

            int delta = HospitalContractScore.ReputationDelta(score);
            HospitalReputation.Add(delta);

            return new HospitalContractSettlement(
                score,
                delta,
                payment,
                qualified.Count,
                hasHighPurity);
        }
    }
}
