using System;
using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 装置 key → 装置类型 → 状态适配器。
    ///
    /// 启动时扫描本程序集里的全部 <see cref="DeviceStateAdapter{TDevice}"/> 子类，
    /// 读取 <see cref="DeviceAdapterAttribute"/> 建立：
    ///   key → 装置类型
    ///   装置类型 → 适配器类型
    ///
    /// 新增装置只需要写装置类、适配器类和 items.json；Logic 不再维护 key 目录。
    /// 没有 key、key 重复、装置类型与泛型不一致都会在启动扫描时直接抛错。
    /// </summary>
    public static class DeviceAdapterCatalog
    {
        private static readonly Catalog _catalog;

        static DeviceAdapterCatalog()
        {
            _catalog = Build();
        }

        /// <summary>已登记的 device key。</summary>
        public static IReadOnlyCollection<string> Keys
        {
            get { return _catalog.DeviceTypes.Keys; }
        }

        /// <summary>按 key 创建一台新的 Logic 装置。</summary>
        public static object CreateDevice(string key)
        {
            Type deviceType;
            if (!_catalog.DeviceTypes.TryGetValue(key, out deviceType))
            {
                throw new ArgumentException(
                    "没有登记该装置 key：" + key,
                    nameof(key));
            }

            return Activator.CreateInstance(deviceType);
        }

        /// <summary>按装置实例创建对应的状态适配器。</summary>
        public static DeviceStateAdapter CreateAdapter(object device)
        {
            Type adapterType = ResolveAdapterType(device.GetType());

            return (DeviceStateAdapter)Activator.CreateInstance(
                adapterType,
                System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic,
                null,
                new object[] { device },
                null);
        }

        private static Catalog Build()
        {
            Catalog catalog = new Catalog();
            Type[] types = typeof(DeviceAdapterCatalog).Assembly.GetTypes();

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                Type deviceType;

                if (!TryGetAdapterDeviceType(type, out deviceType))
                {
                    continue;
                }

                AddKeyMappings(catalog, type, deviceType);
            }

            return catalog;
        }

        /// <summary>
        /// 找出具体适配器声明的泛型装置基类；普通类型返回 false。
        /// </summary>
        private static bool TryGetAdapterDeviceType(Type type, out Type deviceType)
        {
            for (Type baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
            {
                if (baseType.IsGenericType
                    && baseType.GetGenericTypeDefinition() == typeof(DeviceStateAdapter<>))
                {
                    deviceType = baseType.GetGenericArguments()[0];
                    return true;
                }
            }

            deviceType = null;
            return false;
        }

        /// <summary>把一个适配器上的全部 key 写入目录，并校验装置类型。</summary>
        private static void AddKeyMappings(Catalog catalog, Type adapterType, Type genericDeviceType)
        {
            object[] attributes = adapterType.GetCustomAttributes(
                typeof(DeviceAdapterAttribute),
                false);

            if (attributes.Length == 0)
            {
                throw new InvalidOperationException(
                    "状态适配器没有登记 DeviceAdapterAttribute：" + adapterType.FullName);
            }

            for (int i = 0; i < attributes.Length; i++)
            {
                DeviceAdapterAttribute attribute = (DeviceAdapterAttribute)attributes[i];
                Type deviceType = attribute.DeviceType;

                if (!genericDeviceType.IsAssignableFrom(deviceType) || deviceType.IsAbstract)
                {
                    throw new InvalidOperationException(
                        "DeviceAdapter 的装置类型与适配器泛型不一致：" + adapterType.FullName
                        + " / " + deviceType.FullName);
                }

                catalog.DeviceTypes.Add(attribute.Key, deviceType);
                catalog.AdapterTypes.Add(deviceType, adapterType);
            }
        }

        /// <summary>按具体装置类型精确查适配器。</summary>
        private static Type ResolveAdapterType(Type deviceType)
        {
            Type adapterType;
            if (_catalog.AdapterTypes.TryGetValue(deviceType, out adapterType))
            {
                return adapterType;
            }

            throw new ArgumentException(
                "没有为该装置登记状态适配器：" + deviceType.FullName,
                nameof(deviceType));
        }

        private sealed class Catalog
        {
            public readonly Dictionary<string, Type> DeviceTypes
                = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

            public readonly Dictionary<Type, Type> AdapterTypes
                = new Dictionary<Type, Type>();
        }
    }
}
