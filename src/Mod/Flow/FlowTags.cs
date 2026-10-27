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
        /// <summary>
        /// 存量：int 槽位，直接绑定**原版的水分 tag**（`CURRENT_PART_` + WATER）。
        ///
        /// 理由：水分本来就是游戏的概念，没必要自造一套同义的 tag。
        /// 已实测：**非容器物品**上的这个 tag 不会被原版处理（tooltip、容器逻辑、水质都不触发），
        /// 因为原版先看 `LIQUID_CONTAINER_TAG` 判定身份。以后原版若改变这一行为，
        /// 再换回自有 tag —— 兼容性留给那时处理。
        ///
        /// 注意：物品真的是原版容器时（旧的接水路径），这个 tag 由原版自己维护，
        /// 我们的写入与 `WaterHelper.Remove` 会作用于同一份数据。
        /// </summary>
        public const string Stock = "CURRENT_PART_WATER";

        /// <summary>流体/装置温度：float 槽位（发射、收集、冰块共用同一个映射）。</summary>
        public const string FlowTemperature = "FLOW_TEMPERATURE";

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
