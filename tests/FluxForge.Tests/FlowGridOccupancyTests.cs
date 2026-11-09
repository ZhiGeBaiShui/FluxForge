using FluxForge.Mod;
using Xunit;
namespace FluxForge.Tests
{
    public class FlowGridOccupancyTests
    {
        [Fact]
        public void OrdinaryShapeRegistersOnlySolidCellsAndUnregistersAll()
        {
            var item = new GameItem { modifiedShape = new GridShape(2,2) { minX=3, minY=4 } };
            item.modifiedShape.cells[1] = 0;
            var grid = new FlowGridAdapter(new GameGridInventory());
            grid.Register(item);
            Assert.Equal(FlowCellKind.Occupied, grid.GetCellKind(3,4));
            Assert.Equal(FlowCellKind.Empty, grid.GetCellKind(4,4));
            Assert.Equal(FlowCellKind.Occupied, grid.GetCellKind(4,5));
            grid.Unregister(item);
            Assert.Equal(FlowCellKind.Empty, grid.GetCellKind(3,4));
            Assert.Equal(FlowCellKind.Empty, grid.GetCellKind(4,5));
        }

        [Theory]
        [InlineData(0,false)] [InlineData(1,false)] [InlineData(2,true)] [InlineData(3,true)]
        public void OccupancyUsesTransformedShapeAndClearsOldLocation(int orientation, bool flip)
        {
            var shape = new GridShape(2,3) { orientation=orientation, flipped=flip, minX=2, minY=2 };
            System.Array.Clear(shape.cells,0,shape.cells.Length); shape.cells[1]=1;
            var item = new GameItem { modifiedShape=shape };
            var grid = new FlowGridAdapter(new GameGridInventory());
            grid.Register(item);
            var old = shape.ForwardTransform(1,0);
            Assert.Equal(FlowCellKind.Occupied, grid.GetCellKind(old.Item1,old.Item2));
            shape.minX += 5;
            grid.RefreshRegistered(item);
            Assert.Equal(FlowCellKind.Empty, grid.GetCellKind(old.Item1,old.Item2));
            var current=shape.ForwardTransform(1,0);
            Assert.Equal(FlowCellKind.Occupied, grid.GetCellKind(current.Item1,current.Item2));
            grid.Unregister(item);
            grid.RefreshRegistered(item);
            Assert.Equal(FlowCellKind.Empty, grid.GetCellKind(current.Item1,current.Item2));
        }

        [Fact]
        public void RebuildRestoresOrdinaryItemsAndRemovesStaleOccupancy()
        {
            var inventory = new GameGridInventory();
            var item = new GameItem { modifiedShape=new GridShape(1,1) { minX=2,minY=2 } };
            inventory.childItems.Add(item);
            var grid = new FlowGridAdapter(inventory);
            grid.Rebuild();
            Assert.Equal(FlowCellKind.Occupied,grid.GetCellKind(2,2));
            inventory.childItems.Clear();
            grid.Rebuild();
            Assert.Equal(FlowCellKind.Empty,grid.GetCellKind(2,2));
        }
    }
}
