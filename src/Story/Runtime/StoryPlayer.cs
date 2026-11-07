using System;
using System.Threading.Tasks;

namespace FluxForge.Story
{
    /// <summary>监控一轮剧情，完成后推进到下一个节点或退出。</summary>
    internal sealed class StoryPlayer
    {
        private readonly GameGridInventory _grid;
        private readonly Func<string> _npcName;
        private readonly Action _onExit;
        private StoryAssembly _assembly;

        internal StoryPlayer(
            GameGridInventory grid,
            Func<string> npcName,
            Action onExit)
        {
            _grid = grid;
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

        private async Task RunEdge(
            StoryEdge edge,
            Dialogue prefix)
        {
            StorySequence sequence = new StorySequence(
                _grid,
                _npcName,
                prefix);

            await edge.Execute(sequence);

            if (edge.To == null)
            {
                await sequence.Run(null, null);
                ExitStory();
                return;
            }

            bool attached = await sequence.Run(edge.To, RunOption);

            if (!attached)
            {
                StoryNodeDialogue.Play(
                    StoryNodeDialogue.Build(edge.To, RunOption));
            }
        }

        private void ExitStory()
        {
            _assembly.Complete();
            _onExit();
        }
    }
}
