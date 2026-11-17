using System.Collections.Generic;

namespace FluxForge.Story.Hospital
{
    /// <summary>医院常规收货的品类、最低浓度和基础分配置；专项任务与文案另行维护。</summary>
    internal static class HospitalContractDefinition
    {
        private static readonly ChemicalRequest[] Requests =
        {
            new ChemicalRequest(FlowProps.Acid),
            new ChemicalRequest(FlowProps.Base),
            new ChemicalRequest(FlowProps.Alcohol, 0.90d, 0.8d),
        };

        private static readonly ChemicalRequest[] CompletedRequests =
        {
            Requests[0], Requests[1], Requests[2],
            // 纯液权重为3；基础分5/3使最终得分恰为每mL 5分。
            new ChemicalRequest(FlowProps.Synaprest, 1d, 5d / 3d),
        };

        internal static IReadOnlyList<ChemicalRequest> Chemicals
        {
            get { return HospitalSynaprestState.Completed ? CompletedRequests : Requests; }
        }
    }
}
