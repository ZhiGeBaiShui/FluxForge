using MelonLoader;

[assembly: MelonInfo(typeof(ProbablyBagMod.BagMod), "ProbablyBagMod", "0.1.0", "ProbablyBagMod")]
[assembly: MelonGame(null, null)]

namespace ProbablyBagMod
{
    /// <summary>
    /// Mod 入口。当前阶段只做物品背包属性的定义与注册，
    /// 具体行为后续在此挂接。
    /// </summary>
    public class BagMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("ProbablyBagMod 已加载。");
        }
    }
}
