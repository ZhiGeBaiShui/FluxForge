namespace FluxForge.Mod
{
    public sealed class ManualAttachment : ItemAttachment
    {
        public override string Name => "manual";
        public override void Attach(GameItem item, FlowItemDefinition def)
        {
            item.EnableTag("DOCUMENT");
            item.EnableTag("IMPORTANT_TAG");
            item.EnableTag("GUIDE_PAPER_TAG");
            item.EnableTag(def.manualTag);
            BookHelper.InitGuide(item);
            item.canActivateSlotItemFunc = (_, _, _) => HealthData.CanRead();
            item.mayActivateSlotItemFunc = (_, _, _) => HealthData.CanRead();
            item.onActivateSlotItemFunc = (manual, _, _) => ManualCatalog.Open(manual); // manual在这里定义，manual是GameItem类型，指当前装配的物品
        }
    }
}
