namespace FluxForge.Traits
{
    /// <summary>
    /// 官方 StartingPerk 的 Mod 声明。
    ///
    /// 新增特质只需在 Traits 程序集里写一个 public sealed 实现；
    /// TraitLayer 启动时会自动发现并注册，不需要改注册表。
    /// </summary>
    public interface IStartingPerkTrait
    {
        string Id { get; }
        int Cost { get; }
        StartingPerk.StartingPerkType Type { get; }
        string Name { get; }
        string Description { get; }
        string IconKey { get; }

        /// <summary>新游戏开始时，且该特质被选中时调用。</summary>
        void Apply();
    }
}
