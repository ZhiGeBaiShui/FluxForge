using FluxForge.Story.AcidBuyerArrival;
using FluxForge.Story.AcidBuyerRepeatVisit;
using FluxForge.Story.FlowEmitterMerchant;
using FluxForge.Story.Hospital;
using FluxForge.Story.Portraits;

namespace FluxForge.Story.AcidBuyer
{
    /// <summary>薇拉的访客身份、到访规则和剧情事件注册。</summary>
    internal static class AcidBuyerStory
    {
        internal static NpcStoryContent Create()
        {
            NpcDefinition definition = new NpcDefinition(
                "fluxforge_acid_buyer",
                "薇拉",
                NpcPortraitRegistry.AcidBuyerSpriteKey,
                null,
                false,
                HospitalReputation.FactionId);

            VisitRule visit = new VisitRule(
                "acid_buyer.seven_days",
                definition.Identifier,
                10,
                1d,
                7,
                VisitKind.Dialogue,
                delegate { return FlowEmitterMerchantProgress.HasArrivalSeen; },
                null);

            VisitRule contractPickup = new VisitRule(
                "acid_buyer.contract_pickup",
                definition.Identifier,
                110,
                1d,
                0,
                VisitKind.Dialogue,
                delegate(VisitContext context)
                {
                    return HospitalContractState.ShouldRunPickup(
                        context.Day);
                },
                null);

            VisitRule contractMaterial = new VisitRule(
                "acid_buyer.contract_material",
                definition.Identifier,
                105,
                1d,
                0,
                VisitKind.Dialogue,
                delegate(VisitContext context)
                {
                    return HospitalContractState.ShouldRunMaterial(
                        context.Day);
                },
                null);

            StoryEvent arrival = new StoryEvent(
                "acid_buyer.arrival",
                definition.Identifier,
                StoryTrigger.VisitStarted,
                100,
                1,
                null,
                delegate { return !AcidBuyerProgress.HasMet; });

            StoryEvent repeatVisit = new StoryEvent(
                "acid_buyer.repeat_visit",
                definition.Identifier,
                StoryTrigger.VisitStarted,
                10,
                0,
                null,
                delegate { return true; });

            StoryEvent contractOffer = new StoryEvent(
                "acid_buyer.contract_offer",
                definition.Identifier,
                StoryTrigger.VisitStarted,
                150,
                1,
                null,
                delegate
                {
                    return HospitalContractState.OfferReady
                        && !HospitalContractState.Started
                        && !HospitalContractState.OfferDeclined;
                });

            StoryEvent contractPickupEvent = new StoryEvent(
                "acid_buyer.contract_pickup",
                definition.Identifier,
                StoryTrigger.VisitStarted,
                200,
                0,
                null,
                delegate(StoryContext context)
                {
                    return HospitalContractState.ShouldRunPickup(
                        context.Day);
                });

            StoryEvent contractMaterialEvent = new StoryEvent(
                "acid_buyer.contract_material",
                definition.Identifier,
                StoryTrigger.VisitStarted,
                190,
                0,
                null,
                delegate(StoryContext context)
                {
                    return HospitalContractState.ShouldRunMaterial(
                        context.Day);
                });

            StoryEvent contractTermination = new StoryEvent(
                "acid_buyer.contract_termination",
                definition.Identifier,
                StoryTrigger.VisitStarted,
                300,
                1,
                null,
                delegate
                {
                    return HospitalContractState.Started
                        && !HospitalContractState.Terminated
                        && HospitalReputation.Get() <= -20;
                });

            return new NpcStoryContent(
                definition,
                new[] { visit, contractPickup, contractMaterial },
                new[]
                {
                    new StoryEventDefinition(
                        arrival,
                        new AcidBuyerArrivalAssembly(),
                        StoryOutcome.Leave),
                    new StoryEventDefinition(
                        repeatVisit,
                        new AcidBuyerRepeatVisitAssembly(),
                        StoryOutcome.Leave),
                    new StoryEventDefinition(
                        contractOffer,
                        new HospitalContractOfferAssembly(),
                        StoryOutcome.Leave),
                    new StoryEventDefinition(
                        contractPickupEvent,
                        new HospitalContractPickupAssembly(),
                        StoryOutcome.Leave),
                    new StoryEventDefinition(
                        contractMaterialEvent,
                        new HospitalContractMaterialAssembly(),
                        StoryOutcome.Leave),
                    new StoryEventDefinition(
                        contractTermination,
                        new HospitalContractTerminationAssembly(),
                        StoryOutcome.Leave),
                });
        }
    }
}
