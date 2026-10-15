namespace ProbablyBagMod
{
    /// <summary>
    /// 流源：在背包格间传导的「一团东西」。
    ///
    /// 它是三层共用的核心对象：
    ///   · 运动层管理它（推进、转向）
    ///   · 装置的注册入口（分流/汇聚产生的新流源就是它）
    ///   · 待生成容器的条目本体
    ///
    /// 纯逻辑对象，不含任何 Unity 内容。进入游戏后，由 mod 层为每个 FlowEntity
    /// 配一个 GameObject 壳（壳持有本对象引用），显式与动画全在壳上——本类不知道壳存在。
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
