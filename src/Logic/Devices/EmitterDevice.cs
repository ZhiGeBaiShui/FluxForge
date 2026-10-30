namespace ProbablyBagMod
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
    /// </summary>
    public sealed class EmitterDevice : IWaterState, ITemperatureState
    {
        /// <summary>流源速度（格/秒）。</summary>
        private const double Speed = 2d;

        /// <summary>存量（水量 / 温度），外部可整体读写。发射后被清空。</summary>
        public FlowProperties Stock { get; set; }

        /// <summary>tag 映射视图：存量就是 <see cref="Stock"/> 的两个字段。</summary>
        double IWaterState.Water
        {
            get { return Stock.Water; }
            set { Stock.Water = value; }
        }

        double ITemperatureState.Temperature
        {
            get { return Stock.Temperature; }
            set { Stock.Temperature = value; }
        }

        public EmitterDevice(double water, double temperature)
        {
            Stock = new FlowProperties(water, temperature);
        }

        /// <summary>还有没有东西可发。</summary>
        public bool HasContent
        {
            get { return Stock.Water > 0d; }
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

            // 载荷用整份存量，库存随之清空——物质守恒，不能一边发一边留
            FlowProperties payload = new FlowProperties(Stock.Water, Stock.Temperature);
            FlowPos position = new FlowPos(origin.X, origin.Y);
            FlowMotionData motion = new FlowMotionData(direction, Speed);
            FlowEntity entity = new FlowEntity(position, motion, payload);

            flows.Accept(entity);
            Stock.Water = 0d;

            return true;
        }
    }
}
