using System;
using System.Collections.Generic;
using FluxForge.Mod.Visual.Internal;
using FluxForge.Presentation.Flow.Particles;
using FluxForge.Presentation.Flow.Visual;
using UnityEngine;

namespace FluxForge.Mod.Visual
{
    /// <summary>
    /// 每块网格一个视觉视图：把本网格 FlowSpawner 里的流源翻译成
    /// <see cref="FlowDrawItem"/>，交给本网格自己的 <see cref="FlowAtlasLayer"/> 绘制。
    ///
    /// 与旧 <c>FlowAtlasVisual</c>（已删）的区别：
    ///   · 状态全在**实例**里：实体名单、blocked 缓存、每源粒子场、12fps 时钟、层；
    ///     不同网格的流源与粒子互不干扰；
    ///   · 生死广播由本视图直接订阅本网格 <see cref="FlowSpawner"/>，委托存字段，Dispose 时退订；
    ///   · 层与网格 RectTransform 一一对应：面板重建时就地重建层，环境退役时 Dispose。
    ///
    /// 生命周期（由 <see cref="FlowDriver"/> 驱动）：
    ///   FlowRuntime 构造时 new → runtime.Visual；
    ///   FlowDriver.LateUpdateVisuals → Tick(dt)；
    ///   FlowDriver.Retire / Reset → Dispose()。
    ///
    /// Tier-0 的退化（REFACTOR-A §4）：
    ///   成分固定纯水（Tier-0 没有成分数据源）、射程固定 0（预留字段，不渲染射程点）、
    ///   状态只区分 flow/blocked；collect 死亡贴图已删除，死亡改由粒子场原地淡出表达。
    ///   温度/含量/方向/帧号全部来自 FlowEntity 的现有字段。
    /// </summary>
    public sealed class FlowVisualLayer : IDisposable
    {
        /// <summary>
        /// 动效时钟：12 帧 / 秒（SPEC §6.1）。
        /// 帧数与静态哨兵统一用 <see cref="FlowVisualState.FrameCount"/> / <see cref="FlowVisualState.NoFrame"/>；
        /// 这里只保留「每秒帧数」这个纯时钟常量，因为全仓库只有本视图消费它。
        /// </summary>
        private const float FrameRate = 12f;

        private readonly GameGridInventory _inventory;
        private readonly FlowSpawner _flows;

        /// <summary>本网格当前活着的流源。出生加、死亡删，Tick 只遍历它。</summary>
        private readonly List<FlowEntity> _entities = new List<FlowEntity>();

        /// <summary>受阻事件缓存，与实体名单同生同灭。</summary>
        private readonly Dictionary<FlowEntity, bool> _blockedStates
            = new Dictionary<FlowEntity, bool>();

        /// <summary>本视图独立的粒子系统。</summary>
        private readonly FlowParticleSystem _particles = new FlowParticleSystem();

        /// <summary>每帧复用的粒子快照列表：由各场逐颗拷贝进来，交给层一次性绘制。</summary>
        private readonly List<FlowAmbientParticle> _dots = new List<FlowAmbientParticle>();

        /// <summary>每帧复用的绘制参数列表：只 Clear/Add，稳态不产生托管分配。</summary>
        private readonly List<FlowDrawItem> _items = new List<FlowDrawItem>();

        // 订阅用的委托要留着：Dispose 时要 -=（lambda 每次是不同实例，不留引用就再也退订不掉）
        private readonly Action<FlowEntity> _addedHandler;
        private readonly Action<FlowEntity> _removedHandler;
        private readonly Action<FlowEntity, bool> _blockedChangedHandler;

        /// <summary>本网格的图集层；面板还没建好时为 null。</summary>
        private FlowAtlasLayer _layer;

        /// <summary>当前层挂在哪个网格 RectTransform 上（变了就重建层）。</summary>
        private RectTransform _rect;

        /// <summary>本视图自己的 12fps 时钟，各网格独立相位。</summary>
        private float _clock;

        private bool _disposed;

        public FlowVisualLayer(GameGridInventory inventory, FlowSpawner flows)
        {
            _inventory = inventory;
            _flows = flows;

            _addedHandler = OnEntityAdded;
            _removedHandler = OnEntityRemoved;
            _blockedChangedHandler = OnEntityBlockedChanged;

            if (_flows != null)
            {
                _flows.OnEntityAdded += _addedHandler;
                _flows.OnEntityRemoved += _removedHandler;
                _flows.OnEntityBlockedChanged += _blockedChangedHandler;
            }
        }

        /// <summary>
        /// 每帧由 <see cref="FlowDriver.LateUpdateVisuals"/> 调用（已经在其 try 保护内）。
        /// 顺序：推进时钟 → 解析网格 RectTransform（变了重建层）→ 面板关闭则整块跳过 →
        /// 逐实体换算本体 → 发射/推进粒子场 → 一次性提交本体与粒子。
        /// </summary>
        public void Tick(float deltaTime)
        {
            // 暂停帧不推进视觉时钟和粒子。
            // 写成 !(deltaTime > 0f) 而不是 deltaTime <= 0f：NaN 也要挡在门外。
            if (!(deltaTime > 0f))
            {
                return;
            }

            if (_disposed)
            {
                return;
            }

            _clock += deltaTime;

            // 12fps 动效相位。先取模再兜一次负数，避免 float 溢出成 int.MinValue，
            // 也避免异常大的 deltaTime 让 (int) 转换落到未定义区间。
            int frame = ((int)(_clock * FrameRate)) % FlowVisualState.FrameCount;
            if (frame < 0)
            {
                frame += FlowVisualState.FrameCount;
            }

            // 层为 null 表示这块网格当前还画不了（网格/ RectTransform 已失效 / 还没建好），
            // 此时整块跳过；能走到下面说明 rect 与 layer 都有效。
            RectTransform rect = ResolveRect(out FlowAtlasLayer layer);
            if (layer == null)
            {
                return;
            }

            // 面板关闭：整块跳过，不更新 sprite。网格失活时层也跟着隐藏，
            // 下一帧打开时位置/尺寸会重新同步，不会留下冻结的旧帧。
            // 粒子不跨关闭期存活：关闭期间不推进寿命，重开时会闪出一批定格粒子，
            // 所以这里把所有场清空，并把死亡场的淡出队列一并丢掉。
            if (!rect.gameObject.activeInHierarchy)
            {
                _particles.ClearAmbient();
                _dots.Clear();
                return;
            }

            _items.Clear();

            // 单次遍历：每个实体只读一次快照，贴图与粒子共用，避免两条路径重复换算。
            for (int i = 0; i < _entities.Count; i++)
            {
                FlowEntity entity = _entities[i];

                FlowFrameInput snapshot = ReadSnapshot(entity);
                _items.Add(BuildDrawItem(in snapshot, frame));

                // 两张表同生命周期；缺粒子槽说明不变量被破坏，让 KeyNotFound 冒到
                // FlowDriver 的边界 catch（退役本网格环境），不静默少画粒子。
                _particles.Update(entity, in snapshot, deltaTime);
            }

            // 死亡实体的场只积分、不发射；粒子清空即移出队列。
            _particles.CompleteFrame(deltaTime, _dots);

            // 本帧的本体与粒子一次性提交；层在解析处已确认存活。
            layer.Submit(_items);
            layer.SubmitDots(_dots);
        }

        /// <summary>
        /// 释放整个视觉层的静态资源（图集贴图/Sprite 缓存）。
        /// 由 <c>FFModEntry.OnDisable</c> 调用；实例的 <see cref="Dispose"/> 只管本网格。
        /// </summary>
        public static void Shutdown()
        {
            FlowAtlasTexture.Shutdown();
        }

        /// <summary>
        /// 退订广播 + 销毁层 + 清空本网格所有表。可重复调用。
        /// 环境退役（FlowDriver.Retire）与读档（FlowDriver.Reset）走同一条路径，
        /// 不会留下指向死 FlowSpawner 的订阅，也不会把层留在已经销毁的网格下。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_flows != null)
            {
                _flows.OnEntityAdded -= _addedHandler;
                _flows.OnEntityRemoved -= _removedHandler;
                _flows.OnEntityBlockedChanged -= _blockedChangedHandler;
            }

            if (_layer != null)
            {
                _layer.Destroy();
                _layer = null;
            }

            _rect = null;
            _clock = 0f;
            _entities.Clear();
            _blockedStates.Clear();
            _particles.Reset();
            _dots.Clear();
            _items.Clear();
        }

        // ---- 生命周期回调（只来自本网格 FlowSpawner） ----

        private void OnEntityAdded(FlowEntity entity)
        {
            _blockedStates[entity] = false;

            // 每个流源独立一个场：共用场会让多个流源的粒子混在一起，死亡时也无法只淡出一源
            _particles.Add(entity);

            _entities.Add(entity);
        }

        private void OnEntityRemoved(FlowEntity entity)
        {
            _entities.Remove(entity);
            _blockedStates.Remove(entity);

            // 死亡后只把粒子场转入淡出队列：已经存在的粒子留在原地自然衰减，
            // 视觉上就是「散掉」而不是「换一张图」。
            _particles.Remove(entity);
        }

        private void OnEntityBlockedChanged(FlowEntity entity, bool blocked)
        {
            // 更早的订阅者可能已终结该源，不重建死亡条目。
            if (_blockedStates.ContainsKey(entity))
            {
                _blockedStates[entity] = blocked;
            }
        }

        // ---- 每帧换算 ----

        /// <summary>
        /// 拿本网格的 RectTransform（缓存；面板重建后 rectTransform 会变，失效就重挂层）。
        /// <paramref name="layer"/> 返回本次可用的层；为 null 表示这块网格当前还画不了，整块跳过。
        /// </summary>
        private RectTransform ResolveRect(out FlowAtlasLayer layer)
        {
            layer = null;

            if (_inventory == null)
            {
                return null;
            }

            RectTransform rect = _inventory.rectTransform;
            if (rect == null)
            {
                return null;
            }

            if (_layer != null && _layer.IsAlive && _rect == rect)
            {
                layer = _layer;
                return rect;
            }

            // 面板重建：旧层随旧 rect 一起被销毁，这里重新挂一份
            if (_layer != null)
            {
                _layer.Destroy();
                _layer = null;
            }

            _rect = rect;
            _layer = new FlowAtlasLayer(rect, ResolveBelow());
            layer = _layer;
            return rect;
        }

        /// <summary>
        /// 流源层要插在物品容器（<c>inner</c>）之前：格子背景之上、所有物品之下。
        /// 调用方 <see cref="ResolveRect"/> 已确认 <c>_inventory</c> 非空。
        /// </summary>
        private RectTransform ResolveBelow()
        {
            TreeNodeRender inner = _inventory.inner;
            return inner == null ? null : inner.rectTransform;
        }

        /// <summary>
        /// 读一条流源这一帧的快照：位置/方向/速度 + 成分/温度/含量/状态。
        ///
        /// 受阻状态只读事件缓存；贴图与粒子共用快照。
        /// </summary>
        private FlowFrameInput ReadSnapshot(FlowEntity entity)
        {
            // 缓存与实体同生命周期；缺条目让 KeyNotFound 冒到边界 catch。
            bool blocked = _blockedStates[entity];

            // Logic 中心先换算成游戏格中心，再把游戏格中心绑定到贴图中心。
            // 贴图左上 = 游戏中心 - 0.5，因此贴图中心与游戏中心重合。
            double gameCenterX = FlowGameSpace.ToGameCenter(entity.Position.X);
            double gameCenterY = FlowGameSpace.ToGameCenter(entity.Position.Y);
            double positionX = FlowPixelMath.TileLeftFromGameCenter(gameCenterX);
            double positionY = FlowPixelMath.TileLeftFromGameCenter(gameCenterY);

            int direction = entity.Motion == null ? 1 : entity.Motion.Direction;
            double speed = entity.Motion == null ? 0d : entity.Motion.Speed;

            // 本体色按 36 种液体的存量加权算好，再交给三层染色。
            // 射程暂无数据源，FlowVisualState.Range 保持 0（预留：字段就绪前不渲染射程点）。
            return new FlowFrameInput(
                positionX,
                positionY,
                direction,
                speed,
                ComputeBodyColor(entity.Properties),
                FlowTierMap.TempFrom((float)entity.Properties.Get(FlowProps.Temperature)),
                FlowTierMap.ContentFromWater(entity.Properties.SubstanceTotal),
                FlowTierMap.StateFrom(blocked));
        }

        /// <summary>
        /// 液体混合色，逐行对齐游戏原版 <c>WaterHelper.GetLiquidContainerColor</c>：
        /// <code>
        /// weight = amount × Mathf.Max(1, colorMultiplier)
        /// color  = Σ(rgb × weight) / Σweight     // 截断取整，不是四舍五入
        /// </code>
        ///
        /// 与原版保持一致的三件事（刻意**不**加任何额外修饰）：
        ///   · 没有含量阈值——只要 &gt;0 就参与，1 单位氰化物也会影响颜色；
        ///   · 没有水权重压制——water 的 colorMultiplier 就是 1；
        ///   · 取整用截断（<see cref="FlowColor.TruncateToByte"/>），对齐
        ///     Unity <c>Color</c> → <c>Color32</c> 的隐式转换。
        ///
        /// 唯一差异：原版空容器返回 <c>"000000"</c> 黑（容器没有液体层，不显示），
        /// 而流源不存在「空」的语义——真拿到全 0 说明数据缺失，返回兜底色
        /// <see cref="FlowLiquidPalette.Default"/> 比黑块更容易发现问题。
        ///
        /// 精度说明：原版用 float 累加，这里用 double。数学式完全一致，
        /// 极少数通道可能差 1，肉眼不可辨。
        /// </summary>
        private static FlowColor ComputeBodyColor(IFlowState properties)
        {
            double weightedR = 0d;
            double weightedG = 0d;
            double weightedB = 0d;
            double weightTotal = 0d;

            foreach (FlowPropertyDef def in FlowPropertyRegistry.All)
            {
                if (def.Kind != FlowPropertyKind.Liquid)
                {
                    continue;
                }

                double amount = properties.Get(def.Key);
                if (amount <= 0d)
                {
                    continue;
                }

                FlowColor color = FlowLiquidPalette.Get(def.Id);
                double weight = amount * FlowLiquidPalette.GetColorMultiplier(def.Id);

                weightedR += color.R * weight;
                weightedG += color.G * weight;
                weightedB += color.B * weight;
                weightTotal += weight;
            }

            if (weightTotal <= 0d)
            {
                return FlowLiquidPalette.Default;
            }

            return new FlowColor(
                FlowColor.TruncateToByte(weightedR / weightTotal),
                FlowColor.TruncateToByte(weightedG / weightTotal),
                FlowColor.TruncateToByte(weightedB / weightTotal));
        }
        /// <summary>把快照翻译成一条绘制指令（纯函数，不查表、不改状态）。</summary>
        private static FlowDrawItem BuildDrawItem(in FlowFrameInput snapshot, int frame)
        {
            FlowDir dir = FlowTierMap.DirFrom(snapshot.Direction);

            // 只有 flow 有 12 帧动效；blocked/split/merge 都用静态 tile（collect 已不再生成）。
            int visualFrame = snapshot.State == FlowState.Flow ? frame : FlowVisualState.NoFrame;

            FlowVisualState visual = new FlowVisualState(
                snapshot.BodyColor, snapshot.Temp, snapshot.Content, snapshot.State, dir, visualFrame);

            return new FlowDrawItem(
                snapshot.X,
                snapshot.Y,
                visual,
                (int)dir);
        }
    }
}
