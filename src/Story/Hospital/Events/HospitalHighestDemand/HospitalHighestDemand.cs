namespace FluxForge.Story.Hospital
{
    /// <summary>最高需求自行注册插入内容，正常送料/取货只声明业务插入点。</summary>
    internal static class HospitalHighestDemand
    {
        internal static bool Completing
        {
            get { return HospitalHighestDemandState.Active && HospitalHighestDemandState.TargetMet; }
        }

        internal static bool CanOffer
        {
            get
            {
                return HospitalReputation.Get() >= HospitalContractScore.TrustReputationThreshold
                    && !HospitalHighestDemandState.Started && !HospitalHighestDemandState.Declined;
            }
        }
        internal static StoryEventDefinition[] CreateEvents(string npcId)
        {
            return new[]
            {
                new StoryEventDefinition(
                    new StoryEvent(
                        "hospital.highest_demand.progress",
                        npcId,
                        HospitalStoryPoints.PickupSettled,
                        20,
                        0,
                        null,
                        context => HospitalHighestDemandState.Active),
                    new HospitalHighestDemandProgressAssembly(),
                    StoryOutcome.Default),
                new StoryEventDefinition(
                    new StoryEvent(
                        "hospital.highest_demand.complete",
                        npcId,
                        HospitalStoryPoints.PickupSettled,
                        10,
                        0,
                        null,
                        context => Completing),
                    new HospitalHighestDemandCompleteAssembly(),
                    StoryOutcome.Default),
            };
        }
    }
}
