namespace FluxForge.Mod
{
    /// <summary>
    /// <see cref="ITagStore"/> 的物品实现：把装置适配器的 tag 读写落到 <see cref="GameItem"/>。
    ///
    /// 读走 <c>modifiedState</c>：它是游戏校验过的快照，装置回灌时与 tooltip / 存档看到的值一致；
    /// 写走 <c>state</c>：它才是活的 <see cref="TagSystem"/>，写 <c>modifiedState</c> 不会生效。
    ///
    /// 只做单个 tag 的存在性、读取和写入，不负责刷新快照（<c>SyncModifiedState</c>）、
    /// 不触发存档 / 同步，也没有任何兜底分支——生命周期由调用方保证。
    /// </summary>
    public sealed class GameItemTagStore : ITagStore
    {
        private readonly GameItem _item;

        public GameItemTagStore(GameItem item)
        {
            _item = item;
        }

        /// <summary>读整数；tag 不存在时按 0 处理（与 <see cref="FlowStock"/> 的读取一致）。</summary>
        public int ReadInt(string tag)
        {
            TagState state;
            return _item.modifiedState.dict.TryGetValue(tag, out state) ? state.GetInt() : 0;
        }

        /// <summary>读 double；tag 不存在时按 0 处理。</summary>
        public double ReadDouble(string tag)
        {
            TagState state;
            return _item.modifiedState.dict.TryGetValue(tag, out state) ? state.GetDouble() : 0d;
        }

        /// <summary>
        /// 写整数；值没变时返回 false。
        ///
        /// tag 还不存在且要写 0 时直接跳过，避免给装置凭空创建一堆零值 tag；
        /// 写非零值前先 <see cref="TagState.Enable"/>，与游戏写入方保持一致。
        /// </summary>
        public bool WriteInt(string tag, int value)
        {
            TagState state;
            if (_item.state.dict.TryGetValue(tag, out state))
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

                state = _item.state.GetTag(tag);
            }

            state.Enable();
            state.SetInt(value);
            return true;
        }

        /// <summary>写 double；值没变时返回 false；不存在且写 0 时不创建 tag。</summary>
        public bool WriteDouble(string tag, double value)
        {
            TagState state;
            if (_item.state.dict.TryGetValue(tag, out state))
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

                state = _item.state.GetTag(tag);
            }

            state.Enable();
            state.SetDouble(value);
            return true;
        }
    }
}
