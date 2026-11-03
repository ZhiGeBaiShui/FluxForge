namespace FluxForge.Presentation.Flow.Visual
{
    /// <summary>
    /// 一帧内一条流源的全部视觉输入：位置/方向/速度 + 成分/温度/含量/状态。
    ///
    /// 这是贴图与粒子共用的**唯一**输入类型：视图每实体每帧构造一次，
    /// 贴图（<c>BuildDrawItem</c>）与粒子（<see cref="FlowParticleRules.Build"/>）
    /// 直接读同一个值，不再从快照往另一个结构体里逐个字段拷贝。
    ///
    /// 只读值类型：构造后不可变，<c>in</c> 传递零拷贝，纯逻辑层可单测。
    /// </summary>
    public readonly struct FlowFrameInput
    {
        /// <summary>图块左上角的连续 X，已由逻辑中心坐标减半格。</summary>
        public readonly double X;

        /// <summary>图块左上角的连续 Y，向下为正。</summary>
        public readonly double Y;

        /// <summary>运动方向（0=Up、1=Right、2=Down、3=Left）。</summary>
        public readonly int Direction;

        /// <summary>运动速度（格/秒）。</summary>
        public readonly double Speed;

        /// <summary>六分量成分（相对权重，不要求归一化）。</summary>
        public readonly FlowColor BodyColor;

        /// <summary>温度档。</summary>
        public readonly FlowTemp Temp;

        /// <summary>含量档（决定出生包围盒与拖尾层数）。</summary>
        public readonly FlowContent Content;

        /// <summary>流源状态（flow/blocked/split/merge/collect）。</summary>
        public readonly FlowState State;

        public FlowFrameInput(
            double x,
            double y,
            int direction,
            double speed,
            FlowColor bodyColor,
            FlowTemp temp,
            FlowContent content,
            FlowState state)
        {
            X = x;
            Y = y;
            Direction = direction;
            Speed = speed;
            BodyColor = bodyColor;
            Temp = temp;
            Content = content;
            State = state;
        }
    }
}
