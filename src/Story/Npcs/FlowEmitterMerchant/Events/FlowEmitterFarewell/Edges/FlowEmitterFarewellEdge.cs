using System.Threading.Tasks;
using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterFarewell
{
    /// <summary>离场边：说完赠礼和到访提醒，再赠礼并离开。</summary>
    internal sealed class FlowEmitterFarewellEdge : StoryEdge
    {
        private static readonly string[] GiftItemKeys =
        {
            "flow_straight_speed",
            "flow_heater",
            "flow_cooler",
            "l_turn_device",
            "l_turn_device",
            "l_turn_device",
            "l_turn_device",
        };

        internal FlowEmitterFarewellEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(FlowEmitterFarewellTexts.Thanks);
            player.SayNpc(FlowEmitterFarewellTexts.Intro);
            player.SayNpc(FlowEmitterFarewellTexts.Cooperation);
            player.SayNpc(FlowEmitterFarewellTexts.VisitReminder);

            player.Do(delegate
            {
                foreach (string itemKey in GiftItemKeys)
                {
                    GameItem item = DirectoryMaster.Item(itemKey);
                    FlowEmitterMerchantStock.MarkShabby(item);
                    GeneralHelper.SetItemOwned(item, true);
                    GraphUtils.EmporiumTryAdd(
                        EmporiumEntry.Instance.frontInvinvElement,
                        item);
                }
            });

            return Task.CompletedTask;
        }
    }
}
