using FluxForge.Story.FlowEmitterMerchant;

namespace FluxForge.Story.FlowEmitterArrival
{
    /// <summary>
    /// 首次体验线：邀请体验；接受后赠料，可提交验收或直接告别。
    /// </summary>
    internal sealed class FlowEmitterArrivalAssembly : StoryAssembly
    {
        internal override StoryEdge Entry { get; }

        internal FlowEmitterArrivalAssembly()
        {
            FlowEmitterGreetingEdge greeting = new FlowEmitterGreetingEdge();
            StoryNode interest = new StoryNode();
            StoryNode work = new StoryNode();
            greeting.Connect(null, interest);
            interest.Connect(new FlowEmitterStarterGiftEdge(), work);
            interest.Connect(new FlowEmitterDeclineEdge(), null);
            FlowEmitterSubmitAcidEdge submit = new FlowEmitterSubmitAcidEdge();
            work.Connect(submit, null);
            submit.Branch(() => submit.Passed, null, work);
            work.Connect(new FlowEmitterEncourageEdge(), null);
            Entry = greeting;
        }
    }
}
