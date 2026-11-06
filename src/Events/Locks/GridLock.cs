using System.Collections.Generic;

namespace FluxForge.Events
{
    /// <summary>
    /// 网格和物品的交互锁。计数锁允许多个剧情步骤共享同一把锁；
    /// 每次 Lock 保存原值，最后一次 Unlock 才恢复，避免清掉其它系统的锁。
    /// </summary>
    internal static class GridLock
    {
        private sealed class ItemState
        {
            internal GameItem Item;
            internal bool DisableUse;
            internal bool DisableActivate;
        }

        private sealed class GridState
        {
            internal int Count;
            internal bool LockInsert;
            internal bool LockRemove;
            internal readonly List<ItemState> Items = new List<ItemState>();
        }

        private static readonly Dictionary<GameGridInventory, GridState> States
            = new Dictionary<GameGridInventory, GridState>();

        internal static void Lock(GameGridInventory grid)
        {
            GridState state;
            if (!States.TryGetValue(grid, out state))
            {
                state = new GridState
                {
                    LockInsert = grid.overrideLockInsert,
                    LockRemove = grid.overrideLockRemove,
                };

                States.Add(grid, state);
                grid.LockInv();

                List<GameItem> items = grid.childItems;
                for (int i = 0; i < items.Count; i++)
                {
                    Protect(state, items[i]);
                }
            }

            state.Count++;
        }

        internal static void Unlock(GameGridInventory grid)
        {
            GridState state;
            if (!States.TryGetValue(grid, out state))
            {
                return;
            }

            state.Count--;
            if (state.Count > 0)
            {
                return;
            }

            grid.overrideLockInsert = state.LockInsert;
            grid.overrideLockRemove = state.LockRemove;

            for (int i = state.Items.Count - 1; i >= 0; i--)
            {
                ItemState item = state.Items[i];
                if (item.Item == null)
                {
                    continue;
                }

                item.Item.forceDisableUse = item.DisableUse;
                item.Item.forceDisableActivate = item.DisableActivate;
            }

            state.Items.Clear();
            States.Remove(grid);
        }

        internal static void Track(GameItem item)
        {
            GameGridInventory grid = item.parentInventory as GameGridInventory;
            GridState state;
            if (States.TryGetValue(grid, out state))
            {
                Protect(state, item);
            }
        }

        private static void Protect(GridState state, GameItem item)
        {
            state.Items.Add(new ItemState
            {
                Item = item,
                DisableUse = item.forceDisableUse,
                DisableActivate = item.forceDisableActivate,
            });

            item.forceDisableUse = true;
            item.forceDisableActivate = true;
        }
    }
}
