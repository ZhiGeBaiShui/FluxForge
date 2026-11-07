using System;

namespace FluxForge.Story
{
    /// <summary>把节点的玩家选项装配到原版 Dialogue。</summary>
    internal static class StoryNodeDialogue
    {
        internal static void Play(Dialogue dialogue)
        {
            DialogUIManager.Instance.StartDialogue(
                dialogue,
                forcedStart: true);
        }

        internal static Dialogue Build(
            StoryNode node,
            Action<StoryEdge, Dialogue> runOption)
        {
            Dialogue dialogue = new Dialogue();
            dialogue.SetText("", "");
            Attach(dialogue, node, runOption);
            return dialogue;
        }

        internal static void Attach(
            Dialogue dialogue,
            StoryNode node,
            Action<StoryEdge, Dialogue> runOption)
        {
            foreach (StoryEdge option in node.Outgoing)
            {
                Dialogue label = new Dialogue();
                label.SetText(PlayerStore.Instance.playerName, option.Label);

                Dialogue selected = new Dialogue();
                selected.SetText("PLAYER", option.Label);

                label.SetNextDialogue(selected);

                StoryEdge chosen = option;
                Dialogue chosenLine = selected;

                selected.SetStartAction(delegate
                {
                    runOption(chosen, chosenLine);
                });

                dialogue.AddChoice(
                    DirectoryMaster.Item(option.IconItemKey),
                    option.Label,
                    choiceNextDialogue: label);
            }
        }
    }
}
