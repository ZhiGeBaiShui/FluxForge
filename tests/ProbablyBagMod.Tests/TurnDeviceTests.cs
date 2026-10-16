using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace ProbablyBagMod.Tests
{
    /// <summary>
    /// L 型转向装置（2 通道 SplitDevice）的端到端验证：
    /// 流源从左向右发射，在 (10,10) 撞上 L 装置后转向朝上。
    /// 坐标系：x 自左往右递增，y 自下往上递增。
    /// </summary>
    public class TurnDeviceTests
    {
        private readonly ITestOutputHelper _output;

        public TurnDeviceTests(ITestOutputHelper output)
        {
            _output = output;
            FlowSpawner.Instance.Reset();
            FlowManager.Instance.Reset();
        }

        private sealed class FakeGrid : IFlowGrid
        {
            private readonly Dictionary<(int, int), IFlowRouter> _routers
                = new Dictionary<(int, int), IFlowRouter>();

            public FakeGrid SetRouter(int cellX, int cellY, IFlowRouter router)
            {
                _routers[(cellX, cellY)] = router;
                return this;
            }

            public IFlowRouter GetRouterAt(int cellX, int cellY)
            {
                IFlowRouter router;
                return _routers.TryGetValue((cellX, cellY), out router) ? router : null;
            }

            public IFlowDevice GetReactorAt(int cellX, int cellY)
            {
                return null;
            }
        }

        [Fact]
        public void FlowFromRight_TurnsUpAtLDevice()
        {
            // L 形装置：两开口朝左、下（左进下出）。
            // 通道集：朝左开口收右行流源(1)；朝下开口为出口——流源出去时朝下走(2)。
            SplitDevice lDevice = new SplitDevice(facing: 0, new[] { 1, 2 });
            FakeGrid grid = new FakeGrid().SetRouter(10, 10, lDevice);
            FlowManager.Instance.Bind(grid);

            // 流源：起点 (1,10)，朝右(1)，1 格/秒
            FlowEntity entity = new FlowEntity(
                new FlowPos(1, 10),
                new FlowMotionData(1, 1),
                new FlowProperties(100, 20));
            FlowSpawner.Instance.Register(entity, 0);

            // 逐帧推进并记录跨格轨迹（dt = 0.5s/帧 → 0.5 格/帧）
            const double dt = 0.5d;
            List<(double Time, int CellX, int CellY, int Dir, double Speed)> trajectory
                = new List<(double, int, int, int, double)>();
            double time = 0d;
            int lastCellX = 1;
            int lastCellY = 10;

            for (int frame = 0; frame < 60; frame++)
            {
                FlowSpawner.Instance.Advance(dt);
                FlowManager.Instance.Tick(dt);
                time += dt;

                int cellX = (int)Math.Floor(entity.Position.X);
                int cellY = (int)Math.Floor(entity.Position.Y);
                if (cellX != lastCellX || cellY != lastCellY)
                {
                    trajectory.Add((time, cellX, cellY, entity.Motion.Direction, entity.Motion.Speed));
                    lastCellX = cellX;
                    lastCellY = cellY;
                }
            }

            // 输出轨迹：时间 / 格子 / 方向 / 速度
            _output.WriteLine("时间(s) | 格子(x,y) | 方向 | 速度");
            foreach ((double t, int cx, int cy, int dir, double speed) in trajectory)
            {
                _output.WriteLine("{0,6:F2} | ({1},{2}) | {3} | {4}", t, cx, cy, dir, speed);
            }

            // ① 到达过装置格 (10,10)
            Assert.True(trajectory.Exists(t => t.CellX == 10 && t.CellY == 10),
                "轨迹应经过装置格 (10,10)");

            int deviceIndex = trajectory.FindIndex(t => t.CellX == 10 && t.CellY == 10);

            // ② 到达装置时 direction 已从 1（右）转为 2（下）
            Assert.Equal(2, trajectory[deviceIndex].Dir);

            // ③ 到达装置前仍是向右
            Assert.Equal(1, trajectory[deviceIndex - 1].Dir);

            // ④ 转向后朝下走（y 递减）
            Assert.True(trajectory[trajectory.Count - 1].CellY < 10,
                "转向后应朝下走（y 递减）");

            // ⑤ 速度全程不变
            foreach ((double t, int cx, int cy, int dir, double speed) in trajectory)
            {
                Assert.Equal(1, speed);
            }
        }
    }
}
