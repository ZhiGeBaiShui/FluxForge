namespace ProbablyBagMod
{
    /// <summary>
    /// 有「水分 / 含量」的装置。
    ///
    /// 按**属性**而不是按装置类型拆接口：没有这个量的装置就不会有这个成员，
    /// 映射层（<c>FlowTagMap</c>）也不需要为每种装置写分支。
    ///
    /// 有 set 是因为读档接管时要把 tag 里的值写回装置。
    /// </summary>
    public interface IMoistureState
    {
        double Moisture { get; set; }
    }
}
