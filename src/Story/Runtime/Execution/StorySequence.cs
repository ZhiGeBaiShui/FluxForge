using System;
using System.Threading.Tasks;

namespace FluxForge.Story
{
    /// <summary>一轮边注册产生的对话序列。</summary>
    internal sealed class StorySequence
    {
        private readonly Func<string> _npcName;
        private readonly bool _hasPrefix;

        private Dialogue _first;
        private Dialogue _tail;

        internal StorySequence(
            Func<string> npcName,
            Dialogue prefix)
        {
            _npcName = npcName;
            _first = prefix;
            _tail = prefix;
            _hasPrefix = prefix != null;
        }

        internal void SayNpc(string content)
        {
            AddLine(_npcName(), content);
        }

        internal void SayPlayer(string content)
        {
            AddLine(PlayerStore.Instance.playerName, content);
        }

        internal void Do(Action action)
        {
            if (_tail == null)
            {
                action();
                return;
            }

            _tail.endAction = (Action)Delegate.Combine(
                action,
                _tail.endAction);
        }

        internal async Task<bool> Run(
            StoryNode node,
            Action<StoryEdge, Dialogue> runOption)
        {
            bool attached = node != null && _tail != null;

            if (attached)
            {
                StoryNodeDialogue.Attach(_tail, node, runOption);
            }

            if (_first != null)
            {
                TaskCompletionSource<bool> speech
                    = new TaskCompletionSource<bool>();

                _tail.endAction = (Action)Delegate.Combine(
                    _tail.endAction,
                    new Action(delegate
                    {
                        speech.TrySetResult(true);
                    }));

                if (!_hasPrefix)
                {
                    DialogUIManager.Instance.StartDialogue(
                        _first,
                        forcedStart: true);
                }

                await speech.Task;
            }

            return attached;
        }

        private void AddLine(string speaker, string content)
        {
            Dialogue line = new Dialogue();
            line.SetText(speaker, content);

            if (_first == null)
            {
                _first = line;
            }
            else
            {
                _tail.SetNextDialogue(line);
            }

            _tail = line;
        }
    }
}
