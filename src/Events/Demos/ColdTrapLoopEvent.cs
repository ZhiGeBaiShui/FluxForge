using System.Collections;
using FluxForge.Mod;
using UnityEngine;

namespace FluxForge.Events
{
    /// <summary>
    /// 演示四：左右发射器相对，中间放冷萃装置；流源往返 3 个来回。
    /// </summary>
    internal static class ColdTrapLoopEvent
    {
        private const string EmitterKey = "flow_emitter";
        private const string ColdTrapKey = "flow_cold_trap";
        private const int WaterMicrolitres = 1000000;
        private const int AlcoholMicrolitres = 10000;
        private const int RoundTrips = 3;
        private const float FirstEmitDelaySeconds = 2f;
        private const float RelayDelaySeconds = 2f;

        internal static bool Start(GameGridInventory grid)
        {
            FlowRuntime runtime = GridRegistry.Get(grid);
            EventSession session = new EventSession(grid, runtime);

            int middleY = grid.inventoryShape.height / 2;
            int groupX = (grid.inventoryShape.width - 7) / 2;

            GameItem left = DemoPlacement.Spawn(
                session,
                grid,
                EmitterKey,
                groupX + 1,
                middleY,
                3,
                false);

            if (left == null)
            {
                session.Fail("演示发射器放置失败");
                return false;
            }

            DemoPlacement.FillWater(left, WaterMicrolitres);

            GameItem coldTrap = DemoPlacement.Spawn(
                session,
                grid,
                ColdTrapKey,
                groupX + 3,
                middleY,
                0,
                false);

            if (coldTrap == null)
            {
                session.Fail("演示冷萃装置放置失败");
                return false;
            }

            GameItem right = DemoPlacement.Spawn(
                session,
                grid,
                EmitterKey,
                groupX + 5,
                middleY,
                1,
                false);

            if (right == null)
            {
                session.Fail("演示发射器放置失败");
                return false;
            }

            FlowProperties payload = FlowStock.Read(left);
            payload.Set(FlowProps.Alcohol, AlcoholMicrolitres);
            FlowStock.Write(left, payload);

            session.RunRoutine(Run(session, left, right));
            return true;
        }

        private static IEnumerator Run(
            EventSession session,
            GameItem left,
            GameItem right)
        {
            yield return new WaitForSeconds(FirstEmitDelaySeconds);

            GameItem emitter = left;

            for (int trip = 0; trip < RoundTrips; trip++)
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    emitter.ActivateSlotItem();

                    while (session.Runtime.Flows.Active.Count > 0)
                    {
                        yield return null;
                    }

                    yield return new WaitForSeconds(RelayDelaySeconds);
                    emitter = emitter == left ? right : left;
                }
            }

            session.Complete();
        }
    }
}
