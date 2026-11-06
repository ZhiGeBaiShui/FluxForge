using System.Collections;
using FluxForge.Mod;
using UnityEngine;

namespace FluxForge.Events
{
    /// <summary>
    /// 演示二：左右各放一个相对朝向的发射器；左侧先发射，右侧接收后再发射。
    /// Start 只启动协程；全部事件逻辑在 Run 里。
    /// </summary>
    internal static class TwoEmitterRelayEvent
    {
        private const string EmitterKey = "flow_emitter";
        private const int WaterMicrolitres = 1000000;
        private const float FirstEmitDelaySeconds = 2f;
        private const float RelayDelaySeconds = 2f;
        private const float FlowSpeedCellsPerSecond = 2f;

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
            int rightX = session.Grid.inventoryShape.width - 2;

            GameItem left = DemoPlacement.Spawn(
                session,
                session.Grid,
                EmitterKey,
                1,
                middleY,
                3,
                false);

            if (left == null)
            {
                session.Fail("演示发射器放置失败");
                yield break;
            }

            DemoPlacement.FillWater(left, WaterMicrolitres);

            GameItem right = DemoPlacement.Spawn(
                session,
                session.Grid,
                EmitterKey,
                rightX,
                middleY,
                1,
                false);

            if (right == null)
            {
                session.Fail("演示发射器放置失败");
                yield break;
            }

            float travelSeconds = (rightX - 1) / FlowSpeedCellsPerSecond;

            yield return new WaitForSeconds(FirstEmitDelaySeconds);
            session.Runtime.Emit(left);

            yield return new WaitForSeconds(travelSeconds);
            yield return null;
            yield return new WaitForSeconds(RelayDelaySeconds);

            session.Runtime.Emit(right);
            yield return new WaitForSeconds(travelSeconds);
            yield return null;

            session.Complete();
        }
    }
}
