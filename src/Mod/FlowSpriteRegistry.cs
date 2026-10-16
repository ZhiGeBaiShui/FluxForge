using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 贴图管理者（单例）：配置驱动（嵌入 assets/sprites.json），
    /// 按 key 从嵌入资源加载 PNG → Sprite，并缓存。
    ///
    /// 用法：
    ///   Sprite s = FlowSpriteRegistry.Instance.GetSprite("flow_machine");
    ///   if (FlowSpriteRegistry.Instance.TryMatch(atlasPath, name, out SpriteEntry e)) ...
    ///
    /// 新增贴图 = 放一个 png 进 assets/ + 在 sprites.json 加一条 + 重编译。
    /// </summary>
    public sealed class FlowSpriteRegistry
    {
        public static FlowSpriteRegistry Instance { get; } = new FlowSpriteRegistry();

        private readonly Dictionary<string, SpriteEntry> _entries = new Dictionary<string, SpriteEntry>();
        private readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private readonly string _resourcePrefix;

        private FlowSpriteRegistry()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            _resourcePrefix = EmbeddedResources.FindPrefix(assembly, "sprites.json");
            LoadConfig(assembly);
        }

        /// <summary>已配置的贴图 key 列表。</summary>
        public IReadOnlyCollection<string> Keys
        {
            get { return _entries.Keys; }
        }

        /// <summary>配置项数量。</summary>
        public int Count
        {
            get { return _entries.Count; }
        }

        /// <summary>按 key 取贴图（首次加载，之后走缓存）。</summary>
        public Sprite GetSprite(string key)
        {
            Sprite cached;
            if (_cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            SpriteEntry entry;
            if (!_entries.TryGetValue(key, out entry))
            {
                MelonLoader.MelonLogger.Warning("[贴图] 未配置的 key：" + key);
                return null;
            }

            Sprite sprite = LoadPng(entry);
            if (sprite != null)
            {
                _cache[key] = sprite;
            }

            return sprite;
        }

        /// <summary>按 (atlas, name) 匹配配置——供 LoadFromAtlas 拦截判断。</summary>
        public bool TryMatch(string atlasPath, string name, out SpriteEntry entry)
        {
            entry = null;

            SpriteEntry candidate;
            if (_entries.TryGetValue(name, out candidate) && candidate.atlas == atlasPath)
            {
                entry = candidate;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 按 atlas 名匹配配置（atlas 每物品唯一——sprite 名沿用原版物品的，无需替换）。
        /// </summary>
        public bool TryMatchAtlas(string atlasPath, out SpriteEntry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(atlasPath))
            {
                return false;
            }

            foreach (KeyValuePair<string, SpriteEntry> pair in _entries)
            {
                if (pair.Value.atlas == atlasPath)
                {
                    entry = pair.Value;
                    return true;
                }
            }

            return false;
        }

        /// <summary>取配置项（不加载贴图）。</summary>
        public SpriteEntry GetEntry(string key)
        {
            SpriteEntry entry;
            return _entries.TryGetValue(key, out entry) ? entry : null;
        }

        private void LoadConfig(Assembly assembly)
        {
            Stream stream = assembly.GetManifestResourceStream(_resourcePrefix + "sprites.json");
            if (stream == null)
            {
                MelonLoader.MelonLogger.Error("[贴图] 嵌入配置 sprites.json 未找到");
                return;
            }

            using (stream)
            using (StreamReader reader = new StreamReader(stream))
            {
                string json = reader.ReadToEnd();
                SpriteConfig config = JsonSerializer.Deserialize<SpriteConfig>(json);

                if (config == null || config.sprites == null)
                {
                    MelonLoader.MelonLogger.Error("[贴图] 配置解析失败");
                    return;
                }

                for (int i = 0; i < config.sprites.Length; i++)
                {
                    SpriteEntry entry = config.sprites[i];
                    if (entry == null || string.IsNullOrEmpty(entry.key))
                    {
                        continue;
                    }

                    if (!ValidateEntry(entry))
                    {
                        continue;   // 配置非法：跳过（警告已在校验里打）
                    }

                    if (_entries.ContainsKey(entry.key))
                    {
                        MelonLoader.MelonLogger.Warning("[贴图] 重复的 key：" + entry.key
                            + "（后一条覆盖前一条）");
                    }
                    _entries[entry.key] = entry;
                }
            }

            MelonLoader.MelonLogger.Msg("[贴图] 配置加载完成，共 " + _entries.Count + " 项");
        }

        /// <summary>
        /// 校验单条配置：key/atlas/file 非空、shape 只含 0/1、长度能被 shapeWidth 整除。
        /// 非法配置跳过加载并打警告。
        /// </summary>
        private static bool ValidateEntry(SpriteEntry entry)
        {
            if (string.IsNullOrEmpty(entry.atlas))
            {
                MelonLoader.MelonLogger.Warning("[贴图] " + entry.key + " 缺少 atlas");
                return false;
            }

            if (string.IsNullOrEmpty(entry.file))
            {
                MelonLoader.MelonLogger.Warning("[贴图] " + entry.key + " 缺少 file");
                return false;
            }

            if (string.IsNullOrEmpty(entry.shape))
            {
                MelonLoader.MelonLogger.Warning("[贴图] " + entry.key + " 缺少 shape");
                return false;
            }

            if (entry.shapeWidth <= 0)
            {
                MelonLoader.MelonLogger.Warning("[贴图] " + entry.key + " shapeWidth 必须大于 0");
                return false;
            }

            for (int i = 0; i < entry.shape.Length; i++)
            {
                char c = entry.shape[i];
                if (c != '0' && c != '1')
                {
                    MelonLoader.MelonLogger.Warning("[贴图] " + entry.key
                        + " shape 含非法字符 '" + c + "'（只允许 0/1）");
                    return false;
                }
            }

            if (entry.shape.Length % entry.shapeWidth != 0)
            {
                MelonLoader.MelonLogger.Warning("[贴图] " + entry.key + " shape 长度 "
                    + entry.shape.Length + " 不能被 shapeWidth " + entry.shapeWidth + " 整除");
                return false;
            }

            return true;
        }

        private Sprite LoadPng(SpriteEntry entry)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream stream = assembly.GetManifestResourceStream(_resourcePrefix + entry.file);
            if (stream == null)
            {
                MelonLoader.MelonLogger.Error("[贴图] 嵌入图片未找到：" + entry.file);
                return null;
            }

            byte[] png;
            using (stream)
            {
                png = new byte[stream.Length];
                stream.Read(png, 0, png.Length);
            }

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;    // 像素风：不模糊
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.hideFlags = HideFlags.DontSave;   // 防 Unity 资源管理卸载

            // Il2Cpp：Texture2D.LoadImage 不可直接调，经 ImageConversion.LoadImage
            MethodInfo loadImage = typeof(ImageConversion).GetMethod(
                "LoadImage",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Texture2D), typeof(Il2CppStructArray<byte>) },
                null);

            if (loadImage == null)
            {
                MelonLoader.MelonLogger.Error("[贴图] ImageConversion.LoadImage 未找到");
                return null;
            }

            loadImage.Invoke(null, new object[] { texture, (Il2CppStructArray<byte>)png });

            // 长宽比校验：贴图像素比 与 配置格子数比 必须一致
            ValidateAspect(entry, texture);

            // 网格对齐校验：贴图尺寸须为「每格像素」整数倍，且换算格数 = 配置形状格数
            ValidateGridAlignment(entry, texture);

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                entry.ppu > 0f ? entry.ppu : 100f);
            sprite.hideFlags = HideFlags.DontSave;

            MelonLoader.MelonLogger.Msg("[贴图] 已加载 " + entry.key
                + " → " + texture.width + "x" + texture.height);
            return sprite;
        }

        /// <summary>
        /// 校验贴图长宽比与形状包围盒是否一致（不一致仅警告，不阻断加载）。
        /// 例：贴图 16×32（1:2）↔ 形状 1列×2行（1:2）→ 通过。
        /// </summary>
        private static void ValidateAspect(SpriteEntry entry, Texture2D texture)
        {
            int shapeHeight = entry.ShapeHeight;
            if (entry.shapeWidth <= 0 || shapeHeight <= 0)
            {
                MelonLoader.MelonLogger.Warning("[贴图] " + entry.key
                    + " 形状未配置（shape/shapeWidth），跳过比例校验");
                return;
            }

            float textureRatio = (float)texture.width / texture.height;
            float shapeRatio = (float)entry.shapeWidth / shapeHeight;

            if (Math.Abs(textureRatio - shapeRatio) > 0.02f)
            {
                MelonLoader.MelonLogger.Warning("[贴图] " + entry.key + " 长宽比不匹配：贴图 "
                    + texture.width + "x" + texture.height + "（" + textureRatio.ToString("F3")
                    + "） vs 形状 " + entry.shapeWidth + "列x" + shapeHeight + "行（"
                    + shapeRatio.ToString("F3") + "）");
            }
            else
            {
                MelonLoader.MelonLogger.Msg("[贴图] " + entry.key + " 比例校验通过（贴图 "
                    + texture.width + "x" + texture.height + " ↔ 形状 "
                    + entry.shapeWidth + "列x" + shapeHeight + "行）");
            }
        }

        private sealed class SpriteConfig
        {
            public SpriteEntry[] sprites { get; set; }
        }

        /// <summary>原版美术的标准密度（px/格）——仅作提示，非硬性约束。</summary>
        public const int StandardCellPixels = 16;

        /// <summary>
        /// 网格对齐校验：
        /// ① 贴图宽高必须能被形状格数整除（每格像素为整数）；
        /// ② 每列像素 == 每行像素（格子为正方形，不拉伸变形）。
        /// 满足即可对齐（每格像素多少不限——16px 是原版标准密度，高清贴图会由游戏缩放渲染）。
        /// 不一致仅警告，不阻断加载。
        /// </summary>
        private static void ValidateGridAlignment(SpriteEntry entry, Texture2D texture)
        {
            int shapeHeight = entry.ShapeHeight;
            if (entry.shapeWidth <= 0 || shapeHeight <= 0)
            {
                return;   // 形状未配置（ValidateAspect 已提示）
            }

            if (texture.width % entry.shapeWidth != 0 || texture.height % shapeHeight != 0)
            {
                MelonLoader.MelonLogger.Warning("[贴图] " + entry.key + " 尺寸 "
                    + texture.width + "x" + texture.height + " 不能被形状 "
                    + entry.shapeWidth + "x" + shapeHeight + " 整除 → 每格像素非整数，与格子对不齐");
                return;
            }

            int cellPixelsX = texture.width / entry.shapeWidth;
            int cellPixelsY = texture.height / shapeHeight;

            if (cellPixelsX != cellPixelsY)
            {
                MelonLoader.MelonLogger.Warning("[贴图] " + entry.key + " 每格像素宽高不一致（"
                    + cellPixelsX + " x " + cellPixelsY + "）→ 贴图会被拉伸变形");
                return;
            }

            string density = cellPixelsX == StandardCellPixels
                ? "标准密度"
                : "高清（非标准 " + StandardCellPixels + "px/格，游戏会缩放渲染）";

            MelonLoader.MelonLogger.Msg("[贴图] " + entry.key + " 网格对齐校验通过（每格 "
                + cellPixelsX + "px，" + density + "）");
        }
    }

    /// <summary>
    /// 单张贴图的配置项（对应 sprites.json 的一条）。
    /// 形状表示法依照游戏原版（GridShapeBuilder.SetData）：
    /// 0/1 字符串按行拼接 + 每行宽度，高度 = 长度 ÷ 宽度。
    /// </summary>
    public sealed class SpriteEntry
    {
        /// <summary>逻辑名（物品/装置按它取图）。</summary>
        public string key { get; set; }

        /// <summary>自定义 atlas 名（与游戏原生 atlas 区分）。</summary>
        public string atlas { get; set; }

        /// <summary>嵌入资源文件名（assets/ 下）。</summary>
        public string file { get; set; }

        /// <summary>
        /// 形状数据：0/1 字符串，按行拼接（1=占格）。
        /// 例：L 型（2×2 缺右下）= "1110"，shapeWidth=2。
        /// </summary>
        public string shape { get; set; }

        /// <summary>每行宽度（列数）。</summary>
        public int shapeWidth { get; set; }

        /// <summary>pixelsPerUnit（Sprite.Create 用，默认 100）。</summary>
        public float ppu { get; set; }

        /// <summary>形状高度（行数）= 数据长度 ÷ 每行宽度。</summary>
        public int ShapeHeight
        {
            get
            {
                if (string.IsNullOrEmpty(shape) || shapeWidth <= 0)
                {
                    return 0;
                }
                return shape.Length / shapeWidth;
            }
        }
    }
}
