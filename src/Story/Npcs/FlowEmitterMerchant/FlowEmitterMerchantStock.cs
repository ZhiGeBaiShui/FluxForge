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
                FlowEmitterMerchantProgress.FirstStoryKey,
                "flow_emitter",
                2,
                false),
            new StoryStockRule(
                FlowEmitterMerchantProgress.FirstStoryKey,
                "flow_filter",
                1,
                false),
            new StoryStockRule(
                FlowEmitterMerchantProgress.SecondVisitKey,
                "flow_cold_trap",
                1,
                false),
            new StoryStockRule(
                FlowEmitterMerchantProgress.ThirdVisitKey,
                "flow_straight_speed",
                2,
                false),
            new StoryStockRule(
                FlowEmitterMerchantProgress.ThirdVisitKey,
                "l_turn_device",
                2,
                false),
            new StoryStockRule(
                FlowEmitterMerchantProgress.ThirdVisitKey,
                "flow_straight",
                4,
                false),
        };

        internal static void Place()
        {
            StoryStockRule[] rules
                = FlowEmitterMerchantProgress.HasFirstStock
                    ? RepeatRules
                    : FirstVisitRules;

            PlaceRules(rules);

            if (!FlowEmitterMerchantProgress.HasFirstStock)
            {
                FlowEmitterMerchantProgress.MarkFirstStock();
            }
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
                MarkShabby(item);
            }

            if (!PlayerStore.Instance.TryAddDirectSellingItemToTable(item))
            {
                item.Destroy();
            }
        }

        internal static void MarkShabby(GameItem item)
        {
            ItemFeatureHelper.SetShabby(item);

            ItemFeature feature = item.FindItemFeatureByCategory(
                "CATEGORY_SHABBY");

            if (feature == null)
            {
                return;
            }

            int index = item.itemFeatures.IndexOf(feature);
            if (index > 0)
            {
                item.itemFeatures.RemoveAt(index);
                item.itemFeatures.Insert(0, feature);
            }
        }
    }
}
