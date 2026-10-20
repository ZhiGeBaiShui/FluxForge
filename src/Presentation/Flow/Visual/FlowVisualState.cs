namespace ProbablyBagMod.Presentation.Flow.Visual
{
    /// <summary>
    /// 温度档。阈值见 <see cref="FlowTierMap.TempFrom"/>：≤5℃ 冷、≥60℃ 热，其余常温。
    /// 只用于染色（<see cref="FlowTint.Edge"/>），**不进图集 key**。
    /// </summary>
    public enum FlowTemp
    {
        Cold = 0,
        Normal = 1,
        Hot = 2,
    }

    /// <summary>
    /// 含量档。四档的图集 tile 尺寸按 SPEC §1 递进：6×1 / 6×6 / 8×8 / 12×8。
    /// </summary>
    public enum FlowContent
    {
        Empty = 0,
        Small = 1,
        Mid = 2,
        Large = 3,
    }

    /// <summary>
    /// 流源状态（SPEC §5）。图集里每个状态都有静态 tile；
    /// 只有 <see cref="Flow"/> 另有 12 帧动效。
    /// </summary>
    public enum FlowState
    {
        Flow = 0,
        Blocked = 1,
        Split = 2,
        Merge = 3,
        Collect = 4,
    }

    /// <summary>
    /// 流动方向。整数 0/1/2/3 与游戏里 <c>Motion.Direction</c> 的顺序一致（上/右/下/左）。
    /// </summary>
    public enum FlowDir
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3,
    }

    /// <summary>
    /// 一次绘制所需的全部视觉属性。**纯数据、零 Unity 依赖**，可以脱离游戏单测。
    ///
    /// A 方案（REFACTOR-A §4）冻结的 key 只有三段 + 可选帧号：
    /// <c>content|state|dir[|fN]</c>。
    ///   · 成分不再进 key：颜色由 <see cref="Composition"/> 在运行时乘到灰度蒙版上；
    ///   · 温度也不再进 key：<see cref="Temp"/> 只驱动边缘染色（冷/热），不改形状；
    ///   · <see cref="Range"/> 仍不进 key：射程渲染已移除（字段保留，等 RangeRemaining 就绪再接）。
    ///
    /// 枚举名统一用 <c>static readonly string[]</c> 查表，
    /// **绝不调用 <c>Enum.ToString()</c>**——后者走反射 + 装箱，是每帧热路径上最不该出现的分配。
    /// </summary>
    public readonly struct FlowVisualState
    {
        /// <summary>静态 tile 的帧哨兵：<see cref="Frame"/> = -1 表示不追加 <c>|f{n}</c>。</summary>
        public const int NoFrame = -1;

        /// <summary>动效帧数（f0..f11），与 SPEC §6.1 的 12 帧包络一致。</summary>
        public const int FrameCount = 12;

        // 查表用的枚举名。下标必须与枚举值一一对应；顺序即图集 key 的通道顺序。
        private static readonly string[] ContentNames = { "empty", "small", "mid", "large" };
        private static readonly string[] StateNames = { "flow", "blocked", "split", "merge", "collect" };
        private static readonly string[] DirNames = { "up", "right", "down", "left" };

        // 动效帧后缀也预生成：避免每帧 int.ToString()。
        private static readonly string[] FrameSuffixes =
        {
            "|f0", "|f1", "|f2", "|f3", "|f4", "|f5",
            "|f6", "|f7", "|f8", "|f9", "|f10", "|f11",
        };

        /// <summary>
        /// 六分量成分。只影响运行时颜色（<see cref="FlowTint.Body"/>），不参与 key。
        /// </summary>
        public readonly FlowComposition Composition;

        /// <summary>温度档。只影响边缘染色（<see cref="FlowTint.Edge"/>），不参与 key。</summary>
        public readonly FlowTemp Temp;

        /// <summary>含量（key 第 1 段）。</summary>
        public readonly FlowContent Content;

        /// <summary>状态（key 第 2 段）。</summary>
        public readonly FlowState State;

        /// <summary>方向（key 第 3 段）。</summary>
        public readonly FlowDir Dir;

        /// <summary>
        /// 动效帧序号。构造时统一钳到 -1..11：<see cref="NoFrame"/>（-1）表示用静态 tile，
        /// 0..11 对应图集里的 f0..f11。只有 <see cref="FlowState.Flow"/> 会把它写进 key。
        /// </summary>
        public readonly int Frame;

        /// <summary>
        /// 剩余射程档 0..3。**0 = 未知**。
        /// 字段保留给后续 RangeRemaining 数据；当前渲染层不画射程点，也不参与 key。
        /// </summary>
        public readonly int Range;

        /// <summary>
        /// 构造一个视觉状态。<paramref name="composition"/> 必须显式传入（通常取
        /// <see cref="FlowComposition.PureWater"/> 作为 Tier-0 默认值）；
        /// <paramref name="frame"/> 缺省为 <see cref="NoFrame"/>（静态 tile），
        /// 渲染层在 flow 状态时显式传 0..11。越界帧在这里统一钳到 -1..11，
        /// 后续 <see cref="ToKey"/> 与图集缓存不再各自兜底。
        /// </summary>
        public FlowVisualState(
            FlowComposition composition,
            FlowTemp temp,
            FlowContent content,
            FlowState state,
            FlowDir dir,
            int frame = NoFrame,
            int range = 0)
        {
            Composition = composition;
            Temp = temp;
            Content = content;
            State = state;
            Dir = dir;

            // 帧越界的唯一钳制点：-1 是静态哨兵，0..11 是合法动效帧。
            // 这样 ToKey 的查表与图集缓存的位打包都只需处理已归一化的值。
            if (frame < NoFrame)
            {
                frame = NoFrame;
            }
            else if (frame >= FrameCount)
            {
                frame = FrameCount - 1;
            }

            Frame = frame;
            Range = range;
        }

        /// <summary>
        /// 生成图集索引 key：<c>content|state|dir</c>，全小写、竖线分隔。
        /// 仅当 <see cref="State"/> 是 flow 且 <see cref="Frame"/> ≥ 0 时追加 <c>|f{n}</c>，
        /// 例如 <c>large|flow|right|f7</c>。
        ///
        /// 分配：每次调用会产生一个 key 字符串（枚举名来自查表，不经过 Enum.ToString）。
        /// 渲染层应缓存上一次的 key，只在状态/帧变化时重算。
        /// </summary>
        public string ToKey()
        {
            string head = string.Concat(
                ContentName(Content), "|",
                StateName(State), "|",
                DirName(Dir));

            if (State != FlowState.Flow || Frame < 0)
            {
                return head;
            }

            // 构造时已把 Frame 钳到 0..11，所以这里不需要越界兜底
            return string.Concat(head, FrameSuffixes[Frame]);
        }

        /// <summary>含量的 key 片段。传入越界枚举值时回退为 empty。</summary>
        public static string ContentName(FlowContent content)
        {
            return NameAt(ContentNames, (int)content);
        }

        /// <summary>状态的 key 片段。传入越界枚举值时回退为 flow。</summary>
        public static string StateName(FlowState state)
        {
            return NameAt(StateNames, (int)state);
        }

        /// <summary>方向的 key 片段。传入越界枚举值时回退为 up。</summary>
        public static string DirName(FlowDir dir)
        {
            return NameAt(DirNames, (int)dir);
        }

        /// <summary>
        /// 越界安全的查表：枚举被强转成非法值时返回表首项（cold/empty/flow/up），
        /// 而不是让数组下标异常冒到每帧的渲染循环里。
        /// </summary>
        private static string NameAt(string[] table, int index)
        {
            if ((uint)index >= (uint)table.Length)
            {
                return table[0];
            }

            return table[index];
        }
    }
}
