using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 生命周期 #6：读档完成。
    ///
    /// 订阅 <c>ModHook.OnGameLoadedLate</c>——不用 <c>Init</c>：
    /// 四个阶段都在 <c>PlayerStore.LoadGame</c> 里依次触发，
    /// <c>Init</c> 是第一步（`PlayerStore.txt:6134`），
    /// <c>Late</c> 在方法末尾（`:6934`），那时物品才全部恢复完。
    ///
    /// 为什么需要它：读档恢复物品走 <c>SaveManager.DecodeNodes</c>，
    /// 直接反序列化 <c>modifiedShape</c> / <c>parentInventory</c> 字段，
    /// 不调 <c>AddParent</c>，生命周期 #2（<see cref="ItemAttached"/>）收不到信号。
    ///
    /// ⚠ 不能把重建放在 #1（<see cref="GridCreatedPatch"/>）时：
    ///   那一刻 <c>childItems</c> 还是空的，全量登记等于空转。
    ///
    /// 完整生命周期表见 `Lifecycle/README.md`。
    /// </summary>
    internal static class SaveLoaded
    {
        internal static void Subscribe()
        {
            ModHook.OnGameLoadedLate += Handle;
        }

        internal static void Unsubscribe()
        {
            ModHook.OnGameLoadedLate -= Handle;
        }

        private static void Handle()
        {
            // 读档会重载本地化表，启动时补的类型名会丢。
            ItemTypeLocalization.RegisterAll();

            FlowRuntime[] runtimes = GridRegistry.Runtimes.ToArray();

            for (int i = 0; i < runtimes.Length; i++)
            {
                FlowRuntime runtime = runtimes[i];

                if (!runtime.IsAlive)
                {
                    continue;
                }

                // 读档前可能残留旧流源（读档不走 QuitToMenu），先清掉。
                runtime.Flows.KillAll();

                // 读档恢复的物品不经过 AddParent，装置要在这里补绑。
                List<GameItem> items = runtime.Inventory.childItems;
                if (items != null)
                {
                    for (int j = 0; j < items.Count; j++)
                    {
                        ItemDeviceBinding.Bind(items[j]);
                    }
                }

                runtime.Grid.Rebuild();
            }

            FFModEntry.Log.Log("[流源] 读档完成，已重建 " + runtimes.Length + " 块网格");
        }
    }
}
