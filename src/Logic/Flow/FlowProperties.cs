using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 流源属性：流源携带的全部性质 / 成分。
    ///
    /// 槽位、种类与分合规则全部来自 <see cref="FlowPropertyRegistry"/>：
    /// 本类只持有一个按注册表顺序排列的数值数组，不写死任何属性名。
    ///
    /// 设计约定：
    ///   - 调用方用无参构造 + <see cref="Set"/> 按键写槽，不写死任何属性名
    ///   - <see cref="Split"/> / <see cref="Merge"/> / <see cref="SubstanceTotal"/> 只按注册表
    ///     里的种类与规则计算
    ///   - <see cref="Split"/> / <see cref="Merge"/> 均为纯函数：返回新对象，不修改本对象与入参
    ///   - <see cref="Clone"/> 深拷贝数组，克隆体可独立修改
    ///   - 不含价值——价值属于物品定义（items.json 的 value），不随流源属性计算
    /// </summary>
    public sealed class FlowProperties : IFlowState
    {
        private readonly double[] _values = new double[FlowPropertyRegistry.Count];

        /// <summary>全零属性。</summary>
        public FlowProperties()
        {
        }

        /// <summary>流源属性拥有注册表里的全部槽位。</summary>
        public IReadOnlyList<FlowPropertyKey> Keys
        {
            get { return FlowPropertyRegistry.AllKeys; }
        }

        /// <summary>读一个属性槽。默认键（Index=0）自然越界。</summary>
        public double Get(FlowPropertyKey key)
        {
            return _values[key.Index - 1];
        }

        /// <summary>写一个属性槽。默认键（Index=0）自然越界。</summary>
        public void Set(FlowPropertyKey key, double value)
        {
            _values[key.Index - 1] = value;
        }

        /// <summary>物质总量：所有 <see cref="FlowPropertyKind.Liquid"/> 属性之和（现算，不缓存）。</summary>
        public double SubstanceTotal
        {
            get
            {
                double total = 0d;
                IReadOnlyList<FlowPropertyDef> defs = FlowPropertyRegistry.All;
                for (int i = 0; i < defs.Count; i++)
                {
                    FlowPropertyDef def = defs[i];
                    if (def.Kind != FlowPropertyKind.Liquid)
                    {
                        continue;
                    }

                    total += _values[def.Key.Index - 1];
                }
                return total;
            }
        }

        /// <summary>
        /// 等分成 count 份。总量守恒——不是复制，Divide 属性每份得到原值的 1/count。
        /// Keep 属性（标量）每份保留原值。
        /// <para>
        /// 实现：先算出「一份」应有的样子，再深拷贝 count 遍。
        /// 每份内容彼此等价但数组相互独立，可各自被单独修改。
        /// </para>
        /// </summary>
        public List<FlowProperties> Split(int count)
        {
            if (count < 2)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "count 必须大于等于 2");
            }

            // ① 算出「一份」
            FlowProperties template = Clone();
            IReadOnlyList<FlowPropertyDef> defs = FlowPropertyRegistry.All;
            for (int i = 0; i < defs.Count; i++)
            {
                FlowPropertyDef def = defs[i];
                if (def.Split != SplitRule.Divide)
                {
                    continue;   // Keep：原样保留
                }

                int slot = def.Key.Index - 1;
                template._values[slot] /= count;
            }

            // ② 深拷贝 count 份
            List<FlowProperties> result = new List<FlowProperties>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(template.Clone());
            }
            return result;
        }

        /// <summary>
        /// 汇聚：以 this 为第一份，把 others 逐份合并进来，返回新对象。
        /// <para>
        /// Add 属性直接相加；WeightedAverage 属性按各份的物质总量加权。
        /// 注意 others 不应包含 this 自身，否则 this 会被计算两次。
        /// </para>
        /// </summary>
        public FlowProperties Merge(List<FlowProperties> others)
        {
            if (others == null)
            {
                throw new ArgumentNullException(nameof(others));
            }

            return MergeAll(others);
        }

        /// <summary>
        /// 汇聚单份属性：等价于 <c>Merge(只含 other 的列表)</c>。
        /// 供"收到一个就并一个"的调用方使用，不需要为一次合并准备列表。
        /// </summary>
        public FlowProperties Merge(FlowProperties other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return MergeAll(new FlowProperties[] { other });
        }

        /// <summary>
        /// 原地汇聚单份属性：把 <paramref name="other"/> 合并进 this，不新建 FlowProperties。
        /// 规则与 <see cref="Merge(FlowProperties)"/> 相同。
        /// </summary>
        public void MergeFrom(FlowProperties other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            MergeAllInto(new FlowProperties[] { other });
        }

        private FlowProperties MergeAll(IReadOnlyList<FlowProperties> others)
        {
            FlowProperties result = Clone();
            result.MergeAllInto(others);
            return result;
        }


        private void MergeAllInto(IReadOnlyList<FlowProperties> others)
        {
            // 原地修改前先把 this 的权重固定下来；否则前面的 Add 会改变 SubstanceTotal。
            double thisWeight = SubstanceTotal;

            IReadOnlyList<FlowPropertyDef> defs = FlowPropertyRegistry.All;
            for (int i = 0; i < defs.Count; i++)
            {
                FlowPropertyDef def = defs[i];
                int slot = def.Key.Index - 1;

                if (def.Merge == MergeRule.Add)
                {
                    double sum = _values[slot];
                    for (int j = 0; j < others.Count; j++)
                    {
                        sum += others[j]._values[slot];
                    }
                    _values[slot] = sum;
                }
                else
                {
                    // 加权平均：按各份的物质总量加权（现算，不信任何缓存状态）
                    double weight = thisWeight;
                    double weightedSum = _values[slot] * thisWeight;

                    for (int j = 0; j < others.Count; j++)
                    {
                        FlowProperties other = others[j];
                        double otherWeight = other.SubstanceTotal;
                        weightedSum += other._values[slot] * otherWeight;
                        weight += otherWeight;
                    }

                    // 分母为 0（几份都没有物质）时保留 this 的值，避免算出 NaN
                    _values[slot] = weight > 0d
                        ? weightedSum / weight
                        : _values[slot];
                }
            }
        }


        /// <summary>深拷贝：数组独立，克隆体与本体互不影响。</summary>
        public FlowProperties Clone()
        {
            FlowProperties clone = new FlowProperties();
            Array.Copy(_values, clone._values, _values.Length);
            return clone;
        }
    }
}
