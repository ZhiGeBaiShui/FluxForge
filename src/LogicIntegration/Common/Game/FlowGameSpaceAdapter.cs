namespace FluxForge.Mod
{
    /// <summary>
    /// Logic 坐标与游戏格坐标之间的唯一换算点。
    ///
    /// Logic 的整数是格中心；游戏格 0 覆盖 [0,1)，中心是 0.5。
    /// 因此：
    ///   Logic 中心 L  → 游戏中心 L + 0.5
    ///   游戏中心 G    → Logic 中心 G - 0.5
    /// </summary>
    public static class FlowGameSpaceAdapter
    {
        public static double ToGameCenter(double logicCenter)
        {
            return logicCenter + 0.5d;
        }

        public static double ToLogicCenter(double gameCenter)
        {
            return gameCenter - 0.5d;
        }
    }
}
