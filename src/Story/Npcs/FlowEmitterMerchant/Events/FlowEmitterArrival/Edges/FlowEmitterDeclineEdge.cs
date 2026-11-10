using System.Threading.Tasks;
namespace FluxForge.Story.FlowEmitterArrival
{
    internal sealed class FlowEmitterDeclineEdge : StoryEdge
    {
        internal FlowEmitterDeclineEdge() : base("这次先不了。", null) { }
        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(FlowEmitterArrivalTexts.Decline);
            player.SayNpc(FlowEmitterArrivalTexts.DeclineFarewell1);
            player.SayNpc(FlowEmitterArrivalTexts.DeclineFarewell2);
            return Task.CompletedTask;
        }
    }
}
