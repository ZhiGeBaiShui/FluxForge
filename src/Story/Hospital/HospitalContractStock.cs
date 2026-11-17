using FluxForge.Mod;

namespace FluxForge.Story.Hospital
{
    /// <summary>
    /// 医院每周送来的受污染原料。
    /// 与首次到访赠送的酸/碱分开；调用方在发放时按当前医院声望计算瓶数。
    /// </summary>
    internal static class HospitalContractStock
    {
        private const int BottleCapacity = 250000;
        private const int PollutionMin = 150000;
        private const int PollutionMax = 190000;

        internal static int WeeklyCount()
        {
            int count = 2;
            int reputation = HospitalReputation.Get();
            if (reputation >= 20)
            {
                count++;
            }

            if (reputation >= 40)
            {
                count++;
            }

            return count;
        }

        internal static void GiveWeekly(int count)
        {
            for (int i = 0; i < count; i++)
            {
                GiveBottle(i % 2 == 0);
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
            properties.Set(FlowProps.Temperature, 20d);
            FlowStock.Write(item, properties);

            item.name = ModHelper.GetLocalized("fluxforge", acid ? "item.stock.hospital_acid.name" : "item.stock.hospital_base.name");
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
