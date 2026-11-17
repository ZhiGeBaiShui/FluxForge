using System;
using System.Collections.Generic;

namespace FluxForge.Story
{
    /// <summary>一个来访内的播放器：有序步骤、子调用返回、命名插入及原版UI续接。</summary>
    internal sealed class StoryPlayer
    {
        private readonly Func<string> _npcName;
        private readonly StoryPlaybackContext _playback;
        private readonly Action _onExit;
        private readonly Stack<StoryCallFrame> _frames = new Stack<StoryCallFrame>();
        private bool _cancelled;

        internal StoryPlayer(
            Func<string> npcName, StoryPlaybackContext playback, Action onExit)
        {
            _npcName = npcName;
            _playback = playback;
            _onExit = onExit;
        }

        internal void Start(StoryAssembly assembly)
        {
            _frames.Push(new StoryCallFrame(assembly, null, _playback.Visit));
            Dialogue first = BuildNext(null);
            if (first != null)
            {
                StoryDialogueAdapter.Play(first);
            }
        }

        internal void Cancel()
        {
            _cancelled = true;
            DialogUIManager.Instance.InterruptDialog();
        }

        private void RunOption(StoryEdge option, Dialogue selected)
        {
            _frames.Peek().Select(option);
            BuildNext(selected);
        }

        private Dialogue Resume()
        {
            return BuildNext(null);
        }

        private Dialogue BuildNext(Dialogue prefix)
        {
            Dialogue first = prefix;
            Dialogue tail = prefix;
            while (!_cancelled && _frames.Count > 0)
            {
                StoryCallFrame frame = _frames.Peek();
                if (frame.Insertion != null)
                {
                    StoryEventDefinition insertion = frame.Insertion.Next();
                    if (insertion == null)
                    {
                        frame.Insertion = null;
                    }
                    else
                    {
                        _frames.Push(new StoryCallFrame(insertion.Content, insertion.Event, frame.Context));
                    }
                    continue;
                }

                if (frame.Sequence == null)
                {
                    frame.Register();
                }

                if (frame.Position < frame.Sequence.Steps.Count)
                {
                    StoryStep step = frame.Sequence.Steps[frame.Position];
                    if ((step is StoryCallStep || step is StoryInsertionStep) && tail != null)
                    {
                        tail.SetNextDialogue(StoryDialogueBridge.Create(Resume));
                        return first;
                    }

                    frame.Position++;
                    if (step is StoryLineStep)
                    {
                        StoryLineStep line = (StoryLineStep)step;
                        Dialogue dialogue = StoryDialogueAdapter.CreateLine(line, _npcName);
                        if (tail == null)
                        {
                            first = dialogue;
                        }
                        else
                        {
                            tail.SetNextDialogue(dialogue);
                        }
                        tail = dialogue;
                    }
                    else if (step is StoryActionStep)
                    {
                        StoryActionStep action = (StoryActionStep)step;
                        if (tail == null)
                        {
                            action.Action();
                        }
                        else
                        {
                            tail.endAction += delegate
                            {
                                if (!_cancelled)
                                {
                                    action.Action();
                                }
                            };
                        }
                    }
                    else if (step is StoryCallStep)
                    {
                        StoryCallStep call = (StoryCallStep)step;
                        _frames.Push(new StoryCallFrame(call.Content, call.Event, call.Context ?? frame.Context));
                    }
                    else if (step is StoryInsertionStep)
                    {
                        StoryInsertionStep insertion = (StoryInsertionStep)step;
                        frame.Insertion = new StoryInsertionCursor(_playback, frame.Context, insertion.Point);
                    }
                    continue;
                }

                if (frame.Next != null)
                {
                    if (tail == null)
                    {
                        return StoryDialogueAdapter.BuildOptions(frame.Next, RunOption);
                    }
                    StoryDialogueAdapter.AttachOptions(tail, frame.Next, RunOption);
                    return first;
                }

                if (tail != null)
                {
                    tail.SetNextDialogue(StoryDialogueBridge.Create(Resume));
                    return first;
                }

                _frames.Pop();
                frame.Content.Complete();
                if (frame.Event != null)
                {
                    _playback.Visit.State.MarkSeen(frame.Event.Id);
                }
            }

            if (!_cancelled)
            {
                _onExit();
            }
            return first;
        }
    }
}
