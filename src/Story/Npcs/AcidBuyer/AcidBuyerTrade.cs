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
    /// 酸/碱收货：只扫描后柜台（玩家放货的一侧），收走所有浓度 >=90% 的酸/碱。
    /// >=99% 的交易会累加高纯度交付次数。
    /// </summary>
    internal static class AcidBuyerTrade
    {
        private const double MinimumConcentration = 0.90d;
        private const double HighConcentration = 0.99d;
        private const double PureConcentration = 1d - 1e-9d;

        internal static AcidBuyerTradeResult TrySellAll(out int reward)
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
            AcidBuyerProgress.SetLastHighPurityDeliveryVolume(
                highPurityVolume);

            return AcidBuyerTradeResult.Success;
        }

        private static int Price(FlowProperties properties, double concentration)
        {
            double volumeMillilitres = properties.SubstanceTotal / 1000d;

            if (concentration + 1e-9d < HighConcentration)
            {
                return (int)Math.Floor(volumeMillilitres / 20d);
            }

            double basePrice = volumeMillilitres / 10d;
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
