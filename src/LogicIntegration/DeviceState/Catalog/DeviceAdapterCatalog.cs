using System;
using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 装置 key → <see cref="DeviceDefinition"/>（key + 单例适配器）。
    ///
    /// 启动时扫描本程序集里的 DeviceStateAdapter&lt;&gt; 子类，读取 key-only
    /// [DeviceAdapter] 并实例化一个适配器单例。不检查实例字段、不反射成员，
    /// 因此不需要 dynamic-code / reflection-external capability。
    /// </summary>
    public static class DeviceAdapterCatalog
    {
        private static readonly Catalog _catalog = Build();

        /// <summary>已登记的 device key，按 ordinal 排序。</summary>
        public static IReadOnlyCollection<string> Keys
        {
            get { return _catalog.Keys; }
        }

        /// <summary>按 key 取装置定义；未登记返回 false。</summary>
        public static bool TryGet(string key, out DeviceDefinition definition)
        {
            return _catalog.ByKey.TryGetValue(key, out definition);
        }

        /// <summary>按 key 取装置定义；未登记抛 <see cref="ArgumentException"/>。</summary>
        public static DeviceDefinition Get(string key)
        {
            DeviceDefinition definition;
            if (!_catalog.ByKey.TryGetValue(key, out definition))
            {
                throw new ArgumentException(
                    "没有登记该装置 key：" + key,
                    nameof(key));
            }

            return definition;
        }

        /// <summary>
        /// 返回物品携带的全部装置定义（按 key ordinal 排序）。
        /// 顺序稳定，Host 的 Router / Reactor 也按这个顺序取第一个。
        /// </summary>
        public static List<DeviceDefinition> FindFor(GameItem item)
        {
            DeviceDefinition[] ordered = _catalog.Ordered;
            List<DeviceDefinition> definitions = new List<DeviceDefinition>();

            for (int i = 0; i < ordered.Length; i++)
            {
                if (item.IsTag(ordered[i].Key))
                {
                    definitions.Add(ordered[i]);
                }
            }

            return definitions;
        }

        private static Catalog Build()
        {
            List<DeviceDefinition> definitions = new List<DeviceDefinition>();
            Dictionary<string, DeviceDefinition> byKey
                = new Dictionary<string, DeviceDefinition>(StringComparer.Ordinal);

            Type[] types = typeof(DeviceAdapterCatalog).Assembly.GetTypes();

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];

                if (type.IsAbstract || type.ContainsGenericParameters)
                {
                    continue;
                }

                if (!IsAdapterType(type))
                {
                    continue;
                }

                AddAdapter(definitions, byKey, type);
            }

            definitions.Sort(CompareByKey);

            string[] keys = new string[definitions.Count];
            for (int i = 0; i < definitions.Count; i++)
            {
                keys[i] = definitions[i].Key;
            }

            return new Catalog(byKey, definitions.ToArray(), keys);
        }

        /// <summary>把一个具体适配器上的全部 key 登记进目录。</summary>
        private static void AddAdapter(
            List<DeviceDefinition> definitions,
            Dictionary<string, DeviceDefinition> byKey,
            Type adapterType)
        {
            object[] attributes = adapterType.GetCustomAttributes(
                typeof(DeviceAdapterAttribute),
                false);

            if (attributes.Length == 0)
            {
                throw new InvalidOperationException(
                    "状态适配器没有登记 DeviceAdapterAttribute：" + adapterType.FullName);
            }

            DeviceStateAdapter adapter = (DeviceStateAdapter)Activator.CreateInstance(adapterType);

            for (int i = 0; i < attributes.Length; i++)
            {
                DeviceAdapterAttribute attribute = (DeviceAdapterAttribute)attributes[i];

                if (byKey.ContainsKey(attribute.Key))
                {
                    throw new InvalidOperationException(
                        "装置 key 重复登记：" + attribute.Key + "（" + adapterType.FullName + "）");
                }

                DeviceDefinition definition = new DeviceDefinition(
                    attribute.Key,
                    adapter);

                byKey.Add(attribute.Key, definition);
                definitions.Add(definition);
            }
        }

        /// <summary>判断一个具体类型是否继承自 DeviceStateAdapter&lt;&gt;。</summary>
        private static bool IsAdapterType(Type type)
        {
            for (Type baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
            {
                if (baseType.IsGenericType
                    && baseType.GetGenericTypeDefinition() == typeof(DeviceStateAdapter<>))
                {
                    return true;
                }
            }

            return false;
        }

        private static int CompareByKey(DeviceDefinition left, DeviceDefinition right)
        {
            return string.CompareOrdinal(left.Key, right.Key);
        }

        private sealed class Catalog
        {
            public readonly Dictionary<string, DeviceDefinition> ByKey;

            public readonly DeviceDefinition[] Ordered;

            public readonly string[] Keys;

            public Catalog(
                Dictionary<string, DeviceDefinition> byKey,
                DeviceDefinition[] ordered,
                string[] keys)
            {
                ByKey = byKey;
                Ordered = ordered;
                Keys = keys;
            }
        }
    }
}
