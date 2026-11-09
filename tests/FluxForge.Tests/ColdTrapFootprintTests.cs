using System;
using FluxForge.Mod;
using Xunit;

namespace FluxForge.Tests
{
    public sealed class ColdTrapFootprintTests : IDisposable
    {
        public ColdTrapFootprintTests() => DeviceRegistry.Clear();
        public void Dispose() => DeviceRegistry.Clear();

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, false)]
        [InlineData(2, true)]
        [InlineData(3, true)]
        public void OnlyCenterReactsWhileArmsKeepTheirWorldAxis(int orientation, bool flipped)
        {
            var shape = new GridShape(3, 3) { minX = 3, minY = 4, orientation = orientation, flipped = flipped };
            Array.Clear(shape.cells, 0, shape.cells.Length);
            foreach (int i in new[] { 1, 3, 4, 5, 7 }) shape.cells[i] = 1;
            var item = new GameItem { modifiedShape = shape };
            item.EnableTag("cold_trap");
            item.state.GetTag(FlowTags.AnchorX).SetInt(1);
            item.state.GetTag(FlowTags.AnchorY).SetInt(1);
            Assert.True(ItemDeviceBinding.Bind(item));
            var inventory = new GameGridInventory();
            inventory.childItems.Add(item);
            var grid = new FlowGridAdapter(inventory);
            grid.Register(item);
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
            {
                var world = shape.ForwardTransform(x, y);
                bool solid = shape.GetLocal(x, y) != 0;
                Assert.Equal(solid ? FlowCellKind.Device : FlowCellKind.Empty, grid.GetCellKind(world.Item1, world.Item2));
                if (x == 1 && y == 1) Assert.IsType<ColdTrapDevice>(grid.GetReactorAt(world.Item1, world.Item2));
                else
                {
                    Assert.Null(grid.GetReactorAt(world.Item1, world.Item2));
                    var router = grid.GetRouterAt(world.Item1, world.Item2);
                    if (solid)
                    {
                        Assert.IsType<FixedAxisStraightRouter>(router);
                        Assert.False(router is IFacing);
                        var inlet = Assert.IsAssignableFrom<IFlowInlet>(router);
                        int axis = world.Item1 == shape.ForwardTransform(1, 1).Item1 ? 0 : 1;
                        Assert.Equal(axis == 0, inlet.CanEnter(0));
                        Assert.Equal(axis == 1, inlet.CanEnter(1));
                    }
                    else Assert.Null(router);
                }
            }
            var old = shape.ForwardTransform(1, 0);
            shape.minX += 5;
            grid.RefreshRegistered(item);
            Assert.Equal(FlowCellKind.Empty, grid.GetCellKind(old.Item1, old.Item2));
            grid.Rebuild();
            var moved = shape.ForwardTransform(1, 0);
            Assert.NotNull(grid.GetRouterAt(moved.Item1, moved.Item2));
            grid.Unregister(item);
            Assert.Equal(FlowCellKind.Empty, grid.GetCellKind(moved.Item1, moved.Item2));
        }
    }
}
