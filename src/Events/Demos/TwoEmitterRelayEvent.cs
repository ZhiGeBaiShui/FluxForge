using System.Collections;
using FluxForge.Mod;
using UnityEngine;

namespace FluxForge.Events
{
    /// <summary>
    /// 演示二：左侧发射后等待当前流源消失，等 2 秒发射右侧；
    /// 右侧流源消失后，再等 2 秒结束。
    /// </summary>
    internal static class TwoEmitterRelayEvent
    {
        private const string EmitterKey = "flow_emitter";
        private const int WaterMicrolitres = 1000000;
        private const float FirstEmitDelaySeconds = 2f;
        private const float RelayDelaySeconds = 2f;

        internal static bool Start(GameGridInventory grid)
        {
            FlowRuntime runtime = GridRegistry.Get(grid);
            EventSession session = new EventSession(grid, runtime);

            int middleY = grid.inventoryShape.height / 2;
            int rightX = grid.inventoryShape.width - 2;

            GameItem left = DemoPlacement.Spawn(
                session,
                grid,
                EmitterKey,
                1,
                middleY,
                3,
                false);

            if (left == null)
            {
                session.Fail("演示发射器放置失败");
                return false;
            }

            DemoPlacement.FillWater(left, WaterMicrolitres);

            GameItem right = DemoPlacement.Spawn(
                session,
                grid,
                EmitterKey,
                rightX,
                middleY,
                1,
                false);

            if (right == null)
            {
                session.Fail("演示发射器放置失败");
                return false;
            }

            session.RunRoutine(Run(session, left, right));
            return true;
        }

        private static IEnumerator Run(
            EventSession session,
            GameItem left,
            GameItem right)
        {
            yield return new WaitForSeconds(FirstEmitDelaySeconds);
            session.Runtime.Emit(left);

            while (session.Runtime.Flows.Active.Count > 0)
            {
                yield return null;
            }

            yield return new WaitForSeconds(RelayDelaySeconds);
            session.Runtime.Emit(right);

            while (session.Runtime.Flows.Active.Count > 0)
            {
                yield return null;
            }

            yield return new WaitForSeconds(RelayDelaySeconds);
            session.Complete();
        }
    }
}
