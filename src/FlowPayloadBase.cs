using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace ProbablyBagMod
{
    /// <summary>
    /// 属性类基类：零样板代码接入分流 / 汇聚。
    ///
    /// 用法：
    ///   1. 继承本类，定义 public double 字段
    ///   2. 覆写 <see cref="Rules"/>，为每个字段声明分合规则
    ///
    /// 约定（重要）：
    ///   - <see cref="Rules"/> 必须返回**静态数据**（如 <c>static readonly</c> 数组）。
    ///     基类构造函数会读取它，而那时子类的实例字段尚未初始化。
    ///   - 类型必须有可供 <see cref="Activator.CreateInstance(Type)"/> 调用的无参构造函数。
    ///   - 构造时校验「字段 ↔ 规则」一一对应：漏规则、或规则指向不存在的字段，都会抛异常。
    ///     这样新增字段时忘记配规则会立刻暴露，而不是静默算错。
    ///
    /// Split / Merge 均为纯函数：新建实例承载结果，不修改本对象与入参。
    /// 反射结果按类型缓存，每个属性类只扫描一次。
    ///
    /// 特殊字段（例如温度需要加权平均）不适用 Divide/Add 时，
    /// 子类应自行覆写 <see cref="Split"/> / <see cref="Merge"/>。
    /// </summary>
    public abstract class FlowPayloadBase : IFlowPayload
    {
        private static readonly ConcurrentDictionary<Type, FieldInfo[]> FieldCache
            = new ConcurrentDictionary<Type, FieldInfo[]>();

        private readonly FieldInfo[] _fields;
        private readonly Dictionary<string, PropertyRule> _ruleMap;

        protected FlowPayloadBase()
        {
            _fields = GetCachedFields(GetType());
            _ruleMap = BuildRuleMap();
            Validate(_fields, _ruleMap);
        }

        /// <summary>本属性类的分合规则表。必须为静态数据。</summary>
        protected abstract IReadOnlyList<PropertyRule> Rules { get; }

        /// <summary>本实例的全部字段。装置需要遍历全部属性时使用。</summary>
        public IEnumerable<FieldInfo> Fields => _fields;

        public IReadOnlyList<IFlowPayload> Split(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var result = new List<IFlowPayload>(count);
            for (int i = 0; i < count; i++)
            {
                object copy = Activator.CreateInstance(GetType());
                foreach (FieldInfo f in _fields)
                {
                    object value = f.GetValue(this);
                    if (_ruleMap[f.Name].Split == SplitRule.Divide
                        && f.FieldType == typeof(double))
                    {
                        value = (double)value / count;
                    }
                    f.SetValue(copy, value);
                }
                result.Add((IFlowPayload)copy);
            }
            return result;
        }

        public IFlowPayload Merge(IFlowPayload other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (other.GetType() != GetType())
            {
                throw new ArgumentException(
                    "不能合并不同类型的载荷：" + GetType().Name + " 与 " + other.GetType().Name);
            }

            object result = Activator.CreateInstance(GetType());
            var source = (FlowPayloadBase)other;
            foreach (FieldInfo f in _fields)
            {
                object value = f.GetValue(this);
                if (_ruleMap[f.Name].Merge == MergeRule.Add
                    && f.FieldType == typeof(double))
                {
                    value = (double)value + (double)f.GetValue(source);
                }
                f.SetValue(result, value);
            }
            return (IFlowPayload)result;
        }

        private Dictionary<string, PropertyRule> BuildRuleMap()
        {
            IReadOnlyList<PropertyRule> rules = Rules;
            var map = new Dictionary<string, PropertyRule>(rules.Count);
            foreach (PropertyRule rule in rules)
            {
                map[rule.FieldName] = rule;
            }
            return map;
        }

        private static FieldInfo[] GetCachedFields(Type type)
        {
            return FieldCache.GetOrAdd(type,
                t => t.GetFields(BindingFlags.Public | BindingFlags.Instance));
        }

        private static void Validate(FieldInfo[] fields, Dictionary<string, PropertyRule> ruleMap)
        {
            foreach (FieldInfo f in fields)
            {
                if (!ruleMap.ContainsKey(f.Name))
                {
                    throw new InvalidOperationException(
                        "属性字段缺少分合规则：" + f.DeclaringType?.Name + "." + f.Name
                        + "（请在 Rules 中为该字段声明 PropertyRule）");
                }
            }

            foreach (string name in ruleMap.Keys)
            {
                bool matched = false;
                foreach (FieldInfo f in fields)
                {
                    if (f.Name == name)
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    throw new InvalidOperationException(
                        "分合规则指向了不存在的字段：" + name);
                }
            }
        }
    }
}
