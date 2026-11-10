using FluxForge.Mod;
using FluxForge.Story.FlowEmitterMerchant;
namespace FluxForge.Story.FlowEmitterArrival
{
    internal enum AcidSubmission { Missing, TooDilute, Passed }
    internal static class FlowEmitterArrivalMaterials
    {
        internal static void GiveStarterKit()
        {
            GameItem acid = DirectoryMaster.Item("med_bottle_red");
            FlowProperties contents = new FlowProperties();
            contents.Set(FlowProps.Acid, 90000);
            contents.Set(FlowProps.ChemicalContaminant, 10000);
            contents.Set(FlowProps.Temperature, FlowStock.Read(acid).Get(FlowProps.Temperature));
            FlowStock.Write(acid, contents);
            Give(acid);
            GiveDevice("flow_emitter", 2);
            GiveDevice("flow_heater", 1);
            GiveDevice("flow_filter", 1);
            Give(DirectoryMaster.Item("fluxforge_starter_note"));
        }
        internal static void GiveDepartureGifts()
        {
            GiveDevice("l_turn_device", 4);
            GiveDevice("flow_straight_speed", 1);
            GiveDevice("flow_cooler", 1);
        }
        private static void GiveDevice(string key, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameItem item = DirectoryMaster.Item(key);
                FlowEmitterMerchantStock.MarkShabby(item);
                Give(item);
            }
        }
        private static void Give(GameItem item)
        {
            GeneralHelper.SetItemOwned(item, true);
            GraphUtils.EmporiumTryAdd(EmporiumEntry.Instance.frontInvinvElement, item);
        }
        internal static AcidSubmission CheckAcid()
        {
            bool found = false;
            foreach (GameItem item in EmporiumEntry.Instance.backInvinvElement.childItems)
            {
                if (item.identifier != "med_bottle_red") continue;
                FlowProperties contents = FlowStock.Read(item);
                double acid = contents.Get(FlowProps.Acid);
                if (acid <= 0) continue;
                found = true;
                if (acid >= contents.SubstanceTotal * 0.95)
                    return AcidSubmission.Passed;
            }
            return found ? AcidSubmission.TooDilute : AcidSubmission.Missing;
        }
    }
}
