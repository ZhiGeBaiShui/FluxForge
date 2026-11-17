using System.Text.Json;
using System.Text.RegularExpressions;
using FluxForge.Story.Hospital;
using Xunit;

namespace FluxForge.Story.Tests
{
    public sealed class ItemLocalizationTests : IDisposable
    {
        public void Dispose()
        {
            ModHelper.CurrentLocale = "zh";
        }

        [Fact]
        public void StoryKeysHaveBothTranslations()
        {
            var chinese = NativeLocalizationData.Read("zh");
            var english = NativeLocalizationData.Read("en");
            foreach (Type type in typeof(HospitalContractPickupTexts).Assembly.GetTypes()
                .Where(t => t.Namespace != null && t.Namespace.StartsWith("FluxForge.Story")))
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            {
                if (!field.IsLiteral || field.FieldType != typeof(string)) continue;
                string key = (string)field.GetRawConstantValue();
                if (!key.StartsWith("story.")) continue;
                Assert.True(chinese.ContainsKey(key), key);
                Assert.True(english.ContainsKey(key), key);
            }
        }

        [Fact]
        public void ExistingGraphAndNpcResolveLabelsAfterLanguageChanges()
        {
            ModHelper.CurrentLocale = "zh";
            var assembly = new HospitalContractOfferAssembly(new FluxForge.Story.NpcState(
                FluxForge.Story.AcidBuyer.AcidBuyerStory.Create().Definition));
            var npc = FluxForge.Story.AcidBuyer.AcidBuyerStory.Create().Definition;
            var edge = new FluxForge.Story.AcidBuyerFirstHighPurity.AcidBuyerHighPurityAnswerEdge(
                HospitalContractPickupTexts.FinishOption, HospitalContractPickupTexts.Greeting);
            ModHelper.CurrentLocale = "en";
            Assert.Equal(NativeLocalizationData.Read("en")[HospitalContractPickupTexts.FinishOption], edge.Label);
            Assert.Equal(NativeLocalizationData.Read("en")["npc.vera.name"], npc.DisplayName);
            Assert.All(HospitalVisitorTexts.Visitor01, line => Assert.DoesNotMatch("[\u4e00-\u9fff]", line));
            var conversation = new StoryFlowTests.Conversation(assembly);
            Assert.All(conversation.Last.Choices, choice => Assert.DoesNotMatch("[\u4e00-\u9fff]", choice.Text));
        }

        [Fact]
        public void NativeLanguageTablesHaveMatchingKeysAndFormatArguments()
        {
            Dictionary<string, string> chinese = NativeLocalizationData.Read("zh");
            Dictionary<string, string> english = NativeLocalizationData.Read("en");
            Assert.Equal(chinese.Keys.OrderBy(k => k), english.Keys.OrderBy(k => k));
            foreach (string key in chinese.Keys)
            {
                Assert.DoesNotContain("\"", key);
                Assert.NotEmpty(english[key]);
                Assert.DoesNotMatch("[\u4e00-\u9fff]", english[key]);
                Assert.Equal(Regex.Matches(chinese[key], @"\{\d+\}").Select(m => m.Value).OrderBy(v => v),
                    Regex.Matches(english[key], @"\{\d+\}").Select(m => m.Value).OrderBy(v => v));
                Assert.Equal(Regex.Matches(chinese[key], @"</?[^>]+>").Select(m => m.Value).OrderBy(v => v),
                    Regex.Matches(english[key], @"</?[^>]+>").Select(m => m.Value).OrderBy(v => v));
            }
        }

        [Theory]
        [InlineData("zh")]
        [InlineData("en")]
        public void EveryConfiguredItemAndStatusLineHasAnExternalTranslation(string locale)
        {
            Dictionary<string, string> table = NativeLocalizationData.Read(locale);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Data", "items.json")));
            foreach (JsonElement item in document.RootElement.GetProperty("items").EnumerateArray())
            {
                Assert.False(item.TryGetProperty("name", out _));
                Assert.False(item.TryGetProperty("desc", out _));
                Assert.False(item.TryGetProperty("flavor", out _));
                Assert.False(item.TryGetProperty("paperImage", out _));
                Assert.NotEmpty(table[item.GetProperty("nameLoc").GetString()]);
                Assert.NotEmpty(table[item.GetProperty("descriptionLoc").GetString()]);
                if (item.TryGetProperty("flavorLoc", out JsonElement flavor))
                    Assert.NotEmpty(table[flavor.GetString()]);
                if (item.TryGetProperty("statusLines", out JsonElement lines))
                    foreach (JsonElement line in lines.EnumerateArray())
                        Assert.NotEmpty(table[line.GetProperty("labelLoc").GetString()]);
            }
        }

        [Theory]
        [InlineData("zh")]
        [InlineData("en")]
        public void StarterNoteUsesNativeLocalizedTextAndNoDuplicateItemRegistration(string locale)
        {
            Dictionary<string, string> table = NativeLocalizationData.Read(locale);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Data", "starter_note.json")));
            JsonElement book = document.RootElement;
            Assert.False(book.TryGetProperty("item", out _));
            HashSet<string> ids = new HashSet<string>();
            foreach (JsonElement element in book.GetProperty("pages")[0].GetProperty("elements").EnumerateArray())
            {
                Assert.True(ids.Add(element.GetProperty("id").GetString()));
                if (element.GetProperty("type").GetString() == "text")
                {
                    Assert.False(element.TryGetProperty("text", out _));
                    Assert.NotEmpty(table[element.GetProperty("loc").GetString()]);
                }
            }
            Assert.Contains("emitter", ids);
            Assert.Contains("receiver", ids);
        }

        [Theory]
        [InlineData("zh", "医院报废的塞纳普斯原料")]
        [InlineData("en", "Discarded Hospital Synaprest Stock")]
        public void QuestStockUsesCurrentLanguageWithoutChangingLiquidContents(string locale, string expected)
        {
            new StoryFlowTests();
            ModHelper.CurrentLocale = locale;
            HospitalSynaprestStock.GiveInitial();
            GameItem item = Assert.Single(EmporiumEntry.Instance.frontInvinvElement.childItems);
            Assert.Equal(expected, item.name);
            Assert.Equal(600000d, item.Contents.Get(FlowProps.Synaprest));
            Assert.Equal(606061d, item.Contents.SubstanceTotal);
        }
    }
}
