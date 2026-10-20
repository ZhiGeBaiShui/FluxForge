using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace ProbablyBagMod.Mod.Rendering.ItemSprites
{
    /// <summary>
    /// 贴图管理者（单例）：读取嵌入的 assets/sprites.json，按 key / atlas 查询贴图（首次加载后缓存）。
    /// 新增贴图 = 放 png 进 assets/ + sprites.json 加一条 + 重编译。
    /// </summary>
    public sealed class FlowSpriteRegistry
    {
        /// <summary>原版美术的每格像素数——仅作密度换算基准，不是引擎约束。</summary>
        private const int StandardCellPixels = 16;

        /// <summary>原版 ppu。两者相除得「每格的世界尺寸」= 0.16。</summary>
        private const float StandardPixelsPerUnit = 100f;

        // Il2Cpp 下 Texture2D.LoadImage 不可直接调，经 ImageConversion.LoadImage
        private static readonly MethodInfo LoadImageMethod = typeof(ImageConversion).GetMethod(
            "LoadImage", BindingFlags.Public | BindingFlags.Static, null,
            new[] { typeof(Texture2D), typeof(Il2CppStructArray<byte>) }, null);

        public static FlowSpriteRegistry Instance { get; } = new FlowSpriteRegistry();

        private readonly Dictionary<string, SpriteEntry> _entries = new Dictionary<string, SpriteEntry>();
        private readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();
        private readonly string _resourcePrefix;

        private FlowSpriteRegistry()
        {
            _resourcePrefix = EmbeddedResources.FindPrefix(Assembly.GetExecutingAssembly(), "sprites.json");
            LoadConfig();
        }

        // ---- 对外查询 ----

        public SpriteEntry GetEntry(string key)
        {
            SpriteEntry entry;
            return _entries.TryGetValue(key, out entry) ? entry : null;
        }

        public Sprite GetSprite(string key)
        {
            Sprite cached;
            if (_cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            SpriteEntry entry = GetEntry(key);
            if (entry == null)
            {
                MelonLogger.Warning("[贴图] 未配置的 key：" + key);
                return null;
            }

            Sprite sprite = LoadPng(entry);
            if (sprite != null)
            {
                _cache[key] = sprite;
            }

            return sprite;
        }

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

        // ---- 配置读取 ----

        private void LoadConfig()
        {
            byte[] json = EmbeddedResources.Read(_resourcePrefix, "sprites.json");
            SpriteConfig config = null;

            // 本单例可能是在 LoadFromAtlas 的 Prefix 里首次构造的（游戏 UI 原生调用链），
            // 抛异常会直接杀进程，所以配置解析失败只降级为「没有自定义贴图」
            try
            {
                if (json != null)
                {
                    config = JsonSerializer.Deserialize<SpriteConfig>(Encoding.UTF8.GetString(json));
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error("[贴图] sprites.json 解析失败：" + e.Message);
                return;
            }

            if (config == null || config.sprites == null)
            {
                MelonLogger.Error("[贴图] 配置读取失败（sprites.json）");
                return;
            }

            for (int i = 0; i < config.sprites.Length; i++)
            {
                SpriteEntry entry = config.sprites[i];
                if (IsUsable(entry))
                {
                    _entries[entry.key] = entry;
                }
            }

            MelonLogger.Msg("[贴图] 配置加载完成，共 " + _entries.Count + " 项");
        }

        /// <summary>字段齐全 + shape 全为 0/1 + 长度能被 shapeWidth 整除。</summary>
        private static bool IsUsable(SpriteEntry entry)
        {
            bool ok = entry != null
                && !string.IsNullOrEmpty(entry.key)
                && !string.IsNullOrEmpty(entry.atlas)
                && !string.IsNullOrEmpty(entry.file)
                && !string.IsNullOrEmpty(entry.shape)
                && entry.shapeWidth > 0
                && entry.shape.Length % entry.shapeWidth == 0
                && entry.shape.Trim('0', '1').Length == 0;

            if (!ok)
            {
                MelonLogger.Warning("[贴图] 配置项无效，已跳过："
                    + (entry == null ? "<null>" : entry.key));
            }

            return ok;
        }

        // ---- 贴图加载 ----

        private Sprite LoadPng(SpriteEntry entry)
        {
            byte[] png = EmbeddedResources.Read(_resourcePrefix, entry.file);
            if (png == null || LoadImageMethod == null)
            {
                MelonLogger.Error("[贴图] 图片读取失败：" + entry.file);
                return null;
            }

            // 这段可能跑在 RenderHandler.LoadFromAtlas 的 Prefix 里（原生 UI 调用链），
            // 异常不拦就会把进程带走，所以整段兜住、失败即返回 null 走原生贴图
            try
            {
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point;    // 像素风：不模糊
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.hideFlags = HideFlags.DontSave;   // 防 Unity 资源管理卸载
                LoadImageMethod.Invoke(null, new object[] { texture, (Il2CppStructArray<byte>)png });

                // ppu = 贴图宽 ÷ (列数 × 每格世界尺寸)：640 / (2 × 0.16) = 2000
                // 这样任意像素宽度的贴图都会铺满「列数」个格子，格数由 shape 决定而非贴图尺寸
                float ppu = texture.width / (entry.shapeWidth * (StandardCellPixels / StandardPixelsPerUnit));
                Sprite sprite = Sprite.Create(
                    texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), ppu);
                sprite.hideFlags = HideFlags.DontSave;

                MelonLogger.Msg("[贴图] 已加载 " + entry.key + " → "
                    + texture.width + "x" + texture.height + " ppu=" + ppu);
                return sprite;
            }
            catch (Exception e)
            {
                MelonLogger.Error("[贴图] 解码失败 " + entry.file + "：" + e);
                return null;
            }
        }

        private sealed class SpriteConfig
        {
            public SpriteEntry[] sprites { get; set; }
        }
    }
}
