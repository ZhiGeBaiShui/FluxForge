namespace FluxForge.Story
{
    /// <summary>
    /// 剧情装载类的统一入口。
    /// 剧情结束时由 StoryPlayer 自动调用 Complete，不需要调用方再手写完成回调。
    /// </summary>
    internal abstract class StoryAssembly
    {
        internal abstract StoryEdge Entry { get; }

        /// <summary>剧情完成动作；不需要任何结束时动作的剧情可以不重写。</summary>
        internal virtual void Complete()
        {
        }
    }
}
