using System;

namespace FluxForge.Mod
{
    /// <summary>流源属性 id → 原版 tag 名的唯一命名规则。</summary>
    public static class FlowTagNaming
    {
        public const string Water = "CURRENT_PART_WATER";
        public const string ContainerTemperature = "CONTAINER_TEMP_TAG";

        /// <summary>外来液体聚合桶的 tag；不会和真实液体 ID 冲突。</summary>
        public const string ForeignLiquid = "FLUXFORGE_FOREIGN_LIQUID";

        public static string ForProperty(string id, FlowPropertyKind kind)
        {
            if (kind == FlowPropertyKind.Scalar)
            {
                return id.ToUpperInvariant() + "_TAG";
            }

            if (kind == FlowPropertyKind.Foreign)
            {
                return ForeignLiquid;
            }

            return "CURRENT_PART_" + id.ToUpperInvariant();
        }
    }
}
