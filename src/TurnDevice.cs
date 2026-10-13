using System;

namespace ProbablyBagMod
{
    /// <summary>
    /// 转向装置（对应 L 型）：流源从指定入口进，从指定出口出。
    /// 载荷原样传递，不做任何修改。
    /// </summary>
    public sealed class TurnDevice : IFlowDevice
    {
        private readonly Direction _entry;
        private readonly Direction _exit;

        /// <param name="entry">唯一有效入口（流源从装置哪一侧进来）。</param>
        /// <param name="exit">出口方向。</param>
        public TurnDevice(Direction entry, Direction exit)
        {
            _entry = entry;
            _exit = exit;
        }

        public IReadOnlyList<FlowOutput> OnFlowEnter(FlowToken incoming, Direction entrySide)
        {
            // 从非入口进入视为接错，吸收掉（流源被浪费）
            if (entrySide != _entry)
            {
                return Array.Empty<FlowOutput>();
            }

            return new[] { new FlowOutput(_exit, incoming.Payload) };
        }
    }
}
