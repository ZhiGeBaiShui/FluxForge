using System.Collections;
using FluxForge.Mod;
using UnityEngine;

namespace FluxForge.Events
{
    /// <summary>
    /// 冷萃进阶演示：右侧发射一次，经降温、冷萃、升温后由左侧接收。
    /// </summary>
    internal static class ColdTrapAdvancedLoopEvent
    {
        private const string EmitterKey = "flow_emitter";
        private const string ColdTrapKey = "flow_cold_trap";
        private const int WaterMicrolitres = 1000000;
        private const int AlcoholMicrolitres = 10000;
        private const float FirstEmitDelaySeconds = 2f;
        private const float CompletionHoldSeconds = 2f;

        internal static bool Start(GameGridInventory grid)
        {
            FlowRuntime runtime = GridRegistry.Get(grid);
            EventSession session = new EventSession(grid, runtime);

            int middleY = grid.inventoryShape.height / 2;
            int groupX = (grid.inventoryShape.width - 11) / 2;

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

            session.RunRoutine(SpawnAndRun(session, grid, groupX, middleY));
            return true;
        }

        private static IEnumerator SpawnAndRun(
            EventSession session,
            GameGridInventory grid,
            int groupX,
            int middleY)
        {
            yield return new WaitForSeconds(
                DemoPlacement.ItemSpawnIntervalSeconds);

            GameItem heater = DemoPlacement.Spawn(
                session, grid, "flow_heater", groupX + 3, middleY, 0, false);
            if (heater == null)
            {
                session.Fail("演示升温装置放置失败");
                yield break;
            }

            yield return new WaitForSeconds(DemoPlacement.ItemSpawnIntervalSeconds);

            GameItem coldTrap = DemoPlacement.Spawn(
                session,
                grid,
                ColdTrapKey,
                groupX + 5,
                middleY,
                0,
                false);

            if (coldTrap == null)
            {
                session.Fail("演示冷萃装置放置失败");
                yield break;
            }

            yield return new WaitForSeconds(
                DemoPlacement.ItemSpawnIntervalSeconds);

            GameItem cooler = DemoPlacement.Spawn(
                session, grid, "flow_cooler", groupX + 7, middleY, 0, false);
            if (cooler == null)
            {
                session.Fail("演示降温装置放置失败");
                yield break;
            }

            yield return new WaitForSeconds(DemoPlacement.ItemSpawnIntervalSeconds);

            GameItem right = DemoPlacement.Spawn(
                session,
                grid,
                EmitterKey,
                groupX + 9,
                middleY,
                1,
                false);

            if (right == null)
            {
                session.Fail("演示发射器放置失败");
                yield break;
            }

            DemoPlacement.FillWater(right, WaterMicrolitres);
            FlowProperties payload = FlowStock.Read(right);
            payload.Set(FlowProps.Alcohol, AlcoholMicrolitres);
            FlowStock.Write(right, payload);

            session.RunRoutine(Run(session, right));
        }

        private static IEnumerator Run(
            EventSession session,
            GameItem right)
        {
            yield return new WaitForSeconds(FirstEmitDelaySeconds);

            right.ActivateSlotItem();

            while (session.Runtime.Flows.Active.Count > 0)
            {
                yield return null;
            }

            yield return new WaitForSeconds(CompletionHoldSeconds);
            session.Complete();
        }
    }
}
