namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 流量装置在物品 tag 上的读写唯一入口。
    ///
    /// 集中原因：tag 名字符串原先散落在 <see cref="FlowRuntime"/>、<see cref="FlowDeviceEvents"/>、
    /// <see cref="ItemSetup"/> 三处，改一个常量要同步改三份副本；
    /// 而且读/写对 API 的要求不同，很容易写错。
    ///
    /// 读写语义（与游戏 API 一致）：
    ///   · 读：<c>TagSystem.GetTag</c> 取到槽位后读标量字段，值本身就是存档里的真值；
    ///   · 写：必须拿**活**的 <c>TagState</c> 再改字段，不能改 <c>GameItem.GetTagReadonly</c>
    ///     返回的 Clone 副本（改副本不会影响活 tag，这个坑踩过两次）。
    /// 两者都通过 <c>item.state</c> 进入，所以 <c>state == null</c> 是真实外部风险：
    /// 读返回 fallback，写静默忽略，调用方不必各自判空。
    /// </summary>
    internal static class FlowTags
    {
        /// <summary>存量：int 槽位（收集装置与冰块的液体量）。</summary>
        public const string Stock = "FLOW_STOCK";

        /// <summary>流体温度：float 槽位（收集装置与发射装置）。</summary>
        public const string FlowTemperature = "FLOW_TEMPERATURE";

        /// <summary>冰块温度：float 槽位（与流体温度分开存，避免互相覆盖）。</summary>
        public const string IceTemperature = "ICE_TEMPERATURE";

        /// <summary>读 int 槽位；<paramref name="state"/> 或 tag 槽位缺失时返回 <paramref name="fallback"/>。</summary>
        public static int ReadInt(Il2Cpp.TagSystem state, string tag, int fallback)
        {
            if (state == null)
            {
                return fallback;
            }

            Il2Cpp.TagState tagState = state.GetTag(tag);
            return tagState == null ? fallback : tagState.valueInt;
        }

        /// <summary>读 float 槽位；<paramref name="state"/> 或 tag 槽位缺失时返回 <paramref name="fallback"/>。</summary>
        public static float ReadFloat(Il2Cpp.TagSystem state, string tag, float fallback)
        {
            if (state == null)
            {
                return fallback;
            }

            Il2Cpp.TagState tagState = state.GetTag(tag);
            return tagState == null ? fallback : tagState.valueFloat;
        }

        /// <summary>写 int 槽位；<paramref name="state"/> 或 tag 槽位缺失时静默忽略。</summary>
        public static void WriteInt(Il2Cpp.TagSystem state, string tag, int value)
        {
            if (state == null)
            {
                return;
            }

            Il2Cpp.TagState tagState = state.GetTag(tag);
            if (tagState != null)
            {
                tagState.valueInt = value;
            }
        }

        /// <summary>写 float 槽位；<paramref name="state"/> 或 tag 槽位缺失时静默忽略。</summary>
        public static void WriteFloat(Il2Cpp.TagSystem state, string tag, float value)
        {
            if (state == null)
            {
                return;
            }

            Il2Cpp.TagState tagState = state.GetTag(tag);
            if (tagState != null)
            {
                tagState.valueFloat = value;
            }
        }
    }
}
