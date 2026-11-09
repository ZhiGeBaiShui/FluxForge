using System.Collections;
using FluxForge.Mod;
using UnityEngine;

namespace FluxForge.Events
{
    /// <summary>
    /// 演示三：左右发射器相对，中间放过滤装置；流源往返 3 个来回。
    /// </summary>
    internal static class FilterLoopEvent
    {
        private const string EmitterKey = "flow_emitter";
        private const string FilterKey = "flow_filter";
        private const int WaterMicrolitres = 1000000;
        private const int ChemicalMicrolitres = 20000;
        private const int HeavyMetalMicrolitres = 20000;
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

            FlowProperties contaminated = FlowStock.Read(left);
            contaminated.Set(FlowProps.ChemicalContaminant, ChemicalMicrolitres);
            contaminated.Set(FlowProps.HeavyMetal, HeavyMetalMicrolitres);
            FlowStock.Write(left, contaminated);

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

            GameItem filter = DemoPlacement.Spawn(
                session,
                grid,
                FilterKey,
                groupX + 3,
                middleY,
                0,
                false);

            if (filter == null)
            {
                session.Fail("演示过滤装置放置失败");
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

            session.RunRoutine(Run(session, left, right));
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
