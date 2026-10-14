using System;
using System.Collections.Generic;
using System.Reflection;

namespace ProbablyBagMod
{
    /// <summary>
    /// 流源属性：流源携带的全部性质 / 成分。对外提供公开成员，由反应框架直接读写。
    ///
    /// 不含价值——价值由外部工具类根据属性动态算出，不存储。
    ///
    /// 设计约定：
    ///   - 只提供带参构造，外部无法 <c>new FlowProperties()</c> 造出半空对象
    ///   - <see cref="Split"/> / <see cref="Merge"/> 均为纯函数：返回新对象，不修改本对象与入参
    ///   - 造出的新对象内部已重算 <see cref="TotalMass"/>，拿出来就是自洽的
    ///   - 新增 public double 字段时，必须在 <c>RuleTable</c> 声明分合规则，
    ///     否则静态构造函数直接抛异常点名
    /// </summary>
    public sealed class FlowProperties
    {
        /// <summary>水分含量。</summary>
        public double Moisture;

        /// <summary>温度。单位与刻度由上层约定，框架不解释。</summary>
        public double Temperature;

        /// <summary>
        /// 总物质含量。只读，外部改不了，由 <see cref="Recalculate"/> 写入。
        /// 作为加权平均的权重：物质越多，该份话语权越大。
        /// </summary>
        public double TotalMass { get; private set; }

        private static readonly PropertyRule[] RuleTable = new[]
        {
            new PropertyRule(nameof(Moisture),    SplitRule.Divide, MergeRule.Add),
            new PropertyRule(nameof(Temperature), SplitRule.Keep,    MergeRule.WeightedAverage),
        };

        private static readonly Dictionary<string, FieldInfo> FieldMap = BuildFieldMap();

        static FlowProperties()
        {
            Validate();
        }

        public FlowProperties(double moisture, double temperature)
        {
            Moisture = moisture;
            Temperature = temperature;
            Recalculate();
        }

        /// <summary>
        /// 等分成 count 份。总量守恒——不是复制，每份得到原值的 1/count。
        /// Keep 规则的字段（温度）每份保留原值。
        /// <para>
        /// 实现：先算出「一份」应有的样子，再复制 count 遍。
        /// 每份内容彼此等价但相互独立（引用不同），可各自被单独修改。
        /// </para>
        /// </summary>
        public List<FlowProperties> Split(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            // ① 算出「一份」
            FlowProperties template = (FlowProperties)MemberwiseClone();
            foreach (PropertyRule rule in RuleTable)
            {
                if (rule.Split != SplitRule.Divide)
                {
                    continue;   // Keep：原样保留
                }

                FieldInfo field = FieldMap[rule.FieldName];
                double value = (double)field.GetValue(this);
                field.SetValue(template, value / count);
            }
            template.Recalculate();

            // ② 复制 count 份（MemberwiseClone 不走构造函数，故可反复复制）
            List<FlowProperties> result = new List<FlowProperties>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add((FlowProperties)template.MemberwiseClone());
            }
            return result;
        }

        /// <summary>
        /// 汇聚：以 this 为第一份，把 others 逐份合并进来，返回新对象。
        /// <para>
        /// 注意 others 不应包含 this 自身，否则 this 会被计算两次。
        /// </para>
        /// </summary>
        public FlowProperties Merge(List<FlowProperties> others)
        {
            if (others == null)
            {
                throw new ArgumentNullException(nameof(others));
            }

            FlowProperties result = (FlowProperties)MemberwiseClone();

            foreach (PropertyRule rule in RuleTable)
            {
                FieldInfo field = FieldMap[rule.FieldName];

                if (rule.Merge == MergeRule.Add)
                {
                    double sum = (double)field.GetValue(this);
                    foreach (FlowProperties other in others)
                    {
                        sum += (double)field.GetValue(other);
                    }
                    field.SetValue(result, sum);
                }
                else
                {
                    // 加权平均：按各份的总物质加权
                    double weight = ComputeMass(this);
                    double weightedSum = (double)field.GetValue(this) * weight;

                    foreach (FlowProperties other in others)
                    {
                        double otherWeight = ComputeMass(other);
                        weightedSum += (double)field.GetValue(other) * otherWeight;
                        weight += otherWeight;
                    }

                    // 分母为 0（几份都没有物质）时保留 this 的值，避免算出 NaN
                    double averaged = weight > 0d
                        ? weightedSum / weight
                        : (double)field.GetValue(this);

                    field.SetValue(result, averaged);
                }
            }

            result.Recalculate();
            return result;
        }

        /// <summary>重算总物质含量：把所有 Merge=Add 的字段求和。</summary>
        public void Recalculate()
        {
            TotalMass = ComputeMass(this);
        }

        /// <summary>
        /// 现算一份的总物质。刻意不读 <see cref="TotalMass"/>，
        /// 因为合并时各份是否刚被刷过不可信，不能依赖入参状态。
        /// </summary>
        private static double ComputeMass(FlowProperties p)
        {
            double total = 0d;
            foreach (PropertyRule rule in RuleTable)
            {
                if (rule.Merge != MergeRule.Add)
                {
                    continue;
                }
                total += (double)FieldMap[rule.FieldName].GetValue(p);
            }
            return total;
        }

        private static Dictionary<string, FieldInfo> BuildFieldMap()
        {
            FieldInfo[] fields = typeof(FlowProperties).GetFields(
                BindingFlags.Public | BindingFlags.Instance);

            Dictionary<string, FieldInfo> map = new Dictionary<string, FieldInfo>(fields.Length);
            foreach (FieldInfo field in fields)
            {
                map[field.Name] = field;
            }
            return map;
        }

        private static void Validate()
        {
            // 规则指向的字段必须存在
            foreach (PropertyRule rule in RuleTable)
            {
                if (!FieldMap.ContainsKey(rule.FieldName))
                {
                    throw new InvalidOperationException(
                        "分合规则指向了不存在的字段：" + rule.FieldName);
                }
            }

            // 每个 public 字段都必须配规则
            foreach (string name in FieldMap.Keys)
            {
                bool matched = false;
                foreach (PropertyRule rule in RuleTable)
                {
                    if (rule.FieldName == name)
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    throw new InvalidOperationException(
                        "属性字段缺少分合规则：FlowProperties." + name
                        + "（请在 RuleTable 中为该字段声明 PropertyRule）");
                }
            }
        }
    }
}
