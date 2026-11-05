namespace FluxForge.Mod
{
    /// <summary>
    /// Mod 容量统一使用 µL；只有原版 WaterHelper 的容器初始化仍以 mL 为单位。
    /// 所有装配件都从这里过边界，避免各装置自己维护两套单位。
    /// </summary>
    internal static class FlowCapacity
    {
        public static int ToMillilitres(int microlitres)
        {
            return microlitres / 1000;
        }
    }
}
