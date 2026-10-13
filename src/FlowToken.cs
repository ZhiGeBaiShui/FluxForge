namespace ProbablyBagMod
{
    /// <summary>
    /// 流源：在网格中流动的载体。
    /// 只含运动信息 + 不透明载荷。
    /// </summary>
    public sealed class FlowToken
    {
        /// <summary>当前所在格子。</summary>
        public GridPos Position { get; set; }

        /// <summary>当前朝向。</summary>
        public Direction Direction { get; set; }

        /// <summary>速度（格/帧）。为后续加速类装置预留，当前实现按整数步数处理。</summary>
        public float Speed { get; set; }

        /// <summary>载荷。框架不解释其内容。</summary>
        public IFlowPayload Payload { get; set; }

        public FlowToken(GridPos position, Direction direction, IFlowPayload payload, float speed = 1f)
        {
            Position = position;
            Direction = direction;
            Payload = payload;
            Speed = speed;
        }
    }
}
