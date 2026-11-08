namespace FluxForge.Mod
{
    /// <summary>
    /// <see cref="GameItem"/> 的最小静态 tag 读写工具：只做单个 tag 的读取与“值变化才写入”，
    /// 供 Mod 层把装置 / 流源状态映射到物品 tag。
    ///
    /// 读走 <c>modifiedState</c>：它是游戏校验过的快照，装置回灌时与 tooltip / 存档看到的值一致；
    /// 写走 <c>state</c>：它才是活的 <see cref="TagSystem"/>，写 <c>modifiedState</c> 不会生效。
    ///
    /// 不负责刷新快照（<c>SyncModifiedState</c>）、不触发存档 / 同步，也没有任何兜底分支——
    /// 生命周期与提交时机由调用方保证（流源写入由 <see cref="FlowStock.Write"/> 在末尾统一提交）。
    /// </summary>
    public static class GameItemTags
    {
        /// <summary>读整数；tag 不存在时按 0 处理。</summary>
        public static int ReadInt(GameItem item, string tag)
        {
            TagState state;
            return item.modifiedState.dict.TryGetValue(tag, out state) ? state.GetInt() : 0;
        }

        /// <summary>读 double；tag 不存在时按 0 处理。</summary>
        public static double ReadDouble(GameItem item, string tag)
        {
            TagState state;
            return item.modifiedState.dict.TryGetValue(tag, out state) ? state.GetDouble() : 0d;
        }

        /// <summary>
        /// 写整数；值没变时返回 false。
        ///
        /// tag 还不存在且要写 0 时直接跳过，避免给装置凭空创建一堆零值 tag；
        /// 写非零值前先 <see cref="TagState.Enable"/>，与游戏写入方保持一致。
        /// </summary>
        public static bool WriteInt(GameItem item, string tag, int value)
        {
            TagState state;
            if (item.state.dict.TryGetValue(tag, out state))
            {
                if (state.GetInt() == value)
                {
                    return false;
                }
            }
            else
            {
                if (value == 0)
                {
                    return false;
                }

                state = item.state.GetTag(tag);
            }

            state.Enable();
            state.SetInt(value);
            return true;
        }

        /// <summary>写 double；值没变时返回 false；不存在且写 0 时不创建 tag。</summary>
        public static bool WriteDouble(GameItem item, string tag, double value)
        {
            TagState state;
            if (item.state.dict.TryGetValue(tag, out state))
            {
                if (state.GetDouble() == value)
                {
                    return false;
                }
            }
            else
            {
                if (value == 0d)
                {
                    return false;
                }

                state = item.state.GetTag(tag);
            }

            state.Enable();
            state.SetDouble(value);
            return true;
        }
    }
}
