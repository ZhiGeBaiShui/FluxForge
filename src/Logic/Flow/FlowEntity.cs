namespace ProbablyBagMod
{
    /// <summary>
    /// 流源：系统里流转的一个对象，携带全部游戏属性。纯数据对象。
    ///
    /// 它就是一个对象——不是水流，也不是「一团/一群」可以再分的东西。
    /// 同一时刻可能有很多个流源各自独立存在、各自移动，彼此没有关联。
    ///
    /// 不持有任何事件与方法——生命周期三时机全部由框架集中广播：
    ///   生：FlowSpawner.Accept（装置交付）→ OnEntityAdded 广播
    ///   动：FlowManager.Tick 推进 → 位置直接写在 Position 上（不广播，视觉层自己读）
    ///   死：FlowSpawner.Kill（装置发起）→ OnEntityRemoved 广播
    ///
    /// 不自带网格：网格属于机器，由 <see cref="FlowManager"/> 构造时注入，
    /// 所以一个流源只可能在它所属那台机器的网格里跑。
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

        public FlowEntity(FlowPos position, FlowMotionData motion, FlowProperties properties)
        {
            Position = position;
            Motion = motion;
            Properties = properties;
        }
    }
}
