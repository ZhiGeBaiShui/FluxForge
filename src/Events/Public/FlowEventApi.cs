using System;

namespace FluxForge.Events
{
    /// <summary>
    /// 事件层唯一公开入口。剧情层直接用这三个函数，不经过 Mod 层反射。
    /// </summary>
    public static partial class FlowEventApi
    {
        public static Action<GameGridInventory> EventCompleted;

        public static void LockGrid(GameGridInventory grid)
        {
            GridLock.Lock(grid);
        }

        /// <summary>释放一次网格锁；最后一次释放恢复进入前的交互状态。</summary>
        public static void UnlockGrid(GameGridInventory grid)
        {
            GridLock.Unlock(grid);
        }

        /// <summary>
        /// 演示一：最左侧放一个朝右的发射器，注水后发射，流源从边界消失时结束并销毁演示物品。
        /// 返回 true 只表示事件已启动；完成由 FlowSpawner 事件驱动。
        /// </summary>
        public static bool RunBoundaryEmitter(GameGridInventory grid)
        {
            return BoundaryEmitterEvent.Start(grid);
        }
    }
}
