using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 原版 22 条容器反应 + 环境降温的纯逻辑移植验证。
    ///
    /// 这里只验证公式和 Solver 的自动发现；不涉及 GameItem / tag / Unity。
    /// </summary>
    public class ReactionRuleTests
    {
        [Fact]
        public void SolverDiscoversAllRulesInOriginalOrder()
        {
            Type[] ruleTypes = typeof(FlowReactionSolver).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(FlowReactionRule).IsAssignableFrom(type))
                .ToArray();

            Assert.Equal(23, ruleTypes.Length);

            FieldInfo rulesField = typeof(FlowReactionSolver).GetField(
                "Rules", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(rulesField);

            var rules = (List<FlowReactionRule>)rulesField.GetValue(null);
            Assert.Equal(23, rules.Count);
            Assert.Equal(
                Enumerable.Range(1, 23).Select(index => index * 10),
                rules.Select(rule => rule.Order));
        }

        [Fact]
        public void SolverCanReactAnEmptyFlowWithoutThrowing()
        {
            FlowReactionSolver.React(new FlowProperties());
        }

        [Fact]
        public void AcidBaseNeutralizationConsumesBothWithoutSaltProduct()
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Acid, 5000d);
            properties.Set(FlowProps.Base, 5000d);

            new AcidBaseNeutralizationRule().React(properties);

            // min(acid, base) = 5000；max(1000, 10%) = 1000。
            Assert.Equal(4000d, properties.Get(FlowProps.Acid));
            Assert.Equal(4000d, properties.Get(FlowProps.Base));
            Assert.Equal(0d, properties.Get(FlowProps.Mineral));
            Assert.Equal(0d, properties.Get(FlowProps.Debris));
        }

        [Fact]
        public void BoilingUsesTheSparseTableOnly()
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Temperature, 101d);
            properties.Set(FlowProps.Water, 10000d);
            properties.Set(FlowProps.Alcohol, 1000d);
            properties.Set(FlowProps.LipobondAgent, 1000d);
            properties.Set(FlowProps.Fat, 1000d);

            new BoilingRule().React(properties);

            // 101 度时三者过热度不同（沸点处 2%，每高 20 度翻倍）：
            //   水 过热度 1.00  → 2.07%，10000 掉 207
            //   酒精 过热度 22.63 → 4.38%，1000 算出 43 → 被 100µL 下限抬到 100
            //   lipobond 过热度 46.00 → 9.85%，1000 算出 98 → 同样被抬到 100
            // 脂肪不在 FlowBoilingTable 里，不蒸发。
            Assert.Equal(9793d, properties.Get(FlowProps.Water));
            Assert.Equal(900d, properties.Get(FlowProps.Alcohol));
            Assert.Equal(900d, properties.Get(FlowProps.LipobondAgent));
            Assert.Equal(1000d, properties.Get(FlowProps.Fat));
        }

        [Fact]
        public void HigherTemperatureEvaporatesFaster()
        {
            FlowProperties nearBoiling = new FlowProperties();
            nearBoiling.Set(FlowProps.Temperature, 101d);
            nearBoiling.Set(FlowProps.Water, 10000d);

            FlowProperties farAboveBoiling = new FlowProperties();
            farAboveBoiling.Set(FlowProps.Temperature, 141d);
            farAboveBoiling.Set(FlowProps.Water, 10000d);

            new BoilingRule().React(nearBoiling);
            new BoilingRule().React(farAboveBoiling);

            double nearRemoved = 10000d - nearBoiling.Get(FlowProps.Water);
            double farRemoved = 10000d - farAboveBoiling.Get(FlowProps.Water);

            Assert.True(farRemoved > nearRemoved);
            // 过热度 1 度 vs 41 度：2^2 = 4 倍左右。
            Assert.Equal(4d, farRemoved / nearRemoved, 1);
        }

        [Fact]
        public void DreamCapMushDegradationProducesPlantMatter()
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Temperature, 60d);
            properties.Set(FlowProps.DreamCapMush, 1000d);

            new DreamCapMushDegradationRule().React(properties);

            Assert.Equal(900d, properties.Get(FlowProps.DreamCapMush));
            Assert.Equal(100d, properties.Get(FlowProps.PlantMatter));
        }

        [Fact]
        public void AmbientTemperatureCoolsHotFlowAndLargeVolumeCoolsSlower()
        {
            FlowProperties small = new FlowProperties();
            small.Set(FlowProps.Temperature, 100d);
            small.Set(FlowProps.Water, 100d);

            FlowProperties large = new FlowProperties();
            large.Set(FlowProps.Temperature, 100d);
            large.Set(FlowProps.Water, 10000d);

            AmbientTemperatureRule rule = new AmbientTemperatureRule();
            rule.React(small);
            rule.React(large);

            Assert.InRange(small.Get(FlowProps.Temperature), 26d, 99.999d);
            Assert.InRange(large.Get(FlowProps.Temperature), 26d, 99.999d);
            Assert.True(small.Get(FlowProps.Temperature) < large.Get(FlowProps.Temperature));
        }

        [Fact]
        public void AmbientTemperatureLeavesAmbientFlowUnchanged()
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Temperature, 26d);
            properties.Set(FlowProps.Water, 1000d);

            new AmbientTemperatureRule().React(properties);

            Assert.Equal(26d, properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void SolverRunsMultipleRulesInTheOriginalOrder()
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Temperature, 101d);
            properties.Set(FlowProps.Water, 10000d);
            properties.Set(FlowProps.Acid, 5000d);
            properties.Set(FlowProps.Base, 5000d);

            FlowReactionSolver.React(properties);

            // 101 度：水过热度 1 度 → 2.07%，10000 去掉 207。
            Assert.Equal(9793d, properties.Get(FlowProps.Water));
            Assert.Equal(4000d, properties.Get(FlowProps.Acid));
            Assert.Equal(4000d, properties.Get(FlowProps.Base));
        }
    }
}
