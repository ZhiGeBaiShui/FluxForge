using FluxForge.Story.AcidBuyerArrival;
using FluxForge.Story.AcidBuyerRepeatVisit;
using FluxForge.Story.FlowEmitterMerchant;
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
                false);

            VisitRule visit = new VisitRule(
                "acid_buyer.seven_days",
                definition.Identifier,
                10,
                1d,
                7,
                VisitKind.Dialogue,
                delegate { return FlowEmitterMerchantProgress.HasArrivalSeen; },
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

            return new NpcStoryContent(
                definition,
                new[] { visit },
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
                });
        }
    }
}
