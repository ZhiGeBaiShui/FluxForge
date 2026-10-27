namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 把物品 JSON 里的容量翻译成原版调用：<c>WaterHelper.InitLiquidContainerItem</c>。
    ///
    /// 这个装配件**不创造任何功能**——接水、容量限制、显示刷新、温度维护全部由原版负责。
    /// 它存在的唯一理由：<see cref="FlowItemDefinition"/> 是纯数据，不能自己调游戏 API，
    /// 总得有一行代码把 <c>capacityMl</c> 交出去。
    /// </summary>
    public sealed class FlowIntakeAttachment : ItemAttachment
    {
        public override string Name => "flow_intake";

        public override void Attach(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            Il2Cpp.WaterHelper.InitLiquidContainerItem(item, def.capacityMl, 0);
        }
    }
}
