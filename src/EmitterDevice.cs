using System;

namespace ProbablyBagMod
{
    /// <summary>
    /// 启动装置（发射器）。
    ///
    /// 与输入槽 1:1 强绑定。框架不会主动调用它产生流源——
    /// 由外部在「启动」时机调 <see cref="Spawn"/> 拿到流源，再
    /// <see cref="FlowSimulator.AddSource"/> 投入。这样「何时启动」
    /// 「流源初始属性怎么来」都归外部决定。
    /// </summary>
    public sealed class EmitterDevice : IFlowDevice
    {
        /// <summary>流源发射方向。</summary>
        public Direction ExitDirection { get; }

        private readonly Func<IFlowPayload> _payloadFactory;

        public EmitterDevice(Direction exitDirection, Func<IFlowPayload> payloadFactory)
        {
            ExitDirection = exitDirection;
            _payloadFactory = payloadFactory;
        }

        /// <summary>
        /// 流源不应流回启动装置；若发生则直接吸收，避免倒流。
        /// </summary>
        public IReadOnlyList<FlowOutput> OnFlowEnter(FlowToken incoming, Direction entrySide)
        {
            return Array.Empty<FlowOutput>();
        }

        /// <summary>
        /// 产生一个流源。<paramref name="position"/> 应为发射器所在格。
        /// </summary>
        public FlowToken Spawn(GridPos position, float speed = 1f)
        {
            return new FlowToken(position, ExitDirection, _payloadFactory(), speed);
        }
    }
}
