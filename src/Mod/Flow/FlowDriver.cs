using System.Collections.Generic;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 多机器调度器：只管「有哪些机器」和每帧的调用顺序，不再持有任何网格引用。
    ///
    /// 顺序有讲究：
    ///   ① 剔除已失效的机器（物品被销毁后不再触碰）
    ///   ② 全局 Advance 一次 → 所有机器的流源一起出队，同一帧就能被推进，不白等一帧
    ///   ③ 全局 Tick 一次 → 运动层按流源自带的 Grid 分别探测、分别越界回收
    /// 装置的状态回写与移除由装置自己的事件驱动，调度器不参与。
    /// 分流放不在这里做：网格归属由 FlowEntity.Grid 决定，调度器不需要知道谁是谁的。
    /// </summary>
    public static class FlowDriver
    {
        private static readonly List<FlowMachine> _machines = new List<FlowMachine>();

        /// <summary>流量机创建时登记。机器物品被销毁后会在下一次 Update 里自动剔除。</summary>
        public static void Add(FlowMachine machine)
        {
            _machines.Add(machine);
        }

        /// <summary>由 MelonMod.OnUpdate 每帧调用。</summary>
        public static void Update(double deltaTime)
        {
            for (int i = _machines.Count - 1; i >= 0; i--)
            {
                FlowMachine machine = _machines[i];

                // 物品被销毁后原生指针失效，再访问就会碰到野指针。
                // 一台机器出问题不该连累其他机器：这里把它的流源和显示一并撤掉。
                if (!machine.IsAlive)
                {
                    Retire(machine);
                    _machines.RemoveAt(i);
                }
            }

            FlowSpawner.Instance.Advance(deltaTime);
            FlowManager.Instance.Tick(deltaTime);
        }

        /// <summary>撤掉一台作废的机器：它网格上的流源、装置登记、显示登记、网格过滤都要清。</summary>
        private static void Retire(FlowMachine machine)
        {
            FlowManager.Instance.KillAllOfGrid(machine.Grid);

            // 网格里的装置物品还活着的话，多半只是这台机器被删了、物品被挪走；
            // 指针已失效的则必须注销，否则原生地址被新物品复用时会被误认成装置
            Il2CppSystem.Collections.Generic.List<Il2Cpp.GameItem> items = machine.Inventory.childItems;
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    Il2Cpp.GameItem item = items[i];
                    if (item != null)
                    {
                        DeviceRegistry.Unregister(item.Pointer);
                    }
                }
            }

            FlowVisual.UnregisterGrid(machine.Grid);
            MelonLoader.MelonLogger.Msg("[流量机] 已移除失效的机器");
        }

        /// <summary>读档 / 换场景时整体归零（旧机器、旧网格、旧流源都已失效）。</summary>
        public static void Reset()
        {
            _machines.Clear();
            DeviceRegistry.Clear();

            FlowManager.Instance.Reset();
            FlowSpawner.Instance.Reset();
            FlowVisual.Clear();
        }
    }
}
