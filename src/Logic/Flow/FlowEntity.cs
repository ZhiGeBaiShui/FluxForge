namespace ProbablyBagMod
{
    /// <summary>
    /// 流源：系统里流转的一个对象，携带全部游戏属性。纯数据对象。
    ///
    /// 它就是一个对象——不是水流，也不是「一团/一群」可以再分的东西。
    /// 同一时刻可能有很多个流源各自独立存在、各自移动，彼此没有关联。
    ///
    /// 不持有任何事件与方法——生命周期三时机全部由框架集中广播：
    ///   生：FlowSpawner.Register（装置注册）→ OnEntityAdded 广播
    ///   动：FlowManager.Tick 推进 → 位置直接写在 Position 上（不广播，视觉层自己读）
    ///   死：FlowSpawner.Kill（装置发起）→ OnEntityRemoved 广播
    ///
    /// 自带 <see cref="Grid"/>：流源诞生于哪个网格就只在那个网格里跑，
    /// 这样多台装置（多个网格）可以同时运行而互不干扰。
    ///
    /// 纯逻辑对象，不含 Unity 内容。显示由 mod 层的 GameObject 壳经框架广播间接关联。
    /// </summary>
    public class FlowEntity
    {
        /// <summary>位置（连续坐标，整数点为格中心）。</summary>
        public FlowPos Position;

        /// <summary>运动数据（方向 / 速度）。</summary>
        public FlowMotionData Motion;

        /// <summary>流源属性（水分 / 温度……）。</summary>
        public FlowProperties Properties;

        /// <summary>所属网格：决定在哪套装置里传导、走到哪里算越界。</summary>
        public IFlowGrid Grid;

        public FlowEntity(FlowPos position, FlowMotionData motion, FlowProperties properties, IFlowGrid grid)
        {
            Position = position;
            Motion = motion;
            Properties = properties;
            Grid = grid;
        }
    }
}
