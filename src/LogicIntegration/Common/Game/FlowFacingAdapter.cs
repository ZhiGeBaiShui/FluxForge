namespace FluxForge.Mod
{
    /// <summary>
    /// 游戏物品朝向 → 方向码（0上 1右 2下 3左）。
    ///
    /// 这是游戏/适配层的东西：Logic 只认识 Facing 0..3，不认识
    /// <c>modifiedShape.orientation</c>。发射方向与分流器 Facing 共用这一份换算。
    /// </summary>
    public static class FlowFacingAdapter
    {
        public static int FromOrientation(int orientation)
        {
            return (4 - (orientation & 3)) & 3;
        }
    }
}
