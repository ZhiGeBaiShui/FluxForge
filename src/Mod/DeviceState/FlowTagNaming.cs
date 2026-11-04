using System;

namespace FluxForge.Mod
{
    /// <summary>流源属性 id → 原版 tag 名的唯一命名规则。</summary>
    public static class FlowTagNaming
    {
        public const string Water = "CURRENT_PART_WATER";
        public const string ContainerTemperature = "CONTAINER_TEMP_TAG";

        public static string ForProperty(string id, FlowPropertyKind kind)
        {
            return kind == FlowPropertyKind.Liquid
                ? "CURRENT_PART_" + id.ToUpperInvariant()
                : id.ToUpperInvariant() + "_TAG";
        }
    }
}
