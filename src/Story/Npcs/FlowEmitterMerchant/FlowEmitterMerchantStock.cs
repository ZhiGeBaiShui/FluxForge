namespace FluxForge.Story.FlowEmitterMerchant
{
    /// <summary>一条库存规则：看过某段剧情后，上架哪种货物。</summary>
    internal sealed class StoryStockRule
    {
        internal string StoryKey;
        internal string ItemKey;
        internal int Count;
        internal bool Shabby;

        internal StoryStockRule(
            string storyKey,
            string itemKey,
            int count,
            bool shabby)
        {
            StoryKey = storyKey;
            ItemKey = itemKey;
            Count = count;
            Shabby = shabby;
        }
    }

    /// <summary>根据剧情进度计算本次上架的货物。</summary>
    internal static class FlowEmitterMerchantStock
    {
        private static readonly StoryStockRule[] FirstVisitRules =
        {
            new StoryStockRule(null, "flow_emitter", 2, true),
            new StoryStockRule(null, "flow_filter", 1, true),
        };

        private static readonly StoryStockRule[] RepeatRules =
        {
            new StoryStockRule(
                FlowEmitterMerchantProgress.EmitterDemoKey,
                "flow_emitter",
                2,
                false),
            new StoryStockRule(
                FlowEmitterMerchantProgress.FilterDemoKey,
                "flow_filter",
                1,
                false),
            new StoryStockRule(
                FlowEmitterMerchantProgress.ColdTrapDemoKey,
                "flow_cold_trap",
                1,
                false),
        };

        internal static void Place()
        {
            StoryStockRule[] rules = FlowEmitterMerchantProgress.HasArrivalSeen
                ? RepeatRules
                : FirstVisitRules;

            PlaceRules(rules);
        }

        private static void PlaceRules(StoryStockRule[] rules)
        {
            foreach (StoryStockRule rule in rules)
            {
                if (rule.StoryKey != null
                    && !PlayerStore.Instance.modData.ContainsKey(
                        rule.StoryKey))
                {
                    continue;
                }

                for (int i = 0; i < rule.Count; i++)
                {
                    AddStock(rule.ItemKey, rule.Shabby);
                }
            }
        }

        private static void AddStock(string itemKey, bool shabby)
        {
            GameItem item = DirectoryMaster.Item(itemKey);

            if (shabby)
            {
                ItemFeatureHelper.SetShabby(item);
            }

            if (!PlayerStore.Instance.TryAddDirectSellingItemToTable(item))
            {
                item.Destroy();
            }
        }
    }
}
