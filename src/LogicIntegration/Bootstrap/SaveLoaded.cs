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
    /// 这里为什么还要补一次 Bind + Rebuild：读档走 <c>SaveManager.DecodeNodes</c>
    /// → <c>GameInventory.UncheckedAccept</c> → <c>GameItem.AddParent</c>，
    /// 生命周期 #2（<see cref="ItemAttached"/>）确实会触发；但 <c>UncheckedAccept</c>
    /// 在 <c>AddParent</c> 之后才 <c>ValidateShapeState</c> / <c>Validate</c>，
    /// 反序列化的 <c>modifiedShape</c> 可能被校正，登记时的格位不一定是最终格位。
    ///
    /// ⚠ 不能把重建放在 #1（<see cref="GridCreatedPatch"/>）时：
    ///   那一刻 <c>childItems</c> 还是空的，全量登记等于空转。
    ///
    /// 完整生命周期表见 `Lifecycle/README.md`。
    /// </summary>
    public static class SaveLoaded
    {
        public static void Subscribe()
        {
            ModHook.OnGameLoadedLate += Handle;
        }

        public static void Unsubscribe()
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

                // 读档前可能残留旧流源（ContinueGame 不经过 #5 的退出钩子），先清掉。
                runtime.Flows.KillAll();

                // AddParent 时 ItemAttached 已经绑过装置；这里再调一次 Bind。
                // 绑定是幂等的：已登记的 Host 不重建，按读档后的 tag 立即重灌适配器。
                // 因此不依赖挂载钩子的触发时机，也不会丢掉存档里的装置状态。
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
