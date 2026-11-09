using System.Collections;
using FluxForge.Mod;
using UnityEngine;

namespace FluxForge.Events
{
    /// <summary>
    /// 离心机演示：左侧发射一次，流源经过中间的离心机，最后由右侧发射器接收。
    /// 不来回循环。
    /// </summary>
    internal static class CentrifugeEvent
    {
        private const string EmitterKey = "flow_emitter";
        private const string CentrifugeKey = "flow_centrifuge";
        private const int WaterMicrolitres = 100000;
        // 400 g 稠咖啡糊按约 350 mL 计。
        private const int CoffeePasteMicrolitres = 350000;
        private const double DemoCentrifugeK = 2d;
        private const float FirstEmitDelaySeconds = 2f;
        private const float EndDelaySeconds = 1.5f;

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

            FlowProperties payload = FlowStock.Read(left);
            payload.Set(FlowProps.CoffeeMush, CoffeePasteMicrolitres);
            FlowStock.Write(left, payload);

            session.RunRoutine(SpawnAndRun(session, grid, left, groupX, middleY));
            return true;
        }

        private static IEnumerator SpawnAndRun(
            EventSession session,
            GameGridInventory grid,
            GameItem left,
            int groupX,
            int middleY)
        {
            yield return new WaitForSeconds(
                DemoPlacement.ItemSpawnIntervalSeconds);

            GameItem centrifuge = DemoPlacement.Spawn(
                session,
                grid,
                CentrifugeKey,
                groupX + 3,
                middleY,
                0,
                false,
                delegate(GameItem item)
                {
                    GameItemTags.WriteDouble(
                        item,
                        FlowTags.CentrifugeK,
                        DemoCentrifugeK);
                    item.SyncModifiedState();
                });

            if (centrifuge == null)
            {
                session.Fail("演示离心机放置失败");
                yield break;
            }

            yield return new WaitForSeconds(
                DemoPlacement.ItemSpawnIntervalSeconds);

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
                yield break;
            }

            session.RunRoutine(Run(session, left));
        }

        private static IEnumerator Run(
            EventSession session,
            GameItem left)
        {
            yield return new WaitForSeconds(FirstEmitDelaySeconds);

            left.ActivateSlotItem();

            while (session.Runtime.Flows.Active.Count > 0)
            {
                yield return null;
            }

            yield return new WaitForSeconds(EndDelaySeconds);
            session.Complete();
        }
    }
}
