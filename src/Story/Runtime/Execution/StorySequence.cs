using System;
using System.Threading.Tasks;
using FluxForge.Events;

namespace FluxForge.Story
{
    /// <summary>一轮边注册产生的对话与事件序列。</summary>
    internal sealed class StorySequence
    {
        private readonly GameGridInventory _grid;
        private readonly Func<string> _npcName;
        private readonly bool _hasPrefix;

        private Dialogue _first;
        private Dialogue _previousTail;
        private Dialogue _tail;
        private Task _flowTail;
        private bool _hasFlows;

        internal StorySequence(
            GameGridInventory grid,
            Func<string> npcName,
            Dialogue prefix)
        {
            _grid = grid;
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

        internal void StartFlow(
            Func<GameGridInventory, bool> flow,
            Action onComplete = null)
        {
            _hasFlows = true;

            if (_tail == null)
            {
                _flowTail = RunFlowAfter(
                    _flowTail,
                    flow,
                    onComplete);
                return;
            }

            Action start = delegate
            {
                _flowTail = RunFlowAfter(
                    _flowTail,
                    flow,
                    onComplete);
            };

            _tail.endAction = (Action)Delegate.Combine(
                start,
                _tail.endAction);
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
            Dialogue finalLine = null;

            if (node != null && _hasFlows && _tail != null)
            {
                finalLine = _tail;
                _tail = _previousTail;

                if (_previousTail == null)
                {
                    _first = null;
                }
                else
                {
                    _previousTail.SetNextDialogue(null);
                }
            }

            bool attached = node != null
                && finalLine == null
                && _tail != null;

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

            await WaitFlows();

            if (finalLine != null)
            {
                StoryNodeDialogue.Attach(finalLine, node, runOption);
                StoryNodeDialogue.Play(finalLine);
                return true;
            }

            return attached;
        }

        private void AddLine(string speaker, string content)
        {
            Dialogue line = new Dialogue();
            line.SetText(speaker, content);

            _previousTail = _tail;

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

        private async Task RunFlowAfter(
            Task previous,
            Func<GameGridInventory, bool> flow,
            Action onComplete)
        {
            if (previous != null)
            {
                await previous;
            }

            Task current = StartFlowNow(flow);
            await current;

            if (onComplete != null)
            {
                onComplete();
            }
        }

        private async Task WaitFlows()
        {
            if (_flowTail != null)
            {
                await _flowTail;
            }
        }

        private Task StartFlowNow(Func<GameGridInventory, bool> flow)
        {
            TaskCompletionSource<bool> completion
                = new TaskCompletionSource<bool>();

            Action<GameGridInventory> handler = null;
            handler = delegate(GameGridInventory grid)
            {
                if (grid != _grid)
                {
                    return;
                }

                FlowEventApi.EventCompleted -= handler;
                FlowEventApi.UnlockGrid(_grid);
                completion.TrySetResult(true);
            };

            FlowEventApi.EventCompleted += handler;
            FlowEventApi.LockGrid(_grid);

            if (!flow(_grid))
            {
                FlowEventApi.EventCompleted -= handler;
                FlowEventApi.UnlockGrid(_grid);
                completion.TrySetResult(false);
            }

            return completion.Task;
        }
    }
}
