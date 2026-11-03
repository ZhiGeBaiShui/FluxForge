namespace FluxForge.Mod
{
    /// <summary>
    /// 原版水瓶装配：按原版 SmallBottledWater 的方式初始化透明液体容器，
    /// 让 WaterHelper 注册 small_*_100 / small_hl_100 贴图修饰层。
    /// </summary>
    public sealed class WaterBottleAttachment : ItemAttachment
    {
        public override string Name => "water_bottle";

        public override void Attach(GameItem item, FlowItemDefinition def)
        {
            WaterHelper.InitLiquidContainerItem(
                item,
                def.capacityMl,
                0,
                isFilterContainer: false,
                isGraduated: false,
                isDropper: false,
                isTransparentContainer: true);
        }
    }
}
