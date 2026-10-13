using System;
using System.Collections.Generic;

namespace ProbablyBagMod
{
    /// <summary>
    /// 运动层：驱动流源在网格中流动。完全不理解载荷内容。
    ///
    /// Tick 由外部逐帧调用；停止信号也由外部触发（玩家 / 垃圾桶 / 进入下一天）。
    /// </summary>
    public sealed class FlowSimulator
    {
        private readonly IFlowGrid _grid;
        private readonly List<FlowToken> _active = new List<FlowToken>();
        private readonly List<FlowToken> _collected = new List<FlowToken>();

        /// <summary>流源进入某个装置时触发。UI 层订阅它做视觉反馈（高亮 / 动画等）。</summary>
        public event Action<FlowToken, IFlowDevice> OnFlowEnterDevice;

        /// <summary>流源到达输出口时触发。</summary>
        public event Action<FlowToken> OnFlowReachExit;

        /// <summary>已到达输出口的流源。</summary>
        public IReadOnlyList<FlowToken> CollectedOutputs => _collected;

        public FlowSimulator(IFlowGrid grid)
        {
            _grid = grid;
        }

        /// <summary>投入一个流源。由外部在启动装置触发时调用。</summary>
        public void AddSource(FlowToken token)
        {
            if (token != null)
            {
                _active.Add(token);
            }
        }

        /// <summary>放弃：取消所有活跃流源。</summary>
        public void CancelAll()
        {
            _active.Clear();
        }

        /// <summary>清空已收集的输出。</summary>
        public void ClearCollected()
        {
            _collected.Clear();
        }

        /// <summary>推进一帧。由外部每帧调用。</summary>
        public void Tick()
        {
            // 取快照：事件回调中可能增删流源
            FlowToken[] snapshot = _active.ToArray();
            foreach (FlowToken token in snapshot)
            {
                if (token != null)
                {
                    Step(token);
                }
            }
        }

        private void Step(FlowToken token)
        {
            int steps = token.Speed < 1f ? 1 : (int)token.Speed;
            GridPos target = token.Position;

            // 朝当前方向推进；路径上遇到装置就把目的地设为该装置格
            for (int i = 0; i < steps; i++)
            {
                GridPos next = target.PlusOffset(token.Direction);
                if (_grid.GetDeviceAt(next) != null)
                {
                    target = next;
                    break;
                }
                target = next;
            }

            if (target.Equals(token.Position))
            {
                return;
            }

            token.Position = target;

            IFlowDevice device = _grid.GetDeviceAt(target);
            if (device == null)
            {
                return; // 空格，下一帧继续走
            }

            OnFlowEnterDevice?.Invoke(token, device);

            if (device is IFlowOutput)
            {
                _active.Remove(token);
                _collected.Add(token);
                OnFlowReachExit?.Invoke(token);
                return;
            }

            IReadOnlyList<FlowOutput> outputs = device.OnFlowEnter(token, token.Direction.Opposite());

            _active.Remove(token);
            if (outputs == null)
            {
                return;
            }

            foreach (FlowOutput output in outputs)
            {
                _active.Add(new FlowToken(target, output.Direction, output.Payload, token.Speed));
            }
        }
    }
}
