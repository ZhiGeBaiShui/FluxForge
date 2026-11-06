using System;
using System.Collections;
using System.Threading.Tasks;
using FluxForge.Events;
using UnityEngine;

namespace FluxForge.Story
{
    /// <summary>
    /// 剧情运行时：走边、显示节点/选项、台词、事件等待、延时。
    /// NPC 名字由外部提供；玩家名字统一取游戏存档。
    /// </summary>
    internal sealed class StoryPlayer
    {
        private readonly GameGridInventory _grid;
        private readonly Func<string> _npcName;
        private readonly Action _onExit;

        internal StoryPlayer(
            GameGridInventory grid,
            Func<string> npcName,
            Action onExit)
        {
            _grid = grid;
            _npcName = npcName;
            _onExit = onExit;
        }

        internal async Task Start(StoryEdge edge)
        {
            if (edge == null)
            {
                _onExit();
                return;
            }

            try
            {
                Debug.Log("[FluxForge][Story] start edge " + edge.Label);
                await edge.Execute(this);
                Arrive(edge.To);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _onExit();
            }
        }

        private void Arrive(StoryNode node)
        {
            if (node == null || node.Outgoing.Count == 0)
            {
                _onExit();
                return;
            }

            Dialogue dialogue = new Dialogue();
            dialogue.SetText(_npcName(), node.Prompt);

            foreach (StoryEdge option in node.Outgoing)
            {
                Dialogue label = new Dialogue()
                    .SetText(PlayerName, option.Label);

                dialogue.AddChoice(
                    DirectoryMaster.Item(option.IconItemKey),
                    option.Label,
                    choiceNextDialogue: label,
                    choiceAction: () =>
                        DialogUIManager.Instance.StartCoroutine(
                            StartAfterChoice(option)));
            }

            DialogUIManager.Instance.StartDialogue(dialogue, forcedStart: true);
        }

        internal Task SayNpc(string content)
        {
            return Say(_npcName(), content);
        }

        internal Task SayPlayer(string content)
        {
            return Say(PlayerName, content);
        }

        private Task Say(string speaker, string content)
        {
            TaskCompletionSource<bool> completion
                = new TaskCompletionSource<bool>();

            Dialogue line = new Dialogue();
            line.SetText(speaker, content);
            line.SetEndAction(() => completion.TrySetResult(true));
            Debug.Log("[FluxForge][Story] say: " + content);
            DialogUIManager.Instance.StartDialogue(line, forcedStart: true);

            return completion.Task;
        }

        internal Task RunFlow(Func<GameGridInventory, bool> flow)
        {
            TaskCompletionSource<bool> completion
                = new TaskCompletionSource<bool>();

            Action<GameGridInventory> handler = null;
            handler = grid =>
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

        internal Task Wait(float seconds)
        {
            TaskCompletionSource<bool> completion
                = new TaskCompletionSource<bool>();

            DialogUIManager.Instance.StartCoroutine(
                WaitRoutine(seconds, completion));

            return completion.Task;
        }

        private IEnumerator StartAfterChoice(StoryEdge edge)
        {
            Debug.Log("[FluxForge][Story] choice selected: " + edge.Label);
            yield return new WaitForSeconds(0.45f);
            _ = Start(edge);
        }

        private static string PlayerName
        {
            get { return PlayerStore.Instance.playerName; }
        }

        private static IEnumerator WaitRoutine(
            float seconds,
            TaskCompletionSource<bool> completion)
        {
            yield return new WaitForSeconds(seconds);
            completion.TrySetResult(true);
        }
    }
}
