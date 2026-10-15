using System;

namespace ProbablyBagMod
{
    /// <summary>
    /// 连续位置（浮点）。
    ///
    /// 约定：整数点即格中心——(1.0, 1.0) 就是格子 (1,1) 的中心。
    /// 流源出生、被装置拦截时都落在格中心（整数点），行进途中可以是小数。
    ///
    /// 注意 <see cref="IsSameCell"/> 用 Math.Floor 切格（边界在整数处），与上述
    /// 「中心」约定有半格偏移——本框架不追求严谨碰撞检测，可接受。
    ///
    /// 声明为 class：被流源（<see cref="FlowEntity"/>）按引用持有，
    /// 各层改的是同一份位置数据。
    /// </summary>
    public class FlowPos
    {
        public float X;
        public float Y;

        public FlowPos(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>
        /// 判断给定坐标是否与本位置处于同一格：各自向下取整、舍弃小数位后比较。
        /// </summary>
        public bool IsSameCell(float x, float y)
        {
            return Math.Floor(x) == Math.Floor(X)
                && Math.Floor(y) == Math.Floor(Y);
        }

        public override string ToString()
        {
            return "(" + X + ", " + Y + ")";
        }
    }
}
