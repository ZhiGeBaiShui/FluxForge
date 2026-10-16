using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using MelonLoader;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 物品定义管理者（单例）：配置驱动（嵌入 assets/items.json），
    /// 描述每个自定义物品（identifier / 原版模板 / 贴图 key / 显示名）。
    ///
    /// 新增物品 = items.json 加一条 + 配好贴图（sprites.json）+ 重编译——零 C# 改动。
    /// </summary>
    public sealed class FlowItemRegistry
    {
        public static FlowItemRegistry Instance { get; } = new FlowItemRegistry();

        private readonly Dictionary<string, FlowItemDefinition> _definitions
            = new Dictionary<string, FlowItemDefinition>();
        private readonly string _resourcePrefix;

        private FlowItemRegistry()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            _resourcePrefix = EmbeddedResources.FindPrefix(assembly, "items.json");
            LoadConfig(assembly);
        }

        /// <summary>已配置的物品 key 列表。</summary>
        public IReadOnlyCollection<string> Keys
        {
            get { return _definitions.Keys; }
        }

        /// <summary>配置项数量。</summary>
        public int Count
        {
            get { return _definitions.Count; }
        }

        /// <summary>按 key 取物品定义。</summary>
        public FlowItemDefinition Get(string key)
        {
            FlowItemDefinition def;
            return _definitions.TryGetValue(key, out def) ? def : null;
        }

        private void LoadConfig(Assembly assembly)
        {
            Stream stream = assembly.GetManifestResourceStream(_resourcePrefix + "items.json");
            if (stream == null)
            {
                MelonLogger.Error("[物品] 嵌入配置 items.json 未找到");
                return;
            }

            using (stream)
            using (StreamReader reader = new StreamReader(stream))
            {
                ItemConfig config = JsonSerializer.Deserialize<ItemConfig>(reader.ReadToEnd());
                if (config == null || config.items == null)
                {
                    return;
                }

                for (int i = 0; i < config.items.Length; i++)
                {
                    FlowItemDefinition def = config.items[i];
                    if (def != null && !string.IsNullOrEmpty(def.key))
                    {
                        _definitions[def.key] = def;
                    }
                }
            }

            MelonLogger.Msg("[物品] 定义加载完成，共 " + _definitions.Count + " 项");
        }

        private sealed class ItemConfig
        {
            public FlowItemDefinition[] items { get; set; }
        }
    }

    /// <summary>单个自定义物品的定义（对应 items.json 的一条）。</summary>
    public sealed class FlowItemDefinition
    {
        /// <summary>物品 identifier（= 贴图 key 约定）。</summary>
        public string key { get; set; }

        /// <summary>原版模板物品 id（功能/标签/属性来源）。</summary>
        public string template { get; set; }

        /// <summary>贴图 key（空则回退用 key 本身）。</summary>
        public string sprite { get; set; }

        /// <summary>显示名（空则沿用原版模板的名称）。</summary>
        public string name { get; set; }

        /// <summary>贴图查找键（sprite 为空时回退 key）。</summary>
        public string SpriteKey
        {
            get { return string.IsNullOrEmpty(sprite) ? key : sprite; }
        }
    }
}
