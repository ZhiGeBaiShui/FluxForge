using System.Collections.Generic;
using FluxForge.Mod;

namespace FluxForge.Story.Hospital
{
    internal static class HospitalSynaprestTrade
    {
        // 此任务与原最高需求一样只接受单一成分纯液，不以显示四舍五入后的100%判断。
        internal static double Submit(HospitalPickupSession session, out bool sawSynaprest)
        {
            List<GameItem> accepted = new List<GameItem>();
            double volume = 0d;
            sawSynaprest = false;
            GameInventory counter = EmporiumEntry.Instance.backInvinvElement;
            for (int i = 0; i < counter.childItems.Count; i++)
            {
                GameItem item = counter.childItems[i];
                if (!item.IsTag(FlowTags.LiquidContainerTag) || item.IsTag(FlowTags.FlowDevice))
                {
                    continue;
                }
                FlowProperties contents = FlowStock.Read(item);
                double active = contents.Get(FlowProps.Synaprest);
                if (active <= 0d)
                {
                    continue;
                }
                sawSynaprest = true;
                if (active == contents.SubstanceTotal)
                {
                    accepted.Add(item);
                    volume += active;
                }
            }
            if (accepted.Count > 0)
            {
                for (int i = 0; i < accepted.Count; i++)
                {
                    accepted[i].Destroy();
                }
                session.Add(volume / 1000d * 5d, accepted.Count);
            }
            return volume;
        }
    }
}
