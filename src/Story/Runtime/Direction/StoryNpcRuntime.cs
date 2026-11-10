namespace FluxForge.Story
{
    /// <summary>剧情内容向通用运行器传递交易完成结果。</summary>
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

        internal static void EndStory(string clientId)
        {
            if (_activeClientId == clientId)
            {
                _activePlayer = null;
                _activeClientId = null;
            }
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
