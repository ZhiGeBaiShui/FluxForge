using FluxForge.Mod;
using FluxForge.Story;
using FluxForge.Story.AcidBuyer;
using FluxForge.Story.Hospital;
using Xunit;
using Conversation = FluxForge.Story.Tests.StoryFlowTests.Conversation;

namespace FluxForge.Story.Tests
{
    public sealed class HospitalSynaprestTests : IDisposable
    {
        public HospitalSynaprestTests()
        {
            new StoryFlowTests();
            ReputationUIManager.Instance = new ReputationUIManager();
            RNG.PickOffset = 0;
            RNG.RollSample = 0;
            HospitalContractState.Start(0);
        }

        private static GameItem Bottle(double ml, double purity = 1d, FlowPropertyKey key = default)
        {
            if (key.Equals(default(FlowPropertyKey))) key = FlowProps.Synaprest;
            GameItem item = new GameItem();
            item.Tags.Add(FlowTags.LiquidContainerTag);
            item.Contents.Set(key, ml * 1000d * purity);
            item.Contents.Set(FlowProps.Starch, ml * 1000d * (1d - purity));
            EmporiumEntry.Instance.backInvinvElement.childItems.Add(item);
            return item;
        }

        private static Conversation Pickup()
        {
            StoreStation.Day = 7;
            return new Conversation(new HospitalContractPickupAssembly());
        }

        private static void Ready()
        {
            HospitalHighestDemandState.Start();
            HospitalHighestDemandState.Complete();
            HospitalContractState.MarkMaterial(3);
            HospitalContractState.MarkPickup(7);
            TestHospitalReputation.Value = 100;
            StoreStation.Day = 8;
        }

        private static void PurifyHospitalRaw(GameItem bottle)
        {
            // 使用当前冷萃配置：每次1µL、存量1000µL；满后倒空再继续。
            ColdTrapDevice trap = new ColdTrapDevice(-5d, 0d, 1, 20d, 1000);
            FlowEntity flow = new FlowEntity(new FlowPos(0d, 0d),
                new FlowMotionData(1, 2.5d), bottle.Contents);
            int pollution = (int)(flow.Properties.SubstanceTotal - flow.Properties.Get(FlowProps.Synaprest));
            for (int i = 0; i < pollution; i++)
            {
                if (trap.Extracted.SubstanceTotal == 1000d)
                    trap.ReplaceExtracted(new FlowProperties());
                trap.TryReact(flow);
            }
            Assert.Equal(flow.Properties.Get(FlowProps.Synaprest), flow.Properties.SubstanceTotal);
        }

        public void Dispose() { ModHelper.CurrentLocale = "zh"; }

        [Theory]
        [InlineData("zh")]
        [InlineData("en")]
        public void AcceptedHospitalMainlineReachesRenewableSynaprestSupplyAndOrdinaryResale(string locale)
        {
            ModHelper.CurrentLocale = locale;
            PlayerStore.Instance = new PlayerStore();
            NpcStoryContent content = AcidBuyerStory.Create();
            Conversation contract = new Conversation(content.FindEvent(HospitalContractOfferAssembly.EventId).Content);
            contract.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.AskOption));
            contract.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.AcceptOption));
            Assert.True(HospitalContractState.Started);
            for (int week = 1; week <= 3; week++)
            {
                StoreStation.Day = week * 7;
                Conversation pickup = new Conversation(new HospitalContractPickupAssembly());
                Bottle(1000, 1, FlowProps.Acid);
                pickup.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
                pickup.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            }
            Assert.Equal(60, TestHospitalReputation.Value);
            StoreStation.Day = 28;
            Conversation request = new Conversation(new HospitalContractPickupAssembly());
            request.Choose(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.AcceptOption));
            Bottle(1000, 1, FlowProps.Acid);
            Bottle(1000, 1, FlowProps.Base);
            Bottle(1000, 1, FlowProps.Alcohol);
            request.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            request.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(HospitalHighestDemandState.Completed);
            StoreStation.Day = 35;
            Conversation fifth = new Conversation(new HospitalContractPickupAssembly());
            Bottle(1000, 1, FlowProps.Acid);
            fifth.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            fifth.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.Equal(100, TestHospitalReputation.Value);
            StoreStation.Day = 36;
            Assert.True(HospitalSynaprestState.CanOffer(36));
            Conversation quest = new Conversation(new HospitalSynaprestOfferAssembly());
            quest.Choose(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.AcceptOption));
            GameItem raw = EmporiumEntry.Instance.frontInvinvElement.childItems.Last();
            PurifyHospitalRaw(raw);
            Assert.Equal(600000d, raw.Contents.Get(FlowProps.Synaprest));
            raw.Tags.Add(FlowTags.LiquidContainerTag);
            EmporiumEntry.Instance.frontInvinvElement.childItems.Remove(raw);
            EmporiumEntry.Instance.backInvinvElement.childItems.Add(raw);
            StoreStation.Day = 42;
            Conversation completion = new Conversation(new HospitalContractPickupAssembly());
            completion.Choose(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));
            Assert.True(HospitalSynaprestState.Completed);
            completion.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            StoreStation.Day = 45;
            new Conversation(new HospitalContractMaterialAssembly());
            GameItem weeklyRaw = EmporiumEntry.Instance.frontInvinvElement.childItems.Last();
            Assert.Equal(400000d, weeklyRaw.Contents.Get(FlowProps.Synaprest));
            PurifyHospitalRaw(weeklyRaw);
            weeklyRaw.Tags.Add(FlowTags.LiquidContainerTag);
            EmporiumEntry.Instance.backInvinvElement.childItems.Add(weeklyRaw);
            StoreStation.Day = 49;
            Conversation repeat = new Conversation(new HospitalContractPickupAssembly());
            repeat.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.True(weeklyRaw.Destroyed);
            Assert.Equal(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Score, "2000"), ReputationUIManager.Instance.Notifications.Last());
            repeat.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(repeat.Finished);
        }

        [Theory]
        [InlineData(99, 0, -3)]
        [InlineData(99, 100, -2)]
        [InlineData(100, 0, 0)]
        [InlineData(100, 100, 0)]
        [InlineData(100, 300, 3)]
        [InlineData(150, 100, 0)]
        public void ExternalExpertProtectsOnlyShortWeeklyDelivery(int reputation, int millilitres, int delta)
        {
            HospitalHighestDemandState.Start();
            HospitalHighestDemandState.Complete();
            TestHospitalReputation.Value = reputation;
            Conversation c = Pickup();
            if (millilitres > 0)
            {
                Bottle(millilitres, .9, FlowProps.Acid);
                c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            }
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            bool protectedShort = reputation >= 100 && millilitres < 300;
            if (reputation < 100 && millilitres < 300)
            {
                c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.AcceptShortOption));
            }
            Assert.True(c.Finished);
            Assert.Equal(reputation + delta, TestHospitalReputation.Value);
            Assert.Equal(HospitalContractScore.Payment(millilitres, reputation), PlayerStore.Instance.Cash);
            Assert.Equal(protectedShort, c.Lines.Any(l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Busy1)));
            if (protectedShort)
            {
                Assert.Equal(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Busy1), c.Lines[c.Lines.Count - 2].Text);
                Assert.Equal(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Busy2), c.Last.Text);
                Assert.DoesNotContain(c.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ShortQuestion)
                    || l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ZeroQuestion)
                    || l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ZeroSettled)
                    || l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ShortSettled)
                    || l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Farewell));
            }
        }

        [Theory]
        [InlineData(99, false)]
        [InlineData(100, true)]
        public void ExternalExpertPerkUsesNativeThresholdIconAndSpecifiedText(int reputation, bool unlocked)
        {
            TestHospitalReputation.Value = reputation;
            HospitalReputation.EnsureFaction();
            StoreReputation faction = StoreReputation.GetStoreReputation(HospitalReputation.FactionId);
            StoreReputationPerk perk = Assert.Single(faction.perks.Where(p => p.perkId == HospitalReputationTexts.ExternalExpertPerkId));
            Assert.Equal(100, perk.perkThreshold);
            Assert.Equal("Referal", perk.iconName);
            Assert.Equal(unlocked, perk.isUnlocked);
            var table = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetTable("RepPerkTable");
            string prefix = "rep_perk_" + HospitalReputationTexts.ExternalExpertPerkId;
            Assert.Equal("外聘专家", table.Entries[prefix + "_name"]);
            Assert.Equal("即使每周不提交足够的货物，也不会降低医院声望。", table.Entries[prefix + "_desc"]);
            Assert.Equal("中心医院已经完全信任了你的能力。", table.Entries[prefix + "_flavor"]);
        }

        [Theory]
        [InlineData(2, false, false)]
        [InlineData(4, false, true)]
        [InlineData(5, false, false)]
        [InlineData(8, true, false)]
        [InlineData(9, false, false)]
        [InlineData(15, true, false)]
        [InlineData(18, false, true)]
        [InlineData(22, true, false)]
        public void MondayContractKeepsMondayPickupAndThursdayDeliveryAfterDayJumps(
            int today, bool pickup, bool material)
        {
            PlayerStore.Instance = new PlayerStore();
            HospitalContractState.Start(1);
            Assert.Equal(pickup, HospitalContractState.ShouldRunPickup(today));
            Assert.Equal(material, HospitalContractState.ShouldRunMaterial(today));
        }

        [Fact]
        public void ContractDoesNotQueueOrdinarySevenDayVisitsOnOtherWeekdays()
        {
            PlayerStore.Instance = new PlayerStore();
            HospitalContractState.Start(1);
            FluxForge.Story.FlowEmitterMerchant.FlowEmitterMerchantProgress.MarkArrivalSeen();
            NpcStoryContent content = AcidBuyerStory.Create();
            VisitDecision decision = VisitRuleSelector.Select(content.VisitRules,
                new VisitContext(content.Definition, new NpcState(content.Definition), 12),
                delegate { return 0d; });
            Assert.Null(decision);
        }

        [Fact]
        public void FinishingScheduledVisitDoesNotShiftWeekdayOrRepeatSameDay()
        {
            PlayerStore.Instance = new PlayerStore();
            HospitalContractState.Start(1);
            HospitalContractState.MarkMaterial(18);
            HospitalContractState.MarkPickup(22);
            Assert.False(HospitalContractState.ShouldRunMaterial(18));
            Assert.False(HospitalContractState.ShouldRunPickup(22));
            Assert.True(HospitalContractState.ShouldRunMaterial(25));
            Assert.True(HospitalContractState.ShouldRunPickup(29));
            Assert.False(HospitalContractState.ShouldRunPickup(30));
        }

        [Fact]
        public void HighestDemandCompletionOverridesOrdinaryClosingWithoutChangingSettlement()
        {
            HospitalHighestDemandState.Start();
            TestHospitalReputation.Value = 60;
            Bottle(1000, 1, FlowProps.Acid);
            Bottle(1000, 1, FlowProps.Base);
            Bottle(1000, 1, FlowProps.Alcohol);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
            Assert.True(HospitalHighestDemandState.Completed);
            Assert.Equal(1008, PlayerStore.Instance.Cash);
            Assert.Equal(80, TestHospitalReputation.Value);
            Assert.DoesNotContain(c.Lines, l => l.Text.StartsWith("本次总收货分数：")
                || l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.OverThanks)
                || l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Over3)
                || l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Met1));
            Assert.Single(c.Lines.Where(l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Complete)));
            Assert.Single(c.Lines.Where(l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Farewell)));
            Assert.Equal(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Complete), c.Lines[c.Lines.Count - 2].Text);
        }

        [Fact]
        public void HighestDemandRefusalReturnsToPickupWithoutCountingOrRepeatingOffer()
        {
            TestHospitalReputation.Value = 60;
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.DeclineOption));
            Assert.True(HospitalHighestDemandState.Declined);
            Assert.False(HospitalHighestDemandState.Started);
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Bottle(100, 1, FlowProps.Acid);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
            Assert.Equal(36, PlayerStore.Instance.Cash);
            Assert.Equal(0d, HospitalHighestDemandState.Acid);
            StoreStation.Day = 14;
            Conversation next = new Conversation(new HospitalContractPickupAssembly());
            Assert.DoesNotContain(next.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Offer1));
            next.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void HighestDemandDecisionReturnsToDeliveryOnVisibleNpcResponse(bool accept)
        {
            TestHospitalReputation.Value = 60;
            Conversation c = Pickup();
            Assert.Equal(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.Greeting), c.Lines.First().Text);
            Assert.DoesNotContain(c.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Greeting));
            c.Choose(accept ? ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.AcceptOption) : ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.DeclineOption));
            Assert.Equal(accept ? ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.AcceptResponse2)
                : ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.DeclineResponse), c.Last.Text);
            Assert.False(string.IsNullOrWhiteSpace(c.Last.Text));
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.Equal(accept, HospitalHighestDemandState.Active);
            Assert.Equal(!accept, HospitalHighestDemandState.Declined);
            Bottle(100, 1, FlowProps.Acid);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
        }

        [Theory]
        [InlineData(20)]
        [InlineData(40)]
        [InlineData(200)]
        public void UnseenReputationMilestonesCannotReplaceWeeklyPickup(int reputation)
        {
            HospitalContractState.MarkMaterial(3);
            TestHospitalReputation.Value = reputation;
            NpcStoryContent content = AcidBuyerStory.Create();
            StoryEvent selected = StoryEventSelector.Select(content.Events,
                new StoryContext(content.Definition, new NpcState(content.Definition),
                    StoryTrigger.VisitStarted, VisitKind.Dialogue, 7, null), new HashSet<string>());
            Assert.Equal("acid_buyer.contract_pickup", selected.Id);
            Conversation c = new Conversation(content.FindEvent(selected.Id).Content);
            if (reputation >= 60) c.Choose(ModHelper.GetLocalized("fluxforge", HospitalHighestDemandTexts.DeclineOption));
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.DoesNotContain(c.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractMilestoneTexts.At20_1)
                || l.Text == ModHelper.GetLocalized("fluxforge", HospitalContractMilestoneTexts.At40_1));
        }

        [Fact]
        public void OfferUsesFirstFreeDayAfterWeeklyPickupWithoutSevenDayCooldown()
        {
            Ready();
            NpcStoryContent content = AcidBuyerStory.Create();
            NpcState state = new NpcState(content.Definition);
            state.SetLastVisitDay(7);
            VisitDecision decision = VisitRuleSelector.Select(content.VisitRules,
                new VisitContext(content.Definition, state, 8), delegate { return 0d; });
            Assert.Equal("acid_buyer.synaprest_offer", decision.Rule.Id);
            StoryEvent selected = StoryEventSelector.Select(content.Events,
                new StoryContext(content.Definition, state, StoryTrigger.VisitStarted,
                    VisitKind.Dialogue, 8, null), new HashSet<string>());
            Assert.Equal("hospital.synaprest.offer", selected.Id);
        }

        [Theory]
        [InlineData(7)]
        [InlineData(10)]
        public void OfferNeverReplacesDuePickupOrMaterial(int day)
        {
            Ready();
            if (day == 7) PlayerStore.Instance.modData["hospital.contract.last_pickup_day"] = "0";
            Assert.False(HospitalSynaprestState.CanOffer(day));
        }

        [Fact]
        public void OfferRequiresCompletedSixtyQuestAndOneHundredReputation()
        {
            HospitalContractState.MarkMaterial(3);
            HospitalContractState.MarkPickup(7);
            TestHospitalReputation.Value = 100;
            Assert.False(HospitalSynaprestState.CanOffer(8));
            HospitalHighestDemandState.Complete();
            TestHospitalReputation.Value = 99;
            Assert.False(HospitalSynaprestState.CanOffer(8));
            TestHospitalReputation.Value = 100;
            Assert.True(HospitalSynaprestState.CanOffer(8));
            HospitalContractState.Terminate();
            Assert.False(HospitalSynaprestState.CanOffer(8));
        }

        [Fact]
        public void InitialRefusalClosesQuestWithoutEndingContractOrGivingStock()
        {
            Ready();
            Conversation c = new Conversation(new HospitalSynaprestOfferAssembly());
            Assert.Equal(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Greeting1), c.Lines[0].Text);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.DeclineOption));
            Assert.True(c.Finished);
            Assert.Equal(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Decline), c.Last.Text);
            Assert.True(HospitalSynaprestState.Closed);
            Assert.False(HospitalContractState.Terminated);
            Assert.False(HospitalSynaprestState.CanOffer(9));
            Assert.Empty(EmporiumEntry.Instance.frontInvinvElement.childItems);
        }

        [Fact]
        public void AcceptanceGivesSixHundredActiveMillilitresOnlyAfterGiftLine()
        {
            Conversation c = new Conversation(new HospitalSynaprestOfferAssembly());
            Assert.Empty(EmporiumEntry.Instance.frontInvinvElement.childItems);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.AcceptOption));
            GameItem item = Assert.Single(EmporiumEntry.Instance.frontInvinvElement.childItems);
            Assert.Equal("bottled_water", item.identifier);
            Assert.Equal(600000d, item.Contents.Get(FlowProps.Synaprest));
            Assert.InRange(item.Contents.Get(FlowProps.Synaprest) / item.Contents.SubstanceTotal, .98999, .99001);
            Assert.Equal(606061d, item.Contents.SubstanceTotal);
            Assert.True(HospitalSynaprestState.Active);
            Assert.True(c.Finished);
            Assert.Equal(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Accept6), c.Last.Text);
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(50, false)]
        public void RawStockContainsStarchAndExactlyOneOfAcidOrBase(int sample, bool acid)
        {
            RNG.RollSample = sample;
            HospitalSynaprestStock.GiveInitial();
            GameItem item = Assert.Single(EmporiumEntry.Instance.frontInvinvElement.childItems);
            Assert.True(item.Contents.Get(FlowProps.Starch) > 0);
            Assert.Equal(acid, item.Contents.Get(FlowProps.Acid) > 0);
            Assert.Equal(!acid, item.Contents.Get(FlowProps.Base) > 0);
            Assert.Equal(6061, item.Contents.SubstanceTotal - item.Contents.Get(FlowProps.Synaprest));
        }

        [Fact]
        public void PickupAsksAboutTaskOnlyWhileActive()
        {
            HospitalSynaprestState.Start();
            Conversation c = Pickup();
            Assert.Equal(new[] { ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Greeting), ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Ask1), ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Ask2) }, c.Lines.Select(l => l.Text));
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));
            Assert.Equal(3, c.Last.Choices.Count);
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void FailedTaskSubmissionsReturnWithoutTakingOrCounting(int kind)
        {
            HospitalSynaprestState.Start();
            if (kind == 1) Bottle(100, 1, FlowProps.Water);
            if (kind == 2) Bottle(100, .99999);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));
            string expected = kind == 0 ? ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Empty)
                : kind == 1 ? ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Missing) : ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Impure);
            Assert.Equal(expected, c.Last.Text);
            c.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));
            Assert.Equal(0d, HospitalSynaprestState.Volume);
            Assert.Empty(ReputationUIManager.Instance.Notifications);
            Assert.Equal(0, PlayerStore.Instance.Cash);
        }

        [Fact]
        public void TaskTakesOnlyPureSynaprestAndLeavesImpureBottlesAndDevices()
        {
            HospitalSynaprestState.Start();
            GameItem pure = Bottle(100);
            GameItem impure = Bottle(100, .99);
            GameItem device = Bottle(100);
            device.Tags.Add(FlowTags.FlowDevice);
            GameItem other = Bottle(100, 1, FlowProps.Acid);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));
            Assert.True(pure.Destroyed);
            Assert.False(impure.Destroyed);
            Assert.False(device.Destroyed);
            Assert.False(other.Destroyed);
            Assert.Equal(100000d, HospitalSynaprestState.Volume);
            Assert.Equal("收货分数500/300", Assert.Single(ReputationUIManager.Instance.Notifications));
            Assert.Contains(c.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.First1));
            Assert.Contains(c.Lines, l => l.Text == "现在已经完成100/550了，感谢你的帮助。");
            Assert.Equal(0, PlayerStore.Instance.Cash);
        }

        [Fact]
        public void MixedOrdinaryAndTaskBatchesPayOnceFromCombinedScore()
        {
            HospitalSynaprestState.Start();
            Conversation c = Pickup();
            Bottle(100, .9, FlowProps.Acid);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Bottle(40);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));
            Assert.Equal(0, PlayerStore.Instance.Cash);
            Assert.Equal(0, TestHospitalReputation.Value);
            Assert.Equal(7, HospitalContractState.NextPickupDay);
            Assert.Equal(new[] { "收货分数100/300", "收货分数300/300" }, ReputationUIManager.Instance.Notifications);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.True(c.Finished);
            Assert.Equal(30, PlayerStore.Instance.Cash);
            Assert.Equal(3, TestHospitalReputation.Value);
            Assert.Contains(c.Lines, l => l.Text == "本次总收货分数：300 总报酬：30");
            Assert.Equal(14, HospitalContractState.NextPickupDay);
        }

        [Fact]
        public void LeavingQuestUnfinishedKeepsOrdinaryDeliveryAndProgressAcrossVisits()
        {
            HospitalSynaprestState.Start();
            Bottle(100);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));
            Assert.Equal(3, c.Last.Choices.Count);
            Bottle(100, .9, FlowProps.Acid);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.Equal(60, PlayerStore.Instance.Cash);
            Assert.True(HospitalSynaprestState.Active);
            Assert.Equal(100000d, HospitalSynaprestState.Volume);
            StoreStation.Day = 14;
            Conversation next = new Conversation(new HospitalContractPickupAssembly());
            Assert.Equal(3, next.Last.Choices.Count);
            next.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            next.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            next.HasChoice(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));
            Assert.False(HospitalSynaprestState.Closed);
            Assert.False(HospitalContractState.Terminated);
        }

        [Theory]
        [InlineData(549999d, false)]
        [InlineData(550000d, true)]
        [InlineData(550001d, true)]
        public void TaskCompletesAt550MillilitresAndUpdatesMenuImmediately(double microlitres, bool completed)
        {
            HospitalSynaprestState.Start();
            Bottle(microlitres / 1000d);
            Conversation c = Pickup();

            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));

            Assert.Equal(completed, HospitalSynaprestState.Completed);
            Assert.Equal(microlitres, HospitalSynaprestState.Volume, 6);
            Assert.Equal(!completed, c.Last.Choices.Any(choice => choice.Text == ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption)));
            Assert.Equal(completed, c.Lines.Any(line => line.Text == ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Complete3)));
        }

        [Fact]
        public void CompletionRemovesBothTaskChoicesImmediatelyAndOrdinarySubmissionAcceptsSynaprest()
        {
            HospitalSynaprestState.Start();
            Bottle(700);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));
            Assert.True(HospitalSynaprestState.Completed);
            Assert.Equal(700000d, HospitalSynaprestState.Volume);
            Assert.Contains(c.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.AllAtOnce));
            Assert.Contains(c.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Complete3));
            Assert.Equal(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.CompleteContinue), c.Last.Text);
            Assert.DoesNotContain(c.Last.Choices, c => c.Text == ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));
            Bottle(20);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.Equal("收货分数3600/300", ReputationUIManager.Instance.Notifications.Last());
            Assert.Equal(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Continue), c.Last.Text);
            Assert.Single(c.Lines.Where(l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.First1)));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.Equal(360, PlayerStore.Instance.Cash);
            Assert.Equal(20, TestHospitalReputation.Value);
        }

        [Fact]
        public void SubsequentPartialSubmissionUsesProgressLineAndCanFinishAcrossVisits()
        {
            HospitalSynaprestState.Start();
            Bottle(200);
            Conversation first = Pickup();
            first.Choose(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));
            first.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            StoreStation.Day = 14;
            Bottle(500);
            Conversation next = new Conversation(new HospitalContractPickupAssembly());
            next.Choose(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));
            Assert.Contains(next.Lines, l => l.Text == "原液进度700/550，非常感谢你的帮助。");
            Assert.DoesNotContain(next.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.First1) || l.Text == ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.AllAtOnce));
            Assert.True(HospitalSynaprestState.Completed);
            next.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.Equal(350, PlayerStore.Instance.Cash);
        }

        [Fact]
        public void OrdinarySubmissionExcludesQuestRawEvenWhenItContainsAcid()
        {
            HospitalSynaprestState.Start();
            GameItem raw = Bottle(600, .99);
            raw.Contents.Set(FlowProps.Acid, 1);
            GameItem acid = Bottle(300, .9, FlowProps.Acid);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.False(raw.Destroyed);
            Assert.True(acid.Destroyed);
            Assert.Equal(0d, HospitalSynaprestState.Volume);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.Equal(30, PlayerStore.Instance.Cash);
        }

        [Fact]
        public void CompletedQuestUsesOrdinaryLowConcentrationRejection()
        {
            HospitalSynaprestState.Start();
            HospitalSynaprestState.Record(600000);
            GameItem impure = Bottle(100, .99);
            GameItem pure = Bottle(100);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.Equal(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.UnderConcentration), c.Last.Text);
            Assert.False(pure.Destroyed);
            Assert.False(impure.Destroyed);
            Assert.DoesNotContain(c.Lines, l => l.Text == ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Ask1));
        }

        [Fact]
        public void FinishConfirmationDoesNotCollectTableAgain()
        {
            Bottle(20, 1, FlowProps.Acid);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            GameItem unsubmitted = Bottle(1000, 1, FlowProps.Acid);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.AcceptShortOption));
            Assert.False(unsubmitted.Destroyed);
            Assert.Equal(6, PlayerStore.Instance.Cash);
        }

        [Fact]
        public void ReusedPickupAssemblyResetsVisitTotalsWithoutPersistentAccount()
        {
            HospitalContractPickupAssembly assembly = new HospitalContractPickupAssembly();
            StoreStation.Day = 7;
            Conversation first = new Conversation(assembly);
            Bottle(300, .9, FlowProps.Acid);
            first.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.Equal(0, PlayerStore.Instance.Cash);
            first.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            StoreStation.Day = 14;
            Conversation next = new Conversation(assembly);
            next.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            next.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.AcceptShortOption));
            Assert.Equal(30, PlayerStore.Instance.Cash);
            Assert.Equal(0, TestHospitalReputation.Value);
            Assert.DoesNotContain(PlayerStore.Instance.modData.Keys,
                k => k.Contains("pickup.score") || k.Contains("pickup.pending"));
        }

        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 3)]
        public void WeeklyRawIsExtraOnlyAfterCompletion(bool completed, int count)
        {
            if (completed) { HospitalSynaprestState.Start(); HospitalSynaprestState.Record(600000); }
            StoreStation.Day = 3;
            new Conversation(new HospitalContractMaterialAssembly());
            Assert.Equal(count, EmporiumEntry.Instance.frontInvinvElement.childItems.Count);
            if (completed)
            {
                GameItem raw = EmporiumEntry.Instance.frontInvinvElement.childItems.Last();
                Assert.Equal("small_bottled_water", raw.identifier);
                Assert.Equal(400000, raw.Contents.Get(FlowProps.Synaprest));
                Assert.InRange(raw.Contents.Get(FlowProps.Synaprest) / raw.Contents.SubstanceTotal, .95, .999999);
            }
        }

        [Fact]
        public void PaymentRoundsOnceForCombinedBatchesInsteadOfRoundingEverySubmission()
        {
            Conversation c = Pickup();
            Bottle(6, .9, FlowProps.Acid);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Bottle(6, .9, FlowProps.Acid);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ConfirmOption));
            Assert.Equal(0, PlayerStore.Instance.Cash);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.AcceptShortOption));
            Assert.Equal(1, PlayerStore.Instance.Cash);
        }

        [Fact]
        public void TaskCreditsReceiveExistingTrustPaymentBonusAtFinalSettlement()
        {
            HospitalSynaprestState.Start();
            TestHospitalReputation.Value = 100;
            HospitalHighestDemandState.Start();
            HospitalHighestDemandState.Complete();
            Bottle(600);
            Conversation c = Pickup();
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.SubmitOption));
            Assert.Equal(0, PlayerStore.Instance.Cash);
            Assert.Equal(100, TestHospitalReputation.Value);
            c.Choose(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.FinishOption));
            Assert.Equal(360, PlayerStore.Instance.Cash);
            Assert.Equal(120, TestHospitalReputation.Value);
            Assert.Contains(c.Lines, l => l.Text == "本次总收货分数：3000 总报酬：360");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(21050)]
        public void WeeklyPollutionRangeFitsBottleAndAlwaysContainsBothChosenPollutants(int offset)
        {
            RNG.PickOffset = offset;
            HospitalSynaprestStock.GiveWeekly();
            GameItem item = Assert.Single(EmporiumEntry.Instance.frontInvinvElement.childItems);
            Assert.Equal("small_bottled_water", item.identifier);
            Assert.InRange(item.Contents.SubstanceTotal, 400002d, 421052d);
            Assert.InRange(400000d / item.Contents.SubstanceTotal, .95d, .999999d);
            Assert.True(item.Contents.Get(FlowProps.Starch) > 0d);
            Assert.True(item.Contents.Get(FlowProps.Acid) > 0d);
            Assert.Equal(0d, item.Contents.Get(FlowProps.Base));
        }
    }
}
