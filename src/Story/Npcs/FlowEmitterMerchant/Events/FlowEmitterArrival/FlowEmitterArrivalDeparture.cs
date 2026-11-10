using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterArrival
{
    internal static class FlowEmitterArrivalDeparture
    {
        internal static void Append(StorySequence player)
        {
            player.SayNpc(FlowEmitterArrivalTexts.DepartureGift1);
            player.SayNpc(FlowEmitterArrivalTexts.DepartureGift2);
            player.SayNpc(FlowEmitterArrivalTexts.DepartureGift3);
            player.Do(FlowEmitterArrivalMaterials.GiveDepartureGifts);
            player.SayNpc(FlowEmitterArrivalTexts.Farewell1);
            player.SayNpc(FlowEmitterArrivalTexts.Farewell2);
            player.Do(FlowEmitterMerchantProgress.MarkArrivalSeen);
        }
    }
}
