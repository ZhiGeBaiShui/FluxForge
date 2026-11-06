namespace FluxForge.Events
{
    /// <summary>
    /// 纯整数几何：复刻 GridShape.ForwardTransform，供放置和单元测试共用。
    /// </summary>
    internal static class PlacementMath
    {
        internal static int TransformX(
            int localX,
            int localY,
            int width,
            int height,
            bool flipped,
            int orientation)
        {
            if (flipped)
            {
                localX = width - 1 - localX;
            }

            switch (orientation & 3)
            {
                case 1:
                    return localY;
                case 2:
                    return width - 1 - localX;
                case 3:
                    return height - 1 - localY;
                default:
                    return localX;
            }
        }

        internal static int TransformY(
            int localX,
            int localY,
            int width,
            int height,
            bool flipped,
            int orientation)
        {
            if (flipped)
            {
                localX = width - 1 - localX;
            }

            switch (orientation & 3)
            {
                case 1:
                    return width - 1 - localX;
                case 2:
                    return height - 1 - localY;
                case 3:
                    return localX;
                default:
                    return localY;
            }
        }

        internal static void MinForAnchor(
            int targetX,
            int targetY,
            int anchorX,
            int anchorY,
            int width,
            int height,
            bool flipped,
            int orientation,
            out int minX,
            out int minY)
        {
            int offsetX = TransformX(anchorX, anchorY, width, height, flipped, orientation);
            int offsetY = TransformY(anchorX, anchorY, width, height, flipped, orientation);
            minX = targetX - offsetX;
            minY = targetY - offsetY;
        }
    }
}
