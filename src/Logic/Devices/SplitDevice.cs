namespace ProbablyBagMod
{
    /// <summary>
    /// 同步分流装置。
    ///
    /// 通道模型（逐步实现中——第三步：通道集就位，分发主体待填）：
    ///   · 装置由「通道方向集」定义，数组长度 = 通道数（如 2 = 一字直通、3 = T 型、4 = 十字）
    ///   · 流源从**任意**通道进来都可以被接收
    ///   · 分流 = 把流源从「除进入通道外的所有通道」分发出去——有几个非进入通道就分几道流源
    ///     （四通道：1 个入口 + 3 个出口 = 分 3 道流源）
    ///   · 「同步」= 分完立刻注册进 <see cref="FlowSpawner"/>（延迟 0）
    ///   · 通道方向按「相对 Facing」存储：装置旋转（索引 +1 = 右旋 90°）后全部通道一起转
    /// </summary>
    public class SplitDevice : IFlowRouter
    {
        /// <summary>旋转属性（0=上 1=右 2=下 3=左）。后续由游戏侧转换赋值。</summary>
        private int _facing;
        public int Facing
        {
            get { return _facing; }
            set
            {
                _facing = value;                              // 先存值
                RecalculateRotatedChannelDirs();              // 再用新值重算（你原来的顺序是反的，会用旧 Facing 重算）
            }
        }
        private void RecalculateRotatedChannelDirs()
        {
            for(int i = 0; i < _rotatedChannelDirs.Length; i++)
            {
                _rotatedChannelDirs[i] = (_absoluteChannelDirs[i] + _facing) % 4;
            }
        }

        /// <summary>通道方向集（相对 Facing）。数组长度即装置通道数。</summary>
        private readonly int[] _absoluteChannelDirs;
        private readonly int[] _rotatedChannelDirs;

        public SplitDevice(int facing, int[] absoluteChannelDirs)
        {
            if (absoluteChannelDirs == null)
            {
                throw new ArgumentNullException(nameof(absoluteChannelDirs));
            }
            if (absoluteChannelDirs.Length < 2)
            {
                throw new ArgumentException(
                    "通道数不能少于 2：转向/分流装置至少需要 1 个入口 + 1 个出口，收到 " + absoluteChannelDirs.Length + " 个",
                    nameof(absoluteChannelDirs));
            }
            _absoluteChannelDirs = absoluteChannelDirs;
            _rotatedChannelDirs = new int[absoluteChannelDirs.Length];
            Facing = facing;
        }
        /// <summary>路由入口工作函数</summary>
        public bool Route(FlowEntity incoming)
        {
            int direction = incoming.Motion.Direction;
            if (!CanEnter(direction))
            {
                return false;
            }
            List<FlowProperties> flowSplit = new List<FlowProperties>();
            if(_rotatedChannelDirs.Length == 2)
            {
                // 说明此装置只负责转向，不涉及到分割流源
                for(int i = 0; i < _rotatedChannelDirs.Length; i++)
                {
                    if(incoming.Motion.Direction == _rotatedChannelDirs[i])
                    {
                        continue;
                    }
                    incoming.Motion.Direction = _rotatedChannelDirs[i];
                }
                return true;
            }
            else
            {
                flowSplit.AddRange(incoming.Properties.Split(_rotatedChannelDirs.Length - 1));
            }
            int exitIndex = 0;
            for(int i = 0; i < _rotatedChannelDirs.Length; i++)
            {
                if(_rotatedChannelDirs[i] == direction)
                {
                    continue;
                }
                FlowMotionData flowMotionData = new FlowMotionData(_rotatedChannelDirs[i], incoming.Motion.Speed);
                FlowPos flowPos = new FlowPos(incoming.Position.X, incoming.Position.Y);
                FlowEntity flowEntity = new FlowEntity(flowPos, flowMotionData, flowSplit[exitIndex]);
                exitIndex++;
                FlowSpawner.Instance.Register(flowEntity, 0d);
                // TODO: FlowEntity需要运动层管理者注册，这里等运动层实现
            }
            FlowSpawner.Instance.Kill(incoming);
            return true;
        }

        /// <summary>
        /// 入口检测：流源朝向是否落在「通道集按 Facing 换算后」的集合里。
        /// 任意通道都接受流入——本方法只回答「这是不是本装置的通道」。
        /// </summary>
        /// <param name="direction">流源当前的移动朝向（0=上 1=右 2=下 3=左）。</param>
        private bool CanEnter(int direction)
        {
            for (int i = 0; i < _rotatedChannelDirs.Length; i++)
            {
                // 通道方向按「相对 Facing」存储，加 Facing 换算成世界绝对方向再比
                int absolute = _rotatedChannelDirs[i];
                if (absolute == direction)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
