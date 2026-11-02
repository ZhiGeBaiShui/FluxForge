namespace FluxForge.Tests
{
    /// <summary>
    /// 测试专用分流装置：任意通道集 + 任意发射数量。
    ///
    /// <see cref="SplitDevice"/> 的构造是 <c>protected</c>——通道配置是装置类型的固有属性，
    /// 产品程序集不暴露可配构造，也不放测试钩子。测试通过子类拿到基类能力，
    /// 用来覆盖 Turn / TJunction / DoubleEmit 三个常量变体之外的边界组合。
    ///
    /// 参数语义与基类完全一致（见 <see cref="SplitDevice"/>）：
    ///   · <paramref name="relativeChannelDirs"/> 是相对 Facing 的通道朝向，长度即通道数；
    ///   · <paramref name="emitCounts"/> 与通道集同序，null = 每个通道 1 道，
    ///     入口通道的值不参与计算，0 = 该通道不出流。
    /// 校验也全部由基类完成（重复方向 / 长度不匹配 / 少于 2 通道 / 负数发射数）。
    /// </summary>
    internal sealed class AnySplit : SplitDevice
    {
        public AnySplit(int[] relativeChannelDirs, int[] emitCounts = null)
            : base(relativeChannelDirs, emitCounts)
        {
        }
    }
}
