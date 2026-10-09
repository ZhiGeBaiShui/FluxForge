namespace ProbablyBagMod
{
    /// <summary>
    /// 物品的背包属性。纯数据，只读不可变。
    ///
    /// 用途：背包系统通过「计算背包运算后的价值」决定最终产物的总价值。
    /// 每个物品自带 <see cref="Value"/>，并通过 <see cref="AdditiveInfluence"/> /
    /// <see cref="MultiplicativeInfluence"/> 影响邻近物品的取值：
    /// 加法影响 = <c>x + a</c>，乘法影响 = <c>x * m</c>。
    ///
    /// <see cref="Priority"/> 表示该物品在哪个优先级里参与计算；
    /// 同一优先级的物品之间不互相影响，以避免无限递归。
    ///
    /// 本类只负责承载数据。注入到游戏物品、以及回收，均由管理类统一处理。
    /// </summary>
    public sealed class ItemBagProperty
    {
        /// <summary>物品自身价值。</summary>
        public int Value { get; }

        /// <summary>对邻近物品的加法影响（作用为 <c>x + a</c>）。</summary>
        public int AdditiveInfluence { get; }

        /// <summary>对邻近物品的乘法影响（作用为 <c>x * m</c>）。</summary>
        public float MultiplicativeInfluence { get; }

        /// <summary>计算优先级；相同优先级之间不互相影响，用于避免无限递归。</summary>
        public int Priority { get; }

        public ItemBagProperty(int value, int additiveInfluence, float multiplicativeInfluence, int priority)
        {
            Value = value;
            AdditiveInfluence = additiveInfluence;
            MultiplicativeInfluence = multiplicativeInfluence;
            Priority = priority;
        }
    }
}
