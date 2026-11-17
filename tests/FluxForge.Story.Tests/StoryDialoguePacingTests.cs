using FluxForge.Story;
using Xunit;

namespace FluxForge.Story.Tests
{
    public sealed class StoryDialoguePacingTests
    {
        public StoryDialoguePacingTests()
        {
            DialogUIManager.Instance = new DialogUIManager();
        }

        [Fact]
        public void NativeSkipDelaySurvivesAfterTheGameResetsItsSkipFlag()
        {
            Dialogue line = new Dialogue();
            DialoguePacing.Set(line, 2f);
            DialogUIManager.Instance.SetCurrentDialog(line);
            // 原版 OnCurrentTextDisplayed 先清除标记，再传入 0.01f；此时不能再靠标记识别跳过。
            DialogUIManager.Instance.isSkipCurrentDialog = false;
            float delay = 0.01f;

            DialogueAutoAdvancePatch.Prefix(ref delay);

            Assert.Equal(0.01f, delay);
        }

        [Fact]
        public void RegisteredLineUsesItsAuthoredPauseDuringNormalAdvance()
        {
            Dialogue line = new Dialogue();
            DialoguePacing.Set(line, 2f);
            DialogUIManager.Instance.SetCurrentDialog(line);
            float delay = 0.99f;

            DialogueAutoAdvancePatch.Prefix(ref delay);

            Assert.Equal(2f, delay);
        }

        [Fact]
        public void UnregisteredGameLineKeepsTheNativeDelay()
        {
            DialogUIManager.Instance.SetCurrentDialog(new Dialogue());
            float delay = 0.99f;

            DialogueAutoAdvancePatch.Prefix(ref delay);

            Assert.Equal(0.99f, delay);
        }
    }
}
