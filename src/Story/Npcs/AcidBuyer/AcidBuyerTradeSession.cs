namespace FluxForge.Story.AcidBuyer
{
    /// <summary>累计本次到访各批高浓度交付体积，供首次问答和告别判定；所有交易边共享。</summary>
    internal sealed class AcidBuyerTradeSession
    {
        private const int SufficientHighPurityVolume = 100000;
        private int _highPurityVolume;

        internal bool ShouldShowHighPurityDialogue
        {
            get
            {
                return _highPurityVolume > 0
                    && !AcidBuyerProgress.HasHighPurityDialogueDone;
            }
        }

        internal bool HasSufficientHighPurity
        {
            get { return _highPurityVolume >= SufficientHighPurityVolume; }
        }

        internal void BeginVisit()
        {
            _highPurityVolume = 0;
        }

        internal void RecordHighPurityDelivery(int volume)
        {
            _highPurityVolume += volume;
        }
    }
}
