namespace FluxForge
{
    /// <summary>
    /// 同口双发装置：2 通道，相对通道 { 上(0), 下(2) }，每个通道发 2 道。
    ///
    /// Facing 0 的真值表（进入方向 → 出口方向）：
    ///   · 向上进入 → 向上出 2 道
    ///   · 向下进入 → 向下出 2 道
    ///   · 向左 / 向右进入 → 通道不匹配，不被接收，正常穿过
    ///
    /// 双向双发（设计稿 §3.7）：两个方向进入都不会被吸收，也不依赖 Facing 及时同步，
    /// 旋转后不会静默吞流。两颗子流源同格、同向、同速，完全重合，视觉上像一道；
    /// 这是设计事实，不是 bug。
    ///
    /// 同口双发必须靠 emitCounts 表达，不能写重复方向（重复方向会被基类校验拒绝）。
    /// </summary>
    public sealed class DoubleEmitDevice : SplitDevice
    {
        /// <summary>相对通道方向（相对美术原图，Facing 0 时为世界方向）。</summary>
        private static readonly int[] RelativeChannelDirs = new[] { 0, 2 };

        /// <summary>每个通道发 2 道；入口通道的值被忽略。</summary>
        private static readonly int[] EmitCounts = new[] { 2, 2 };

        public DoubleEmitDevice()
            : base(RelativeChannelDirs, EmitCounts)
        {
        }
    }
}
