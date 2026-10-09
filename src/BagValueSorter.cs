using System.Collections.Generic;
using Il2Cpp;

namespace ProbablyBagMod
{
    /// <summary>
    /// 静态价值计算的分层工具类。
    ///
    /// 输入一组游戏物品，取出各自的 <see cref="ItemBagProperty"/>，
    /// 按 <see cref="ItemBagProperty.Priority"/> 分层，层与层之间按优先级降序排列
    /// （Priority 大的在前），输出 <c>List&lt;List&lt;ItemBagProperty&gt;&gt;</c>。
    ///
    /// 结构等价于 C++ 的 <c>priority_queue&lt;vector&lt;ItemBagProperty&gt;&gt;</c>：
    /// 外层按优先级出队，元素本身是同优先级的物品集合。
    /// 同一优先级内部不排序（顺序无关，因为同层物品互不影响）。
    ///
    /// 没有背包属性的物品静默跳过，不进队列。
    /// </summary>
    public static class BagValueSorter
    {
        /// <summary>
        /// 按优先级降序分层。
        /// </summary>
        /// <param name="items">待分层的游戏物品；可含 null 与无属性的物品。</param>
        /// <returns>外层按 Priority 降序（大的在前），内层是同优先级的属性集合。</returns>
        public static List<List<ItemBagProperty>> SplitByPriority(IList<GameItem> items)
        {
            var result = new List<List<ItemBagProperty>>();
            if (items == null)
            {
                return result;
            }

            // 先分组：Priority -> 该层的属性集合。无序，最后统一排序。
            var groups = new Dictionary<int, List<ItemBagProperty>>();
            for (int i = 0; i < items.Count; i++)
            {
                ItemBagProperty property = BagPropertyManager.Get(items[i]);
                if (property == null)
                {
                    continue;
                }

                if (!groups.TryGetValue(property.Priority, out List<ItemBagProperty> layer))
                {
                    layer = new List<ItemBagProperty>();
                    groups[property.Priority] = layer;
                }
                layer.Add(property);
            }

            // 层按优先级降序（大的先出场）。
            var priorities = new List<int>(groups.Keys);
            priorities.Sort();
            priorities.Reverse();

            for (int i = 0; i < priorities.Count; i++)
            {
                result.Add(groups[priorities[i]]);
            }

            return result;
        }
    }
}
