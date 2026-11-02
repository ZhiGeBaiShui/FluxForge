namespace FluxForge
{
    /// <summary>
    /// L 型转向装置：开口在右、下两格，流源从右臂进入时转向下，从下臂进入时转向右。
    ///
    /// 通道方向是相对 Facing 的常量；转向行为完全复用 SplitDevice 的“原地改方向”。
    /// </summary>
    public sealed class LRightDownDevice : SplitDevice
    {
        public LRightDownDevice()
            : base(new[] { 1, 2 })
        {
        }
    }
}
