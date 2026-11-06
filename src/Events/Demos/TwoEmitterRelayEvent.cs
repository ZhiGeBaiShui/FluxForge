using System.Collections;
using FluxForge.Mod;
using UnityEngine;

namespace FluxForge.Events
{
    /// <summary>
    /// 演示二：左侧发射器注水后发射，等待流源到达右侧，再让右侧发射器发射。
    /// </summary>
    internal static class TwoEmitterRelayEvent
    {
        private const string EmitterKey = "flow_emitter";
        private const int WaterMicrolitres = 1000000;
        private const float RelayDelaySeconds = 2f;
        private const float FlowSpeedCellsPerSecond = 2f;

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

            session.RunRoutine(RelayRoutine(session, left, right, rightX));
            return true;
        }

        private static IEnumerator RelayRoutine(
            EventSession session,
            GameItem left,
            GameItem right,
            int rightX)
        {
            session.Runtime.Emit(left);

            float travelSeconds = (rightX - 1) / FlowSpeedCellsPerSecond;
            yield return new WaitForSeconds(travelSeconds);

            // 让出当前帧，确保 FlowDriver 已完成本帧推进/接收，再进入 2 秒等待。
            yield return null;
            yield return new WaitForSeconds(RelayDelaySeconds);
            session.Runtime.Emit(right);

            yield return new WaitForSeconds(RelayDelaySeconds);
            session.Complete();
        }
    }
}
