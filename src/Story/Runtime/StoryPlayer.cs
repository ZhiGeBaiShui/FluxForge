using System;
using System.Threading.Tasks;

namespace FluxForge.Story
{
    /// <summary>监控一轮剧情，完成后推进到下一个节点或退出。</summary>
    internal sealed class StoryPlayer
    {
        private readonly Func<string> _npcName;
        private readonly Action _onExit;
        private StoryAssembly _assembly;
        private StorySequence _sequence;
        private bool _cancelled;

        internal StoryPlayer(
            Func<string> npcName,
            Action onExit)
        {
            _npcName = npcName;
            _onExit = onExit;
        }

        internal Task Start(StoryAssembly assembly)
        {
            _assembly = assembly;
            return RunEdge(assembly.Entry, null);
        }

        internal void RunOption(StoryEdge option, Dialogue selected)
        {
            _ = RunEdge(option, selected);
        }

        internal void Cancel()
        {
            _cancelled = true;
            _sequence.Cancel();
            DialogUIManager.Instance.InterruptDialog();
        }

        private async Task RunEdge(
            StoryEdge edge,
            Dialogue prefix)
        {
            StorySequence sequence = new StorySequence(
                _npcName,
                prefix);
            _sequence = sequence;

            await edge.Execute(sequence);

            if (_cancelled)
            {
                return;
            }

            StoryNode next = edge.ResolveNext();
            if (next == null)
            {
                await sequence.Run(null, null);

                if (_cancelled)
                {
                    return;
                }

                ExitStory();
                return;
            }

            bool attached = await sequence.Run(next, RunOption);

            if (_cancelled)
            {
                return;
            }

            if (!attached)
            {
                StoryNodeDialogue.Play(
                    StoryNodeDialogue.Build(next, RunOption));
            }
        }

        private void ExitStory()
        {
            _assembly.Complete();
            _onExit();
        }
    }
}
