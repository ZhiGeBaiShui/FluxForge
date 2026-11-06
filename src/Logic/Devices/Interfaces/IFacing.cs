namespace FluxForge
{
    /// <summary>
    /// 装置朝向能力：0=上 1=右 2=下 3=左，另有独立的镜像位。
    ///
    /// 原版有 8 个状态：4 个 rotation × flipped 镜像。
    /// 镜像不是旋转 180°；必须单独保留，否则翻转后的装置会按未翻转的通道工作。
    ///
    /// 不由装置自己从 Unity 旋转读取；物品进入网格时由 Mod 层按
    /// <c>modifiedShape.orientation</c> / <c>modifiedShape.flipped</c> 写入。
    ///
    /// 路由装置、反应装置都可以实现本接口；不需要朝向的装置不实现。
    /// </summary>
    public interface IFacing
    {
        int Facing { get; set; }

        bool Flipped { get; set; }
    }
}
