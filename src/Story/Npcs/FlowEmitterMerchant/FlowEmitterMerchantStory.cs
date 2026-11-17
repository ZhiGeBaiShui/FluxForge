using FluxForge.Story.FlowEmitterArrival;
using FluxForge.Story.FlowEmitterGenericVisit;
using FluxForge.Story.FlowEmitterSecondVisit;
using FluxForge.Story.Portraits;

namespace FluxForge.Story.FlowEmitterMerchant
{
    /// <summary>奥雅的访客身份、到访规则和剧情事件注册。</summary>
    internal static class FlowEmitterMerchantStory
    {
        internal static NpcStoryContent Create()
        {
            NpcDefinition definition = new NpcDefinition(
                "fluxforge_flow_emitter_demo",
                "npc.oya.name",
                NpcPortraitRegistry.SpriteKey,
                FlowEmitterMerchantProgress.VisitCountKey,
                true);

            VisitRule visit = new VisitRule(
                "flow_emitter.every_7_days",
                definition.Identifier,
                10,
                1d,
                7,
                VisitKind.Sell,
                delegate { return true; },
                FlowEmitterMerchantProgress.IncrementVisitCount);

            StoryEvent arrival = new StoryEvent(
                "flow_emitter.arrival",
                definition.Identifier,
                StoryTrigger.VisitStarted,
                100,
                0,
                null,
                delegate
                {
                    return !FlowEmitterMerchantProgress.HasArrivalSeen;
                });

            StoryEvent secondVisit = new StoryEvent(
                "flow_emitter.second_visit",
                definition.Identifier,
                StoryTrigger.VisitStarted,
                90,
                1,
                null,
                delegate
                {
                    return FlowEmitterMerchantProgress.HasArrivalSeen
                        && !FlowEmitterMerchantProgress.HasSecondVisit;
                });

            StoryEvent genericVisit = new StoryEvent(
                "flow_emitter.generic_visit",
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
                        new FlowEmitterArrivalAssembly(),
                        StoryOutcome.Leave),
                    new StoryEventDefinition(
                        secondVisit,
                        new FlowEmitterSecondVisitAssembly(),
                        StoryOutcome.OpenShop),
                    new StoryEventDefinition(
                        genericVisit,
                        new FlowEmitterGenericVisitAssembly(),
                        StoryOutcome.OpenShop),
                },
                visit);
        }
    }
}
