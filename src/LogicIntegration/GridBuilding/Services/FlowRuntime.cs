using System;
using FluxForge.Mod.Visual;

namespace FluxForge.Mod
{
    /// <summary>
    /// 一块网格的运行环境：借用游戏网格，拥有流源、运动采样与视觉视图。
    /// 每个 GameGridInventory 一份，由 <see cref="GridRegistry"/> 在构造期创建，
    /// 生命周期与网格对象一致：网格销毁时由 <see cref="FlowDriver"/> 单条退役，
    /// 退出存档 / mod 卸载时由 <see cref="FlowDriver.Shutdown"/> 整体清理。
    /// 发射是运行环境自己的职责——流源从物品的存量直接生成，不经过装置类。
    /// </summary>
    public sealed class FlowRuntime
    {
        public FlowRuntime(GameGridInventory inventory)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

            // 构造只装配自有对象，不刷新布局、不创建 UGUI 绘制层：
            // 形状与 RectTransform 由适配器和视图在真正用到时现查，
            // 因此构造发生在网格刚建好、还没排版的时刻也没问题。
            Grid = new FlowGridAdapter(inventory);
            Flows = new FlowSpawner();
            Motion = new FlowManager(Grid, Flows);
            // 视觉最后装配：视图会直接订阅本网格的生死广播；
            // 装配完成后再由 GridRegistry 登记，避免半初始化的环境被 Tick。
            Visual = new FlowVisualLayer(inventory, Flows);
        }

        /// <summary>本网格的运动层代理（流源靠它查装置、判越界）。</summary>
        public FlowGridAdapter Grid { get; }

        /// <summary>本网格的流源名单与生死广播。</summary>
        public FlowSpawner Flows { get; }

        /// <summary>本网格的流源推进器，订阅 <see cref="Flows"/> 生死事件维护采样记录。</summary>
        public FlowManager Motion { get; }

        /// <summary>借用的游戏网格，不随运行环境退役而释放。</summary>
        public GameGridInventory Inventory { get; }

        /// <summary>
        /// 网格专属的视觉视图，构造时装配，由 <see cref="FlowDriver"/> 驱动与释放。
        /// 为 null 表示已退役。
        /// </summary>
        public FlowVisualLayer Visual;

        /// <summary>
        /// 网格 UI 还在不在。网格销毁后 <c>rectTransform</c> 变成 Unity 的 fake null；
        /// 面板关闭只是隐藏，引用仍然有效，不会误判。
        /// </summary>
        public bool IsAlive
        {
            get { return Inventory.rectTransform != null; }
        }

        /// <summary>
        /// 激活：把物品的存量整份转成一个流源，并把液体从 tag 里扣掉。
        /// 空库存不发射、不扣量。
        /// </summary>
        public bool Emit(GameItem item)
        {
            if (FlowStock.Total(item) <= 0d)
            {
                return false;
            }

            FlowPos origin;
            int direction;
            if (!ResolveEmit(item, out origin, out direction))
            {
                return false;
            }

            if (!Grid.Contains((int)Math.Floor(origin.X), (int)Math.Floor(origin.Y)))
            {
                return false;
            }

            // 发射前重读一次：游戏会直接改液体 tag（WaterHelper.Add / Remove、倒水等），
            // 不经过任何装置事件，缓存的存量会过期。
            FlowProperties payload = FlowStock.Read(item);

            CollectorDevice collector =
                (CollectorDevice)DeviceRegistry.GetRouter(item);
            double emitSpeed = collector.EmitSpeed;

            FlowEntity entity = new FlowEntity(
                origin,
                new FlowMotionData(direction, emitSpeed),
                payload);

            Flows.Accept(entity);

            // 物质守恒：发出后清空全部液体、保留标量（温度）。
            // 写回 tag 同时完成派生状态与贴图刷新。
            FlowProperties cleared = payload.Clone();
            foreach (FlowPropertyDef def in FlowPropertyRegistry.All)
            {
                if (def.Kind == FlowPropertyKind.Liquid)
                {
                    cleared.Set(def.Key, 0d);
                }
            }

            FlowStock.Write(item, cleared);

            // 发射器可能兼任收集器（flow_emitter 装配 collector）：
            // tag 已经清空，但装置里的库存是独立副本，不同步的话它会保留
            // 发射前的值，下次收集时新旧叠加，看起来就是「翻倍」。
            // 用适配器按 tag 重灌整个 Host；普通物品没有 Host，直接跳过。
            DeviceHost host = DeviceRegistry.GetHost(item);
            if (host != null)
            {
                host.LoadAll();
            }

            return true;
        }

        /// <summary>
        /// 从物品解出「从哪发、往哪发」。
        ///
        /// ⚠ 必须读 <c>modifiedShape</c> 而不是 <c>shape</c>：
        ///   后者是物品的**定义形状**，minX/minY 是定义时的局部原点，拖到哪里都不会变。
        ///   拖放落点写的是 modifiedShape。
        ///
        /// 朝向同样在 modifiedShape 上：orientation 0..3 表示 0°/逆90°/180°/顺90°，
        ///   物品原图朝上，方向码 = <see cref="FlowFacingAdapter.FromOrientation"/>（0上 1右 2下 3左）。
        ///
        /// 发射格优先用 anchor tag（中心格），没有则回退到形状左上角：
        ///   · 声明了 <c>anchorX/anchorY</c> 的物品由 EmitterAttachment 写入 tag，
        ///     多格发射器因此能从中心发射而不是左上角；
        ///   · 旧物品没有 anchor，回退到 <c>minX/minY</c>，行为与之前一致。
        /// </summary>
        private static bool ResolveEmit(GameItem item, out FlowPos origin, out int direction)
        {
            origin = null;
            direction = 0;

            // 调用方已跳过 null 物品，这里直接读形状
            GridShape shape = item.modifiedShape;
            if (shape == null)
            {
                return false;
            }

            direction = FlowFacingAdapter.FromOrientation(shape.orientation);

            // anchor 是局部坐标，旋转/翻转由 ForwardTransform 处理，
            // 与 FlowGridAdapter.RegisterCore 对装置的做法一致。
            TagState anchorXState;
            TagState anchorYState;
            if (item.state.dict.TryGetValue(FlowTags.AnchorX, out anchorXState)
                && item.state.dict.TryGetValue(FlowTags.AnchorY, out anchorYState))
            {
                (int anchorX, int anchorY) =
                    shape.ForwardTransform(anchorXState.GetInt(), anchorYState.GetInt());
                origin = new FlowPos(anchorX, anchorY);
                return true;
            }

            // 游戏侧格坐标以左上角为 1.00；Logic 中心也以该点为 1.00，
            // 不再额外加 0.5。
            origin = new FlowPos(shape.minX, shape.minY);
            return true;
        }
    }
}
