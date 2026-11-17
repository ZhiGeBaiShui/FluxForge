using FluxForge.Story.Hospital;
using UnityEngine.Localization.Settings;
using Xunit;

namespace FluxForge.Story.Tests
{
    public sealed class HospitalProcurementTests : IDisposable
    {
        public HospitalProcurementTests()
        {
            PlayerStore.Instance = new PlayerStore();
            HospitalReputation.EnsureFaction();
            RNG.RollSample = 0;
            RNG.PickOffset = 0;
            RNG.LastChance = 0;
        }

        public void Dispose()
        {
            RNG.RollSample = 0;
            RNG.PickOffset = 0;
            StoreClientListDict.storeClientDict.Remove("test.procurement.a");
            StoreClientListDict.storeClientDict.Remove("test.procurement.b");
        }

        [Theory]
        [InlineData(79, false)]
        [InlineData(80, true)]
        public void PerkUsesNativeThresholdAndRequestedText(int reputation, bool expected)
        {
            TestHospitalReputation.Value = reputation;
            Assert.Equal(expected, StoreReputation.IsPerkUnlocked(HospitalReputationTexts.SpecialProcurementPerkId));
            var entries = LocalizationSettings.StringDatabase.ReputationTable.Entries;
            string key = "rep_perk_" + HospitalReputationTexts.SpecialProcurementPerkId;
            Assert.Equal("特殊采购", entries[key + "_name"]);
            Assert.Equal("每天有10%的概率来一位医院的客人，对他们出售的违禁品仅会产生其价值的50%的可用证据。", entries[key + "_desc"]);
            Assert.Equal("上层区医方不关心货物是否违禁品，也不怕治安部的调查工作。", entries[key + "_flavor"]);
        }

        [Theory]
        [InlineData(79, 0, 0)]
        [InlineData(80, 9, 1)]
        [InlineData(80, 10, 0)]
        public void GenerationHookAddsAtMostOneExtraClient(int reputation, int sample, int extra)
        {
            TestHospitalReputation.Value = reputation;
            RNG.RollSample = sample;
            StoreClientListDict.storeClientDict.Add("test.procurement.a", () => new StoreClient { identifier = "test.procurement.a" });
            var manager = PlayerStore.Instance.storeClientManager;
            var ordinary = new StoreClient { identifier = "ordinary" };
            manager.AddClient(ordinary);
            var procurement = new HospitalProcurement(new[] { "test.procurement.a" });
            procurement.Subscribe();
            try
            {
                ModHook.FireGenerateCustomer(manager);
                Assert.Equal(1 + extra, manager.clientStack.Count);
                Assert.Same(ordinary, manager.clientStack[0]);
                if (reputation >= 80) Assert.Equal(10, RNG.LastChance);
            }
            finally { procurement.Dispose(); }
        }

        [Fact]
        public void DisposeRemovesOnlyProcurementClientsAndDismissesTheirCurrentVisitor()
        {
            var procurement = new HospitalProcurement(HospitalProcurementClients.Identifiers);
            var store = PlayerStore.Instance;
            var ordinary = new StoreClient { identifier = "ordinary" };
            store.storeClientManager.clientStack.Add(ordinary);
            store.storeClientManager.clientStack.Add(new StoreClient { identifier = HospitalProcurementClients.Visitor01Id });
            store.currentClientInstance = new StoreClientInstance(new StoreClient { identifier = HospitalProcurementClients.Visitor04Id });
            procurement.Dispose();
            Assert.Same(ordinary, Assert.Single(store.storeClientManager.clientStack));
            Assert.Null(store.currentClientInstance);
            Assert.Equal(1, store.Dismissals);
        }

        [Fact]
        public void DisposeAtMenuDoesNotCreateSave()
        {
            var procurement = new HospitalProcurement(HospitalProcurementClients.Identifiers);
            PlayerStore.Instance = null;
            procurement.Dispose();
            Assert.False(PlayerStore.HasInstance);
        }

        [Fact]
        public void GenerationCreatesFreshClientFromSelectedNativeFactoryAndStopsAfterDispose()
        {
            TestHospitalReputation.Value = 80;
            StoreClientListDict.storeClientDict.Add("test.procurement.a", () => new StoreClient { identifier = "test.procurement.a" });
            StoreClientListDict.storeClientDict.Add("test.procurement.b", () => new StoreClient { identifier = "test.procurement.b" });
            RNG.PickOffset = 1;
            var procurement = new HospitalProcurement(new[] { "test.procurement.a", "test.procurement.b" });
            var manager = PlayerStore.Instance.storeClientManager;
            procurement.Subscribe();
            try
            {
                ModHook.FireGenerateCustomer(manager);
                ModHook.FireGenerateCustomer(manager);
                Assert.Equal(2, manager.clientStack.Count);
                Assert.All(manager.clientStack, client => Assert.Equal("test.procurement.b", client.identifier));
                Assert.NotSame(manager.clientStack[0], manager.clientStack[1]);
            }
            finally { procurement.Dispose(); }
            ModHook.FireGenerateCustomer(manager);
            Assert.Empty(manager.clientStack);
        }

        [Theory]
        [InlineData("fluxforge_hospital_visitor_01", "npc_hospital_visitor_01", "你好，店主。", "医院让我来买点东西，你这有什么好货？", null)]
        [InlineData("fluxforge_hospital_visitor_02", "npc_hospital_visitor_02", "你好。", "我来替医院采购，先看看你的现货。", null)]
        [InlineData("fluxforge_hospital_visitor_03", "npc_hospital_visitor_03", "你好啊。", "让我看看有没有医院用得上的东西。", null)]
        [InlineData("fluxforge_hospital_visitor_04", "npc_hospital_visitor_04", "你好。", "医院需要添些东西。", "我能看看你这里的货吗？")]
        public void NativeDirectoryCreatesEachVisitorWithAgreedGreetingAndCommonBuyingRules(
            string id, string sprite, string greeting, string purpose, string finalLine)
        {
            StoreStation.Day = 12;
            HospitalProcurementClients.Register();
            try
            {
                StoreClient client = StoreClientListDict.CreateStoreClient(id);
                Assert.Equal(id, client.identifier);
                Assert.Equal("中心医院访客", client.displayName);
                Assert.Equal("中心医院访客", StoreClientListDict.GetClientDisplayName(id));
                Assert.Equal(sprite, client.spriteName);
                Assert.Equal(HospitalReputation.FactionId, client.clientFaction);
                Assert.Equal(StoreClient.ClientIntent.BUY, client.clientIntent);
                Assert.False(client.IsPersonalConsumption);
                Assert.True(client.CanExposeChem);
                Assert.Equal(new Func<ItemFeature, StoreClient, bool>(ClientCanExposeFunc.ULClientExposeCapacity), client.canClientExposeFeature);
                Assert.Equal(274, client.Budget);
                Assert.Equal("ALL_TYPE", Assert.Single(client.clientBuyingTagList));
                Assert.False(client.noAcceptingContraband);
                Assert.True(client.BasicDialogAdded);
                Assert.True(client.CreationCompleted);
                Assert.Equal(greeting, client.mainDialogue.Text);
                Assert.Equal(purpose, client.mainDialogue.Next.Text);
                if (finalLine == null) Assert.Null(client.mainDialogue.Next.Next);
                else
                {
                    Assert.Equal(finalLine, client.mainDialogue.Next.Next.Text);
                    Assert.Null(client.mainDialogue.Next.Next.Next);
                }
            }
            finally { HospitalProcurementClients.Unregister(); }
            Assert.DoesNotContain(id, StoreClientListDict.storeClientDict.Keys);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void SpecialProcurementCanSelectEveryRegisteredVisitor(int selectedIndex)
        {
            TestHospitalReputation.Value = 80;
            RNG.PickOffset = selectedIndex;
            HospitalProcurementClients.Register();
            var procurement = new HospitalProcurement(HospitalProcurementClients.Identifiers);
            procurement.Subscribe();
            try
            {
                ModHook.FireGenerateCustomer(PlayerStore.Instance.storeClientManager);
                StoreClient client = Assert.Single(PlayerStore.Instance.storeClientManager.clientStack);
                Assert.Equal(HospitalProcurementClients.Identifiers[selectedIndex], client.identifier);
            }
            finally
            {
                procurement.Dispose();
                HospitalProcurementClients.Unregister();
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void HospitalFactionSelectsNativeHospitalInspectionWithoutChangingScannerOwnership(bool hasScanner)
        {
            StoreClient client = new StoreClient
            {
                clientFaction = HospitalReputation.FactionId,
                CanExposeChem = hasScanner,
            };
            client.CompleteClientCreation();
            Assert.Equal(new Func<ItemFeature, StoreClient, bool>(ClientCanExposeFunc.ULClientExposeCapacity), client.canClientExposeFeature);
            Assert.Equal(hasScanner, client.CanExposeChem);
        }

        [Theory]
        [InlineData("FACTION_UPPER_LEVEL")]
        [InlineData("FACTION_LOWER_LEVEL")]
        public void HospitalFactionPatchLeavesOtherFactionsInspectionUntouched(string faction)
        {
            StoreClient client = new StoreClient
            {
                clientFaction = faction,
                canClientExposeFeature = ClientCanExposeFunc.BasicClientExposeCapacity,
            };
            HospitalClientCreationPatch.Postfix(client, false);
            Assert.Equal(new Func<ItemFeature, StoreClient, bool>(ClientCanExposeFunc.BasicClientExposeCapacity), client.canClientExposeFeature);
        }

        [Fact]
        public void ExplicitInspectionOverrideRetainsNativeIgnoreExposeFuncSemantics()
        {
            StoreClient client = new StoreClient
            {
                clientFaction = HospitalReputation.FactionId,
                canClientExposeFeature = ClientCanExposeFunc.BasicClientExposeCapacity,
            };
            client.CompleteClientCreation(IgnoreExposeFunc: true);
            Assert.Equal(new Func<ItemFeature, StoreClient, bool>(ClientCanExposeFunc.BasicClientExposeCapacity), client.canClientExposeFeature);
        }

        [Theory]
        [InlineData(79, "FACTION_FLUXFORGE_HOSPITAL", 101, true, -1)]
        [InlineData(80, "FACTION_FLUXFORGE_HOSPITAL", 101, false, 50)]
        [InlineData(80, "FACTION_FLUXFORGE_HOSPITAL", 100, false, 50)]
        [InlineData(80, "FACTION_FLUXFORGE_HOSPITAL", 0, false, 0)]
        [InlineData(80, "FACTION_LOWER_LEVEL", 101, true, -1)]
        [InlineData(80, "FACTION_BLACK_MARKET", 101, true, -1)]
        [InlineData(80, "FACTION_REVOLUTION", 101, true, -1)]
        public void CrimePatchUsesNativeIntegerHalvingOnlyForUnlockedHospital(
            int reputation, string faction, int value, bool runOriginal, int expected)
        {
            TestHospitalReputation.Value = reputation;
            int result = -1;
            Assert.Equal(runOriginal, HospitalCrimeModifierPatch.Prefix(faction, value, ref result));
            Assert.Equal(expected, result);
        }
    }
}
