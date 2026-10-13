namespace ProbablyBagMod
{
    /// <summary>
    /// 格子坐标。值类型，可比较。
    /// </summary>
    public readonly struct GridPos
    {
        public readonly int X;
        public readonly int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        public GridPos Plus(GridPos other) => new GridPos(X + other.X, Y + other.Y);

        public GridPos PlusOffset(Direction dir) => Plus(dir.ToOffset());

        public bool Equals(GridPos other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is GridPos other && Equals(other);

        public override int GetHashCode() => X * 397 ^ Y;

        public override string ToString() => "(" + X + "," + Y + ")";
    }
}
