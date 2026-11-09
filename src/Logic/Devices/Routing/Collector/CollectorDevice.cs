using System;

namespace FluxForge
{
    /// <summary>
    /// 收集装置：吸收从入口方向撞上来的流源，把它们的属性累积成一份存量。
    ///
    /// 有方向：<see cref="Facing"/> 表示**入口朝向**（0=上 1=右 2=下 3=左），
    /// 即「流源从哪一侧来」。方向不对时由运动层在调用 Route 前销毁流源。
    /// 新建时默认入口朝上（接受从上方来、向下移动的流源）。
    ///
    /// 路由语义：接受 0 个新流源 = 吸收，所以本装置同时是流源的终点——
    /// <see cref="Route"/> 在方向匹配时返回 true 让运动层截停，再调
    /// <see cref="FlowSpawner.Kill"/> 走统一的删除流程（mod 层的视觉视图据此移除对应绘制项）。
    ///
    /// 只保留累积后的 <see cref="FlowProperties"/>，不保留流源对象本身：
    /// 收集器需要的是「收到了什么」，不是「每个流源从哪来」。
    ///
    /// 属性累积复用 <see cref="FlowProperties.Merge"/>（纯函数），
    /// 每次吸收都返回新对象，所以必须写回字段，不能指望原对象被就地修改。
    /// </summary>
    public sealed class CollectorDevice
        : IFlowRouter, IFlowNotifier, IFacing, IFlowInlet
    {
        /// <summary>发射速度的默认值（格/秒），也是没有配置 tag 时的回退值。</summary>
        public const double DefaultEmitSpeed = 2d;

        private FlowProperties _stock;

        private int _facing;

        /// <summary>发射装置：流源出射速度（格/秒）。收集器本身不发射，但发射器复用本装置。</summary>
        public double EmitSpeed { get; set; } = DefaultEmitSpeed;

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

        /// <summary>按一份已有存量初始化；入口默认朝上。</summary>
        public CollectorDevice(FlowProperties stock)
        {
            _stock = stock;
            _facing = 0;   // 默认入口朝上
        }

        /// <summary>
        /// 入口朝向（0=上 1=右 2=下 3=左），表示流源从哪一侧来。
        /// 与 <see cref="SplitDevice.Facing"/> 同义：物品旋转时由
        /// <c>FlowGridAdapter</c> 按 <c>modifiedShape.orientation</c> 同步。
        /// </summary>
        public int Facing
        {
            get { return _facing; }
            set { _facing = value; }
        }

        /// <inheritdoc/>
        public bool Flipped { get; set; }

        /// <summary>
        /// 本装置接受的流源移动方向，恒为入口朝向的反向。
        /// 入口朝上（0）→ 只收向下移动（2）的流源。
        /// </summary>
        private int AcceptedDirection
        {
            get { return (_facing + 2) % 4; }
        }

        public bool CanEnter(int direction) => direction == AcceptedDirection;

        /// <summary>累积的属性。每次吸收后换成新对象，读取方应重新取属性而非缓存引用。</summary>
        public FlowProperties Stock
        {
            get { return _stock; }
        }

        /// <summary>
        /// 整体替换存量：<see cref="Stock"/> 每次吸收后都会换成新对象，
        /// 回灌只能替换引用，不能指望旧对象被就地改写。回灌不播报 <see cref="Changed"/>。
        /// </summary>
        public void ReplaceStock(FlowProperties stock)
        {
            _stock = stock;
        }

        /// <inheritdoc/>
        public bool Route(FlowEntity incoming, FlowSpawner flows, int cellX, int cellY)
        {
            // 方向不对就放行：让流源从侧面/背面穿过去，收集器不是实心墙。
            if (incoming.Motion.Direction != AcceptedDirection)
            {
                return false;
            }

            // 只取属性：流源对象没有保留价值，吸收后就地终结。
            double absorbed = incoming.Properties.SubstanceTotal;
            _stock = _stock.Merge(incoming.Properties);

            flows.Kill(incoming);

            // 库存定稿后再播报，订阅方读到的是一次完整的吸收结果
            Changed?.Invoke(this);

            if (absorbed > 0d)
            {
                FlowAudio.Play(FlowSoundIds.Absorbed, this, absorbed);
                FlowAnimation.Play(FlowAnimationIds.CollectorPulse, this, absorbed);
            }

            return true;
        }
    }
}
