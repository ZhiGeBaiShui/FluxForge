using System.Threading.Tasks;

namespace FluxForge.Story.FlowEmitterFarewell
{
    /// <summary>离场边：说三句话，等说完，标记首次剧情完成并赠送。</summary>
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

            player.Do(delegate
            {
                foreach (string itemKey in GiftItemKeys)
                {
                    GameItem item = DirectoryMaster.Item(itemKey);
                    ItemFeatureHelper.SetShabby(item);
                    GeneralHelper.SetItemOwned(item, true);
                    GraphUtils.EmporiumTryAdd(
                        EmporiumEntry.Instance.invElement,
                        item);
                }
            });

            return Task.CompletedTask;
        }
    }
}
