using System;
using System.Collections.Generic;
using ProbablyBagMod.Mod.Rendering.FlowAtlas;
using ProbablyBagMod.Presentation.Flow.Particles;
using ProbablyBagMod.Presentation.Flow.Visual;
using UnityEngine;

namespace ProbablyBagMod.Mod.Flow.View
{
    /// <summary>
    /// **一台机器一个**的视觉视图（REFACTOR-A §5）：把本机 FlowSpawner 里的流源翻译成
    /// <see cref="FlowDrawItem"/>，交给本机自己的 <see cref="FlowAtlasLayer"/> 绘制。
    ///
    /// 与旧 <c>FlowAtlasVisual</c>（已删）的区别：
    ///   · 状态全在**实例**里：实体名单、blocked 计数、每源粒子场、12fps 时钟、层；
    ///     没有任何 <c>Dictionary&lt;IFlowGrid, ...&gt;</c> 静态表，第二台机器落地不会互相干扰；
    ///   · 生死广播由本视图直接订阅本机 <see cref="FlowSpawner"/>，委托存字段，Dispose 时退订；
    ///   · 层与网格 RectTransform 一一对应：面板重建（rect 变了）就地重建层，网格退役随机器 Dispose。
    ///
    /// 生命周期（由 <see cref="FlowDriver"/> 驱动）：
    ///   FlowItemSetup 里 new → machine.AtlasView；
    ///   FlowDriver.LateUpdateVisuals → Tick(dt)；
    ///   FlowDriver.Retire / Reset → Dispose()。
    ///
    /// Tier-0 的退化（REFACTOR-A §4）：
    ///   成分固定纯水（Tier-0 没有成分数据源）、射程固定 0（预留字段，不渲染射程点）、
    ///   状态只区分 flow/blocked；collect 死亡贴图已删除，死亡改由粒子场原地淡出表达。
    ///   温度/含量/方向/帧号全部来自 FlowEntity 的现有字段。
    /// </summary>
    public sealed class FlowAtlasView : IDisposable
    {
        /// <summary>连续静止多少帧才判定为「被阻挡」（REFACTOR-A §4：2 帧）。</summary>
        private const int BlockedStillFrames = 2;

        /// <summary>速度高于该值却连续不动，才算被阻挡；真静止的流源不算。</summary>
        private const double BlockedMinSpeed = 0.01d;

        /// <summary>动效时钟：12 帧 / 秒（SPEC §6.1）。</summary>
        private const float FrameRate = 12f;

        /// <summary>动效帧数（f0..f11）。</summary>
        private const int FrameCount = 12;

        /// <summary>静态 tile 的帧哨兵，语义同 <c>FlowVisualState.Frame</c> 的 -1。</summary>
        private const int NoFrame = -1;

        /// <summary>
        /// 一个流源的逐帧状态：上帧原始位置 + 连续静止帧数（判 blocked 用）。
        ///
        /// 注意 <see cref="FlowPos.X"/> / <see cref="FlowPos.Y"/> 是 double（连续坐标，
        /// 整数点才是格中心），不是整数格号：格内移动时位置每帧都在变，
        /// 所以 blocked 必须比较原始 double，不能比较取整后的格子，否则一进格就误判为卡住。
        /// </summary>
        private sealed class EntityState
        {
            public double LastX;
            public double LastY;
            public int StillFrames;
        }

        /// <summary>
        /// 一帧内一条流源的全部视觉输入（位置/方向/速度/成分/温度/含量/状态）。
        ///
        /// 为什么要有它：贴图与粒子都需要同一组属性，旧实现是两条路径各算一遍
        /// （两遍 `_states` 查表、两遍 Temp/Content/State 换算）。现在每帧只读一次，
        /// 贴图与粒子共用同一份结果，也保证两者的 blocked 判定永远一致。
        /// </summary>
        private readonly struct EntitySnapshot
        {
            public readonly double X;
            public readonly double Y;
            public readonly int Direction;
            public readonly double Speed;
            public readonly FlowComposition Composition;
            public readonly FlowTemp Temp;
            public readonly FlowContent Content;
            public readonly FlowState State;

            public EntitySnapshot(
                double x, double y, int direction, double speed,
                FlowComposition composition, FlowTemp temp, FlowContent content, FlowState state)
            {
                X = x;
                Y = y;
                Direction = direction;
                Speed = speed;
                Composition = composition;
                Temp = temp;
                Content = content;
                State = state;
            }
        }

        /// <summary>
        /// 一个流源的常驻粒子场与发射累加器。
        ///
        /// <see cref="Alive"/> 与实体名单分开标记：实体死亡后先摘出 <c>_emitters</c>，
        /// 场地带着已经存在的粒子转入 <c>_fading</c> 继续积分，直到粒子自然死光。
        /// 容量 32 是稳态估算：8 颗/秒 × 0.65 秒寿命 ≈ 5 颗，留足余量。
        /// </summary>
        private sealed class Emitter
        {
            public FlowAmbientField Field;
            public float Accum;
            public bool Alive;
        }

        private readonly IFlowGrid _grid;
        private readonly Il2Cpp.GameGridInventory _inventory;
        private readonly FlowSpawner _flows;

        /// <summary>本机当前活着的流源。出生加、死亡删，Tick 只遍历它。</summary>
        private readonly List<FlowEntity> _entities = new List<FlowEntity>();

        /// <summary>逐流源的 blocked 判定状态（按实体索引；实体死亡即删）。</summary>
        private readonly Dictionary<FlowEntity, EntityState> _states
            = new Dictionary<FlowEntity, EntityState>();

        /// <summary>每流源一个粒子场：出生建、死亡转入 <see cref="_fading"/>。</summary>
        private readonly Dictionary<FlowEntity, Emitter> _emitters
            = new Dictionary<FlowEntity, Emitter>();

        /// <summary>死亡后仍在把剩余粒子走完的场；Count 归零才移除。</summary>
        private readonly List<Emitter> _fading = new List<Emitter>();

        /// <summary>每帧复用的粒子快照列表：由各场逐颗拷贝进来，交给层一次性绘制。</summary>
        private readonly List<FlowAmbientParticle> _dots = new List<FlowAmbientParticle>();

        /// <summary>粒子场随机种子：每源 +1，保证同机多源不会生成完全相同的粒子序列。</summary>
        private int _seed;

        /// <summary>每帧复用的绘制参数列表：只 Clear/Add，稳态不产生托管分配。</summary>
        private readonly List<FlowDrawItem> _items = new List<FlowDrawItem>();

        // 订阅用的委托要留着：Dispose 时要 -=（lambda 每次是不同实例，不留引用就再也退订不掉）
        private readonly Action<FlowEntity> _addedHandler;
        private readonly Action<FlowEntity> _removedHandler;

        /// <summary>本机的图集层；面板还没建好时为 null。</summary>
        private FlowAtlasLayer _layer;

        /// <summary>当前层挂在哪个网格 RectTransform 上（变了就重建层）。</summary>
        private RectTransform _rect;

        /// <summary>本视图自己的 12fps 时钟（每台机器独立相位，互不干扰）。</summary>
        private float _clock;

        private bool _disposed;

        public FlowAtlasView(IFlowGrid grid, Il2Cpp.GameGridInventory inventory, FlowSpawner flows)
        {
            _grid = grid;
            _inventory = inventory;
            _flows = flows;

            _addedHandler = OnEntityAdded;
            _removedHandler = OnEntityRemoved;

            if (_flows != null)
            {
                _flows.OnEntityAdded += _addedHandler;
                _flows.OnEntityRemoved += _removedHandler;
            }
        }

        /// <summary>本视图所属机器的网格（诊断用；视图自己不靠它取数据）。</summary>
        public IFlowGrid Grid
        {
            get { return _grid; }
        }

        /// <summary>
        /// 每帧由 <see cref="FlowDriver.LateUpdateVisuals"/> 调用（已经在其 try 保护内）。
        /// 顺序：推进时钟 → 解析网格 RectTransform（变了重建层）→ 面板关闭则整块跳过 →
        /// 逐实体换算本体 → 发射/推进粒子场 → 一次性提交本体与粒子。
        /// </summary>
        public void Tick(float deltaTime)
        {
            // 暂停帧不推进：timeScale=0 时游戏逻辑停走，流源位置自然也不变。
            // 若继续走下面的 blocked 启发式，"暂停导致的没动"会在两帧后被误判成 blocked。
            if (deltaTime <= 0f)
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
            int frame = ((int)(_clock * FrameRate)) % FrameCount;
            if (frame < 0)
            {
                frame += FrameCount;
            }

            RectTransform rect = ResolveRect();
            if (rect == null)
            {
                return;
            }

            // 面板关闭：整块跳过，不更新 sprite。网格失活时层也跟着隐藏，
            // 下一帧打开时位置/尺寸会重新同步，不会留下冻结的旧帧。
            // 粒子不跨关闭期存活：关闭期间不推进寿命，重开时会闪出一批定格粒子，
            // 所以这里把所有场清空，并把死亡场的淡出队列一并丢掉。
            if (!rect.gameObject.activeInHierarchy)
            {
                ClearAmbientFields();
                return;
            }

            _items.Clear();

            // 单次遍历：每个实体只读一次快照，贴图与粒子共用，避免两条路径重复换算。
            for (int i = 0; i < _entities.Count; i++)
            {
                FlowEntity entity = _entities[i];

                EntitySnapshot snapshot;
                if (!TryReadSnapshot(entity, out snapshot))
                {
                    continue;
                }

                _items.Add(BuildDrawItem(in snapshot, frame));

                Emitter emitter;
                if (_emitters.TryGetValue(entity, out emitter) && emitter.Alive)
                {
                    UpdateEmitter(emitter, in snapshot, deltaTime);
                }
            }

            // 死亡实体的场只积分、不发射；粒子清空即移出队列。
            AdvanceFading(deltaTime);
            CollectDots();

            if (_layer != null)
            {
                _layer.Submit(_items);
                _layer.SubmitDots(_dots);
            }
        }

        /// <summary>
        /// 退订广播 + 销毁层 + 清空本机所有表。可重复调用。
        /// 机器退役（FlowDriver.Retire）与读档（FlowDriver.Reset）走同一条路径，
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
            }

            if (_layer != null)
            {
                _layer.Destroy();
                _layer = null;
            }

            _rect = null;
            _clock = 0f;
            _entities.Clear();
            _states.Clear();
            _emitters.Clear();
            _fading.Clear();
            _dots.Clear();
            _items.Clear();
        }

        // ---- 生命周期回调（只来自本机 FlowSpawner） ----

        private void OnEntityAdded(FlowEntity entity)
        {
            if (_disposed || entity == null)
            {
                return;
            }

            if (!_states.ContainsKey(entity))
            {
                _entities.Add(entity);
            }

            // 初始位置就是当前位置：下一帧若还在这里，才开始累计静止帧
            EntityState record = new EntityState();
            record.LastX = entity.Position == null ? 0d : entity.Position.X;
            record.LastY = entity.Position == null ? 0d : entity.Position.Y;
            record.StillFrames = 0;
            _states[entity] = record;

            // 每个流源独立一个场：共用场会让多个流源的粒子混在一起，死亡时也无法只淡出一源
            if (!_emitters.ContainsKey(entity))
            {
                Emitter emitter = new Emitter();
                emitter.Field = new FlowAmbientField(32, _seed++);
                emitter.Accum = 0f;
                emitter.Alive = true;
                _emitters[entity] = emitter;
            }
        }

        private void OnEntityRemoved(FlowEntity entity)
        {
            if (entity == null)
            {
                return;
            }

            _states.Remove(entity);
            _entities.Remove(entity);

            // 视图已经销毁：层都没了，不必再把场转入淡出
            if (_disposed)
            {
                return;
            }

            // 死亡不再生成 collect 贴图：本体下一帧从提交列表里消失，
            // 已经存在的粒子留在原地自然衰减，视觉上就是「散掉」而不是「换一张图」。
            Emitter emitter;
            if (!_emitters.TryGetValue(entity, out emitter))
            {
                return;
            }

            _emitters.Remove(entity);
            emitter.Alive = false;
            _fading.Add(emitter);
        }

        // ---- 每帧换算 ----

        /// <summary>
        /// 拿本机网格的 RectTransform（缓存；面板重建后 rectTransform 会变，失效就重挂层）。
        /// 返回 null 表示这块网格当前还画不了，整块跳过。
        /// </summary>
        private RectTransform ResolveRect()
        {
            if (_inventory == null || _inventory.Pointer == IntPtr.Zero)
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
                return rect;
            }

            // 面板重建：旧层随旧 rect 一起被销毁，这里重新挂一份
            if (_layer != null)
            {
                _layer.Destroy();
                _layer = null;
            }

            _rect = rect;
            _layer = new FlowAtlasLayer(rect);
            return rect;
        }

        /// <summary>
        /// 读一条流源这一帧的快照：位置/方向/速度 + 成分/温度/含量/状态。
        ///
        /// blocked 计数只在这里更新，每帧每实体一次；贴图与粒子都吃同一份结果。
        /// 位置判定写在读取属性之前：blocked 只依赖格位与速度，与视觉枚举无关。
        /// </summary>
        private bool TryReadSnapshot(FlowEntity entity, out EntitySnapshot snapshot)
        {
            snapshot = default(EntitySnapshot);

            if (entity == null || entity.Properties == null || entity.Position == null)
            {
                return false;
            }

            EntityState record;
            if (!_states.TryGetValue(entity, out record))
            {
                return false;
            }

            double positionX = entity.Position.X;
            double positionY = entity.Position.Y;
            int direction = entity.Motion == null ? 1 : entity.Motion.Direction;
            double speed = entity.Motion == null ? 0d : entity.Motion.Speed;

            // 「位置与上帧相同且 Speed > 0.01」连续 2 帧才 blocked：
            // 位置变了、或者速度本来就低（真静止），都从 0 重新计数。
            if (positionX == record.LastX && positionY == record.LastY && speed > BlockedMinSpeed)
            {
                record.StillFrames++;
            }
            else
            {
                record.StillFrames = 0;
            }

            record.LastX = positionX;
            record.LastY = positionY;

            // Tier-0 的 blocked 只是**启发式**：真正的 blocked 需要逻辑层标志
            // （FlowManager/装置判定"这条路走不通"），这里只有"位置与上帧相同且仍在运动"
            // 连续 2 帧。暂停帧已在 Tick 开头早退，所以 timeScale=0 不会被算成 blocked；
            // 但它仍可能把"动画卡顿/浮点抖动"误读成受阻，逻辑层标志落地后应改为读标志。
            bool blocked = record.StillFrames >= BlockedStillFrames;

            // Tier-0：成分固定纯水（REFACTOR-A §4）；射程暂无数据源，
            // FlowVisualState.Range 保持 0（预留：字段就绪前不渲染射程点）。
            snapshot = new EntitySnapshot(
                positionX,
                positionY,
                direction,
                speed,
                FlowComposition.PureWater,
                FlowTierMap.TempFrom((float)entity.Properties.Temperature),
                FlowTierMap.ContentFromMoisture(entity.Properties.Moisture),
                FlowTierMap.StateFrom(false, blocked));
            return true;
        }

        /// <summary>把快照翻译成一条绘制指令（纯函数，不查表、不改状态）。</summary>
        private static FlowDrawItem BuildDrawItem(in EntitySnapshot snapshot, int frame)
        {
            FlowDir dir = FlowTierMap.DirFrom(snapshot.Direction);

            // 只有 flow 有 12 帧动效；blocked/split/merge 都用静态 tile（collect 已不再生成）。
            int visualFrame = snapshot.State == FlowState.Flow ? frame : NoFrame;

            FlowVisualState visual = new FlowVisualState(
                snapshot.Composition, snapshot.Temp, snapshot.Content, snapshot.State, dir, visualFrame);

            return new FlowDrawItem(
                snapshot.X,
                snapshot.Y,
                visual,
                FlowTierMap.TrailLayersFromContent(snapshot.Content));
        }

        /// <summary>
        /// 推进一条活着的流源的粒子场：按快照建 profile、积分一帧、按 SpawnRate 补发。
        ///
        /// 死亡实体的场不会进这里（它们已在 <see cref="_fading"/> 里），
        /// 所以「死亡后停止生成」是由调用方（Tick 的单次遍历）保证的，不靠这里的判断。
        /// </summary>
        private static void UpdateEmitter(Emitter emitter, in EntitySnapshot snapshot, float deltaTime)
        {
            FlowParticleInput input = new FlowParticleInput(
                snapshot.Composition, snapshot.Temp, snapshot.Content, snapshot.State, (float)snapshot.Speed);
            FlowParticleProfile profile = FlowParticleRules.Build(in input);

            emitter.Field.Content = snapshot.Content;
            emitter.Field.Advance(deltaTime);

            // 按 SpawnRate 累加，整数部分才真正生成；不足一颗的余量留到下一帧。
            // 用累加器而不是每帧四舍五入，低帧率时发射数也不会被截断。
            emitter.Accum += profile.SpawnRate * deltaTime;
            while (emitter.Accum >= 1f)
            {
                emitter.Accum -= 1f;
                emitter.Field.Spawn(
                    profile,
                    snapshot.X,
                    snapshot.Y,
                    snapshot.Direction,
                    FlowParticleRules.ColorFor(in input));
            }
        }

        /// <summary>死亡实体的场：只积分到粒子清空，清空后从淡出队列移除。</summary>
        private void AdvanceFading(float deltaTime)
        {
            // 倒序遍历：RemoveAt 只会挪动后面的元素，不会跳过尚未检查的场。
            for (int i = _fading.Count - 1; i >= 0; i--)
            {
                Emitter emitter = _fading[i];
                emitter.Field.Advance(deltaTime);
                if (emitter.Field.Count == 0)
                {
                    _fading.RemoveAt(i);
                }
            }
        }

        /// <summary>把所有活场与淡出场里的粒子拷进本帧复用列表。</summary>
        private void CollectDots()
        {
            _dots.Clear();
            foreach (Emitter emitter in _emitters.Values)
            {
                AppendDots(emitter.Field);
            }

            for (int i = 0; i < _fading.Count; i++)
            {
                AppendDots(_fading[i].Field);
            }
        }

        /// <summary>把一个场的存活粒子逐颗拷进本帧列表（Get 返回的是值快照）。</summary>
        private void AppendDots(FlowAmbientField field)
        {
            int count = field.Count;
            for (int i = 0; i < count; i++)
            {
                _dots.Add(field.Get(i));
            }
        }

        /// <summary>
        /// 面板关闭时清场：所有粒子立即消失，淡出队列也丢掉。
        /// 关闭期间 Tick 不推进寿命，留着会让重开后的第一帧闪出关闭前的定格粒子。
        /// </summary>
        private void ClearAmbientFields()
        {
            foreach (Emitter emitter in _emitters.Values)
            {
                emitter.Field.Clear();
            }

            for (int i = 0; i < _fading.Count; i++)
            {
                _fading[i].Field.Clear();
            }

            _fading.Clear();
            _dots.Clear();
        }
    }
}
