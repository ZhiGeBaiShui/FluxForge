using System.Collections.Generic;
using FluxForge.Presentation.Flow.Particles;
using FluxForge.Presentation.Flow.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace FluxForge.Mod.Visual.Internal
{
    /// <summary>
    /// 一帧里一个流源的绘制参数（值类型：放进 List 不产生堆分配）。
    /// 只由 <see cref="FlowVisualLayer"/> 生产、<see cref="FlowAtlasLayer"/> 消费。
    /// </summary>
    internal readonly struct FlowDrawItem
    {
        /// <summary>
        /// 图块左上角的连续 X（逻辑中心坐标减半格）。
        /// 直接用 <c>PositionX * 16</c> 作 tile 左边缘，流源才是平滑移动而不是整格跳。
        /// </summary>
        public readonly double PositionX;

        /// <summary>图块左上角的连续 Y，向下增大。</summary>
        public readonly double PositionY;

        /// <summary>完整视觉状态：蒙版 key 与三层颜色都从这里取。</summary>
        public readonly FlowVisualState State;

        /// <summary>
        /// 拖尾层数（1–6）：由含量档决定（见 <see cref="FlowTierMap.TrailLayersFromContent"/>）。
        /// 尾巴长度必须跟体量走，否则轻/重流源拖一样长的尾，含量读数会被拉平。
        /// </summary>
        public readonly int TrailLayers;

        public FlowDrawItem(double positionX, double positionY,
            FlowVisualState state, int trailLayers)
        {
            PositionX = positionX;
            PositionY = positionY;
            State = state;
            TrailLayers = trailLayers;
        }
    }

    /// <summary>
    /// 一台机器的蒙版渲染层：一个挂在网格 RectTransform 下的 GameObject，内部是分通道的 Image 池。
    ///
    /// 为什么挂在网格下：面板关闭时网格失活 → 整层自动隐藏；裁切、缩放、拖动全部跟随原版，
    /// 不需要世界坐标换算，也不会出现「窗口关了流源还飘在屏幕上」。
    ///
    /// A 方案把颜色拆成三层（REFACTOR-A §3）：
    ///   · body   —— 本体蒙版 × <see cref="FlowTint.Body"/>；
    ///   · edge   —— 边缘蒙版 × <see cref="FlowTint.Edge"/>（温度只影响这一层）；
    ///   · accent —— 亮核/高光/霜/沫蒙版 × <c>FlowTint.Accent(composition)</c>（按成分提亮）；
    ///   · wall   —— 墙图整张白 tint（贴图自带 #C9C2B8），只在 blocked 显示。
    /// 四张蒙版来自同一份索引、同一组坐标，所以一次 Get 就能拿全。
    ///
    /// 本层管理本体 4 通道各 64、残影 384 的池；粒子池与白图由每层独立的
    /// FlowParticleRenderer 管理。本层保留粒子提交守卫及本体新建后的重排请求。
    /// 射程点已删除（Tier-0 无数据源）：`FlowVisualState.Range` 仍是预留字段，
    /// 在数据源就绪前整条射程点绘制链不再存在。
    /// </summary>
    internal sealed class FlowAtlasLayer
    {
        /// <summary>每个流源最多 6 层残影：亚格间距叠出连续渐隐的拖尾。</summary>
        private const int GhostsPerSource = 6;

        /// <summary>本体通道池的槽位数（body/edge/accent/wall 各一份）。</summary>
        private const int SourceCapacity = 64;

        /// <summary>残影池槽位数：每个本体槽位 <see cref="GhostsPerSource"/> 层。</summary>
        private const int GhostCapacity = SourceCapacity * GhostsPerSource;

        /// <summary>残影相对本体的后退像素（亚格：3px 一档，6 层共 18px ≈ 1.1 格）。</summary>
        private static readonly int[] GhostOffsetsPx = { 3, 6, 9, 12, 15, 18 };

        /// <summary>残影 alpha：越远越淡（与 final/png/trail-probe.png 的 A 方案一致，Σα≈1.43）。</summary>
        private static readonly float[] GhostAlpha = { 0.42f, 0.34f, 0.27f, 0.20f, 0.13f, 0.07f };

        /// <summary>叠放顺序检查的间隔（次 Submit）：面板重建才会插新节点，没必要每帧查。</summary>
        private const int SiblingChecksEveryNSubmits = 30;

        /// <summary>某个 Image 上一次真正写入的 sprite/color（相同则跳过赋值）。</summary>
        private struct QuadCache
        {
            public Sprite Sprite;
            public Color Color;

            /// <summary>
            /// 只在 sprite 变化时写 Image：sprite/color 赋值会触发 Canvas 重建，
            /// 位置每帧变没关系，材质不变就不该重建。sprite 为空时同时关掉 Image.enabled。
            /// </summary>
            public static void SetSprite(Image image, ref QuadCache cache, Sprite sprite)
            {
                if (ReferenceEquals(cache.Sprite, sprite))
                {
                    return;
                }

                image.sprite = sprite;
                image.enabled = sprite != null;
                cache.Sprite = sprite;
            }

            /// <summary>只在颜色变化时写 Image（UGUI 的颜色比较是逐通道浮点比较）。</summary>
            public static void SetColor(Image image, ref QuadCache cache, Color color)
            {
                if (cache.Color == color)
                {
                    return;
                }

                image.color = color;
                cache.Color = color;
            }
        }

        // 下标约束：body/edge/accent/wall 的下标 ∈ [0, SourceCapacity)；
        // 残影下标 = body 下标 * GhostsPerSource + 层号 ∈ [0, GhostCapacity)。
        // Submit 已按 SourceCapacity 截断提交数，所以池内下标不会越界。

        /// <summary>本体通道池：一个流源一个 Image。</summary>
        private readonly Image[] _bodies = new Image[SourceCapacity];

        /// <summary>边缘通道池（温度色只作用在这一层）。</summary>
        private readonly Image[] _edges = new Image[SourceCapacity];

        /// <summary>亮核通道池（高光/霜/沫）。</summary>
        private readonly Image[] _accents = new Image[SourceCapacity];

        /// <summary>墙通道池：只有 blocked 且墙图可用时才创建。</summary>
        private readonly Image[] _walls = new Image[SourceCapacity];

        /// <summary>残影池：一个流源 6 个 Image，下标 = 本体下标 * 6 + k。</summary>
        private readonly Image[] _ghosts = new Image[GhostCapacity];

        // 每槽位上次写入的 sprite/color：位置每帧都写，但 sprite/color 只在变化时写，
        // 避免每帧触发 Canvas 的材质/网格重建。
        private readonly QuadCache[] _bodyCache = new QuadCache[SourceCapacity];
        private readonly QuadCache[] _edgeCache = new QuadCache[SourceCapacity];
        private readonly QuadCache[] _accentCache = new QuadCache[SourceCapacity];
        private readonly QuadCache[] _wallCache = new QuadCache[SourceCapacity];
        private readonly QuadCache[] _ghostCache = new QuadCache[GhostCapacity];

        private readonly RectTransform _gridRect;
        private readonly RectTransform _root;

        /// <summary>借用本层根节点，独立管理粒子池与运行时白图。</summary>
        private readonly FlowParticleRenderer _particleRenderer;

        /// <summary>上一帧用到第几个流源（用于把多出来的池对象关掉）。</summary>
        private int _activeSources;

        /// <summary>
        /// 新建 body/edge/accent 后为 true：这些节点会追加在粒子池之后，
        /// 下一帧提交粒子时要把已有池对象重新提到最后（只在结构变化时做，不是每帧）。
        /// </summary>
        private bool _dotOrderDirty;

        /// <summary>距下一次叠放顺序检查还剩几次提交；层重建时随新实例归零重算。</summary>
        private int _siblingCheckCountdown = SiblingChecksEveryNSubmits;

        /// <summary>Destroy 之后不再提交（旧层可能比网格晚一帧才真正销毁）。</summary>
        private bool _destroyed;

        public FlowAtlasLayer(RectTransform gridRect)
        {
            _gridRect = gridRect;

            GameObject go = new GameObject("FlowAtlas");
            go.layer = gridRect.gameObject.layer;

            _root = go.AddComponent<RectTransform>();
            _root.SetParent(gridRect, false);

            // 与网格左上角重合：anchoredPosition 直接就是「相对网格左上角」的偏移，
            // 像素对齐公式（见 Submit）才能和旧粒子层完全一致。
            _root.anchorMin = new Vector2(0f, 1f);
            _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0f, 1f);
            _root.anchoredPosition = Vector2.zero;
            _root.sizeDelta = gridRect.rect.size;
            _root.localScale = Vector3.one;

            // 画在网格里所有物品之后，流源才不会被装置挡住
            _root.SetAsLastSibling();

            // 子 Canvas = 重建边界：本层每帧都有本体/粒子在动，若不隔离，
            // 每帧都会把背包网格所在的 Canvas 整体标脏（重建范围含全部物品）。
            // 只挂 Canvas，不挂 GraphicRaycaster——本层不接收点击。
            go.AddComponent<Canvas>();

            _particleRenderer = new FlowParticleRenderer(_root);
        }

        /// <summary>销毁之后不再绘制。供 <see cref="FlowVisualLayer"/> 判断旧层是否还能用。</summary>
        public bool IsAlive
        {
            get { return !_destroyed && _root != null; }
        }

        /// <summary>提交前的公共前置：守卫 + 同步根尺寸 + 取局部矩形与 ppu。返回 false 表示本层已销毁。</summary>
        private bool TryBeginSubmit(out Rect area, out float ppu)
        {
            area = default(Rect);
            ppu = 1f;

            if (!IsAlive)
            {
                return false;
            }

            SyncRootSize();

            area = _root.rect;
            ppu = _root.lossyScale.x;
            if (ppu <= 0f)
            {
                ppu = 1f;
            }

            return true;
        }

        /// <summary>
        /// 格坐标 → 本层局部锚点（左上角）：先把左上角吸到屏幕像素网格，再减回局部原点。
        ///
        /// 复用 <see cref="FlowPixelMath.Snap"/>（<c>round(value * scale) / scale</c>）：
        /// 这里的位置是连续 double（流源在格内平滑移动），
        /// 所以不能套 <see cref="FlowPixelMath.CellToLocal"/>（它只接受整数格 + 整数像素）。
        /// </summary>
        internal static Vector2 PositionFor(Rect area, float ppu, double cellX, double cellY)
        {
            float left = FlowPixelMath.Snap(
                area.xMin + (float)cellX * FlowPixelMath.CellPixels, ppu);
            float top = FlowPixelMath.Snap(
                area.yMax - (float)cellY * FlowPixelMath.CellPixels, ppu);
            return new Vector2(left - area.xMin, top - area.yMax);
        }

        /// <summary>
        /// 本体/残影共用的方块边长（屏幕像素）：<c>round(16 * ppu)</c>，最小 1。
        /// 缩放到 1px 以下时仍保留 1px——0 尺寸的 Image 不会再被任何路径点亮。
        /// </summary>
        private static float QuadSizePx(float ppu)
        {
            float sizePx = Mathf.Round(FlowPixelMath.CellPixels * ppu);
            if (sizePx < 1f)
            {
                sizePx = 1f;
            }

            return sizePx;
        }

        /// <summary>
        /// 提交一帧的全部流源。空列表也要提交——那是清屏信号。
        ///
        /// 像素对齐（沿用旧层的取整公式）：
        ///   leftPx = Round((area.xMin + cellX * 16) * ppu)
        ///   topPx  = Round((area.yMax - cellY * 16) * ppu)
        ///   sizePx = Round(16 * ppu)
        /// Image 的锚点/pivot 都是左上角，所以 anchoredPosition 就是「相对网格左上角的偏移」。
        /// 注意 area 是 _root.rect：根节点 pivot 在左上，area.yMax == 0，topPx 天然是负值（向下）。
        /// </summary>
        /// <param name="items">本帧要画的流源；调用方保证非空（可以是空列表）。</param>
        public void Submit(List<FlowDrawItem> items)
        {
            if (!TryBeginSubmit(out Rect area, out float ppu))
            {
                return;
            }

            // 面板重建可能新建了物品节点：确保本层仍在最上层。
            // 只有重建才会插进新节点，所以每 30 次提交查一次即可（层重建时构造器已 SetAsLastSibling）。
            if (--_siblingCheckCountdown <= 0)
            {
                _siblingCheckCountdown = SiblingChecksEveryNSubmits;

                Transform parent = _root.parent;
                if (parent != null && _root.GetSiblingIndex() != parent.childCount - 1)
                {
                    _root.SetAsLastSibling();
                }
            }

            float sizePx = QuadSizePx(ppu);
            Vector2 quadSize = new Vector2(sizePx / ppu, sizePx / ppu);

            int count = ClampCount(items.Count, _bodies.Length);

            for (int i = 0; i < count; i++)
            {
                FlowDrawItem item = items[i];
                FlowVisualState state = item.State;

                EnsureQuad(i);

                // 四张蒙版共用同一组图集坐标，一次查表全拿到
                FlowSprites sprites = FlowAtlasTexture.Get(state);

                Vector2 position = PositionFor(area, ppu, item.PositionX, item.PositionY);

                // 三层颜色：成分为唯一本体色来源，温度只进边缘，亮核恒白
                Color bodyTint = ToColor(FlowTint.Body(state.Composition));
                Color edgeTint = ToColor(FlowTint.Edge(state.Composition, state.Temp));
                Color accentTint = ToColor(FlowTint.Accent(state.Composition));

                ShowQuad(_bodies[i], ref _bodyCache[i], sprites.Body, bodyTint, position, quadSize);
                ShowQuad(_edges[i], ref _edgeCache[i], sprites.Edge, edgeTint, position, quadSize);
                ShowQuad(_accents[i], ref _accentCache[i], sprites.Accent, accentTint, position, quadSize);
                ShowWall(i, sprites.Wall, state.State == FlowState.Blocked, position, quadSize);

                // 拖尾用同一张 body 蒙版，只改 alpha：残影是"运动痕迹"，不是体积
                SubmitGhosts(i, in item, bodyTint, sprites.Body, area, ppu, quadSize);
            }

            // 流源数变少：把上一帧多出来的所有池对象关掉（不销毁，下一波复用）
            for (int i = count; i < _activeSources; i++)
            {
                Deactivate(_bodies[i]);
                Deactivate(_edges[i]);
                Deactivate(_accents[i]);
                Deactivate(_walls[i]);

                for (int gh = 0; gh < GhostsPerSource; gh++)
                {
                    Deactivate(_ghosts[i * GhostsPerSource + gh]);
                }
            }

            _activeSources = count;
        }

        /// <summary>
        /// 提交本帧的常驻粒子（由 <see cref="FlowVisualLayer"/> 的粒子场汇总）。
        /// 空列表也要提交——那是清场信号。
        /// 本层完成前置检查，绘制器正常返回后才消费重排请求。
        /// </summary>
        /// <param name="dots">本帧要画的粒子；调用方保证非空（可以是空列表）。</param>
        public void SubmitDots(List<FlowAmbientParticle> dots)
        {
            if (!TryBeginSubmit(out Rect area, out float ppu))
            {
                return;
            }

            _particleRenderer.Submit(dots, area, ppu, _dotOrderDirty);
            _dotOrderDirty = false;
        }

        /// <summary>销毁整层（面板重建 / 网格退役 / 读档）。可重复调用。</summary>
        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;

            // 先释放绘制器拥有的白图，再销毁本层拥有的根及其子 Image。
            _particleRenderer.DestroyResources();

            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root.gameObject);
            }
        }

        /// <summary>网格尺寸可能在面板重建后变化，跟着同步一次；尺寸没变时赋值也很便宜。</summary>
        private void SyncRootSize()
        {
            Vector2 gridSize = _gridRect.rect.size;
            if (_root.sizeDelta != gridSize)
            {
                _root.sizeDelta = gridSize;
            }
        }

        /// <summary>确保某个流源的 body/edge/accent 三个 Image 已创建（懒创建，顺序即叠放顺序）。</summary>
        private void EnsureQuad(int index)
        {
            if (_bodies[index] == null)
            {
                _bodies[index] = CreateImage(_root, "FlowAtlasBody");
                _dotOrderDirty = true;
            }

            if (_edges[index] == null)
            {
                _edges[index] = CreateImage(_root, "FlowAtlasEdge");
                _dotOrderDirty = true;
            }

            if (_accents[index] == null)
            {
                _accents[index] = CreateImage(_root, "FlowAtlasAccent");
                _dotOrderDirty = true;
            }
        }

        /// <summary>
        /// 摆一个蒙版 Image：位置/尺寸每帧写（流源在动），sprite/color 只在变化时写。
        /// sprite 为空时只是不显示（不销毁节点）；<paramref name="cache"/> 记住上次真正写入的值。
        /// </summary>
        private static void ShowQuad(Image image, ref QuadCache cache, Sprite sprite, Color color,
            Vector2 position, Vector2 size)
        {
            Activate(image);

            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;

            QuadCache.SetSprite(image, ref cache, sprite);
            QuadCache.SetColor(image, ref cache, color);
        }

        /// <summary>
        /// 墙：只有 blocked 且墙图可用时创建/显示。
        /// 墙图是**彩色**小图（固定 #C9C2B8），所以这里用白色 tint，不再乘成分色。
        /// 墙与本体在像素上互不重叠（角色 W 独占那些像素），所以叠放顺序不影响观感；
        /// 但为了不让墙盖住相邻流源的亮核，这里把它压到本体三层的下面。
        /// </summary>
        private void ShowWall(int index, Sprite sprite, bool blocked, Vector2 position, Vector2 size)
        {
            Image wall = _walls[index];
            bool show = blocked && sprite != null;

            if (!show)
            {
                Deactivate(wall);
                return;
            }

            if (wall == null)
            {
                wall = CreateImage(_root, "FlowAtlasWall");
                // 墙色恒白（贴图自带 #C9C2B8），缓存里不需要颜色比较
                wall.color = Color.white;
                // 后创建的节点会在上面：把墙挪到这一组的最底层（body 之前）
                wall.rectTransform.SetAsFirstSibling();
                _walls[index] = wall;
            }

            Activate(wall);

            wall.rectTransform.anchoredPosition = position;
            wall.rectTransform.sizeDelta = size;

            // 墙图自身带 #C9C2B8，颜色恒为白；sprite 只在状态/方向/含量变化时换
            QuadCache.SetSprite(wall, ref _wallCache[index], sprite);
        }

        /// <summary>点亮一个池对象（与 <see cref="Deactivate"/> 对称；null 安全）。</summary>
        internal static void Activate(Image image)
        {
            if (image != null && !image.gameObject.activeSelf)
            {
                image.gameObject.SetActive(true);
            }
        }

        /// <summary>隐掉一个池对象（null 安全；不销毁，留待复用）。</summary>
        internal static void Deactivate(Image image)
        {
            if (image != null && image.gameObject.activeSelf)
            {
                image.gameObject.SetActive(false);
            }
        }

        /// <summary>提交数量钳到池容量：超出部分按提交顺序截断，永不越界。</summary>
        internal static int ClampCount(int count, int capacity)
        {
            return count > capacity ? capacity : count;
        }

        /// <summary>
        /// 画拖尾：同一张 body 蒙版往运动反方向按 **亚格间距**（3px 一档）叠出连续渐隐的残影。
        ///
        /// 为什么用残影而不是把尾巴画长：16px 下任何收尖都会被读成"前端"，
        /// 所以方向线索必须锁在"钝头 + 亮核"上；流体感改由运动痕迹提供。
        /// 为什么是亚格间距而不是 1 格：1 格间距会读成"几个独立虚影"，3px 才连成一条。
        /// 层数由含量档决定（空 1 / 少 2 / 中 4 / 多 6）。
        /// 残影只属于 flow：blocked/split/merge 各自有静态签名，叠拖尾会混淆
        /// （collect 死亡贴图已删除，死亡改由粒子淡出表达，不再有 collect 静态签名）。
        /// 颜色 = 本体色带逐层下降的 alpha（不额外染色，否则成分会被拖尾"二次混色"）。
        /// </summary>
        private void SubmitGhosts(int bodyIndex, in FlowDrawItem item, Color bodyTint,
            Sprite bodySprite, Rect area, float ppu, Vector2 quadSize)
        {
            // 前置条件：TrailLayers 的值域是 1..6（FlowTierMap.TrailLayersFromContent），
            // 不会超过 GhostsPerSource，也不存在 0 层；这里不再夹取。
            int layers = item.TrailLayers;

            bool show = bodySprite != null && item.State.State == FlowState.Flow;

            int dx = FlowDirMath.StepX(item.State.Dir);
            int dy = FlowDirMath.StepY(item.State.Dir);

            for (int gh = 0; gh < GhostsPerSource; gh++)
            {
                int ghostIndex = bodyIndex * GhostsPerSource + gh;
                Image ghost = _ghosts[ghostIndex];

                if (!show || gh >= layers)
                {
                    Deactivate(ghost);
                    continue;
                }

                if (ghost == null)
                {
                    ghost = CreateImage(_root, "FlowAtlasGhost");
                    // 拖尾压到最底：后创建的本体自然叠在它上面
                    ghost.rectTransform.SetAsFirstSibling();
                    _ghosts[ghostIndex] = ghost;
                }

                Activate(ghost);

                // 残影同理：sprite/color 只在变化时写，位置每帧写
                QuadCache.SetSprite(ghost, ref _ghostCache[ghostIndex], bodySprite);

                // 残影位置 = 本体位置沿运动反方向退 offsetMap 像素（格坐标换算后交给统一的位置函数）
                float distance = GhostOffsetsPx[gh];
                double ghostX = item.PositionX - (dx * distance) / (double)FlowPixelMath.CellPixels;
                double ghostY = item.PositionY - (dy * distance) / (double)FlowPixelMath.CellPixels;

                ghost.rectTransform.anchoredPosition = PositionFor(area, ppu, ghostX, ghostY);
                // 尺寸与本体完全一致：直接复用 Submit 算好的 quadSize，不再每颗重算。
                ghost.rectTransform.sizeDelta = quadSize;

                Color ghostColor = new Color(bodyTint.r, bodyTint.g, bodyTint.b, GhostAlpha[gh]);
                QuadCache.SetColor(ghost, ref _ghostCache[ghostIndex], ghostColor);
            }
        }

        /// <summary>
        /// 纯逻辑层的 <see cref="FlowColor"/>（0–255 byte）→ UGUI 颜色。
        /// 蒙版图的 RGB 是亮度、A 是形状，所以这里一律给 255 的 alpha：
        /// 形状透明度完全由贴图携带，再乘一次会让整层变灰。
        /// </summary>
        private static Color ToColor(FlowColor color)
        {
            return new Color32(color.R, color.G, color.B, 255);
        }

        /// <summary>创建一个像素对齐的 Image（锚点/pivot 左上角，不接收点击，Simple 拉伸）。</summary>
        internal static Image CreateImage(RectTransform root, string name)
        {
            GameObject go = new GameObject(name);
            go.layer = root.gameObject.layer;

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.SetParent(root, false);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.localScale = Vector3.one;

            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = false;
            image.type = Image.Type.Simple;
            return image;
        }
    }
}
