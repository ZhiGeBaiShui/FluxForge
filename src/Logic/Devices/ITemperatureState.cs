namespace ProbablyBagMod
{
    /// <summary>
    /// 有「温度」的装置。
    ///
    /// 与 <see cref="IMoistureState"/> 分开：只关心温度的装置不必多背一个湿度成员。
    /// </summary>
    public interface ITemperatureState
    {
        double Temperature { get; set; }
    }
}
