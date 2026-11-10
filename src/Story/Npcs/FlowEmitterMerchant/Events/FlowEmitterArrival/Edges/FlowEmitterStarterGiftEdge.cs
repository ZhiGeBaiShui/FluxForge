using System.Threading.Tasks;
namespace FluxForge.Story.FlowEmitterArrival
{
    internal sealed class FlowEmitterStarterGiftEdge : StoryEdge
    {
        internal FlowEmitterStarterGiftEdge() : base("有兴趣，我想试试。", null) { }
        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(FlowEmitterArrivalTexts.Gift1);
            player.SayNpc(FlowEmitterArrivalTexts.Gift2);
            player.SayNpc(FlowEmitterArrivalTexts.Gift3);
            player.Do(FlowEmitterArrivalMaterials.GiveStarterKit);
            player.SayNpc(FlowEmitterArrivalTexts.Instructions1);
            player.SayNpc(FlowEmitterArrivalTexts.Instructions2);
            player.SayNpc(FlowEmitterArrivalTexts.Instructions3);
            player.SayNpc(FlowEmitterArrivalTexts.Instructions4);
            player.SayNpc(FlowEmitterArrivalTexts.Goal1);
            player.SayNpc(FlowEmitterArrivalTexts.Goal2);
            player.SayNpc(FlowEmitterArrivalTexts.Goal3);
            return Task.CompletedTask;
        }
    }
}
