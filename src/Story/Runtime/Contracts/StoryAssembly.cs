namespace FluxForge.Story
{
    /// <summary>
    /// 剧情装载类的统一入口。
    /// 每个子图正常返回时由 StoryPlayer 调用 Complete；取消时不调用，也不代表整次来访已结束。
    /// </summary>
    internal abstract class StoryAssembly
    {
        internal abstract StoryEdge Entry { get; }

        /// <summary>子图正常完成时的动作；不需要完成动作的内容可以不重写。</summary>
        internal virtual void Complete()
        {
        }
    }
}
