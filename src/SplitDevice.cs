using System;

namespace ProbablyBagMod
{
    /// <summary>
    /// 分流装置（对应 T 型的分割种类）：一进两出，载荷**平分成两份**。
    ///
    /// 注意是平分而非复制：水分 100 分流后是 50 + 50，总量守恒，不会凭空增值。
    /// </summary>
    public sealed class SplitDevice : IFlowDevice
    {
        private readonly Direction _entry;
        private readonly Direction _exitA;
        private readonly Direction _exitB;

        /// <param name="entry">入口方向。</param>
        /// <param name="exitA">第一出口方向。</param>
        /// <param name="exitB">第二出口方向。</param>
        public SplitDevice(Direction entry, Direction exitA, Direction exitB)
        {
            _entry = entry;
            _exitA = exitA;
            _exitB = exitB;
        }

        public IReadOnlyList<FlowOutput> OnFlowEnter(FlowToken incoming, Direction entrySide)
        {
            if (entrySide != _entry)
            {
                return Array.Empty<FlowOutput>();
            }

            if (incoming.Payload == null)
            {
                return Array.Empty<FlowOutput>();
            }

            IReadOnlyList<IFlowPayload> halves = incoming.Payload.Split(2);
            if (halves == null || halves.Count < 2)
            {
                return Array.Empty<FlowOutput>();
            }

            return new[]
            {
                new FlowOutput(_exitA, halves[0]),
                new FlowOutput(_exitB, halves[1]),
            };
        }
    }
}
