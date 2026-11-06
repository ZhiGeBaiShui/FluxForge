using FluxForge.Events;
using Xunit;

namespace FluxForge.Tests.Events
{
    public class PlacementMathTests
    {
        [Fact]
        public void OneByTwo_RightFacing_AnchorTop_OffsetsOneCell()
        {
            PlacementMath.MinForAnchor(
                5,
                3,
                0,
                0,
                1,
                2,
                false,
                3,
                out int minX,
                out int minY);

            Assert.Equal(4, minX);
            Assert.Equal(3, minY);
        }

        [Fact]
        public void OneByTwo_Unrotated_AnchorTop_IsShapeOrigin()
        {
            PlacementMath.MinForAnchor(
                5,
                3,
                0,
                0,
                1,
                2,
                false,
                0,
                out int minX,
                out int minY);

            Assert.Equal(5, minX);
            Assert.Equal(3, minY);
        }

        [Fact]
        public void OneByTwo_LeftFacing_AnchorTop_IsShapeOrigin()
        {
            PlacementMath.MinForAnchor(
                5,
                3,
                0,
                0,
                1,
                2,
                false,
                1,
                out int minX,
                out int minY);

            Assert.Equal(5, minX);
            Assert.Equal(3, minY);
        }

        [Fact]
        public void TwoByThree_Rotated_AnchorTransformsInsideShape()
        {
            // Local anchor (1,0), width 2, height 3, orientation 1:
            // x' = y = 0, y' = width - 1 - x = 0.
            PlacementMath.MinForAnchor(
                10,
                7,
                1,
                0,
                2,
                3,
                false,
                1,
                out int minX,
                out int minY);

            Assert.Equal(10, minX);
            Assert.Equal(7, minY);
        }
    }
}
