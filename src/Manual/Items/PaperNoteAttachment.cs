using FluxForge.Mod;

namespace FluxForge.Manual
{
    public sealed class PaperNoteAttachment : ItemAttachment
    {
        public override string Name => "paper_note";

        public override void Attach(GameItem item, FlowItemDefinition def)
        {
            item.EnableTag("DOCUMENT");
            item.EnableTag("IMPORTANT_TAG");
            item.EnableTag("GUIDE_PAPER_TAG");
            BookHelper.InitPaperGuide(item);
            item.canActivateSlotItemFunc = (_, _, _) => HealthData.CanRead();
            item.mayActivateSlotItemFunc = (_, _, _) => HealthData.CanRead();
            item.onActivateSlotItemFunc = (_, _, _) => PaperNoteContent.Open(def.paperImage);
        }
    }
}
