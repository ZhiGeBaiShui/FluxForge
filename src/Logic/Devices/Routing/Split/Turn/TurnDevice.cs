namespace FluxForge
{
    /// <summary>
    /// L 型转向装置：2 通道，相对通道 { 左(3), 下(2) }，每个通道默认 1 道。
    ///
    /// Facing 0 的真值表（进入方向 → 出口方向）：
    ///   · 向右进入 → 向下穿出（右进下出，L 型）
    ///   · 向上进入 → 向左穿出
    ///   · 向下 / 向左进入 → 通道不匹配，不被接收，正常穿过
    ///
    /// 任意时刻只有一个出口有量（total == 1），Route 走「原地改方向」分支：
    /// 不新建流源，流源身份与属性完全不变，所以这是转向不是分流。
    /// 方向表必须按真值表全测，不能只测「右进下出」。
    /// </summary>
    public sealed class TurnDevice : SplitDevice
    {
        /// <summary>相对通道方向（相对美术原图，Facing 0 时为世界方向）。</summary>
        private static readonly int[] RelativeChannelDirs = new[] { 3, 2 };

        public TurnDevice()
            : base(RelativeChannelDirs)
        {
        }
    }
}
