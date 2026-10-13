namespace ProbablyBagMod
{
    /// <summary>
    /// 四方向。
    /// 反向流只是装置返回一个相反方向，不是特殊能力。
    /// </summary>
    public enum Direction
    {
        Up,
        Down,
        Left,
        Right,
    }

    public static class DirectionExtensions
    {
        /// <summary>取反方向。</summary>
        public static Direction Opposite(this Direction dir)
        {
            switch (dir)
            {
                case Direction.Up:   return Direction.Down;
                case Direction.Down: return Direction.Up;
                case Direction.Left: return Direction.Right;
                default:             return Direction.Left;
            }
        }

        /// <summary>方向的单格位移。</summary>
        public static GridPos ToOffset(this Direction dir)
        {
            switch (dir)
            {
                case Direction.Up:   return new GridPos(0, 1);
                case Direction.Down: return new GridPos(0, -1);
                case Direction.Left: return new GridPos(-1, 0);
                default:             return new GridPos(1, 0);
            }
        }
    }
}
