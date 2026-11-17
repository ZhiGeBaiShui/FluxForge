namespace FluxForge.Story.Hospital
{
    /// <summary>任务完成后返回不含原液提交选项的普通菜单。</summary>
    internal sealed class HospitalPickupMenu
    {
        internal StoryNode Ordinary { get; } = new StoryNode();
        internal StoryNode Task { get; } = new StoryNode();
        internal StoryNode Current
        {
            get { return HospitalSynaprestState.Active ? Task : Ordinary; }
        }
    }
}
