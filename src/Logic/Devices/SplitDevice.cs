using System;
using System.Collections.Generic;

namespace ProbablyBagMod
{
    /// <summary>
    /// 同步分流装置。
    ///
    /// 通道模型：
    ///   · 装置由「通道方向集」定义，数组长度 = 通道数（2 = 一字直通、3 = T 型、4 = 十字）
    ///   · 通道方向是**通道口的朝向**，不是流源的进入方向——
    ///     通道朝上时，流源要向下运动才进得来（入口方向恒为通道方向的反方向）
    ///   · 流源从**任意**通道进来都可以被接收
    ///   · 每个通道有独立的**发射数量**：同一个出口可以发 1 道、2 道乃至更多道流源
    ///     （入口通道的发射数量被忽略；发射 0 道 = 该通道不出流）
    ///   · 流源属性按「总发射数」等分，每道各拿一份（总量守恒）
    ///   · 「同步」= 分完立刻注册进 <see cref="FlowSpawner"/>（延迟 0）
    ///   · 通道方向按「相对 Facing」存储：装置旋转（索引 +1 = 右旋 90°）后全部通道一起转
    /// </summary>
    public class SplitDevice : IFlowRouter
    {
        /// <summary>通道方向集（相对 Facing）。数组长度即装置通道数。</summary>
        private readonly int[] _absoluteChannelDirs;

        /// <summary>按 Facing 换算后的世界方向。</summary>
        private readonly int[] _rotatedChannelDirs;

        /// <summary>每个通道的发射数量（与通道集同序）。入口通道的值不参与计算。</summary>
        private readonly int[] _emitCounts;

        /// <summary>旋转（0=上 1=右 2=下 3=左）。赋值后立即重算旋转后的通道方向。</summary>
        private int _facing;

        /// <summary>每个通道默认发射 1 道。</summary>
        public SplitDevice(int facing, int[] absoluteChannelDirs)
            : this(facing, absoluteChannelDirs, null)
        {
        }

        /// <summary>
        /// 指定每个通道的发射数量。
        /// </summary>
        /// <param name="facing">旋转（0=上 1=右 2=下 3=左）。</param>
        /// <param name="absoluteChannelDirs">通道方向集，长度即通道数。</param>
        /// <param name="emitCounts">
        /// 与通道集同序的发射数量；null = 每个通道 1 道。
        /// 0 = 该通道不出流，2 以上 = 同一通道发多道。
        /// </param>
        public SplitDevice(int facing, int[] absoluteChannelDirs, int[] emitCounts)
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

            ValidateDirection(facing, nameof(facing));
            ValidateChannelDirs(absoluteChannelDirs);

            // 复制一份，避免调用方后续改数组导致旋转结果与源数据不一致
            _absoluteChannelDirs = (int[])absoluteChannelDirs.Clone();
            _rotatedChannelDirs = new int[_absoluteChannelDirs.Length];
            _emitCounts = BuildEmitCounts(emitCounts, _absoluteChannelDirs.Length);
            _facing = facing;
            RecalculateRotatedChannelDirs();
        }

        public int Facing
        {
            get { return _facing; }
            set
            {
                ValidateDirection(value, nameof(Facing));
                _facing = value;
                RecalculateRotatedChannelDirs();
            }
        }

        private void RecalculateRotatedChannelDirs()
        {
            for (int i = 0; i < _rotatedChannelDirs.Length; i++)
            {
                _rotatedChannelDirs[i] = (_absoluteChannelDirs[i] + _facing) % 4;
            }
        }

        /// <inheritdoc/>
        public bool Route(FlowEntity incoming)
        {
            int direction = incoming.Motion.Direction;
            if (!CanEnter(direction))
            {
                return false;
            }

            // 入口通道的朝向 = 流源运动方向的反方向
            int enterChannel = Opposite(direction);

            // 统计所有出口的总发射数；只有一个出口有量时顺带记下它
            int exitChannel = -1;
            int total = 0;
            for (int i = 0; i < _rotatedChannelDirs.Length; i++)
            {
                if (_rotatedChannelDirs[i] == enterChannel || _emitCounts[i] <= 0)
                {
                    continue;
                }

                exitChannel = _rotatedChannelDirs[i];
                total += _emitCounts[i];
            }

            // 所有出口都不出流 = 吸收
            if (total == 0)
            {
                FlowSpawner.Instance.Kill(incoming);
                return true;
            }

            // 单出口单发：原地改方向，流源身份不变（直通 / 转向）
            if (total == 1)
            {
                incoming.Motion.Direction = exitChannel;
                return true;
            }

            // 多道：属性按总发射数等分，每道拿一份
            List<FlowProperties> parts = incoming.Properties.Split(total);

            int index = 0;
            for (int i = 0; i < _rotatedChannelDirs.Length; i++)
            {
                if (_rotatedChannelDirs[i] == enterChannel)
                {
                    continue;
                }

                int count = _emitCounts[i];
                for (int k = 0; k < count; k++)
                {
                    FlowMotionData motion = new FlowMotionData(_rotatedChannelDirs[i], incoming.Motion.Speed);
                    FlowPos position = new FlowPos(incoming.Position.X, incoming.Position.Y);
                    FlowEntity entity = new FlowEntity(position, motion, parts[index], incoming.Grid);
                    index++;

                    FlowSpawner.Instance.Register(entity, 0d);
                }
            }

            FlowSpawner.Instance.Kill(incoming);
            return true;
        }

        /// <summary>
        /// 入口检测：流源的运动方向必须是某个通道朝向的**反方向**——
        /// 通道朝上时，流源得向下运动才进得来。任意通道都接受流入。
        /// </summary>
        /// <param name="direction">流源当前的移动朝向（0=上 1=右 2=下 3=左）。</param>
        private bool CanEnter(int direction)
        {
            int enterChannel = Opposite(direction);
            for (int i = 0; i < _rotatedChannelDirs.Length; i++)
            {
                if (_rotatedChannelDirs[i] == enterChannel)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>反方向：0↔2、1↔3。</summary>
        private static int Opposite(int direction)
        {
            return (direction + 2) % 4;
        }

        /// <summary>发射数量：null → 每通道 1 道；否则长度须与通道集一致、每项非负。</summary>
        private static int[] BuildEmitCounts(int[] emitCounts, int channelCount)
        {
            if (emitCounts == null)
            {
                int[] defaults = new int[channelCount];
                for (int i = 0; i < channelCount; i++)
                {
                    defaults[i] = 1;
                }
                return defaults;
            }

            if (emitCounts.Length != channelCount)
            {
                throw new ArgumentException(
                    "发射数量必须与通道集等长：通道 " + channelCount + " 个，发射数量 " + emitCounts.Length + " 个",
                    nameof(emitCounts));
            }

            for (int i = 0; i < emitCounts.Length; i++)
            {
                if (emitCounts[i] < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(emitCounts),
                        "发射数量不能为负：第 " + i + " 个是 " + emitCounts[i]);
                }
            }

            return (int[])emitCounts.Clone();
        }

        /// <summary>
        /// 校验通道方向集：每个值都合法，且互不重复。
        /// 超范围的值会被 <c>% 4</c> 悄悄折回成另一个方向；重复会让入口被跳过两次、少发流源。
        /// </summary>
        private static void ValidateChannelDirs(int[] directions)
        {
            for (int i = 0; i < directions.Length; i++)
            {
                ValidateDirection(directions[i], nameof(directions));

                for (int j = i + 1; j < directions.Length; j++)
                {
                    if (directions[j] == directions[i])
                    {
                        throw new ArgumentException(
                            "通道方向重复：" + directions[i] + "（第 " + i + " 个和第 " + j + " 个）",
                            nameof(directions));
                    }
                }
            }
        }

        /// <summary>校验单个方向值：必须是 0~3（0=上 1=右 2=下 3=左）。</summary>
        private static void ValidateDirection(int direction, string paramName)
        {
            if (direction < 0 || direction > 3)
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    "方向必须是 0~3（0=上 1=右 2=下 3=左），收到 " + direction);
            }
        }
    }
}
