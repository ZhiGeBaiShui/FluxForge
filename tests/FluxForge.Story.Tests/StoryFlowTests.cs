using FluxForge;
using FluxForge.Mod;
using FluxForge.Story;
using FluxForge.Story.AcidBuyer;
using FluxForge.Story.AcidBuyerArrival;
using FluxForge.Story.AcidBuyerRepeatVisit;
using FluxForge.Story.Hospital;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FluxForge.Story.Tests
{
    public sealed class StoryFlowTests
    {
        internal sealed class Conversation
        {
            internal bool Finished;
            internal Dialogue Last;
            internal List<(string Speaker, string Text)> Lines = new();
            internal StoryPlayer Player;

            internal Conversation(StoryAssembly assembly, bool advance = true)
            {
                NpcStoryContent content = AcidBuyerStory.Create();
                StoryContext visit = new StoryContext(
                    content.Definition, new NpcState(content.Definition),
                    StoryTrigger.VisitStarted, VisitKind.Dialogue,
                    StoreStation.Day, null);
                Player = new StoryPlayer(() => content.Definition.DisplayName,
                    new StoryPlaybackContext(content, visit, new HashSet<string>()),
                    () => Finished = true);
                Player.Start(assembly);
                if (advance) Advance(DialogUIManager.Instance.Current);
            }

            internal void Advance(Dialogue line)
            {
                while (line != null)
                {
                    DialogUIManager.Instance.SetCurrentDialog(line);
                    line.StartAction?.Invoke();
                    line = DialogUIManager.Instance.GetCurrentDialog();
                    if (line == null) break;
                    Assert.False(string.IsNullOrWhiteSpace(line.Text));
                    Lines.Add((line.Speaker, line.Text));
                    line.ExecuteEndAction();
                    Last = line;
                    line = line.Next;
                }
            }

            internal void Choose(string text)
            {
                // 对齐原版 SelectChoice → ContinueConversation：跳过菜单载体，从其下一句开始。
                Dialogue choice = Assert.Single(Last.Choices.Where(c => c.Text == text)).Next;
                Advance(choice.Next);
            }

            internal void HasChoice(string text)
            {
                Assert.Contains(Last.Choices, c => c.Text == text);
            }
        }

        public StoryFlowTests()
        {
            PlayerStore.Instance = new PlayerStore();
            EmporiumEntry.Instance = new EmporiumEntry();
            DialogUIManager.Instance = new DialogUIManager();
            StoreStation.Day = 0;
            HospitalReputation.EnsureFaction();
            TestHospitalReputation.Value = 0;
            FlowStock.Reads = 0;
        }

        private static GameItem Bottle(double ml, double concentration,
            FlowPropertyKey chemical = default)
        {
            if (chemical.Equals(default(FlowPropertyKey))) chemical = FlowProps.Acid;
            GameItem item = new GameItem();
            item.Tags.Add(FlowTags.LiquidContainerTag);
            item.Contents.Set(chemical, ml * 1000d * concentration);
            item.Contents.Set(FlowProps.ChemicalContaminant, ml * 1000d * (1d - concentration));
            EmporiumEntry.Instance.backInvinvElement.childItems.Add(item);
            return item;
        }

        private static HospitalContractOfferAssembly OfferAssembly()
        {
            return (HospitalContractOfferAssembly)AcidBuyerStory.Create()
                .FindEvent(HospitalContractOfferAssembly.EventId).Content;
        }

        private static void MarkOfferSeen()
        {
            // 此驱动直接播放装配，未经过已注册事件调用；手动补上 StoryPlayer 正常返回时记录的 SeenCount。
            new NpcState(AcidBuyerStory.Create().Definition)
                .MarkSeen(HospitalContractOfferAssembly.EventId);
        }

        private static Conversation Pickup()
        {
            HospitalContractState.Start(0);
            StoreStation.Day = 7;
            return new Conversation(new HospitalContractPickupAssembly());
        }

        private static void AssertNotSettled(GameItem item)
        {
            Assert.False(item.Destroyed);
            Assert.Equal(0, PlayerStore.Instance.Cash);
            Assert.Equal(0, TestHospitalReputation.Value);
            Assert.Equal(7, HospitalContractState.NextPickupDay);
        }

        [Fact]
        public void InspectionLeavesEmptyContainersOtherLiquidsAndEmittersOutOfTrade()
        {
            GameItem good = Bottle(300, .9);
            GameItem empty = Bottle(0, 1);
            GameItem water = Bottle(100, 1, FlowProps.Water);
            GameItem emitter = Bottle(100, .8);
            emitter.Tags.Add(FlowTags.FlowDevice); // flow_emitter 同时具有原版液体容器身份。
            GameItem other = Bottle(100, .8);
            other.Tags.Clear();
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
            Assert.True(good.Destroyed);
            Assert.False(empty.Destroyed);
            Assert.False(water.Destroyed);
            Assert.False(emitter.Destroyed);
            Assert.False(other.Destroyed);
            Assert.Equal(30, PlayerStore.Instance.Cash);
        }

        [Fact]
        public void AlcoholUsesConfiguredBaseScoreForTheWeeklyTarget()
        {
            Bottle(375, .9, FlowProps.Alcohol);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
            Assert.Equal(30, PlayerStore.Instance.Cash);
        }

        [Fact]
        public void MixedTableRejectsLowConcentrationWithoutTakingQualifiedGoods()
        {
            GameItem good = Bottle(300, .9);
            GameItem bad = Bottle(100, .8);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.Contains(c.Lines, l => l.Speaker == "薇拉" && l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.UnderConcentration));
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            AssertNotSettled(good);
            Assert.False(bad.Destroyed);
            bad.Destroy();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
            Assert.True(good.Destroyed);
            Assert.Equal(30, PlayerStore.Instance.Cash);
        }

        [Fact]
        public void ShortDeliveryCanReturnToOptionsAndThenCompleteOnce()
        {
            GameItem first = Bottle(100, .9);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.True(first.Destroyed);
            Assert.Equal(0, PlayerStore.Instance.Cash);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.AcceptShortOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.RetryOption));
            Bottle(200, .9);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.Equal(0, PlayerStore.Instance.Cash);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
            Assert.Equal(30, PlayerStore.Instance.Cash);
            Assert.Equal(3, TestHospitalReputation.Value);
            Assert.Equal(14, HospitalContractState.NextPickupDay);
            Assert.Equal(2, FlowStock.Reads);
        }

        [Fact]
        public void ConfirmShortDeliveryPaysOnlyPreviouslySubmittedGoods()
        {
            GameItem first = Bottle(100, .9);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            GameItem unsubmitted = Bottle(50, .9);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.AcceptShortOption));
            Assert.True(c.Finished);
            Assert.True(first.Destroyed);
            Assert.False(unsubmitted.Destroyed);
            Assert.Equal(10, PlayerStore.Instance.Cash);
            Assert.Equal(-2, TestHospitalReputation.Value);
            Assert.Equal(14, HospitalContractState.NextPickupDay);
            Assert.Equal(1, FlowStock.Reads);
        }

        [Fact]
        public void FinishConfirmationDoesNotInspectUnsubmittedGoods()
        {
            Bottle(100, .9);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            GameItem unsubmitted = Bottle(100, .8);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.AcceptShortOption));
            Assert.True(c.Finished);
            Assert.False(unsubmitted.Destroyed);
            Assert.Equal(10, PlayerStore.Instance.Cash);
        }

        [Fact]
        public void EmptyTableNeedsItsOwnNoDeliveryConfirmation()
        {
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.Equal(0, TestHospitalReputation.Value);
            Assert.Equal(7, HospitalContractState.NextPickupDay);
            int inspection = c.Lines.FindIndex(l => l.Text == "桌上没有医院要的试剂......");
            Assert.True(inspection >= 0);
            Assert.Equal("你的意思是这周没有试剂提供吗。", c.Lines[inspection + 1].Text);
            Assert.DoesNotContain(c.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ShortQuestion));
            c.HasChoice("搞错了。");
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.AcceptShortOption));
            Assert.True(c.Finished);
            Assert.Equal(-3, TestHospitalReputation.Value);
            Assert.Equal(0, PlayerStore.Instance.Cash);
            Assert.Equal(14, HospitalContractState.NextPickupDay);
            Assert.Contains(c.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ZeroSettled));
            Assert.DoesNotContain(c.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ShortSettled));
        }

        [Fact]
        public void ZeroDeliveryRetryReturnsToPlacementWithoutSettling()
        {
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            c.Choose("搞错了。");
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.Equal(0, TestHospitalReputation.Value);
            Assert.Equal(0, PlayerStore.Instance.Cash);
            Assert.Equal(7, HospitalContractState.NextPickupDay);
            GameItem goods = Bottle(300, .9);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
            Assert.True(goods.Destroyed);
            Assert.Equal(30, PlayerStore.Instance.Cash);
            Assert.Equal(3, TestHospitalReputation.Value);
        }

        [Fact]
        public void ExplicitNoDeliveryDoesNotPurchaseGoodsAddedAfterQuestion()
        {
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            GameItem goods = Bottle(300, .9);
            c.Choose("是。");
            Assert.True(c.Finished);
            Assert.False(goods.Destroyed);
            Assert.Equal(0, PlayerStore.Instance.Cash);
            Assert.Equal(-3, TestHospitalReputation.Value);
            Assert.Equal(14, HospitalContractState.NextPickupDay);
        }

        [Theory]
        [InlineData(599, .9, false)]
        [InlineData(600, .9, true)]
        [InlineData(100, 1, false)]
        public void ExtraThanksDependsOnScoreOnly(double ml, double purity, bool thanks)
        {
            Bottle(ml, purity);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
            Assert.Equal(thanks, c.Lines.Any(l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.OverThanks)));
            Assert.InRange(c.Lines.Count(l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.OverThanks)), 0, 1);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TemporaryRefusalResumesSellingAndIsOfferedAgainAfterSevenDays(bool askFirst)
        {
            HospitalContractState.QueueOffer();
            AcidBuyerProgress.MarkHighPurityDialogueDone();
            Conversation c = new Conversation(OfferAssembly());
            if (askFirst) c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.AskOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.DeclineOption));
            Assert.Contains(c.Lines, l => l.Speaker == "薇拉" && l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.ResumeTrade));
            c.HasChoice(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.TradeOption));
            Assert.False(HospitalContractState.OfferDeclined);
            Assert.True(HospitalContractState.OfferReady);
            Bottle(100, .9);
            c.Choose(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.TradeOption));
            Assert.Equal(10, PlayerStore.Instance.Cash);
            c.Choose(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutOption));
            Assert.True(c.Finished);

            NpcStoryContent content = AcidBuyerStory.Create();
            var state = new NpcState(content.Definition);
            state.SetLastVisitDay(0);
            state.MarkSeen("acid_buyer.contract_offer");
            FluxForge.Story.FlowEmitterMerchant.FlowEmitterMerchantProgress.MarkArrivalSeen();
            Assert.Null(VisitRuleSelector.Select(content.VisitRules,
                new VisitContext(content.Definition, state, 6), () => 0));
            Assert.NotNull(VisitRuleSelector.Select(content.VisitRules,
                new VisitContext(content.Definition, state, 7), () => 0));
            StoryEvent selected = StoryEventSelector.Select(content.Events,
                new StoryContext(content.Definition, state, StoryTrigger.VisitStarted, VisitKind.Dialogue, 7, null),
                new HashSet<string>());
            Assert.Equal("acid_buyer.contract_offer", selected.Id);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RepeatOfferUsesShortGreetingAndRememberedDetails(bool askOnFirstVisit)
        {
            HospitalContractState.QueueOffer();
            Conversation first = new Conversation(OfferAssembly());
            Assert.Contains(first.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.Greeting1));
            if (askOnFirstVisit) first.Choose("怎么供。");
            first.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.DeclineOption));
            first.Choose(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.ExitOption));
            Assert.True(first.Finished);
            MarkOfferSeen();

            // 重建存档持有者和剧情图，确认条件来自持久化字典而非边对象临时字段。
            var savedData = new Dictionary<string, string>(PlayerStore.Instance.modData);
            PlayerStore.Instance = new PlayerStore();
            foreach (var entry in savedData) PlayerStore.Instance.modData.Add(entry.Key, entry.Value);
            StoreStation.Day = 7;
            Conversation repeat = new Conversation(OfferAssembly());
            Assert.Equal(new[] { "你好，店主。", "关于合同，你改变主意了吗？" },
                repeat.Lines.Select(l => l.Text));
            Assert.Equal(3, repeat.Last.Choices.Count);
            repeat.HasChoice("还是只散卖。");
            repeat.HasChoice("没有意愿。");
            repeat.HasChoice(askOnFirstVisit ? "行，我答应了。" : "怎么供。");
            Assert.DoesNotContain(repeat.Last.Choices,
                choice => choice.Text == (askOnFirstVisit ? "怎么供。" : "行，我答应了。"));
            if (!askOnFirstVisit) repeat.Choose("怎么供。");
            repeat.Choose("行，我答应了。");
            Assert.True(repeat.Finished);
            Assert.True(HospitalContractState.Started);
            Assert.Equal(7, HospitalContractState.StartDay);
            Assert.Equal(10, HospitalContractState.NextMaterialDay);
            Assert.Equal(14, HospitalContractState.NextPickupDay);
        }

        [Fact]
        public void InterruptedOfferDoesNotAllowAlreadyExplainedDetailsAgain()
        {
            HospitalContractState.QueueOffer();
            Conversation first = new Conversation(OfferAssembly());
            first.Choose("怎么供。");
            Assert.True(HospitalContractState.DetailsExplained);
            Assert.False(first.Finished);
            Assert.Equal(0, new NpcState(AcidBuyerStory.Create().Definition)
                .SeenCount(HospitalContractOfferAssembly.EventId));
            first.Player.Cancel();

            Conversation repeat = new Conversation(OfferAssembly());
            Assert.Equal(new[] { "你好，店主。", "关于合同，你改变主意了吗？" },
                repeat.Lines.Select(l => l.Text));
            repeat.HasChoice("行，我答应了。");
            Assert.DoesNotContain(repeat.Last.Choices, choice => choice.Text == "怎么供。");
            Assert.Single(EmporiumEntry.Instance.frontInvinvElement.childItems);
        }

        [Fact]
        public void ExplainedOfferKeepsAcceptOptionAcrossMultipleDeferrals()
        {
            HospitalContractState.QueueOffer();
            HospitalContractOfferAssembly assembly = OfferAssembly();
            Conversation first = new Conversation(assembly);
            first.Choose("怎么供。");
            Assert.True(HospitalContractState.DetailsExplained);
            first.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.DeclineOption));
            first.Choose(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.ExitOption));
            MarkOfferSeen();
            int notes = EmporiumEntry.Instance.frontInvinvElement.childItems.Count;
            Assert.Equal(1, notes);

            foreach (int day in new[] { 7, 14, 21 })
            {
                StoreStation.Day = day;
                Conversation repeat = new Conversation(assembly);
                Assert.Equal(new[] { "你好，店主。", "关于合同，你改变主意了吗？" },
                    repeat.Lines.Select(l => l.Text));
                Assert.Equal(3, repeat.Last.Choices.Count);
                repeat.HasChoice("行，我答应了。");
                Assert.DoesNotContain(repeat.Last.Choices, choice => choice.Text == "怎么供。");
                Assert.Equal(notes, EmporiumEntry.Instance.frontInvinvElement.childItems.Count);
                repeat.Choose("还是只散卖。");
                repeat.HasChoice(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.TradeOption));
                repeat.Choose(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.ExitOption));
                Assert.True(repeat.Finished);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PermanentRefusalResumesSellingButStopsOffer(bool askFirst)
        {
            HospitalContractState.QueueOffer();
            Conversation c = new Conversation(OfferAssembly());
            if (askFirst) c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.AskOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.NoInterestOption));
            Assert.False(c.Finished);
            Assert.Contains(c.Lines, l => l.Speaker == "薇拉" && l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.ResumeTrade));
            c.HasChoice(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.TradeOption));
            Assert.True(HospitalContractState.OfferDeclined);
            Assert.False(HospitalContractState.OfferReady);
            c.Choose(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.ExitOption));
            Assert.True(c.Finished);
        }

        [Theory]
        [InlineData(true, "自己做的。")]
        [InlineData(false, "自己做的。")]
        [InlineData(true, "运气好。")]
        [InlineData(false, "运气好。")]
        public void FirstHighPurityQuestionReturnsToSellingAndDoesNotRepeat(bool firstVisit, string answer)
        {
            Bottle(50, .99); // 小于旧 100mL 阈值，也应在第一次高纯度交付时询问。
            Conversation c = new Conversation(firstVisit
                ? new AcidBuyerArrivalAssembly() : new AcidBuyerRepeatVisitAssembly());
            c.Choose(firstVisit ? ModHelper.GetLocalized("fluxforge", AcidBuyerArrivalTexts.TradeOption) : ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.TradeOption));
            int question = c.Lines.FindIndex(l => l.Text == "这么高的浓度，你怎么做到的？");
            Assert.True(question >= 2);
            Assert.Equal("......", c.Lines[question - 1].Text);
            Assert.Equal("这些我收下了。", c.Lines[question - 2].Text);
            c.Choose(answer);
            Assert.True(AcidBuyerProgress.HasHighPurityDialogueDone);
            Assert.True(HospitalContractState.OfferReady);
            Assert.Equal("还有吗？", c.Last.Text);
            Assert.Equal("薇拉", c.Last.Speaker);
            c.HasChoice(firstVisit ? ModHelper.GetLocalized("fluxforge", AcidBuyerArrivalTexts.TradeOption) : ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.TradeOption));
            Bottle(100, .99);
            c.Choose(firstVisit ? ModHelper.GetLocalized("fluxforge", AcidBuyerArrivalTexts.TradeOption) : ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.TradeOption));
            Assert.Single(c.Lines.Where(l => l.Text == "这么高的浓度，你怎么做到的？"));
        }

        private static Conversation SaleConversation(int route)
        {
            if (route == 0) return new Conversation(new AcidBuyerArrivalAssembly());
            if (route == 1) return new Conversation(new AcidBuyerRepeatVisitAssembly());
            HospitalContractState.QueueOffer();
            Conversation c = new Conversation(OfferAssembly());
            c.Choose(route == 2
                ? ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.DeclineOption)
                : ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.NoInterestOption));
            return c;
        }

        private static void Sell(Conversation conversation, int route)
        {
            conversation.Choose(route == 0
                ? ModHelper.GetLocalized("fluxforge", AcidBuyerArrivalTexts.TradeOption) : ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.TradeOption));
            if (conversation.Last.Choices.Any(choice => choice.Text == "自己做的。"))
            {
                conversation.Choose("自己做的。");
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void ContinuingTradeOptionsAttachToVerasMoreQuestion(int route)
        {
            AcidBuyerProgress.MarkHighPurityDialogueDone();
            Conversation c = SaleConversation(route);
            for (int batch = 0; batch < 2; batch++)
            {
                Bottle(100, .9);
                Sell(c, route);
                Assert.Equal("还有吗？", c.Last.Text);
                Assert.Equal("薇拉", c.Last.Speaker);
                Assert.Equal(2, c.Last.Choices.Count);
                c.HasChoice(route == 0
                    ? ModHelper.GetLocalized("fluxforge", AcidBuyerArrivalTexts.TradeOption) : ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.TradeOption));
                c.HasChoice(route == 0
                    ? ModHelper.GetLocalized("fluxforge", AcidBuyerArrivalTexts.SoldOutOption) : ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutOption));
                Assert.Equal("这些我收下了。", c.Lines[c.Lines.Count - 2].Text);
            }
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(1, false)]
        [InlineData(2, true)]
        [InlineData(2, false)]
        [InlineData(3, true)]
        [InlineData(3, false)]
        public void FarewellUsesAllPurchasesOfThisVisitRegardlessOfOrder(int route, bool highFirst)
        {
            Conversation c = SaleConversation(route);
            GameItem first = Bottle(100, highFirst ? .99 : .9);
            Sell(c, route);
            GameItem second = Bottle(100, highFirst ? .9 : .99);
            Sell(c, route);
            c.Choose(route == 0 ? ModHelper.GetLocalized("fluxforge", AcidBuyerArrivalTexts.SoldOutOption) : ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutOption));
            Assert.True(c.Finished);
            Assert.True(first.Destroyed);
            Assert.True(second.Destroyed);
            Assert.Equal(20, PlayerStore.Instance.Cash);
            Assert.Contains(c.Lines, l => l.Text == (route == 0 ? ModHelper.GetLocalized("fluxforge", AcidBuyerArrivalTexts.SoldOutHigh) : ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutHigh)));
            Assert.DoesNotContain(c.Lines, l => l.Text == (route == 0 ? ModHelper.GetLocalized("fluxforge", AcidBuyerArrivalTexts.SoldOutLow) : ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutLow)));
            Assert.Single(c.Lines.Where(l => l.Text == "这么高的浓度，你怎么做到的？"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void SplitHighPurityPurchasesAccumulateForFarewell(int route)
        {
            Conversation c = SaleConversation(route);
            Bottle(60, .99);
            Sell(c, route);
            Bottle(60, .99);
            Sell(c, route);
            c.Choose(route == 0 ? ModHelper.GetLocalized("fluxforge", AcidBuyerArrivalTexts.SoldOutOption) : ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutOption));
            Assert.Contains(c.Lines, l => l.Text == (route == 0 ? ModHelper.GetLocalized("fluxforge", AcidBuyerArrivalTexts.SoldOutHigh) : ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutHigh)));
        }

        [Fact]
        public void FailedExtraSaleDoesNotErasePreviouslyPurchasedGoods()
        {
            Conversation c = SaleConversation(1);
            Bottle(100, .99);
            Sell(c, 1);
            GameItem bad = Bottle(100, .8);
            Sell(c, 1);
            Assert.False(bad.Destroyed);
            Assert.Equal(10, PlayerStore.Instance.Cash);
            c.Choose(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutOption));
            Assert.Contains(c.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutHigh));
        }

        [Fact]
        public void ReusedStoryGraphStartsNextVisitWithEmptyPurchaseTotals()
        {
            AcidBuyerRepeatVisitAssembly assembly = new AcidBuyerRepeatVisitAssembly();
            Conversation first = new Conversation(assembly);
            Bottle(100, .99);
            Sell(first, 1);
            first.Choose(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutOption));
            Assert.Contains(first.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutHigh));

            Conversation nextVisit = new Conversation(assembly);
            Bottle(100, .9);
            Sell(nextVisit, 1);
            nextVisit.Choose(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutOption));
            Assert.Contains(nextVisit.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutLow));
            Assert.DoesNotContain(nextVisit.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutHigh));
            Assert.DoesNotContain(nextVisit.Lines, l => l.Text == "这么高的浓度，你怎么做到的？");
        }

        private static void StartHighestDemand()
        {
            HospitalContractState.Start(0);
            TestHospitalReputation.Value = 60;
            StoreStation.Day = 3;
            Conversation delivery = new Conversation(new HospitalContractMaterialAssembly());
            Assert.True(delivery.Finished);
            new Conversation(new HospitalHighestDemandOfferAssembly()).Choose(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.AcceptOption));
            Assert.True(HospitalHighestDemandState.Started);
        }

        [Fact]
        public void HighestDemandIsOfferedOnPickupDayAndCanBeAcceptedBeforeSubmitting()
        {
            HospitalContractState.Start(0);
            TestHospitalReputation.Value = 60;
            StoreStation.Day = 3;
            Conversation material = new Conversation(new HospitalContractMaterialAssembly());
            Assert.DoesNotContain(material.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Offer1));
            Conversation c = Pickup();
            Assert.False(HospitalHighestDemandState.Started);
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.AcceptOption));
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.DeclineOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.AcceptOption));
            Assert.True(HospitalHighestDemandState.Active);
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Bottle(100, 1);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.Equal(100000d, HospitalHighestDemandState.Acid);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
        }

        [Theory]
        [InlineData(20)]
        [InlineData(40)]
        public void ReputationMilestonesUseTheSharedOnceLimitAndDoNotBackfillLowerMilestones(int reputation)
        {
            HospitalContractState.Start(0);
            TestHospitalReputation.Value = reputation;
            NpcStoryContent content = AcidBuyerStory.Create();
            NpcState state = new NpcState(content.Definition);
            StoryContext visit = new StoryContext(content.Definition, state,
                StoryTrigger.VisitStarted, VisitKind.Dialogue, 0, null);
            StoryContext context = new StoryContext(visit, HospitalStoryPoints.MaterialDelivered);
            StoryEvent first = StoryEventSelector.Select(content.Events, context, new HashSet<string>());
            Assert.Equal("acid_buyer.hospital_rep" + reputation, first.Id);

            state.MarkSeen(first.Id);
            StoryEvent next = StoryEventSelector.Select(content.Events, context, new HashSet<string>());

            Assert.Null(next);
        }

        [Fact]
        public void HighestDemandDoesNotReplaceMilestoneOrDueMaterialDelivery()
        {
            HospitalContractState.Start(0);
            TestHospitalReputation.Value = 60;
            StoreStation.Day = 3;
            NpcStoryContent content = AcidBuyerStory.Create();
            NpcState state = new NpcState(content.Definition);
            StoryEvent selected = StoryEventSelector.Select(content.Events,
                new StoryContext(content.Definition, state, StoryTrigger.VisitStarted,
                    VisitKind.Dialogue, 3, null), new HashSet<string>());
            Assert.Equal("acid_buyer.contract_material", selected.Id);
            Conversation delivery = new Conversation(content.FindEvent(selected.Id).Content);
            Assert.True(delivery.Finished);
            Assert.Equal(4, EmporiumEntry.Instance.frontInvinvElement.childItems.Count);
            Assert.Equal(10, HospitalContractState.NextMaterialDay);
            Assert.False(HospitalHighestDemandState.Started);
        }

        [Theory]
        [InlineData(1d, 400000d)]
        [InlineData(.99999d, 0d)]
        [InlineData(.995d, 0d)]
        [InlineData(.90d, 0d)]
        public void HighestDemandCountsOnlyFullyPureReagents(double concentration, double expected)
        {
            StartHighestDemand();
            Bottle(400, concentration);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
            Assert.Equal(expected, HospitalHighestDemandState.Acid);
            Assert.Equal(0d, HospitalHighestDemandState.Base);
            Assert.Equal(0d, HospitalHighestDemandState.Alcohol);
            Assert.False(HospitalHighestDemandState.Completed);
        }

        [Fact]
        public void HighestDemandDoesNotCountInspectRetriesOrRejectedMixedTables()
        {
            StartHighestDemand();
            Bottle(1000, 1);
            GameItem bad = Bottle(100, .8, FlowProps.Base);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.Equal(0d, HospitalHighestDemandState.Acid);
            Assert.Equal(0, PlayerStore.Instance.Cash);
            bad.Destroy();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
            Assert.Equal(1000000d, HospitalHighestDemandState.Acid);
            Assert.Equal(360, PlayerStore.Instance.Cash);
        }

        [Fact]
        public void HighestDemandCountsSubmittedShortDeliveryBeforePayment()
        {
            StartHighestDemand();
            Bottle(20, 1);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.Equal(20000d, HospitalHighestDemandState.Acid);
            Assert.Equal(0, PlayerStore.Instance.Cash);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.RetryOption));
            Assert.Equal(20000d, HospitalHighestDemandState.Acid);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.AcceptShortOption));
            Assert.Equal(20000d, HospitalHighestDemandState.Acid);
            Assert.Equal(7, PlayerStore.Instance.Cash);
            Assert.Contains(c.Lines, l => l.Text == "已经交了酸 20 ml，碱 0 ml，酒精 0 ml。");
        }

        [Fact]
        public void HighestDemandIgnoresGoodsSoldBeforeOfferAndOrdinaryPurchases()
        {
            HospitalContractState.Start(0);
            TestHospitalReputation.Value = 59;
            Bottle(1000, 1);
            Conversation beforeOffer = Pickup();
            beforeOffer.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.Equal(0d, HospitalHighestDemandState.Acid);
            Assert.DoesNotContain(beforeOffer.Lines, l => l.Text.StartsWith("已经交了酸"));
            StoreStation.Day = 10;
            new Conversation(new HospitalHighestDemandOfferAssembly()).Choose(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.AcceptOption));
            Assert.True(HospitalHighestDemandState.Active);
            int cashBefore = PlayerStore.Instance.Cash;
            Conversation ordinary = SaleConversation(1);
            Bottle(1000, 1);
            Sell(ordinary, 1);
            Assert.Equal(cashBefore + 300, PlayerStore.Instance.Cash);
            Assert.Equal(0d, HospitalHighestDemandState.Acid);
        }

        [Fact]
        public void HighestDemandRequiresEachChemicalRatherThanCombinedVolume()
        {
            StartHighestDemand();
            Bottle(3000, 1);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
            Assert.Equal(3000000d, HospitalHighestDemandState.Acid);
            Assert.Equal(0d, HospitalHighestDemandState.Base);
            Assert.Equal(0d, HospitalHighestDemandState.Alcohol);
            Assert.True(HospitalHighestDemandState.Active);
            Assert.False(HospitalHighestDemandState.Completed);
            Assert.DoesNotContain(c.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Complete));
        }

        [Fact]
        public void HighestDemandPersistsThreeSeparateTotalsAndCompletesWithoutExtraReward()
        {
            StartHighestDemand();
            Bottle(600, 1);
            Bottle(400, 1, FlowProps.Base);
            Bottle(300, 1, FlowProps.Alcohol);
            Conversation first = Pickup();
            first.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            first.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(first.Finished);
            Assert.Equal("已经交了酸 600 ml，碱 400 ml，酒精 300 ml。", first.Lines[first.Lines.Count - 2].Text);
            Assert.Equal("下次见。", first.Last.Text);
            Assert.False(HospitalHighestDemandState.Completed);

            var savedData = new Dictionary<string, string>(PlayerStore.Instance.modData);
            PlayerStore.Instance = new PlayerStore();
            foreach (var entry in savedData) PlayerStore.Instance.modData.Add(entry.Key, entry.Value);
            HospitalReputation.EnsureFaction();
            TestHospitalReputation.Value = 60;
            StoreStation.Day = 14;
            Bottle(400, 1);
            Bottle(600, 1, FlowProps.Base);
            Bottle(700, 1, FlowProps.Alcohol);
            Conversation second = new Conversation(new HospitalContractPickupAssembly());
            second.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            second.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(second.Finished);
            Assert.True(HospitalHighestDemandState.Completed);
            Assert.Contains(second.Lines, l => l.Text == "已经交了酸 1000 ml，碱 1000 ml，酒精 1000 ml。");
            Assert.Equal(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Complete), second.Lines[second.Lines.Count - 2].Text);
            Assert.Equal("下次见。", second.Last.Text);
            Assert.Equal(561, PlayerStore.Instance.Cash); // 正常468元报酬*1.2取整，无任务额外奖励。
            Assert.Equal(80, TestHospitalReputation.Value); // 仅正常周合同声望+20。
            Assert.Equal(21, HospitalContractState.NextPickupDay);

            savedData = new Dictionary<string, string>(PlayerStore.Instance.modData);
            PlayerStore.Instance = new PlayerStore();
            foreach (var entry in savedData) PlayerStore.Instance.modData.Add(entry.Key, entry.Value);
            HospitalReputation.EnsureFaction();
            TestHospitalReputation.Value = 80;
            Assert.True(HospitalHighestDemandState.Completed);
            StoreStation.Day = 21;
            Bottle(100, 1);
            Bottle(100, 1, FlowProps.Base);
            Bottle(100, 1, FlowProps.Alcohol);
            Conversation later = new Conversation(new HospitalContractPickupAssembly());
            later.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            later.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(later.Finished);
            Assert.DoesNotContain(later.Lines, l => l.Text.StartsWith("已经交了酸"));
            Assert.DoesNotContain(later.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Complete));
            Assert.Equal(1000000d, HospitalHighestDemandState.Acid);
            Assert.Equal(28, HospitalContractState.NextPickupDay);
            StoreStation.Day = 24;
            Conversation delivery = new Conversation(new HospitalContractMaterialAssembly());
            Assert.DoesNotContain(delivery.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Offer1));
        }

        [Fact]
        public void ZeroDeliveryDoesNotIncreaseHighestDemandProgress()
        {
            StartHighestDemand();
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.AcceptShortOption));
            Assert.Equal(0d, HospitalHighestDemandState.Acid);
            Assert.Equal(0, PlayerStore.Instance.Cash);
            Assert.Equal(57, TestHospitalReputation.Value);
            Assert.Equal("已经交了酸 0 ml，碱 0 ml，酒精 0 ml。", c.Lines[c.Lines.Count - 2].Text);
            Assert.Equal("下次见。", c.Last.Text);
            Assert.False(HospitalHighestDemandState.Completed);
        }

        [Theory]
        [InlineData(59, false)]
        [InlineData(60, true)]
        [InlineData(61, true)]
        public void TrustPaymentPerkUsesNativeSixtyThresholdAndRegisteredText(int reputation, bool unlocked)
        {
            TestHospitalReputation.Value = reputation;
            HospitalReputation.EnsureFaction();
            StoreReputation faction = StoreReputation.GetStoreReputation(HospitalReputation.FactionId);
            var perk = Assert.Single(faction.perks.Where(p => p.perkId == HospitalReputationTexts.TrustPaymentPerkId));
            Assert.Equal(60, perk.perkThreshold);
            Assert.Equal(1, perk.perkType);
            Assert.Equal(unlocked, perk.isUnlocked);
            var table = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetTable("RepPerkTable");
            string key = "rep_perk_" + HospitalReputationTexts.TrustPaymentPerkId;
            Assert.Equal("信任报酬", table.Entries[key + "_name"]);
            Assert.Equal("交付给医院的货物，现在会提供1.2倍的金额报酬奖励。", table.Entries[key + "_desc"]);
            Assert.Equal("上层人的医院居然会为一位供货商提供额外嘉奖，这事听起来太过奇妙。", table.Entries[key + "_flavor"]);
        }

        [Theory]
        [InlineData(300, 59, 30)]
        [InlineData(300, 60, 36)]
        [InlineData(300, 100, 36)]
        [InlineData(305, 60, 36)]
        [InlineData(100, 60, 12)]
        [InlineData(0, 60, 0)]
        public void TrustPaymentMultipliesFinalWeeklyPaymentOnly(double score, int reputation, int expected)
        {
            Assert.Equal(expected, HospitalContractScore.Payment(score, reputation));
        }

        [Theory]
        [InlineData(19, 2)]
        [InlineData(20, 3)]
        [InlineData(39, 3)]
        [InlineData(40, 4)]
        public void DeliveryUsesReputationAtTheDeliveryCallback(int reputation, int count)
        {
            HospitalContractState.Start(0);
            StoreStation.Day = 3;
            Conversation c = new Conversation(new HospitalContractMaterialAssembly(), false);
            Assert.Empty(EmporiumEntry.Instance.frontInvinvElement.childItems);
            TestHospitalReputation.Value = reputation;
            c.Advance(DialogUIManager.Instance.Current);
            Assert.Equal(count, EmporiumEntry.Instance.frontInvinvElement.childItems.Count);
            Assert.Equal(10, HospitalContractState.NextMaterialDay);
        }

        [Theory]
        [InlineData(20, 3)]
        [InlineData(40, 4)]
        public void MilestoneVisitDeliversDueMaterialsOnce(int reputation, int count)
        {
            HospitalContractState.Start(0);
            StoreStation.Day = 3;
            TestHospitalReputation.Value = reputation;
            new Conversation(new HospitalContractMaterialAssembly());
            Assert.Equal(count, EmporiumEntry.Instance.frontInvinvElement.childItems.Count);
            Assert.False(HospitalContractState.ShouldRunMaterial(3));
            StoreStation.Day = 10;
            Conversation later = new Conversation(new HospitalContractMaterialAssembly());
            Assert.DoesNotContain(later.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractMilestoneTexts.At20_1) || l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractMilestoneTexts.At40_1));
            Assert.Equal(count * 2, EmporiumEntry.Instance.frontInvinvElement.childItems.Count);
        }
    }
}
