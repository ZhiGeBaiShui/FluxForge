using System.Collections.Generic;

namespace ProbablyBagMod
{
    /// <summary>
    /// 同步分流装置：流源一进来就分完、立刻注册出去。
    ///
    /// 所有分流形态（T 型、十字、转向……）共用这一个类，靠入口/出口方向集配置区分，
    /// 不为每种分流写单独类。
    ///
    /// 「同步」= 注册时延迟 0——下一个 Advance 就出队进运动层，不在容器里排队。
    /// （将来要「延迟重放」的装置，注册时给非 0 延迟即可，仍是同一个 <see cref="IFlowRouter"/>。）
    ///
    /// 方向集是「相对装置 Facing」的（0=上 1=右 2=下 3=左，索引 +1 = 右旋 90°）：
    /// 装置旋转后入口/出口一起跟着转，同一份配置在任意朝向下都成立。
    /// 入口方向按「流源的移动朝向」匹配——朝该方向走的流源才会被本装置接收。
    /// </summary>
    public class SplitDevice : IFlowRouter
    {
        /// <summary>同步分发：注册延迟 0 秒。</summary>
        private const double Immediate = 0d;

        /// <summary>旋转属性。后续由游戏侧转换赋值。</summary>
        public int Facing;

        private readonly int[] _entryDirs;   // 入口方向集（相对 Facing）
        private readonly int[] _exitDirs;    // 出口方向集（相对 Facing）

        public SplitDevice(int facing, int[] entryDirs, int[] exitDirs)
        {
            Facing = facing;
            _entryDirs = entryDirs;
            _exitDirs = exitDirs;
        }

        /// <summary>
        /// 路由：入口不符就一个都不注册（流源被吸收，此行为待确认）；
        /// 符合则把属性平分成「出口数」份，每份配一个出口方向，同步注册出去。
        /// </summary>
        public void Route(FlowEntity incoming)
        {
            if (!CanEnter(incoming.Motion.Direction))
            {
                return;
            }

            List<FlowProperties> parts = incoming.Properties.Split(_exitDirs.Length);
            for (int i = 0; i < _exitDirs.Length; i++)
            {
                FlowMotionData motion = new FlowMotionData
                {
                    Direction = Absolute(_exitDirs[i]),
                    Speed = incoming.Motion.Speed
                };

                // 新流源出生在流入流源的位置（装置格中心），属性用平分后的第 i 份。
                FlowEntity entity = new FlowEntity(incoming.Position, motion, parts[i]);
                FlowSpawner.Instance.Register(entity, Immediate);
            }
        }

        /// <summary>入口检测：流源朝向是否落在「入口集按 Facing 换算后」的集合里。</summary>
        private bool CanEnter(int direction)
        {
            for (int i = 0; i < _entryDirs.Length; i++)
            {
                if (Absolute(_entryDirs[i]) == direction)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>把相对 Facing 的方向换算成世界绝对方向。</summary>
        private int Absolute(int relativeDir)
        {
            return (relativeDir + Facing) % 4;
        }
    }
}
