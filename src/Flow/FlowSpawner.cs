using System.Collections.Generic;

namespace ProbablyBagMod
{
    /// <summary>
    /// 待生成流源容器（单例，全局唯一）。
    ///
    /// 时钟是自维护的现实时间轴——参考 Unity Time.time 的做法：
    /// 外部每帧喂一次帧间隔（deltaTime），容器累加出自己的当前时间。
    /// 游戏暂停时没人喂，时间就不走；读档回退也不会错乱。
    /// （不用 DateTime/Stopwatch 墙钟：那和游戏内时间会脱节。）
    ///
    /// 内部用 .NET 6 内置的 PriorityQueue（按目标时刻排序的小顶堆）：
    /// 每次推进只需看堆顶，到点就出队。
    /// </summary>
    public sealed class FlowSpawner
    {
        public static FlowSpawner Instance { get; } = new FlowSpawner();

        private readonly PriorityQueue<FlowEntity, double> _pending;
        private readonly List<FlowEntity> _ready;

        private double _now;   // 当前时间（秒），由 Advance 累加而来

        private FlowSpawner()
        {
            _pending = new PriorityQueue<FlowEntity, double>();
            _ready = new List<FlowEntity>();
        }

        /// <summary>当前时间（秒）。注册与出队都以它为准。</summary>
        public double Now
        {
            get { return _now; }
        }

        /// <summary>
        /// 已到时的流源。运动层每帧 Advance 之后从这里取走并清空。
        /// </summary>
        public List<FlowEntity> Ready
        {
            get { return _ready; }
        }

        /// <summary>
        /// 注册一个流源，让它 delaySeconds 秒后生成（传 0 = 下一次 Advance 便出队）。
        /// </summary>
        public void Register(FlowEntity entity, double delaySeconds)
        {
            _pending.Enqueue(entity, _now + delaySeconds);
        }

        /// <summary>
        /// 推进时间轴。由运动层每帧调用，传入本帧的现实间隔。
        /// 所有到点的流源从队列出队，堆进 <see cref="Ready"/>。
        /// </summary>
        public void Advance(double deltaTime)
        {
            _now += deltaTime;

            while (_pending.Count > 0
                && _pending.TryPeek(out FlowEntity entity, out double due)
                && due <= _now)
            {
                _pending.Dequeue();
                _ready.Add(entity);
            }
        }
    }
}
