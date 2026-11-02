using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace FluxForge.Presentation.Flow.Visual
{
    /// <summary>
    /// 运行时图集的索引（<c>assets/flow_mask.json</c>）：
    /// <code>{"tiles": {"large|flow|right": [x, y], "large|flow|right|f7": [x, y], ...}}</code>
    ///
    /// 这一层只做「key → 图集像素坐标」的纯查表，**不依赖 Unity**：
    /// 贴图加载、Sprite 创建、UV 换算都在 UGUI 层。
    ///
    /// 容错策略见 <see cref="TryGetWithFallback"/>：图集生成器少一张 tile 时
    /// 宁可画一张近似的，也不要让格子空着或抛异常。
    /// </summary>
    public sealed class FlowAtlasIndex
    {
        private readonly Dictionary<string, AtlasTile> _tiles;

        private FlowAtlasIndex(Dictionary<string, AtlasTile> tiles)
        {
            _tiles = tiles;
        }

        /// <summary>索引里的 tile 数量（重复 key 只算一个）。</summary>
        public int Count
        {
            get { return _tiles.Count; }
        }

        /// <summary>
        /// 解析图集索引 JSON。失败一律抛 <see cref="InvalidDataException"/>，
        /// 消息里带输入前 80 个字符，方便直接看出是哪个资源坏了。
        /// </summary>
        public static FlowAtlasIndex Parse(string json)
        {
            if (json == null)
            {
                throw new InvalidDataException("图集索引为 null。上下文：(null)");
            }

            // Unity 的 TextAsset / 记事本可能带 BOM；JsonDocument 不认它，先剥掉。
            string text = json.TrimStart('\uFEFF');

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(text);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "图集索引不是合法 JSON。上下文：" + Describe(text) + "；原因：" + exception.Message,
                    exception);
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException(
                        "图集索引根节点必须是对象。上下文：" + Describe(text));
                }

                JsonElement tilesElement;
                if (!root.TryGetProperty("tiles", out tilesElement) ||
                    tilesElement.ValueKind != JsonValueKind.Object)
                {
                    throw new InvalidDataException(
                        "图集索引缺少 tiles 对象。上下文：" + Describe(text));
                }

                Dictionary<string, AtlasTile> tiles =
                    new Dictionary<string, AtlasTile>(StringComparer.Ordinal);

                foreach (JsonProperty property in tilesElement.EnumerateObject())
                {
                    string key = property.Name;
                    JsonElement coordinate = property.Value;

                    if (coordinate.ValueKind != JsonValueKind.Array || coordinate.GetArrayLength() != 2)
                    {
                        throw new InvalidDataException(
                            "tile 坐标必须是 [x,y] 两个整数：" + key + "。上下文：" + Describe(text));
                    }

                    int x;
                    int y;
                    if (!coordinate[0].TryGetInt32(out x) || !coordinate[1].TryGetInt32(out y))
                    {
                        throw new InvalidDataException(
                            "tile 坐标必须是整数：" + key + "。上下文：" + Describe(text));
                    }

                    // 重复 key 以最后一条为准：生成器理论上不会产出重复，这里只保证不崩。
                    tiles[key] = new AtlasTile(x, y);
                }

                return new FlowAtlasIndex(tiles);
            }
        }

        /// <summary>按完整 key 查 tile 左上角像素坐标；未命中返回 false 且坐标置 0。</summary>
        public bool TryGet(string key, out int x, out int y)
        {
            AtlasTile tile;
            if (key != null && _tiles.TryGetValue(key, out tile))
            {
                x = tile.X;
                y = tile.Y;
                return true;
            }

            x = 0;
            y = 0;
            return false;
        }

        /// <summary>
        /// 逐级降级查 tile，三级顺序固定（REFACTOR-A §4）：
        /// <list type="number">
        /// <item>精确 key（flow 时含 <c>|f{n}</c>）；</item>
        /// <item>状态退到 flow 并去掉动效帧（静态 flow tile）；</item>
        /// <item>再把含量退到 large，**保留原 state/dir**（不是退到 flow）。</item>
        /// </list>
        /// 三级都未命中才返回 false。第 2 级同时承担「帧号越界/缺帧」的兜底；
        /// 第 3 级也去掉帧号，因为静态图集里没有 <c>large|...|fN</c> 组合。
        ///
        /// 成分与温度已经不在 key 里，所以 fallback 不再有「退到 water」这一级：
        /// 颜色由调用方用 <see cref="FlowVisualState.Composition"/> 运行时染，与选哪张蒙版无关。
        /// </summary>
        public bool TryGetWithFallback(FlowVisualState state, out int x, out int y)
        {
            if (TryGet(state.ToKey(), out x, out y))
            {
                return true;
            }

            FlowVisualState flowStatic = new FlowVisualState(
                state.Composition,
                state.Temp,
                state.Content,
                FlowState.Flow,
                state.Dir,
                FlowVisualState.NoFrame,
                state.Range);
            if (TryGet(flowStatic.ToKey(), out x, out y))
            {
                return true;
            }

            FlowVisualState large = new FlowVisualState(
                state.Composition,
                state.Temp,
                FlowContent.Large,
                state.State,
                state.Dir,
                FlowVisualState.NoFrame,
                state.Range);
            if (TryGet(large.ToKey(), out x, out y))
            {
                return true;
            }

            x = 0;
            y = 0;
            return false;
        }

        /// <summary>取输入前 80 个字符作为错误上下文；超长时标注总长度。</summary>
        private static string Describe(string text)
        {
            const int MaxContext = 80;
            string value = text ?? string.Empty;
            if (value.Length <= MaxContext)
            {
                return value;
            }

            return value.Substring(0, MaxContext) + "...（共 " + value.Length + " 字符）";
        }

        /// <summary>一个 tile 在图集里的左上角像素坐标。16×16、无 padding（IMPL-PLAN-A §1）。</summary>
        private readonly struct AtlasTile
        {
            public readonly int X;
            public readonly int Y;

            public AtlasTile(int x, int y)
            {
                X = x;
                Y = y;
            }
        }
    }
}
