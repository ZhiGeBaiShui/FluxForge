using System;
using FluxForge.Mod.Visual;

namespace FluxForge.Mod
{
    /// <summary>
    /// 一块网格的运行环境：借用游戏网格，拥有流源、运动采样与视觉视图。
    /// 发射是运行环境自己的职责——流源从物品的存量直接生成，不经过装置类。
    /// </summary>
    public sealed class FlowRuntime
    {
        /// <summary>流源速度（格/秒）。发射唯一的运动参数。</summary>
        private const double EmitSpeed = 2d;

        public FlowRuntime(GameGridInventory inventory)
        {
            Inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

            // 先读取网格尺寸，再装配自有对象；构造期间不刷新布局、不创建 UGUI 绘制层。
            Grid = new FlowGridAdapter(inventory);
            Flows = new FlowSpawner();
            Motion = new FlowManager(Grid, Flows);
            // 视觉最后装配，完成全部订阅后才交给 Driver 登记及发射。
            // 构造失败时未登记的名单和运动订阅没有外部持有者，可一起回收。
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

            FlowEntity entity = new FlowEntity(
                origin,
                new FlowMotionData(direction, EmitSpeed),
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
        ///   物品原图朝上，方向码 = <see cref="FlowFacing.FromOrientation"/>（0上 1右 2下 3左）。
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

            direction = FlowFacing.FromOrientation(shape.orientation);

            // 游戏侧格坐标以左上角为 1.00；Logic 中心也以该点为 1.00，
            // 不再额外加 0.5。
            origin = new FlowPos(shape.minX, shape.minY);
            return true;
        }
    }
}
