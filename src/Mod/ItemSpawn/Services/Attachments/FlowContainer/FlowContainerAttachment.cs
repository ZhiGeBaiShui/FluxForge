namespace FluxForge.Mod
{
    /// <summary>
    /// 原版水容器生成器：把物品变成原版的液体容器。
    ///
    /// 只用于需要接受外部倒水的装置（当前是流源发射器）。
    /// 不启用这套身份的装置使用 <see cref="DeviceContainerAttachment"/>。
    ///
    /// 它只做一件事——把 JSON 里的 <c>capacityMicrolitres</c> 转成原版 mL 后交给
    /// <c>WaterHelper.InitLiquidContainerItem</c>。接水、容量限制、显示刷新、
    /// 温度维护全部由原版负责，这里不实现任何行为。
    /// </summary>
    public sealed class FlowContainerAttachment : ItemAttachment
    {
        public override string Name => "flow_container";

        public override void Attach(GameItem item, FlowItemDefinition def)
        {
            WaterHelper.InitLiquidContainerItem(
                item,
                FlowCapacity.ToMillilitres(def.capacityMicrolitres),
                0);
        }
    }
}
