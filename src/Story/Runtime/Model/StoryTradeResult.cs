namespace FluxForge.Story
{
    /// <summary>交易完成时可供剧情条件读取的稳定结果。</summary>
    internal sealed class StoryTradeResult
    {
        internal bool Success { get; }
        internal int Amount { get; }
        internal string ItemKey { get; }

        internal StoryTradeResult(bool success, int amount, string itemKey)
        {
            Success = success;
            Amount = amount;
            ItemKey = itemKey;
        }
    }
}
