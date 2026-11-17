using System;
using System.Collections.Generic;
using FluxForge.Mod;

namespace FluxForge.Story.AcidBuyer
{
    internal enum AcidBuyerTradeResult
    {
        NoAcidOrBase,
        ConcentrationTooLow,
        Success,
    }

    /// <summary>
    /// 酸/碱收货：扫描后柜台，跳过流体装置，按最低 90% 浓度门槛整瓶收购。
    /// 将本批 >=99% 的交付体积计入本次到访，供首次问答与告别判定。
    /// </summary>
    internal static class AcidBuyerTrade
    {
        private const double MinimumConcentration = 0.90d;
        private const double HighConcentration = 0.99d;
        private const double PureConcentration = 1d - 1e-9d;

        internal static AcidBuyerTradeResult TrySellAll(
            AcidBuyerTradeSession session, out int reward)
        {
            reward = 0;

            GameInventory counter = EmporiumEntry.Instance.backInvinvElement;
            List<GameItem> qualified = new List<GameItem>();
            bool sawAcidOrBase = false;
            int highPurityVolume = 0;

            for (int i = 0; i < counter.childItems.Count; i++)
            {
                GameItem item = counter.childItems[i];
                if (!item.IsTag(FlowTags.LiquidContainerTag)
                    || item.IsTag(FlowTags.FlowDevice))
                {
                    continue;
                }

                FlowProperties properties = FlowStock.Read(item);
                double acid = properties.Get(FlowProps.Acid);
                double baseAmount = properties.Get(FlowProps.Base);
                double active = Math.Max(acid, baseAmount);

                if (active <= 0d)
                {
                    continue;
                }

                sawAcidOrBase = true;

                double total = properties.SubstanceTotal;
                double concentration = active / total;
                if (concentration + 1e-9d < MinimumConcentration)
                {
                    continue;
                }

                qualified.Add(item);
                reward += Price(properties, concentration);
                if (concentration + 1e-9d >= HighConcentration)
                {
                    highPurityVolume += (int)Math.Round(total);
                }
            }

            if (!sawAcidOrBase)
            {
                return AcidBuyerTradeResult.NoAcidOrBase;
            }

            if (qualified.Count == 0)
            {
                return AcidBuyerTradeResult.ConcentrationTooLow;
            }

            for (int i = 0; i < qualified.Count; i++)
            {
                qualified[i].Destroy();
            }

            PlayerStore.Instance.ModCash(reward);
            session.RecordHighPurityDelivery(
                highPurityVolume);

            return AcidBuyerTradeResult.Success;
        }

        private static int Price(FlowProperties properties, double concentration)
        {
            double volumeMillilitres = properties.SubstanceTotal / 1000d;

            double basePrice = volumeMillilitres / 10d;

            if (concentration + 1e-9d < HighConcentration)
            {
                return (int)Math.Floor(basePrice);
            }

            double multiplier;
            if (concentration >= PureConcentration)
            {
                multiplier = 3d;
            }
            else
            {
                double steps = (concentration - HighConcentration) / 0.001d;
                multiplier = 1d + steps * 0.1d;
            }

            return (int)Math.Floor(basePrice * multiplier);
        }
    }
}
