using FluxForge.Mod;

namespace FluxForge.Story.AcidBuyer
{
    /// <summary>
    /// 首次到访赠送两瓶受污染酸液、两瓶受污染碱液。
    /// 使用原版 250 mL 化学试剂瓶（mini_bottle），成分随机生成。
    /// </summary>
    internal static class AcidBuyerStock
    {
        private const int BottleCapacity = 250000;
        private const int BottlesPerType = 2;
        private const int PollutionMin = 87500;
        private const int PollutionMax = 112500;

        internal static void GiveRaw()
        {
            for (int i = 0; i < BottlesPerType; i++)
            {
                GiveBottle(true);
            }

            for (int i = 0; i < BottlesPerType; i++)
            {
                GiveBottle(false);
            }
        }

        private static void GiveBottle(bool acid)
        {
            GameItem item = PreBuiltItemHelper.CreateChemicalBottle(
                acid ? "acid" : "base");

            int pollution = RNG.GetRandomInt(PollutionMin, PollutionMax);
            int chemical = Share(pollution, 0.12f, 0.20f);
            int heavyMetal = Share(pollution, 0.08f, 0.15f);
            int microplastic = Share(pollution, 0.06f, 0.12f);
            int physical = Share(pollution, 0.06f, 0.12f);
            int mineral = Share(pollution, 0.04f, 0.08f);
            int debris = Share(pollution, 0.04f, 0.08f);

            int active = BottleCapacity
                - chemical
                - heavyMetal
                - microplastic
                - physical
                - mineral
                - debris;

            FlowProperties properties = new FlowProperties();
            properties.Set(
                acid ? FlowProps.Acid : FlowProps.Base,
                active);
            properties.Set(FlowProps.ChemicalContaminant, chemical);
            properties.Set(FlowProps.HeavyMetal, heavyMetal);
            properties.Set(FlowProps.Microplastic, microplastic);
            properties.Set(FlowProps.PhysicalContaminant, physical);
            properties.Set(FlowProps.Mineral, mineral);
            properties.Set(FlowProps.Debris, debris);
            properties.Set(FlowProps.Temperature, 24d);
            FlowStock.Write(item, properties);

            item.name = ModHelper.GetLocalized("fluxforge", acid ? "item.stock.polluted_acid.name" : "item.stock.polluted_base.name");
            GeneralHelper.SetItemOwned(item, true);
            GraphUtils.EmporiumTryAdd(
                EmporiumEntry.Instance.frontInvinvElement,
                item);
        }

        private static int Share(int total, float min, float max)
        {
            return RNG.GetRandomInt(
                (int)(total * min),
                (int)(total * max));
        }
    }
}
