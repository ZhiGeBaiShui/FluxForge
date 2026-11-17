using System;

namespace FluxForge.Story
{
    /// <summary>ContinueConversation先运行startAction再显示currentDialog，因此续接行本身不显示。</summary>
    internal static class StoryDialogueBridge
    {
        internal static Dialogue Create(Func<Dialogue> resume)
        {
            Dialogue bridge = new Dialogue();
            bridge.SetText(string.Empty, string.Empty);
            bridge.SetStartAction(delegate
            {
                DialogUIManager.Instance.SetCurrentDialog(null);
                Dialogue next = resume();
                DialogUIManager.Instance.SetCurrentDialog(next);
            });
            return bridge;
        }
    }
}
