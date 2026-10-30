using System;
using System.Collections.Generic;
using System.Reflection;
using ProbablyBagMod.Presentation.Flow.Visual;
using UnityEngine;

namespace ProbablyBagMod.Mod.Visual.Internal
{
    /// <summary>
    /// 一个视觉状态对应的**四张蒙版 Sprite**（REFACTOR-A §2/§4）。
    ///
    /// A 方案把颜色从图集里拆出来：图集只存「亮度蒙版 + 形状 alpha」，
    /// 实际颜色由 <see cref="FlowTint"/> 在运行时算好、再由 Image.color 相乘上去。
    /// 所以同一个状态的四个通道必须**一起**返回，渲染层不需要再分别查表。
    ///
    /// 任何一张缺失都是 null（图集没加载 / 该状态没有 tile / 墙图没随包发布），
    /// 渲染层据此逐层 setActive，不会抛异常。
    /// </summary>
    internal readonly struct FlowSprites
    {
        /// <summary>本体蒙版（角色 B/R/u/S/~）。</summary>
        public readonly Sprite Body;

        /// <summary>边缘/缝蒙版（角色 E/M/L/r/P/s/S2）。</summary>
        public readonly Sprite Edge;

        /// <summary>亮核/高光/霜/沫蒙版（角色 C/G/I/F）。</summary>
        public readonly Sprite Accent;

        /// <summary>墙图（角色 W，彩色小图，自带 #C9C2B8）。missing 时为 null。</summary>
        public readonly Sprite Wall;

        public FlowSprites(Sprite body, Sprite edge, Sprite accent, Sprite wall)
        {
            Body = body;
            Edge = edge;
            Accent = accent;
            Wall = wall;
        }
    }

    /// <summary>
    /// 运行时**蒙版图集**的贴图与 Sprite 工厂（REFACTOR-A §2）。
    ///
    /// 与旧版的三点差别：
    ///   ① 一次加载 4 张同尺寸蒙版（body/edge/accent/wall）+ **共用一份** <c>flow_mask.json</c>；
    ///   ② 一个 key 返回 <see cref="FlowSprites"/>（4 个 Sprite），而不是 1 个；
    ///   ③ 图集里不再有成分/温度维度，key = <c>content|state|dir[|fN]</c>，
    ///      所以「状态 → 坐标」的记忆表只需要 4 个枚举 + 帧号就能压成 long。
    ///
    /// 这里是全 mod **唯一**共享的可变状态：贴图与 Sprite 缓存。
    /// 它没有网格、没有实体、没有每帧数据——那些都在各自的 <see cref="FlowVisualLayer"/> 里。
    ///
    /// 失败策略：整份加载失败只记一条 Warning 并把内部就绪标志置 false，
    /// 之后所有 Get 返回空（渲染层整层空转）；墙图单独缺失只让 <see cref="FlowSprites.Wall"/>
    /// 为 null，不影响本体/边缘/亮核。
    /// </summary>
    internal static class FlowAtlasTexture
    {
        /// <summary>tile 的像素边长（与 <see cref="FlowPixelMath.CellPixels"/> 一致）。</summary>
        private const int TilePixels = 16;

        /// <summary>Sprite 的 pixelsPerUnit：16 px 的 tile 对应 UGUI 的 1 个单位。</summary>
        private const float PixelsPerUnit = 16f;

        // 蒙版编号：只用于 Sprite 缓存 key 的高位，顺序固定（改这里等于换缓存命名空间）
        private const int TexBody = 0;
        private const int TexEdge = 1;
        private const int TexAccent = 2;
        private const int TexWall = 3;

        /// <summary>蒙版编号 + 图集坐标 → Sprite。key = texId &lt;&lt; 48 | x &lt;&lt; 24 | y。</summary>
        private static readonly Dictionary<long, Sprite> _sprites = new Dictionary<long, Sprite>();

        /// <summary>
        /// 视觉状态 → 四张 Sprite 的记忆表（空 = 该状态没有任何可用 tile）。
        ///
        /// 为什么在 Sprite 缓存之外再加一层：<see cref="FlowVisualState.ToKey"/> 每次都会拼字符串，
        /// 而 Get 是每帧每条流源都会走的热路径。把状态压成 long 记住结果后，
        /// 稳态下不再产生 key 字符串；坐标缓存仍然负责「多个状态共用同一张 tile」。
        /// </summary>
        private static readonly Dictionary<long, FlowSprites> _resolved
            = new Dictionary<long, FlowSprites>();

        private static bool _initialized;
        private static bool _ready;
        private static Texture2D _bodyTexture;
        private static Texture2D _edgeTexture;
        private static Texture2D _accentTexture;
        private static Texture2D _wallTexture;
        private static FlowAtlasIndex _index;

        /// <summary>
        /// 卸载时释放全部缓存：Sprite、四张贴图、状态记忆表一起销毁并清空，
        /// 同时重置懒加载标记。未加载时调用也安全（全是空表 / null）。
        ///
        /// 贴图与 Sprite 都是 <see cref="HideFlags.DontSave"/> 的运行时对象，
        /// 进程退出不会替我们回收；不主动销毁就会在每次热重载/退出时留下泄漏。
        /// </summary>
        public static void Shutdown()
        {
            foreach (Sprite sprite in _sprites.Values)
            {
                if (sprite != null)
                {
                    UnityEngine.Object.Destroy(sprite);
                }
            }

            _sprites.Clear();
            _resolved.Clear();

            DestroyTexture(_bodyTexture);
            DestroyTexture(_edgeTexture);
            DestroyTexture(_accentTexture);
            DestroyTexture(_wallTexture);

            _bodyTexture = null;
            _edgeTexture = null;
            _accentTexture = null;
            _wallTexture = null;
            _index = null;
            _initialized = false;
            _ready = false;
        }

        /// <summary>
        /// 取一个视觉状态对应的四张蒙版 Sprite；图集不可用 / 该状态彻底没有 tile 时返回全空。
        /// 用 in 传引用，避免每次查询复制整个结构体。
        /// </summary>
        public static FlowSprites Get(in FlowVisualState state)
        {
            EnsureLoaded();
            if (!_ready)
            {
                return default(FlowSprites);
            }

            long stateKey = PackState(in state);
            FlowSprites cached;
            if (_resolved.TryGetValue(stateKey, out cached))
            {
                return cached;
            }

            int x;
            int y;

            // 三级降级统一走 FlowAtlasIndex.TryGetWithFallback（与 7 个 Fallback_* 单测
            // 是同一份实现）：精确 key → 同含量/方向的 flow 静态 tile → large 保留原 state。
            // 这里不再自己写一套「强制退到 flow」的 fallback，否则生产路径会画出
            // 单测明确禁止的结果（blocked 被伪装成 flow）。
            if (!_index.TryGetWithFallback(state, out x, out y))
            {
                // 负缓存：同一种状态不再重复拼 key 查表
                _resolved[stateKey] = default(FlowSprites);
                return default(FlowSprites);
            }

            // 墙图与三层蒙版共用同一套 key/坐标；只有 blocked 才有墙，
            // 其它状态不建墙 Sprite（图集对应坐标本来就是空的）。
            // 贴图缺失时 SpriteAt 自己会返回 null，这里不再重复判空。
            Sprite wall = state.State == FlowState.Blocked
                ? SpriteAt(TexWall, _wallTexture, x, y)
                : null;

            FlowSprites sprites = new FlowSprites(
                SpriteAt(TexBody, _bodyTexture, x, y),
                SpriteAt(TexEdge, _edgeTexture, x, y),
                SpriteAt(TexAccent, _accentTexture, x, y),
                wall);

            _resolved[stateKey] = sprites;
            return sprites;
        }

        /// <summary>把视觉状态压成一个 long，作为 <see cref="_resolved"/> 的 key。</summary>
        private static long PackState(in FlowVisualState state)
        {
            // <see cref="FlowVisualState"/> 构造时已把帧钳到 -1..11，编码成 0..12 只占 4 bit
            int frameCode = state.Frame + 1;

            return (long)(int)state.Content
                | ((long)(int)state.State << 2)
                | ((long)(int)state.Dir << 5)
                | ((long)frameCode << 7);
        }

        /// <summary>取（或懒创建）某张蒙版上某个 tile 的 Sprite；贴图为 null 时返回 null。</summary>
        private static Sprite SpriteAt(int textureId, Texture2D texture, int x, int y)
        {
            if (texture == null)
            {
                return null;
            }

            long key = ((long)textureId << 48) | ((long)x << 24) | (uint)y;

            Sprite cached;
            if (_sprites.TryGetValue(key, out cached))
            {
                return cached;
            }

            // PIL 的索引 y 从左上往下，Unity 的 Rect 从左下往上：必须翻。
            // 不翻的话整张图上下颠倒，每格都会取到另一行。
            Rect rect = new Rect(x, texture.height - y - TilePixels, TilePixels, TilePixels);

            Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), PixelsPerUnit);
            sprite.hideFlags = HideFlags.DontSave;
            _sprites[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// 首次访问时加载。只执行一次：失败也把 <see cref="_initialized"/> 置 true，
        /// 之后每帧的 Get 直接走 _ready == false 的短路，不会反复读资源、反复刷日志。
        /// </summary>
        private static void EnsureLoaded()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();

                // 四张图共用一份索引：先探前缀再各读一次，前缀只需探一次
                string prefix = EmbeddedResources.FindPrefix(assembly, "flow_mask.json");
                string json = EmbeddedResources.ReadText(prefix, "flow_mask.json");

                if (json == null)
                {
                    PBModEntry.Log.Warning("[流源] 蒙版图集索引缺失，图集层停用");
                    return;
                }

                Texture2D body = EmbeddedResources.LoadTexture(prefix, "flow_mask_body.png");
                Texture2D edge = EmbeddedResources.LoadTexture(prefix, "flow_mask_edge.png");
                Texture2D accent = EmbeddedResources.LoadTexture(prefix, "flow_mask_accent.png");

                if (body == null || edge == null || accent == null)
                {
                    PBModEntry.Log.Warning("[流源] 蒙版图集缺少 body/edge/accent，图集层停用");
                    return;
                }

                // 墙图是可选通道：缺失只让 Wall 为 null，blocked 状态仍能画出本体
                Texture2D wall = EmbeddedResources.LoadTexture(prefix, "flow_mask_wall.png");
                if (wall == null)
                {
                    PBModEntry.Log.Warning("[流源] 缺少 flow_mask_wall.png，墙体将不显示");
                }

                FlowAtlasIndex index = FlowAtlasIndex.Parse(json);

                _bodyTexture = body;
                _edgeTexture = edge;
                _accentTexture = accent;
                _wallTexture = wall;
                _index = index;
                _ready = true;
                PBModEntry.Log.Log("[流源] 蒙版图集就绪：" + index.Count + " tiles × 4 通道");
            }
            catch (Exception e)
            {
                // 图集坏了不能把异常带进每帧的渲染循环，只降级为空转
                _ready = false;
                _bodyTexture = null;
                _edgeTexture = null;
                _accentTexture = null;
                _wallTexture = null;
                _index = null;
                PBModEntry.Log.Warning("[流源] 蒙版图集加载失败，图集层停用：" + e.Message);
            }
        }

        /// <summary>销毁一张缓存贴图；null 安全（加载失败时字段就是 null）。</summary>
        private static void DestroyTexture(Texture2D texture)
        {
            if (texture != null)
            {
                UnityEngine.Object.Destroy(texture);
            }
        }
    }
}
