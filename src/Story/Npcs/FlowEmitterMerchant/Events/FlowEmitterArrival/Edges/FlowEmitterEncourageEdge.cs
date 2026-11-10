using System.Threading.Tasks;
namespace FluxForge.Story.FlowEmitterArrival
{
    internal sealed class FlowEmitterEncourageEdge : StoryEdge
    {
        internal FlowEmitterEncourageEdge() : base("抱歉，我没做出来。", null) { }
        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(FlowEmitterArrivalTexts.Encourage1);
            player.SayNpc(FlowEmitterArrivalTexts.Encourage2);
            player.SayNpc(FlowEmitterArrivalTexts.Encourage3);
            FlowEmitterArrivalDeparture.Append(player);
            return Task.CompletedTask;
        }
    }
}
