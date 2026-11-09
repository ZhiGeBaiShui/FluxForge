using System;
using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 流源 / 容器物品的运行期 <see cref="FlowProperties"/> ↔ 原生液体 tag 读写点。
    ///
    /// 已注册液体按各自 tag 读写；没有手动注册的 <c>CURRENT_PART_*</c> 汇总到
    /// <see cref="FlowProps.Foreign"/>，只携带体积，不参与装置反应。
    ///
    /// 原始外来 tag 不做改名、不删除：
    ///   · 普通写入只保留它们，不覆盖；
    ///   · 发射成功后才把它们的数值清零，避免同一批液体被重复发射。
    ///
    /// 设备内部库存没有原始液体 tag，因此设备侧的外来体积会写入
    /// <see cref="FlowTagNaming.ForeignLiquid"/> 聚合 tag。
    /// </summary>
    public static class FlowStock
    {
        private const string LiquidTagPrefix = "CURRENT_PART_";

        private static readonly HashSet<string> KnownLiquidTags = BuildKnownLiquidTags();

        /// <summary>读出一份完整的流源属性；不存在的 tag 按 0 处理。</summary>
        public static FlowProperties Read(GameItem item)
        {
            FlowProperties properties = new FlowProperties();

            foreach (FlowPropertyTag property in FlowPropertyTags.All)
            {
                if (property.Kind == FlowPropertyKind.Foreign)
                {
                    continue;
                }

                double value = property.Kind == FlowPropertyKind.Liquid
                    ? GameItemTags.ReadInt(item, property.Tag)
                    : GameItemTags.ReadDouble(item, property.Tag);

                properties.Set(property.Key, value);
            }

            properties.Set(FlowProps.Foreign, ReadForeign(item));
            return properties;
        }

        /// <summary>
        /// 把一份完整的流源属性写回物品 tag，然后交给 <see cref="ItemTagCommit"/> 提交。
        ///
        /// 物品上还有原始外来液体 tag 时，不写聚合 foreign tag，
        /// 让原始 tag 继续作为唯一来源；设备库存没有原始 tag，才写聚合槽。
        /// </summary>
        public static void Write(GameItem item, FlowProperties source)
        {
            WriteCore(item, source, !HasUnknownLiquidTags(item));
            ItemTagCommit.Commit(item);
        }

        /// <summary>
        /// 发射成功后的写回：清空已知液体，并清零原始外来液体 tag。
        /// 外来液体已经进入流源的 foreign 槽，源容器不能保留同一份体积。
        /// </summary>
        public static void WriteAfterEmit(GameItem item, FlowProperties source)
        {
            List<string> unknownTags = FindUnknownLiquidTags(item);

            WriteCore(item, source, true);

            for (int i = 0; i < unknownTags.Count; i++)
            {
                GameItemTags.WriteInt(item, unknownTags[i], 0);
            }

            ItemTagCommit.Commit(item);
        }

        /// <summary>总存量 = 已知液体 + 未注册液体（µL）；温度不计入。</summary>
        public static double Total(GameItem item)
        {
            return Read(item).SubstanceTotal;
        }

        private static void WriteCore(
            GameItem item,
            FlowProperties source,
            bool writeForeign)
        {
            foreach (FlowPropertyTag property in FlowPropertyTags.All)
            {
                if (property.Kind == FlowPropertyKind.Foreign)
                {
                    if (writeForeign)
                    {
                        GameItemTags.WriteInt(
                            item,
                            property.Tag,
                            (int)source.Get(property.Key));
                    }

                    continue;
                }

                double value = source.Get(property.Key);

                if (property.Kind == FlowPropertyKind.Liquid)
                {
                    GameItemTags.WriteInt(item, property.Tag, (int)value);
                }
                else
                {
                    GameItemTags.WriteDouble(item, property.Tag, value);
                }
            }
        }

        private static int ReadForeign(GameItem item)
        {
            List<string> unknownTags = FindUnknownLiquidTags(item);
            if (unknownTags.Count > 0)
            {
                int total = 0;
                for (int i = 0; i < unknownTags.Count; i++)
                {
                    total += GameItemTags.ReadInt(item, unknownTags[i]);
                }

                return total;
            }

            return GameItemTags.ReadInt(item, FlowTagNaming.ForeignLiquid);
        }

        private static bool HasUnknownLiquidTags(GameItem item)
        {
            return FindUnknownLiquidTags(item).Count > 0;
        }

        private static List<string> FindUnknownLiquidTags(GameItem item)
        {
            List<string> result = new List<string>();

            CollectUnknown(item.modifiedState, result);
            CollectUnknown(item.state, result);
            return result;
        }

        private static void CollectUnknown(TagSystem state, List<string> result)
        {
            foreach (string tag in state.dict.Keys)
            {
                if (!IsUnknownLiquidTag(tag) || result.Contains(tag))
                {
                    continue;
                }

                result.Add(tag);
            }
        }

        private static bool IsUnknownLiquidTag(string tag)
        {
            return tag.StartsWith(LiquidTagPrefix, StringComparison.Ordinal)
                && !KnownLiquidTags.Contains(tag);
        }

        private static HashSet<string> BuildKnownLiquidTags()
        {
            HashSet<string> tags = new HashSet<string>(StringComparer.Ordinal);

            foreach (FlowPropertyTag property in FlowPropertyTags.All)
            {
                if (property.Kind == FlowPropertyKind.Liquid)
                {
                    tags.Add(property.Tag);
                }
            }

            return tags;
        }
    }
}
