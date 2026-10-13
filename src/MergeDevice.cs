using System;
using System.Collections.Generic;

namespace ProbablyBagMod
{
    /// <summary>
    /// 汇聚装置（对应 T 型的汇聚种类）：多进一出，等齐后合并输出。
    ///
    /// 有状态——内部 buffer 寄存已到达的载荷，数量达到
    /// <see cref="RequiredCount"/> 才合并并从 <see cref="Exit"/> 放出。
    /// 未等齐前返回空集合，流源在此「等待」。
    ///
    /// 简化版说明：暂未实现「阻塞某一方向、等另外两个方向」的精细行为，
    /// 当前只要是流入本装置的流源都计入缓冲。
    /// </summary>
    public sealed class MergeDevice : IFlowDevice
    {
        private readonly List<IFlowPayload> _buffer = new List<IFlowPayload>();

        /// <summary>需要等齐几份才输出。</summary>
        public int RequiredCount { get; }

        /// <summary>合并后的出口方向。</summary>
        public Direction Exit { get; }

        public MergeDevice(int requiredCount, Direction exit)
        {
            RequiredCount = requiredCount;
            Exit = exit;
        }

        public IReadOnlyList<FlowOutput> OnFlowEnter(FlowToken incoming, Direction entrySide)
        {
            _buffer.Add(incoming.Payload);

            if (_buffer.Count < RequiredCount)
            {
                return Array.Empty<FlowOutput>();   // 等另一个方向
            }

            // 等齐：全部合并
            IFlowPayload merged = _buffer[0];
            for (int i = 1; i < _buffer.Count; i++)
            {
                merged = merged.Merge(_buffer[i]);
            }
            _buffer.Clear();

            return new[] { new FlowOutput(Exit, merged) };
        }

        /// <summary>丢弃当前缓冲的载荷（放弃时调用）。</summary>
        public void Reset()
        {
            _buffer.Clear();
        }
    }
}
