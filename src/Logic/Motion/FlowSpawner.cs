using System;
using System.Collections.Generic;

namespace ProbablyBagMod
{
    /// <summary>
    /// 流源生命周期中枢（单例，全局唯一）：管「生」也管「死」，出入口对偶。
    ///
    /// 生：装置 <see cref="Register"/>（注册）→ <see cref="Advance"/> 到点出队
    ///      → 送进 FlowManager + 发 <see cref="OnEntityAdded"/>（mod 层建壳）
    /// 死：装置 <see cref="Kill"/>（删除）→ FlowManager 移除
    ///      → 发 <see cref="OnEntityRemoved"/>（mod 层毁壳）
    ///
    /// Register / Kill 一对（外部对流的出入口）、
    /// OnEntityAdded / OnEntityRemoved 一对（框架对外的广播）——完全对等。
    ///
    /// 时钟是自维护的现实时间轴——参考 Unity Time.time 的做法：
    /// 外部每帧喂一次帧间隔（deltaTime），本类累加出自己的当前时间。
    /// 游戏暂停时没人喂，时间就不走；读档回退也不会错乱。
    /// </summary>
    public sealed class FlowSpawner
    {
        public static FlowSpawner Instance { get; } = new FlowSpawner();

        private readonly PriorityQueue<FlowEntity, double> _pending;
        private double _now;

        /// <summary>新流源出生时触发（mod 层订阅 → 造 GameObject 壳）。</summary>
        public event Action<FlowEntity> OnEntityAdded;

        /// <summary>流源被删除时触发（mod 层订阅 → 销毁壳）。</summary>
        public event Action<FlowEntity> OnEntityRemoved;

        private FlowSpawner()
        {
            _pending = new PriorityQueue<FlowEntity, double>();
        }

        /// <summary>当前时间（秒）。注册与出队都以它为准。</summary>
        public double Now
        {
            get { return _now; }
        }

        /// <summary>清空待出生队列并复位时间轴（读档 / 重开 / 测试隔离用）。</summary>
        public void Reset()
        {
            _pending.Clear();
            _now = 0;
        }

        /// <summary>
        /// 注册一个流源，delaySeconds 后出生（0 = 下一次 Advance 便出队）。
        /// 排队期间不提供删除（待出生状态「到点必出」）。
        /// </summary>
        public void Register(FlowEntity entity, double delaySeconds)
        {
            _pending.Enqueue(entity, _now + delaySeconds);
        }

        /// <summary>
        /// 删除一个流源：从运动层移除并广播（供装置 / 越界 / 到输出口调用）。
        /// 若目标还在排队（未出生），运动层移除是 no-op——但按约定删除只发生在出生后。
        /// </summary>
        public void Kill(FlowEntity entity)
        {
            FlowManager.Instance.Remove(entity);
            OnEntityRemoved?.Invoke(entity);
        }

        /// <summary>
        /// 推进时间轴。由外部每帧调用，传入本帧现实间隔。
        /// 所有到点的流源出队、送进运动层，并发 OnEntityAdded。
        /// </summary>
        public void Advance(double deltaTime)
        {
            _now += deltaTime;

            while (_pending.Count > 0
                && _pending.TryPeek(out FlowEntity entity, out double due)
                && due <= _now)
            {
                _pending.Dequeue();
                FlowManager.Instance.Accept(entity);
                OnEntityAdded?.Invoke(entity);
            }
        }
    }
}
