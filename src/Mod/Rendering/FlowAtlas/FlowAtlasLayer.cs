using System.Collections.Generic;
using ProbablyBagMod.Presentation.Flow.Particles;
using ProbablyBagMod.Presentation.Flow.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace ProbablyBagMod.Mod.Rendering.FlowAtlas
{
    /// <summary>
    /// 一帧里一个流源的绘制参数（值类型：放进 List 不产生堆分配）。
    /// 只由 <see cref="FlowAtlasView"/> 生产、<see cref="FlowAtlasLayer"/> 消费。
    /// </summary>
    public readonly struct FlowDrawItem
    {
        /// <summary>
        /// 连续位置 X（格坐标：整数 = 格中心，小数 = 格内/跨格）。
        /// 直接用 <c>PositionX * 16</c> 作 tile 左边缘，流源才是平滑移动而不是整格跳。
        /// </summary>
        public readonly double PositionX;

        /// <summary>连续位置 Y（格坐标：整数 = 格中心，向下增大）。</summary>
        public readonly double PositionY;

        /// <summary>完整视觉状态：蒙版 key 与三层颜色都从这里取。</summary>
        public readonly FlowVisualState State;

        /// <summary>
        /// 拖尾层数（0–6）：由含量档决定（见 <see cref="FlowTierMap.TrailLayersFromContent"/>）。
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
    /// 池化：本体 4 通道各 64、残影 384、常驻粒子 512；懒创建、SetActive 复用，
    /// 永不 Instantiate/Destroy。Submit / SubmitDots 只读传入的 List，稳态下 0 托管分配。
    /// 粒子用 1×1 白 sprite + 每颗自己的 RGB/寿命 alpha，池对象永远排在 body/edge/accent 之上。
    /// 射程点已删除（Tier-0 无数据源）：`FlowVisualState.Range` 仍是预留字段，
    /// 在数据源就绪前整条射程点绘制链不再存在。
    /// </summary>
    public sealed class FlowAtlasLayer
    {
        /// <summary>每个流源最多 6 层残影：亚格间距叠出连续渐隐的拖尾。</summary>
        private const int GhostsPerSource = 6;

        /// <summary>残影相对本体的后退像素（亚格：3px 一档，6 层共 18px ≈ 1.1 格）。</summary>
        private static readonly int[] GhostOffsetsPx = { 3, 6, 9, 12, 15, 18 };

        /// <summary>残影 alpha：越远越淡（与 final/png/trail-probe.png 的 A 方案一致，Σα≈1.43）。</summary>
        private static readonly float[] GhostAlpha = { 0.42f, 0.34f, 0.27f, 0.20f, 0.13f, 0.07f };

        /// <summary>叠放顺序检查的间隔（帧）：面板重建才会插新节点，没必要每帧查。</summary>
        private const int SiblingCheckInterval = 30;

        /// <summary>某个 Image 上一次真正写入的 sprite/color（相同则跳过赋值）。</summary>
        private struct QuadCache
        {
            public Sprite Sprite;
            public Color Color;
        }

        /// <summary>本体通道池：一个流源一个 Image。</summary>
        private readonly Image[] _bodies = new Image[64];

        /// <summary>边缘通道池（温度色只作用在这一层）。</summary>
        private readonly Image[] _edges = new Image[64];

        /// <summary>亮核通道池（高光/霜/沫）。</summary>
        private readonly Image[] _accents = new Image[64];

        /// <summary>墙通道池：只有 blocked 且墙图可用时才创建。</summary>
        private readonly Image[] _walls = new Image[64];

        /// <summary>残影池：一个流源 6 个 Image，下标 = 本体下标 * 6 + k。</summary>
        private readonly Image[] _ghosts = new Image[384];

        /// <summary>
        /// 常驻粒子池：每颗一个 1×1 白 Image，位置/颜色每帧写。
        /// 512 上限：每源最多 32 颗，够十几条流源同时存在；超出部分按提交顺序截断。
        /// </summary>
        private readonly Image[] _dots = new Image[512];

        // 每槽位上次写入的 sprite/color：位置每帧都写，但 sprite/color 只在变化时写，
        // 避免每帧触发 Canvas 的材质/网格重建。
        private readonly QuadCache[] _bodyCache = new QuadCache[64];
        private readonly QuadCache[] _edgeCache = new QuadCache[64];
        private readonly QuadCache[] _accentCache = new QuadCache[64];
        private readonly QuadCache[] _wallCache = new QuadCache[64];
        private readonly QuadCache[] _ghostCache = new QuadCache[384];

        private readonly RectTransform _gridRect;
        private readonly RectTransform _root;

        /// <summary>上一帧用到第几个本体（用于把多出来的池对象关掉）。</summary>
        private int _activeBodies;

        /// <summary>上一帧用到第几颗粒子（用于把多出来的池对象关掉）。</summary>
        private int _activeDots;

        /// <summary>1×1 白 sprite，实例字段懒创建；不 static，层销毁后随实例一起失去引用。</summary>
        private Sprite _dotSprite;

        /// <summary>
        /// 新建 body/edge/accent 后为 true：这些节点会追加在粒子池之后，
        /// 下一帧提交粒子时要把已有池对象重新提到最后（只在结构变化时做，不是每帧）。
        /// </summary>
        private bool _dotOrderDirty;

        /// <summary>距下一次叠放顺序检查还剩几帧；层重建时随新实例归零重算。</summary>
        private int _siblingCheckCountdown = SiblingCheckInterval;

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
        }

        /// <summary>销毁之后不再绘制。供 <see cref="FlowAtlasView"/> 判断旧层是否还能用。</summary>
        public bool IsAlive
        {
            get { return !_destroyed && _root != null; }
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
        public void Submit(List<FlowDrawItem> items)
        {
            if (_destroyed || _root == null)
            {
                return;
            }

            // 网格尺寸可能在面板重建后变化，跟着同步一次；尺寸没变时赋值也很便宜
            SyncRootSize();

            // 面板重建可能新建了物品节点：确保本层仍在最上层。
            // 只有重建才会插进新节点，所以每 30 帧查一次即可（层重建时构造器已 SetAsLastSibling）。
            if (--_siblingCheckCountdown <= 0)
            {
                _siblingCheckCountdown = SiblingCheckInterval;

                Transform parent = _root.parent;
                if (parent != null && _root.GetSiblingIndex() != parent.childCount - 1)
                {
                    _root.SetAsLastSibling();
                }
            }

            Rect area = _root.rect;
            float ppu = _root.lossyScale.x;
            if (ppu <= 0f)
            {
                ppu = 1f;
            }

            float sizePx = Mathf.Round(FlowPixelMath.CellPixels * ppu);
            if (sizePx < 1f)
            {
                // 缩放到 1px 以下时仍保留 1px：0 尺寸的 Image 不会再被任何路径点亮
                sizePx = 1f;
            }

            Vector2 quadSize = new Vector2(sizePx / ppu, sizePx / ppu);

            int count = items == null ? 0 : items.Count;
            if (count > _bodies.Length)
            {
                count = _bodies.Length;
            }

            for (int i = 0; i < count; i++)
            {
                FlowDrawItem item = items[i];
                FlowVisualState state = item.State;

                EnsureQuad(i);

                // 四张蒙版共用同一组图集坐标，一次查表全拿到
                FlowSprites sprites = FlowAtlasTexture.Get(state);

                float leftPx = Mathf.Round(
                    (area.xMin + (float)item.PositionX * FlowPixelMath.CellPixels) * ppu);
                float topPx = Mathf.Round(
                    (area.yMax - (float)item.PositionY * FlowPixelMath.CellPixels) * ppu);
                Vector2 position = new Vector2(leftPx / ppu - area.xMin, topPx / ppu - area.yMax);

                // 三层颜色：成分为唯一本体色来源，温度只进边缘，亮核恒白
                Color bodyTint = ToColor(FlowTint.Body(state.Composition));
                Color edgeTint = ToColor(FlowTint.Edge(state.Composition, state.Temp));
                Color accentTint = ToColor(FlowTint.Accent(state.Composition));

                ShowQuad(_bodies[i], ref _bodyCache[i], sprites.Body, bodyTint, position, quadSize);
                ShowQuad(_edges[i], ref _edgeCache[i], sprites.Edge, edgeTint, position, quadSize);
                ShowQuad(_accents[i], ref _accentCache[i], sprites.Accent, accentTint, position, quadSize);
                ShowWall(i, sprites.Wall, state.State == FlowState.Blocked, position, quadSize);

                // 拖尾用同一张 body 蒙版，只改 alpha：残影是"运动痕迹"，不是体积
                SubmitGhosts(i, in item, bodyTint, sprites.Body, area, ppu);
            }

            // 流源数变少：把上一帧多出来的所有池对象关掉（不销毁，下一波复用）
            for (int i = count; i < _activeBodies; i++)
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

            _activeBodies = count;
        }

        /// <summary>
        /// 提交本帧的常驻粒子（由 <see cref="FlowAtlasView"/> 的粒子场汇总）。
        /// 空列表也要提交——那是清场信号。
        ///
        /// 位置与本体同口径：leftPx = Round((area.xMin + X * 16) * ppu)，
        /// topPx = Round((area.yMax - Y * 16) * ppu)；尺寸 = SizeCells 设计像素（最小 1）。
        /// alpha = Life / MaxLife × AlphaStart，RGB 直接用粒子自带的 8 位通道。
        /// 粒子排在 body/edge/accent 之后，散开的粒子不会被本体盖住。
        /// </summary>
        public void SubmitDots(List<FlowAmbientParticle> dots)
        {
            if (_destroyed || _root == null)
            {
                return;
            }

            SyncRootSize();

            Rect area = _root.rect;
            float ppu = _root.lossyScale.x;
            if (ppu <= 0f)
            {
                ppu = 1f;
            }

            float sizePx = Mathf.Round(
                FlowParticleTuning.SizeCells * FlowPixelMath.CellPixels * ppu);
            if (sizePx < 1f)
            {
                // 缩放到 1px 以下时仍保留 1px：0 尺寸的 Image 不会再被任何路径点亮
                sizePx = 1f;
            }

            Vector2 dotSize = new Vector2(sizePx / ppu, sizePx / ppu);

            int count = dots == null ? 0 : dots.Count;
            if (count > _dots.Length)
            {
                count = _dots.Length;
            }

            for (int i = 0; i < count; i++)
            {
                Image dot = _dots[i];
                if (dot == null)
                {
                    dot = CreateImage("FlowAtlasDot");
                    dot.sprite = DotSprite;
                    dot.color = Color.white;
                    _dots[i] = dot;
                }

                if (!dot.gameObject.activeSelf)
                {
                    dot.gameObject.SetActive(true);
                }

                FlowAmbientParticle particle = dots[i];

                float leftPx = Mathf.Round(
                    (area.xMin + particle.X * FlowPixelMath.CellPixels) * ppu);
                float topPx = Mathf.Round(
                    (area.yMax - particle.Y * FlowPixelMath.CellPixels) * ppu);
                dot.rectTransform.anchoredPosition = new Vector2(
                    leftPx / ppu - area.xMin,
                    topPx / ppu - area.yMax);
                dot.rectTransform.sizeDelta = dotSize;

                float lifeRatio = particle.MaxLife > 0f
                    ? particle.Life / particle.MaxLife
                    : 0f;
                float alpha = Mathf.Clamp01(lifeRatio * FlowParticleTuning.AlphaStart);
                dot.color = new Color32(
                    particle.R,
                    particle.G,
                    particle.B,
                    (byte)Mathf.RoundToInt(alpha * 255f));
            }

            // 粒子数变少：把上一帧多出来的池对象关掉（不销毁，下一波复用）
            for (int i = count; i < _activeDots; i++)
            {
                Deactivate(_dots[i]);
            }

            _activeDots = count;

            // 新建的本体节点会追加在粒子池之后：结构变化时把池对象重新提到最后。
            // 只在有新建节点的那一帧做，稳态完全不碰层级。
            if (_dotOrderDirty)
            {
                PromoteDots();
                _dotOrderDirty = false;
            }
        }

        /// <summary>销毁整层（面板重建 / 网格退役 / 读档）。可重复调用。</summary>
        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            _destroyed = true;

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

        /// <summary>
        /// 1×1 白 sprite（懒创建，实例字段）。粒子用白图 + Image.color 染色：
        /// 颜色/透明度每帧不同，烘焙贴图没有意义，还会按颜色数量膨胀资源。
        /// </summary>
        private Sprite DotSprite
        {
            get
            {
                if (_dotSprite == null)
                {
                    Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    texture.SetPixel(0, 0, Color.white);
                    texture.Apply();
                    texture.hideFlags = HideFlags.DontSave;

                    _dotSprite = Sprite.Create(
                        texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                    _dotSprite.hideFlags = HideFlags.DontSave;
                }

                return _dotSprite;
            }
        }

        /// <summary>把现有粒子池对象重新排到所有本体/残影/墙之后（只在结构变化时调用）。</summary>
        private void PromoteDots()
        {
            for (int i = 0; i < _dots.Length; i++)
            {
                Image dot = _dots[i];
                if (dot != null)
                {
                    dot.rectTransform.SetAsLastSibling();
                }
            }
        }

        /// <summary>确保某个流源的 body/edge/accent 三个 Image 已创建（懒创建，顺序即叠放顺序）。</summary>
        private void EnsureQuad(int index)
        {
            if (_bodies[index] == null)
            {
                _bodies[index] = CreateImage("FlowAtlasBody");
                _dotOrderDirty = true;
            }

            if (_edges[index] == null)
            {
                _edges[index] = CreateImage("FlowAtlasEdge");
                _dotOrderDirty = true;
            }

            if (_accents[index] == null)
            {
                _accents[index] = CreateImage("FlowAtlasAccent");
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
            if (!image.gameObject.activeSelf)
            {
                image.gameObject.SetActive(true);
            }

            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;

            if (!ReferenceEquals(cache.Sprite, sprite))
            {
                image.sprite = sprite;
                image.enabled = sprite != null;
                cache.Sprite = sprite;
            }

            if (cache.Color != color)
            {
                image.color = color;
                cache.Color = color;
            }
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
                wall = CreateImage("FlowAtlasWall");
                wall.color = Color.white;
                // 后创建的节点会在上面：把墙挪到这一组的最底层（body 之前）
                wall.rectTransform.SetAsFirstSibling();
                _walls[index] = wall;
            }

            if (!wall.gameObject.activeSelf)
            {
                wall.gameObject.SetActive(true);
            }

            wall.rectTransform.anchoredPosition = position;
            wall.rectTransform.sizeDelta = size;

            // 墙图自身带 #C9C2B8，颜色恒为白；sprite 只在状态/方向/含量变化时换
            if (!ReferenceEquals(_wallCache[index].Sprite, sprite))
            {
                wall.sprite = sprite;
                wall.enabled = true;
                _wallCache[index].Sprite = sprite;
            }
        }

        /// <summary>隐掉一个池对象（null 安全；不销毁，留待复用）。</summary>
        private static void Deactivate(Image image)
        {
            if (image != null && image.gameObject.activeSelf)
            {
                image.gameObject.SetActive(false);
            }
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
            Sprite bodySprite, Rect area, float ppu)
        {
            int layers = item.TrailLayers;
            if (layers > GhostsPerSource)
            {
                layers = GhostsPerSource;
            }

            bool show = bodySprite != null && layers > 0 && item.State.State == FlowState.Flow;

            int dx;
            int dy;
            DirStep(item.State.Dir, out dx, out dy);

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
                    ghost = CreateImage("FlowAtlasGhost");
                    // 拖尾压到最底：后创建的本体自然叠在它上面
                    ghost.rectTransform.SetAsFirstSibling();
                    _ghosts[ghostIndex] = ghost;
                }

                if (!ghost.gameObject.activeSelf)
                {
                    ghost.gameObject.SetActive(true);
                }

                // 残影同理：sprite/color 只在变化时写，位置每帧写
                if (!ReferenceEquals(_ghostCache[ghostIndex].Sprite, bodySprite))
                {
                    ghost.sprite = bodySprite;
                    ghost.enabled = true;
                    _ghostCache[ghostIndex].Sprite = bodySprite;
                }

                float distance = GhostOffsetsPx[gh];
                float leftPx = Mathf.Round(
                    (area.xMin + (float)item.PositionX * FlowPixelMath.CellPixels - dx * distance) * ppu);
                float topPx = Mathf.Round(
                    (area.yMax - (float)item.PositionY * FlowPixelMath.CellPixels + dy * distance) * ppu);
                float sizePx = Mathf.Round(FlowPixelMath.CellPixels * ppu);
                if (sizePx < 1f)
                {
                    sizePx = 1f;
                }

                ghost.rectTransform.anchoredPosition = new Vector2(
                    leftPx / ppu - area.xMin,
                    topPx / ppu - area.yMax);
                ghost.rectTransform.sizeDelta = new Vector2(sizePx / ppu, sizePx / ppu);

                Color ghostColor = new Color(bodyTint.r, bodyTint.g, bodyTint.b, GhostAlpha[gh]);
                if (_ghostCache[ghostIndex].Color != ghostColor)
                {
                    ghost.color = ghostColor;
                    _ghostCache[ghostIndex].Color = ghostColor;
                }
            }
        }

        /// <summary>方向 → 单位步长（Y 向下为正：Up 是 -1）。</summary>
        private static void DirStep(FlowDir dir, out int dx, out int dy)
        {
            switch (dir)
            {
                case FlowDir.Up:
                    dx = 0;
                    dy = -1;
                    return;
                case FlowDir.Down:
                    dx = 0;
                    dy = 1;
                    return;
                case FlowDir.Left:
                    dx = -1;
                    dy = 0;
                    return;
                case FlowDir.Right:
                default:
                    dx = 1;
                    dy = 0;
                    return;
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
        private Image CreateImage(string name)
        {
            GameObject go = new GameObject(name);
            go.layer = _root.gameObject.layer;

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.SetParent(_root, false);
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
