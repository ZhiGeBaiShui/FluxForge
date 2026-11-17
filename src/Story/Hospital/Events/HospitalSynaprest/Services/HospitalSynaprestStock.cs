using FluxForge.Mod;

namespace FluxForge.Story.Hospital
{
    internal static class HospitalSynaprestStock
    {
        internal static void GiveInitial()
        {
            Give("bottled_water", 600000, 6061);
        }

        internal static void GiveWeekly()
        {
            // 400000 / (400000 + 21052) > 95%，两端都保留污染。
            Give("small_bottled_water", 400000, RNG.GetRandomInt(2, 21052));
        }

        private static void Give(string bottle, int synaprest, int pollution)
        {
            GameItem item = DirectoryMaster.Item(bottle);
            FlowProperties contents = new FlowProperties();
            contents.Set(FlowProps.Synaprest, synaprest);
            int starch = RNG.GetRandomInt(1, pollution - 1);
            contents.Set(FlowProps.Starch, starch);
            contents.Set(RNG.Roll(50) ? FlowProps.Acid : FlowProps.Base, pollution - starch);
            contents.Set(FlowProps.Temperature, 20d);
            FlowStock.Write(item, contents);
            item.name = ModHelper.GetLocalized("fluxforge", "item.stock.synaprest.name");
            GeneralHelper.SetItemOwned(item, true);
            GraphUtils.EmporiumTryAdd(EmporiumEntry.Instance.frontInvinvElement, item);
        }
    }
}
