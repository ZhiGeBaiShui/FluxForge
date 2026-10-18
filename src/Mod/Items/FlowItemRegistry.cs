using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
            LoadConfig();
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

        private void LoadConfig()
        {
            byte[] json = EmbeddedResources.Read(_resourcePrefix, "items.json");
            if (json == null)
            {
                MelonLogger.Error("[物品] 配置读取失败（items.json 不存在）");
                return;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (Exception e)
            {
                MelonLogger.Error("[物品] items.json 数据有误：" + e.Message);
                return;
            }

            using (document)
            {
                JsonElement items;
                if (!document.RootElement.TryGetProperty("items", out items)
                    || items.ValueKind != JsonValueKind.Array)
                {
                    MelonLogger.Error("[物品] items.json 数据有误：缺少 items 数组");
                    return;
                }

                int index = 0;
                foreach (JsonElement element in items.EnumerateArray())
                {
                    LoadItem(element, index);
                    index++;
                }
            }

            MelonLogger.Msg("[物品] 定义加载完成，共 " + _definitions.Count + " 项");
        }

        /// <summary>逐项解析：某一条数据非法只跳过它，其余物品照常加载。</summary>
        private void LoadItem(JsonElement element, int index)
        {
            string key = ReadKey(element);
            string label = string.IsNullOrEmpty(key) ? "第 " + (index + 1) + " 项" : "「" + key + "」";

            FlowItemDefinition def;
            try
            {
                def = element.Deserialize<FlowItemDefinition>();
            }
            catch (Exception e)
            {
                // 数据非法（如 valueInt 写了浮点 / 类型不匹配），异常消息自带出错的路径
                MelonLogger.Error("[物品] " + label + "的 json 数据有误：" + e.Message);
                return;
            }

            if (def == null || string.IsNullOrEmpty(def.key))
            {
                MelonLogger.Error("[物品] " + label + "的 json 数据有误：key 为空");
                return;
            }

            _definitions[def.key] = def;
        }

        /// <summary>尽量读出 key 用于报错——即使反序列化失败，也能指明是哪个物品。</summary>
        private static string ReadKey(JsonElement element)
        {
            JsonElement key;
            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("key", out key)
                && key.ValueKind == JsonValueKind.String)
            {
                return key.GetString();
            }

            return null;
        }
    }

    /// <summary>单个自定义物品的定义（对应 items.json 的一条）。</summary>
    public sealed class FlowItemDefinition
    {
        /// <summary>物品 identifier（= 贴图 key 约定；存档按它找回）。</summary>
        public string key { get; set; }

        /// <summary>装配起点："template"（默认，复制模板）或 "empty"（从零装配）。</summary>
        [JsonPropertyName("base")]
        public string baseType { get; set; }

        /// <summary>原版模板物品 id（功能/标签/属性来源）。base=empty 时忽略。</summary>
        public string template { get; set; }

        /// <summary>贴图 key（空则回退用 key 本身）。</summary>
        public string sprite { get; set; }

        /// <summary>显示名（空则沿用原版模板的名称）。</summary>
        public string name { get; set; }

        /// <summary>正文说明（写入 shortDescription；空则沿用模板）。支持 \n 换行与富文本标签。</summary>
        public string desc { get; set; }

        /// <summary>风味文本（写入 flavorText，灰绿斜体；空则沿用模板）。</summary>
        public string flavor { get; set; }

        /// <summary>物品类型（MODULE / STORAGE / TOOL / MACHINE / FOOD / WEAPON / MEDICAL / ARMOR …）。</summary>
        public string type { get; set; }

        /// <summary>
        /// 物品类型列表（优先于 <see cref="type"/>）。每个类型在 tooltip 里显示为一段 "[类型]"。
        /// 空数组 = 不显示任何类目。可用值：MODULE / STORAGE / TOOL / FOOD / WEAPON /
        /// MEDICAL / MATERIAL / DOCUMENT / HOUSEHOLD_GOOD / SUBSTANCE / AMMUNITION …
        /// </summary>
        public string[] itemTypes { get; set; }

        /// <summary>基础价值（0 = 沿用模板）。</summary>
        public long value { get; set; }

        /// <summary>
        /// 标签声明列表（结构直接镜像游戏的 TagState）：
        /// 每项用 valueName 指定 tag 名，再用 valueInt / valueFloat / valueDouble /
        /// valueBool / valueString 指定要写入哪个槽位。只写 valueName = 仅启用不设值。
        /// </summary>
        public TagConfigEntry[] tags { get; set; }

        /// <summary>
        /// tooltip 状态行（声明式）：每项从指定 tag 读值，按 scale/format 渲染成一行。
        /// 由 <see cref="StatusLineTooltipHelper"/> 通用渲染，新增装置不用写代码。
        /// </summary>
        public StatusLine[] statusLines { get; set; }

        /// <summary>是否从零装配（base=empty）。</summary>
        public bool IsEmptyBase
        {
            get { return string.Equals(baseType, "empty", StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>贴图查找键（sprite 为空时回退 key）。</summary>
        public string SpriteKey
        {
            get { return string.IsNullOrEmpty(sprite) ? key : sprite; }
        }
    }

    /// <summary>
    /// 一条 tag 声明，字段名与游戏的 <c>TagState</c> 槽位一一对应。
    ///
    /// 写入哪个槽位由**字段名**决定，与 JSON 数字的字面形态无关——
    /// 所以不会再出现「写成 -5 却声明 float，结果读到 0」这类错配。
    /// 各槽位用可空类型，以区分「没写」和「写了 0」。
    /// </summary>
    public sealed class TagConfigEntry
    {
        /// <summary>tag 名（对应 TagState 的 identifier）。</summary>
        public string valueName { get; set; }

        /// <summary>整数槽位（TagState.valueInt，偏移 +0x30）。</summary>
        public int? valueInt { get; set; }

        /// <summary>单精度槽位（TagState.valueFloat，偏移 +0x34）。</summary>
        public float? valueFloat { get; set; }

        /// <summary>双精度槽位（TagState.valueDouble，偏移 +0x40）。</summary>
        public double? valueDouble { get; set; }

        /// <summary>布尔槽位（TagState.valueBool，偏移 +0x48）。</summary>
        public bool? valueBool { get; set; }

        /// <summary>字符串槽位（TagState.valueString，偏移 +0x28）。</summary>
        public string valueString { get; set; }
    }
}
