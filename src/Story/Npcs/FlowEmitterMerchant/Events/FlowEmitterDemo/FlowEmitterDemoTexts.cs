namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>本剧情所有文本都集中在这里，只存纯文本。</summary>
    internal static class FlowEmitterDemoTexts
    {
        internal const string Greeting
            = "你好，我这儿有几样新奇的装置。";

        internal const string GreetingAsk
            = "有兴趣看看吗？";

        internal const string Question
            = "它们有什么用？";

        internal const string CanDemo
            = "我可以演示给你看。";

        internal const string NeedDemo
            = "有想先了解的吗？";

        internal const string GridBusy
            = "前台得是全空的，我才能放装置，能麻烦收一下东西吗。";

        internal const string RepeatPrompt
            = "还要再看一遍吗？";

        internal const string DevicePrompt
            = "目前有“流源发射器”和“过滤装置”，都可以买。";

        internal const string EmitterIntro
            = "这个装置能把储存的液体一次性发射出去。";

        internal const string EmitterFlowName
            = "我们这行把发射出去的液体叫作“流源”。";

        internal const string RelayIntro
            = "它也能接住正在流动的“流源”。";

        internal const string FilterIntro
            = "这个净化装置可以去掉化学污染和重金属。";

        internal const string FilterIntroLimit
            = "不过一般没法把这两类污染完全除干净。";

        internal const string FilterManual
            = "如果有2台发射器，可以这样使用。";

        internal const string FilterManualAction
            = "手够巧的话，1台发射器不停来回拖也行。";

        internal const string FilterTemperature
            = "流源温度越高，净化装置能处理的上限就越高。";

        internal const string FilterTemperatureLimit
            = "化学污染和重金属都能压到更低的浓度。";

        internal const string FilterLoss
            = "但温度一高，液体也会蒸发，一个不注意，污染物的占比反而可能升高。";

        internal const string FilterLossDetail
            = "水分、酒精这些成分也会跟着损失。";

        internal const string FilterLossControl
            = "所以温度得卡得很准。";

        internal const string FilterSimple
            = "我平时懒得弄这么复杂。";

        internal const string FilterSimpleAction
            = "常温下多操作几次来过滤，够我用了。";

        internal const string FilterSpeed
            = "——我还听说流速对净化也有影响。";

        internal const string FilterSpeedUnknown
            = "但具体影响什么，我没研究出来。";

        internal const string Emitter
            = "流源发射机";

        internal const string Filter
            = "净化装置";

        internal const string NoDemo
            = "不用演示";

        internal const string AllKnown
            = "都知道了";

        internal const string FirstVisitDiscount
            = "第一次见面，给你个实在价。";

        internal const string DiscountSameFunction
            = "价格更低，功能和新品一样。";

        internal const string DiscountOldLook
            = "只是外观旧了点。";

        internal const string DeviceSoundHint
            = "每个装置生效时，都会有一声音效。";

        internal const string NoSoundHint
            = "没听到声音，就说明它没对“流源”起作用。";
    }
}
