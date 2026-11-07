using System;
using System.Threading.Tasks;
using FluxForge.Events;

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
            _ = RunOptionAsync(option, selected);
        }

        private async Task RunOptionAsync(
            StoryEdge option,
            Dialogue selected)
        {
            if (option.RequiresEmptyGrid && !IsGridEmpty())
            {
                await RejectOption(option, selected);
                return;
            }

            if (!option.LocksGrid)
            {
                await RunEdge(option, selected);
                return;
            }

            // 选项按下的瞬间就锁，不等到 Flow 启动。
            FlowEventApi.LockGrid(_grid);
            try
            {
                await RunEdge(option, selected);
            }
            finally
            {
                FlowEventApi.UnlockGrid(_grid);
            }
        }

        private async Task RejectOption(
            StoryEdge option,
            Dialogue selected)
        {
            StorySequence sequence = new StorySequence(
                _grid,
                _npcName,
                selected);

            sequence.SayNpc(option.GridBusyMessage);

            bool attached = await sequence.Run(option.From, RunOption);

            if (!attached && option.From != null)
            {
                StoryNodeDialogue.Play(
                    StoryNodeDialogue.Build(option.From, RunOption));
            }
        }

        private bool IsGridEmpty()
        {
            return _grid.childItems == null
                || _grid.childItems.Count == 0;
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
