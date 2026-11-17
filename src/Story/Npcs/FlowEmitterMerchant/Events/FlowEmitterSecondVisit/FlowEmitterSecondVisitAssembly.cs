using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterSecondVisit
{
    /// <summary>第二次到访：介绍新货、赠送手册；正常结束时记录进度并上货。</summary>
    internal sealed class FlowEmitterSecondVisitAssembly : StoryAssembly
    {
        internal override StoryEdge Entry { get; }

        internal override void Complete()
        {
            FlowEmitterMerchantProgress.MarkSecondVisit();
            FlowEmitterMerchantStock.PlaceSecondVisit();
        }

        internal FlowEmitterSecondVisitAssembly()
        {
            FlowEmitterSecondVisitGreetingEdge greeting
                = new FlowEmitterSecondVisitGreetingEdge();

            greeting.Connect(null, null);
            Entry = greeting;
        }
    }
}
