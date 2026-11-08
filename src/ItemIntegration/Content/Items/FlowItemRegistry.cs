using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace FluxForge.Mod
{
    /// <summary>
    /// 物品定义管理者（单例）：配置驱动（嵌入 assets/items.json），
    /// 描述每个自定义物品（identifier / 贴图 key / 显示名 / 类型 / 标签 / 装配件）。
    ///
    /// 新增物品 = items.json 加一条 + 放一张对应 png（文件名见 sprites.json 的 file）+ 重编译——零 C# 改动。
    /// </summary>
    public sealed class FlowItemRegistry
    {
        public static FlowItemRegistry Instance { get; } = new FlowItemRegistry();

        private readonly Dictionary<string, FlowItemDefinition> _definitions
            = new Dictionary<string, FlowItemDefinition>();
        private readonly Dictionary<string, string> _itemTypes
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _resourcePrefix;

        private FlowItemRegistry()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            _resourcePrefix = EmbeddedResources.FindPrefix(assembly, "items.json");
            LoadConfig();
        }

        /// <summary>物品类型 key → tooltip 里显示的名字（来自 items.json 的 itemTypes 对象）。</summary>
        public IReadOnlyDictionary<string, string> ItemTypes
        {
            get { return _itemTypes; }
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
                FFModEntry.Log.Error("[物品] 配置读取失败（items.json 不存在）");
                return;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (Exception e)
            {
                FFModEntry.Log.Error("[物品] items.json 数据有误：" + e.Message);
                return;
            }

            using (document)
            {
                JsonElement itemTypes;
                if (document.RootElement.TryGetProperty("itemTypes", out itemTypes)
                    && itemTypes.ValueKind == JsonValueKind.Object)
                {
                    foreach (JsonProperty pair in itemTypes.EnumerateObject())
                    {
                        _itemTypes[pair.Name] = pair.Value.GetString();
                    }
                }

                JsonElement items;
                if (!document.RootElement.TryGetProperty("items", out items)
                    || items.ValueKind != JsonValueKind.Array)
                {
                    FFModEntry.Log.Error("[物品] items.json 数据有误：缺少 items 数组");
                    return;
                }

                int index = 0;
                foreach (JsonElement element in items.EnumerateArray())
                {
                    LoadItem(element, index);
                    index++;
                }
            }

            FFModEntry.Log.Log("[物品] 定义加载完成，共 " + _definitions.Count + " 项");
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
                FFModEntry.Log.Error("[物品] " + label + "的 json 数据有误：" + e.Message);
                return;
            }

            if (def == null || string.IsNullOrEmpty(def.key))
            {
                FFModEntry.Log.Error("[物品] " + label + "的 json 数据有误：key 为空");
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

        /// <summary>模组物品贴图 key（查 sprites.json 的 key；空则回退用物品 key 本身）。</summary>
        public string sprite { get; set; }

        /// <summary>显示名（空则留空）。</summary>
        // TODO(l10n): name / desc / flavor / statusLines.label 目前直接存中文；
        // 支持多语言前应改为 key + 分语言资源，不要把这些显示文本当技术 tag。
        public string name { get; set; }

        /// <summary>正文说明（写入 shortDescription；空则留空）。支持 \n 换行与富文本标签。</summary>
        public string desc { get; set; }

        /// <summary>风味文本（写入 flavorText，灰绿斜体；空则留空）。</summary>
        public string flavor { get; set; }

        /// <summary>
        /// 激活音效标识；由负责激活的 attachment 绑定到 GameItem.soundActivate。
        /// 空则不绑定。
        /// </summary>
        public string activateSound { get; set; }

        /// <summary>物品类型（MODULE / STORAGE / TOOL / MACHINE / FOOD / WEAPON / MEDICAL / ARMOR …）。</summary>
        public string type { get; set; }

        /// <summary>
        /// 物品类型列表（优先于 <see cref="type"/>）。每个类型在 tooltip 里显示为一段 "[类型]"。
        /// 空数组 = 不显示任何类目。可用值：MODULE / STORAGE / TOOL / FOOD / WEAPON /
        /// MEDICAL / MATERIAL / DOCUMENT / HOUSEHOLD_GOOD / SUBSTANCE / AMMUNITION …
        /// </summary>
        public string[] itemTypes { get; set; }

        /// <summary>基础价值（0 = 无价）。</summary>
        public long value { get; set; }

        /// <summary>
        /// （预留）面板网格尺寸，如 "4x3" 表示 4 列 3 行。
        /// 当前没有读取方；留作带网格物品的配置位。
        /// </summary>
        public string grid { get; set; }

        /// <summary>
        /// （预留）该网格接受哪个物品类型（类型名即 tag）。
        /// 当前没有读取方，白名单尚未接线。
        /// </summary>
        public string acceptsType { get; set; }

        /// <summary>
        /// 液体容器容量（毫升）。由 flow_container / water_bottle 装配件传给
        /// <c>WaterHelper.InitLiquidContainerItem</c>；
        /// 不声明 = 0，即不给这件物品容器身份。
        /// </summary>
        public int capacityMicrolitres { get; set; }

        /// <summary>
        /// 行为装配件名列表（对应 <see cref="ItemAttachment.Name"/>），按声明顺序执行。
        /// 必须是数组；缺失或空数组 = 纯数据物品（合法，且不输出任何日志）。
        /// 名字未识别记一条 Msg；运行期不检查互斥，同一字段被写两次时后者生效。
        /// </summary>
        public string[] attachments { get; set; }

        /// <summary>
        /// Mod 装置目录 key（<c>DeviceAdapterCatalog</c>）；空 = 纯数据物品。
        ///
        /// 只给 "device" 附件用：它把 key 本身启用为 tag；物品进入容器 / 读档补绑时，
        /// <see cref="ItemDeviceBinding"/> 再按同名 tag 从目录构造并登记装置。
        /// </summary>
        public string device { get; set; }

        /// <summary>
        /// 装置中心格的 X 局部坐标（基础形状坐标，0 起）。
        /// 由 device 附件写入 tag；声明 device 附件时必填，缺失会让该物品装配失败。
        /// </summary>
        public int? anchorX { get; set; }

        /// <summary>装置中心格的 Y 局部坐标；与 <see cref="anchorX"/> 成对使用。</summary>
        public int? anchorY { get; set; }

        /// <summary>
        /// 标签声明列表（结构直接镜像游戏的 TagState）：
        /// 每项用 valueName 指定 tag 名，再用 valueInt / valueDouble /
        /// valueBool / valueString 指定要写入哪个槽位。只写 valueName = 仅启用不设值。
        /// </summary>
        public TagConfigEntry[] tags { get; set; }

        /// <summary>
        /// tooltip 状态行（声明式）：每项从指定 tag 读值，按 scale/format 渲染成一行。
        /// 由 <see cref="StatusLineTooltipHelper"/> 通用渲染，新增装置不用写代码。
        /// </summary>
        public StatusLine[] statusLines { get; set; }

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
    /// 所以不会再出现「写成 -5 却声明 int，结果读到 0」这类错配。
    /// 各槽位用可空类型，以区分「没写」和「写了 0」。
    /// </summary>
    public sealed class TagConfigEntry
    {
        /// <summary>tag 名（对应 TagState 的 identifier）。</summary>
        public string valueName { get; set; }

        /// <summary>整数槽位（TagState.valueInt，偏移 +0x30）。</summary>
        public int? valueInt { get; set; }

        /// <summary>双精度槽位（TagState.valueDouble，偏移 +0x40）。</summary>
        public double? valueDouble { get; set; }

        /// <summary>布尔槽位（TagState.valueBool，偏移 +0x48）。</summary>
        public bool? valueBool { get; set; }

        /// <summary>字符串槽位（TagState.valueString，偏移 +0x28）。</summary>
        public string valueString { get; set; }
    }
}
