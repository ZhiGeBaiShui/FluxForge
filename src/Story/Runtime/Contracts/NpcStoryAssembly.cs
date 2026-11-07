namespace FluxForge.Story
{
    /// <summary>NPC 专用剧情装载类：声明阶段和实时前置条件。</summary>
    internal abstract class NpcStoryAssembly : StoryAssembly
    {
        internal abstract StoryPhase Phase { get; }

        internal abstract bool CanPlay();
    }
}
