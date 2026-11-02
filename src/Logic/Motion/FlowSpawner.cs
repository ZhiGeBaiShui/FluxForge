using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 流源的生命周期：谁还活着、什么时候生、什么时候死。
    ///
    /// 与运动层是**并行的两件事**，互不隶属：
    ///   · 本类维护「活着的流源」名单，广播生死与受阻变化；
    ///   · 怎么动、撞到谁，全由 <see cref="FlowManager"/> 负责。
    /// 两者由持有它们的上层（每台流量机）接线，彼此不持有对方的引用。
    ///
    /// 一家机器一份：多台流量机各管各的名单，清理一台不影响另一台。
    /// 不认识 mod 层——外壳的创建/销毁由订阅 <see cref="OnEntityAdded"/> /
    /// <see cref="OnEntityRemoved"/> 的一方自己做。
    /// </summary>
    public sealed class FlowSpawner
    {
        private readonly List<FlowEntity> _active = new List<FlowEntity>();

        /// <summary>流源出生时触发（mod 层订阅 → 造外壳）。</summary>
        public event Action<FlowEntity> OnEntityAdded;

        /// <summary>流源消失时触发（mod 层订阅 → 毁灭外壳）。</summary>
        public event Action<FlowEntity> OnEntityRemoved;

        /// <summary>受阻变化时同步通知；参数是本次变化快照，回调异常不回滚。</summary>
        public event Action<FlowEntity, bool> OnEntityBlockedChanged;

        /// <summary>当前活着的流源（运动层遍历它来推进）。</summary>
        public IReadOnlyList<FlowEntity> Active
        {
            get { return _active; }
        }

        /// <summary>纳入一个新的流源并广播出生。</summary>
        public void Accept(FlowEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            _active.Add(entity);
            OnEntityAdded?.Invoke(entity);
        }

        internal void ReportBlockedState(FlowEntity entity, bool blocked)
        {
            OnEntityBlockedChanged?.Invoke(entity, blocked);
        }

        /// <summary>
        /// 终结一个流源并广播。不在名单里就直接返回——
        /// 同一个流源不会被播报两次，调用方也不必先判重。
        /// </summary>
        public void Kill(FlowEntity entity)
        {
            if (entity == null || !_active.Remove(entity))
            {
                return;
            }

            OnEntityRemoved?.Invoke(entity);
        }

        /// <summary>
        /// 终结全部（机器退役 / 网格失效时用），逐个广播，外壳才会跟着销毁。
        ///
        /// 必须「先移出名单、再广播」——和 <see cref="Kill"/> 的契约保持一致。
        /// 如果先广播后清空，广播期间 <see cref="Active"/> 里还留着已经死掉的流源，
        /// 订阅者如果顺手再 Kill 一次就会播两遍。
        /// </summary>
        public void KillAll()
        {
            while (_active.Count > 0)
            {
                int last = _active.Count - 1;
                FlowEntity entity = _active[last];
                _active.RemoveAt(last);
                OnEntityRemoved?.Invoke(entity);
            }
        }
    }
}
