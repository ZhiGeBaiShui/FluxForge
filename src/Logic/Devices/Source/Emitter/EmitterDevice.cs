using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 发射装置：持有一份「存量」，启动时把存量转成流源对象吐出去。
    ///
    /// 为什么是一次性而不是按间隔连续发射：
    ///   存量是一整份物质，拆成几十个小流源只是把同一个东西切成很多份，
    ///   下游装置还要逐个处理，语义和性能都更差。
    ///   一次交出一份，一个流源承载全部属性，最直接。
    ///
    /// 本类不参与每帧推进：由外部（mod 层）在玩家点「启动」时调用一次
    /// <see cref="EmitAll"/>，之后流源归运动层管理。
    /// 存量归零后不再重复发射——要再发一次得重新灌水。
    ///
    /// 状态变化通过 <see cref="IFlowNotifier.Changed"/> 广播，由 mod 层统一写回 tag；
    /// 与 Collector / IceBlock 走同一条 FlowStock 映射链路。
    /// </summary>
    public sealed class EmitterDevice : IFlowNotifier
    {
        /// <summary>流源速度（格/秒）。</summary>
        private const double Speed = 2d;

        /// <summary>存量（全部属性），外部可整体读写。发射后清空全部液体、保留标量（温度）。</summary>
        public FlowProperties Stock { get; set; }

        public event Action<IFlowNotifier> Changed;

        /// <summary>发射器可重新灌装，不存在“耗尽后从载体移除”；事件保留给统一订阅链路。</summary>
        public event Action<IFlowNotifier> Exhausted
        {
            add { }
            remove { }
        }

        /// <summary>目录工厂用：空存量，具体内容由 flow_container tag 的 adopt 回灌。</summary>
        public EmitterDevice()
            : this(new FlowProperties())
        {
        }

        public EmitterDevice(FlowProperties stock)
        {
            Stock = stock;
        }

        /// <summary>存量是完整容器：拥有全部 37 个属性槽。</summary>
        public IReadOnlyList<FlowPropertyKey> Keys
        {
            get { return FlowPropertyRegistry.AllKeys; }
        }

        /// <summary>读属性槽：直接委托给存量。</summary>
        public double Get(FlowPropertyKey key)
        {
            return Stock.Get(key);
        }

        /// <summary>写属性槽：直接委托给存量。</summary>
        public void Set(FlowPropertyKey key, double value)
        {
            Stock.Set(key, value);
        }

        /// <summary>还有没有液体可发（温度不算物质）。</summary>
        public bool HasContent
        {
            get { return Stock.SubstanceTotal > 0d; }
        }

        /// <summary>
        /// 把整份存量转成一个流源并注册进队列。
        /// </summary>
        /// <param name="origin">出发格（格中心）。</param>
        /// <param name="direction">出发方向（0=上 1=右 2=下 3=左）。</param>
        /// <param name="flows">接收新流源的生命周期（本机一份）。</param>
        /// <returns>真的发出了流源才返回 true。</returns>
        public bool EmitAll(FlowPos origin, int direction, FlowSpawner flows)
        {
            if (flows == null || origin == null || !HasContent)
            {
                return false;
            }

            // 载荷是整份存量的深拷贝；发出后清空全部液体、保留标量（温度）——
            // 物质守恒，不能一边发一边留。
            FlowProperties payload = Stock.Clone();
            FlowPos position = new FlowPos(origin.X, origin.Y);
            FlowMotionData motion = new FlowMotionData(direction, Speed);
            FlowEntity entity = new FlowEntity(position, motion, payload);

            flows.Accept(entity);

            FlowProperties cleared = Stock.Clone();
            foreach (FlowPropertyDef def in FlowPropertyRegistry.All)
            {
                if (def.Kind == FlowPropertyKind.Liquid)
                {
                    cleared.Set(def.Key, 0d);
                }
            }
            Stock = cleared;

            Changed?.Invoke(this);
            return true;
        }
    }
}

