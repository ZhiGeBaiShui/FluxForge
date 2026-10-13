namespace ProbablyBagMod
{
    /// <summary>
    /// 装置处理流源后的一份输出：朝某方向出去，携带某载荷。
    /// 装置可返回 0 份（吸收）、1 份（直通/转向）或多份（分流）。
    /// </summary>
    public readonly struct FlowOutput
    {
        public readonly Direction Direction;
        public readonly IFlowPayload Payload;

        public FlowOutput(Direction direction, IFlowPayload payload)
        {
            Direction = direction;
            Payload = payload;
        }
    }
}
