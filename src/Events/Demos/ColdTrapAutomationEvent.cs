using System.Collections;
using FluxForge.Mod;
using UnityEngine;

namespace FluxForge.Events
{
    /// <summary>先让流源进入冷萃回路，再补上入口转角，形成自动循环。</summary>
    internal static class ColdTrapAutomationEvent
    {
        internal static bool Start(GameGridInventory grid)
        {
            EventSession session = new EventSession(grid, GridRegistry.Get(grid));
            session.RunRoutine(Run(session, grid));
            return true;
        }

        private static IEnumerator Run(EventSession session, GameGridInventory grid)
        {
            int x = (grid.inventoryShape.width - 9) / 2;
            int y = (grid.inventoryShape.height - 5) / 2;
            GameItem emitter = DemoPlacement.Spawn(
                session, grid, "flow_emitter", x + 1, y + 4, 3, false);
            if (emitter == null)
            {
                session.Fail("演示发射器放置失败");
                yield break;
            }
            DemoPlacement.FillWater(emitter, 1000000);
            FlowProperties payload = FlowStock.Read(emitter);
            payload.Set(FlowProps.Alcohol, 10000);
            FlowStock.Write(emitter, payload);

            // L 型装置原始开口为右、下；游戏 orientation 经 FromOrientation 反向换算为 Facing。
            string[] keys = {
                "l_turn_device", "flow_heater", "flow_cooler", "l_turn_device",
                "flow_cold_trap", "l_turn_device", "flow_heater", "l_turn_device",
                "flow_cooler", "l_turn_device"
            };
            int[] columns = { 3, 3, 4, 5, 5, 5, 6, 7, 7, 7 };
            int[] rows = { 2, 3, 4, 4, 2, 0, 0, 0, 1, 2 };
            int[] rotations = { 0, 0, 0, 2, 0, 0, 0, 3, 0, 2 };
            for (int i = 0; i < keys.Length; i++)
            {
                yield return new WaitForSeconds(DemoPlacement.ItemSpawnIntervalSeconds);
                if (DemoPlacement.Spawn(session, grid, keys[i],
                    x + columns[i], y + rows[i], rotations[i], false) == null)
                {
                    session.Fail("自动化演示装置放置失败：" + keys[i]);
                    yield break;
                }
            }

            yield return new WaitForSeconds(2f);
            emitter.ActivateSlotItem();
            // Position 是逻辑层公开的实时位置；进入冷萃下臂后，入口才可封闭。
            bool entered = false;
            while (!entered)
            {
                foreach (FlowEntity flow in session.Runtime.Flows.Active)
                {
                    if (flow.Position.X >= x + 5 && flow.Position.Y <= y + 3)
                    {
                        entered = true;
                        break;
                    }
                }
                if (!entered)
                {
                    yield return null;
                }
            }
            if (DemoPlacement.Spawn(session, grid, "l_turn_device",
                x + 3, y + 4, 1, false) == null)
            {
                session.Fail("自动化演示入口转角放置失败");
                yield break;
            }
            session.CompleteAfter(10f);
        }
    }
}
