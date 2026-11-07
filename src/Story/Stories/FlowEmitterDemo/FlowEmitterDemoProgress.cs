using FluxForge.Mod;

namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>记录本剧情里已经看过的演示，不负责剧情是否首次。</summary>
    internal static class FlowEmitterDemoProgress
    {
        internal const string EmitterDemoKey
            = "fluxforge.flowEmitterDemo.emitterDemo";

        internal const string FilterDemoKey
            = "fluxforge.flowEmitterDemo.filterDemo";

        internal static void MarkEmitterDemo()
        {
            PlayerStore.Instance.modData[EmitterDemoKey] = "1";
        }

        internal static void MarkFilterDemo()
        {
            PlayerStore.Instance.modData[FilterDemoKey] = "1";
        }

        internal static bool HasEmitterDemo
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    EmitterDemoKey);
            }
        }

        internal static bool HasFilterDemo
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    FilterDemoKey);
            }
        }
    }
}
