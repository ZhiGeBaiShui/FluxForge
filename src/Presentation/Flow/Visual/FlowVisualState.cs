namespace FluxForge.Presentation.Flow.Visual
{
    /// <summary>
    /// 温度档：≤5℃ 冷、≥60℃ 热，其余常温。只影响边缘染色，不参与选图。
    /// </summary>
    public enum FlowTemp
    {
        Cold = 0,
        Normal = 1,
        Hot = 2,
    }

    /// <summary>含量档，决定图集中的形状大小。</summary>
    public enum FlowContent
    {
        Empty = 0,
        Small = 1,
        Mid = 2,
        Large = 3,
    }

    /// <summary>流源状态；只有 Flow 使用逐帧动画，其余状态使用静态贴图。</summary>
    public enum FlowState
    {
        Flow = 0,
        Blocked = 1,
        Split = 2,
        Merge = 3,
        Collect = 4,
    }

    /// <summary>流动方向，与 Motion.Direction 的 0/1/2/3（上/右/下/左）对应。</summary>
    public enum FlowDir
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3,
    }

    /// <summary>
    /// 单个流源的视觉参数，不持有 PNG、Sprite 或其他 Unity 对象。
    /// Content/State/Dir/Frame 用于选图，BodyColor/Temp 用于染色。
    /// </summary>
    public readonly struct FlowVisualState
    {
        /// <summary>静态贴图的帧标记。</summary>
        public const int NoFrame = -1;

        /// <summary>动画帧数，帧序号为 0..11。</summary>
        public const int FrameCount = 12;

        // 染色参数，不参与图集索引。
        /// <summary>本体色：已按各液体存量加权算好，直接用于三层染色。</summary>
        public readonly FlowColor BodyColor;

        /// <summary>温度档，用于计算边缘颜色。</summary>
        public readonly FlowTemp Temp;

        // 选图参数，组合成 content|state|dir[|fN]。
        /// <summary>含量档，决定形状大小。</summary>
        public readonly FlowContent Content;

        /// <summary>流动、受阻、分流等状态，决定贴图形态。</summary>
        public readonly FlowState State;

        /// <summary>流动方向，决定贴图朝向。</summary>
        public readonly FlowDir Dir;

        /// <summary>动画帧 0..11；-1 为静态贴图，仅 Flow 状态使用动画帧。</summary>
        public readonly int Frame;

        /// <summary>预留射程档，0 表示未知；当前不参与选图或绘制。</summary>
        public readonly int Range;

        /// <summary>创建视觉参数，将帧序号限制在 -1..11；默认使用静态贴图。</summary>
        public FlowVisualState(
            FlowColor bodyColor,
            FlowTemp temp,
            FlowContent content,
            FlowState state,
            FlowDir dir,
            int frame = NoFrame,
            int range = 0)
        {
            BodyColor = bodyColor;
            Temp = temp;
            Content = content;
            State = state;
            Dir = dir;

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
        /// 生成图集键，例如 large|flow|right|f7；非动画状态不带帧后缀。
        /// 每次调用会分配字符串，调用方应缓存查询结果。
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

            return string.Concat(head, _frameSuffixes[Frame]);
        }

        /// <summary>含量的 key 片段。传入越界枚举值时回退为 empty。</summary>
        public static string ContentName(FlowContent content)
        {
            return NameAt(_contentNames, (int)content);
        }

        /// <summary>状态的 key 片段。传入越界枚举值时回退为 flow。</summary>
        public static string StateName(FlowState state)
        {
            return NameAt(_stateNames, (int)state);
        }

        /// <summary>方向的 key 片段。传入越界枚举值时回退为 up。</summary>
        public static string DirName(FlowDir dir)
        {
            return NameAt(_dirNames, (int)dir);
        }

        // 图集键编码实现：下标对应枚举值，预存名称和帧后缀以避免装箱及数字转字符串。
        private static readonly string[] _contentNames = { "empty", "small", "mid", "large" };
        private static readonly string[] _stateNames = { "flow", "blocked", "split", "merge", "collect" };
        private static readonly string[] _dirNames = { "up", "right", "down", "left" };
        private static readonly string[] _frameSuffixes =
        {
            "|f0", "|f1", "|f2", "|f3", "|f4", "|f5",
            "|f6", "|f7", "|f8", "|f9", "|f10", "|f11",
        };

        /// <summary>非法枚举值回退到表中第一个名称。</summary>
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
