using System;
using System.Collections.Generic;

namespace ProbablyBagMod
{
    /// <summary>
    /// 收集装置：吸收撞上来的流源，把它们的属性累积成一份存量。
    ///
    /// 路由语义：注册 0 个新流源 = 吸收，所以本装置同时是流源的终点——
    /// <see cref="Route"/> 无条件返回 true 让运动层截停，再调 <see cref="FlowSpawner.Kill"/>
    /// 走统一的删除流程（mod 层据此销毁壳）。
    ///
    /// <see cref="Collected"/> 保留流源对象本身而非只存属性：
    /// 上层要按流源粒度追溯来源（位置 / 速度 / 属性快照）时，只有存量是查不到的。
    /// 但**只保留最近若干个**——发射装置一秒能吐两个，一天下来是几十万个对象，
    /// 长跑必然把内存吃满；而真正需要的是「最近收到了什么」，不是全部历史。
    ///
    /// 属性累积复用 <see cref="FlowProperties.Merge"/>（纯函数），
    /// 所以每收一个都要把返回的新对象存回字段，不能指望原对象被就地修改。
    /// </summary>
    public sealed class CollectorDevice : IFlowRouter, IFlowNotifier
    {
        /// <summary>流源记录的保留上限。超出的从最旧的开始丢。</summary>
        public const int MaxCollected = 64;

        private FlowProperties _stock;
        private readonly List<FlowEntity> _collected = new List<FlowEntity>(MaxCollected);

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
            _stock = new FlowProperties(0d, 0d);
        }

        /// <summary>累积的属性。每次吸收后换成新对象，读取方应重新取属性而非缓存引用。</summary>
        public FlowProperties Stock
        {
            get { return _stock; }
        }

        /// <summary>最近收到的流源（最多 <see cref="MaxCollected"/> 个，已被 Kill，仅作记录）。</summary>
        public IReadOnlyList<FlowEntity> Collected
        {
            get { return _collected; }
        }

        /// <inheritdoc/>
        public bool Route(FlowEntity incoming, FlowSpawner flows, int cellX, int cellY)
        {
            List<FlowProperties> incomingOnly = new List<FlowProperties>(1);
            incomingOnly.Add(incoming.Properties);
            _stock = _stock.Merge(incomingOnly);

            if (_collected.Count >= MaxCollected)
            {
                _collected.RemoveAt(0);
            }

            _collected.Add(incoming);
            flows.Kill(incoming);

            // 库存与记录都定稿后再播报，订阅方读到的是一次完整的吸收结果
            Changed?.Invoke(this);

            return true;
        }

        /// <summary>
        /// 清空收集：流源记录与累积属性一起归零。
        /// 只清列表会让存量留着上一轮的数值，与「空收集器」的语义不符。
        /// </summary>
        public void Clear()
        {
            _collected.Clear();
            _stock = new FlowProperties(0d, 0d);
        }
    }
}
