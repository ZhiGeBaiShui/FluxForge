using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 收集装置：吸收撞上来的流源，把它们的属性累积成一份存量。
    ///
    /// 路由语义：接受 0 个新流源 = 吸收，所以本装置同时是流源的终点——
    /// <see cref="Route"/> 无条件返回 true 让运动层截停，再调 <see cref="FlowSpawner.Kill"/>
    /// 走统一的删除流程（mod 层的视觉视图据此移除对应绘制项）。
    ///
    /// 只保留累积后的 <see cref="FlowProperties"/>，不保留流源对象本身：
    /// 收集器需要的是「收到了什么」，不是「每个流源从哪来」。
    ///
    /// 属性累积复用 <see cref="FlowProperties.Merge"/>（纯函数），
    /// 每次吸收都返回新对象，所以必须写回字段，不能指望原对象被就地修改。
    /// </summary>
    public sealed class CollectorDevice
        : IFlowRouter, IFlowNotifier
    {
        private FlowProperties _stock;

        /// <inheritdoc/>
        public event Action<IFlowNotifier> Changed;

        /// <summary>
        /// 收集装置只会累积存量，不存在「耗尽」状态，所以本事件永不触发。
        /// 用空访问器实现接口：订阅方拿到任何装置都可以统一挂两个事件，
        /// 不需要按装置类型分开处理。
        /// </summary>
        public event Action<IFlowNotifier> Exhausted
        {
            add { }
            remove { }
        }

        public CollectorDevice()
        {
            _stock = new FlowProperties();
        }

        /// <summary>累积的属性。每次吸收后换成新对象，读取方应重新取属性而非缓存引用。</summary>
        public FlowProperties Stock
        {
            get { return _stock; }
        }

        /// <summary>收集器可累积全部液体：拥有注册表的全部 37 个槽位（36 种液体 + 温度）。</summary>
        public IReadOnlyList<FlowPropertyKey> Keys
        {
            get { return FlowPropertyRegistry.AllKeys; }
        }

        /// <summary>读属性槽：直接委托给存量，不另存一份。</summary>
        public double Get(FlowPropertyKey key)
        {
            return _stock.Get(key);
        }

        /// <summary>写属性槽：直接委托给存量，不另存一份。</summary>
        public void Set(FlowPropertyKey key, double value)
        {
            _stock.Set(key, value);
        }

        /// <inheritdoc/>
        public bool Route(FlowEntity incoming, FlowSpawner flows, int cellX, int cellY)
        {
            // 只取属性：流源对象没有保留价值，吸收后就地终结。
            _stock = _stock.Merge(incoming.Properties);

            flows.Kill(incoming);

            // 库存定稿后再播报，订阅方读到的是一次完整的吸收结果
            Changed?.Invoke(this);

            return true;
        }
    }
}
