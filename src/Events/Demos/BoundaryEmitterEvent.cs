using System;
using System.Collections;
using FluxForge.Mod;
using UnityEngine;

namespace FluxForge.Events
{
    /// <summary>
    /// 演示一：单个发射器从最左侧朝右发射，流源在边界消失后结束。
    /// Start 只启动协程；全部事件逻辑在 Run 里。
    /// </summary>
    internal static class BoundaryEmitterEvent
    {
        private const string EmitterKey = "flow_emitter";
        private const int WaterMicrolitres = 1000000;
        private const float FirstEmitDelaySeconds = 2f;

        internal static bool Start(GameGridInventory grid)
        {
            FlowRuntime runtime = GridRegistry.Get(grid);
            EventSession session = new EventSession(grid, runtime);
            session.RunRoutine(Run(session));
            return true;
        }

        private static IEnumerator Run(EventSession session)
        {
            int middleY = session.Grid.inventoryShape.height / 2;
            GameItem emitter = DemoPlacement.Spawn(
                session,
                session.Grid,
                EmitterKey,
                1,
                middleY,
                3,
                false);

            if (emitter == null)
            {
                session.Fail("演示发射器放置失败");
                yield break;
            }

            DemoPlacement.FillWater(emitter, WaterMicrolitres);

            Action<FlowEntity> onRemoved = entity =>
            {
                session.CompleteAfter(1.5f);
            };

            session.Runtime.Flows.OnEntityRemoved += onRemoved;
            session.AddCleanup(() =>
            {
                session.Runtime.Flows.OnEntityRemoved -= onRemoved;
            });

            yield return new WaitForSeconds(FirstEmitDelaySeconds);
            session.Runtime.Emit(emitter);
        }
    }
}
