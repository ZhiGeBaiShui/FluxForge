using System;

namespace FluxForge.Story
{
    /// <summary>把节点的玩家选项装配到原版 Dialogue。</summary>
    internal static class StoryDialogueAdapter
    {
        internal static void Play(Dialogue dialogue)
        {
            DialogUIManager.Instance.StartDialogue(
                dialogue,
                forcedStart: true);
        }

        internal static Dialogue BuildOptions(
            StoryNode node,
            Action<StoryEdge, Dialogue> runOption)
        {
            Dialogue dialogue = new Dialogue();
            dialogue.SetText(string.Empty, string.Empty);
            AttachOptions(dialogue, node, runOption);
            return dialogue;
        }

        internal static Dialogue CreateLine(StoryLineStep line, Func<string> npcName)
        {
            string speaker = string.Empty;
            if (line.Speaker == StorySpeaker.Npc)
            {
                speaker = npcName();
            }
            else if (line.Speaker == StorySpeaker.Player)
            {
                speaker = "PLAYER";
            }

            Dialogue dialogue = new Dialogue();
            dialogue.SetText(speaker, line.Text);
            dialogue.endActionDelay = line.Pause;
            DialoguePacing.Set(dialogue, line.Pause);
            return dialogue;
        }

        internal static void AttachOptions(
            Dialogue dialogue,
            StoryNode node,
            Action<StoryEdge, Dialogue> runOption)
        {
            foreach (StoryEdge option in node.Outgoing)
            {
                string label = option.Label;
                Dialogue selected = new Dialogue();
                selected.SetText("PLAYER", label);

                selected.SetStartAction(delegate
                {
                    runOption(option, selected);
                });

                // SelectChoice 先选中菜单载体，ContinueConversation 再进入下一句；载体不显示为对白。
                Dialogue choice = new Dialogue();
                choice.SetText(string.Empty, label);
                choice.SetNextDialogue(selected);
                dialogue.AddChoice(
                    DirectoryMaster.Item(option.IconItemKey),
                    label,
                    choiceNextDialogue: choice);
            }
        }
    }
}
