namespace ProbablyBagMod
{
    /// <summary>
    /// 流源：在背包格间传导的「一团东西」。纯数据对象。
    ///
    /// 不持有任何事件与方法——生命周期三时机全部由框架集中广播：
    ///   生：FlowSpawner.Register（装置注册）→ OnEntityAdded 广播
    ///   动：FlowManager.Tick 推进 → OnEntityMoved 广播
    ///   死：FlowSpawner.Kill（装置发起）→ OnEntityRemoved 广播
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
