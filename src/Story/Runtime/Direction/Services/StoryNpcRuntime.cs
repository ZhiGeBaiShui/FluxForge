namespace FluxForge.Story
{
    /// <summary>记录活动播放器与待处理交易结果，供客户离场、退出存档和卸载时清理。</summary>
    internal static class StoryNpcRuntime
    {
        private static StoryTradeResult _pendingTrade;
        private static StoryPlayer _activePlayer;
        private static string _activeClientId;

        internal static void BeginStory(
            string clientId,
            StoryPlayer player)
        {
            _activeClientId = clientId;
            _activePlayer = player;
        }

        internal static void EndStory()
        {
            _activePlayer = null;
            _activeClientId = null;
        }

        internal static void CancelForClient(string clientId)
        {
            if (_activeClientId != clientId)
            {
                return;
            }

            StoryPlayer player = _activePlayer;
            _activePlayer = null;
            _activeClientId = null;
            player.Cancel();
        }

        internal static void Shutdown()
        {
            StoryPlayer player = _activePlayer;
            EndStory();
            ClearTradeCompleted();
            if (player != null)
            {
                player.Cancel();
            }
        }

        internal static void QueueTradeCompleted(StoryTradeResult result)
        {
            _pendingTrade = result;
        }

        internal static StoryTradeResult TakeTradeCompleted()
        {
            StoryTradeResult result = _pendingTrade;
            _pendingTrade = null;
            return result;
        }

        internal static void ClearTradeCompleted()
        {
            _pendingTrade = null;
        }
    }
}
