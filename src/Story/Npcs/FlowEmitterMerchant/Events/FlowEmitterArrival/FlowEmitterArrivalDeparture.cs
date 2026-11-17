using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterArrival
{
    internal static class FlowEmitterArrivalDeparture
    {
        internal static void Append(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.DepartureGift1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.DepartureGift2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.DepartureGift3));
            player.Do(FlowEmitterArrivalMaterials.GiveDepartureGifts);
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Farewell1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", FlowEmitterArrivalTexts.Farewell2));
            player.Do(FlowEmitterMerchantProgress.MarkArrivalSeen);
        }
    }
}
