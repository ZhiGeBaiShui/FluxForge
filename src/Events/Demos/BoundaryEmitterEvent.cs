using System.Collections;
using FluxForge.Mod;
using UnityEngine;

namespace FluxForge.Events
{
    /// <summary>
    /// 演示一：单个发射器从最左侧朝右发射，流源在边界消失后结束。
    /// </summary>
    internal static class BoundaryEmitterEvent
    {
        private const string EmitterKey = "flow_emitter";
        private const int WaterMicrolitres = 1000000;
        private const float FirstEmitDelaySeconds = 2f;
        private const float EndDelaySeconds = 1.5f;

        internal static bool Start(GameGridInventory grid)
        {
            FlowRuntime runtime = GridRegistry.Get(grid);
            EventSession session = new EventSession(grid, runtime);

            int middleY = grid.inventoryShape.height / 2;
            GameItem emitter = DemoPlacement.Spawn(
                session,
                grid,
                EmitterKey,
                1,
                middleY,
                3,
                false);

            if (emitter == null)
            {
                session.Fail("演示发射器放置失败");
                return false;
            }

            DemoPlacement.FillWater(emitter, WaterMicrolitres);
            session.RunRoutine(Run(session, emitter));
            return true;
        }

        private static IEnumerator Run(EventSession session, GameItem emitter)
        {
            yield return new WaitForSeconds(FirstEmitDelaySeconds);

            emitter.ActivateSlotItem();

            while (session.Runtime.Flows.Active.Count > 0)
            {
                yield return null;
            }

            yield return new WaitForSeconds(EndDelaySeconds);
            session.Complete();
        }
    }
}
