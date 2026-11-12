using System.Collections.Generic;

namespace FluxForge.Story.Hospital
{
    /// <summary>当前医院合同接受的化学物；以后新增品类只改这里。</summary>
    internal static class HospitalContractDefinition
    {
        private static readonly ChemicalRequest[] Requests =
        {
            new ChemicalRequest(FlowProps.Acid),
            new ChemicalRequest(FlowProps.Base),
            new ChemicalRequest(FlowProps.Alcohol, 0.90d, 0.8d),
        };

        internal static IReadOnlyList<ChemicalRequest> Chemicals
        {
            get { return Requests; }
        }
    }
}
