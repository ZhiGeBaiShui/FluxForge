using System.Threading.Tasks;
namespace FluxForge.Story.FlowEmitterArrival
{
    internal sealed class FlowEmitterSubmitAcidEdge : StoryEdge
    {
        internal bool Passed { get; private set; }
        internal FlowEmitterSubmitAcidEdge() : base("是这样吗？（把酸试剂放在桌上）", null) { }
        internal override Task Execute(StorySequence player)
        {
            AcidSubmission result = FlowEmitterArrivalMaterials.CheckAcid();
            Passed = result == AcidSubmission.Passed;
            if (!Passed)
            {
                if (result == AcidSubmission.Missing)
                {
                    player.SayNpc(FlowEmitterArrivalTexts.Missing1);
                    player.SayNpc(FlowEmitterArrivalTexts.Missing2);
                }
                else
                {
                    player.SayNpc(FlowEmitterArrivalTexts.TooDilute1);
                    player.SayNpc(FlowEmitterArrivalTexts.TooDilute2);
                }
                return Task.CompletedTask;
            }
            player.SayNpc(FlowEmitterArrivalTexts.Success1);
            player.SayNpc(FlowEmitterArrivalTexts.Success2);
            player.SayNpc(FlowEmitterArrivalTexts.Success3);
            FlowEmitterArrivalDeparture.Append(player);
            return Task.CompletedTask;
        }
    }
}
