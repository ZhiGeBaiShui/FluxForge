namespace FluxForge
{
    /// <summary>
    /// 三通分流装置：3 通道，相对通道 { 上(0), 右(1), 左(3) }（枝朝上、缺下口），
    /// 每个通道 1 道。
    ///
    /// Facing 0 的真值表（进入方向 → 出口方向）：
    ///   · 向右进入 → 上 + 右（2 路）
    ///   · 向下进入 → 右 + 左（2 路）
    ///   · 向左进入 → 上 + 左（2 路）
    ///   · 向上进入 → 通道不匹配，不被接收，正常穿过
    ///
    /// 常量基准是「枝朝上」。若贴图画成常见的枝朝下 T 形，常量应改成 { 3, 2, 1 }，
    /// emitCounts 不变（见 docs/device-binding-design.md §3.7）。
    /// </summary>
    public sealed class TJunctionDevice : SplitDevice
    {
        /// <summary>相对通道方向（相对美术原图，Facing 0 时为世界方向）。</summary>
        private static readonly int[] RelativeChannelDirs = new[] { 0, 1, 3 };

        /// <summary>每个通道各发 1 道；入口通道的值被忽略。</summary>
        private static readonly int[] EmitCounts = new[] { 1, 1, 1 };

        public TJunctionDevice()
            : base(RelativeChannelDirs, EmitCounts)
        {
        }
    }
}
