# Logic 反应分层

纯 C# 反应相关代码按**触发方式**分目录。

```text
Reactions/
├─ Realtime/     # 每个流源按自己的等待时间持续检查的反应
└─ Formulas/     # 尚未接入触发器的纯反应公式
```

## Realtime

```text
Core/FlowReactionRule.cs    反应规则基类
Core/FlowReactionSolver.cs  扫描全部规则并顺序执行
```

`FlowReactionSolver` 在静态初始化时扫描本程序集，自动收集所有具体的
`FlowReactionRule` 子类，按 `Order` 排序。新增实时规则只需要：

```csharp
public sealed class XxxRule : FlowReactionRule
{
    public override int Order => 100;

    public override void React(FlowProperties properties)
    {
        // 条件判断 + 修改 properties
    }
}
```

不需要修改 Solver。

当前 Realtime/Rules 为原版 `HandleTemperatureReaction` 的 22 条移植，执行顺序：

```text
10 MicrobeHeat
20 MicrobeAlcohol
30 OrganicWasteHeat
40 DreamCapMushReaction
50 NightmareCapMushReaction
60 CoffeeBeanSolventExtraction
70 CoffeeBeanWaterExtraction
80 Nomore
90 DreamCapExtractDegradation
100 DreamCapMushDegradation
110 NightmareCapExtractDegradation
120 NightmareCapMushDegradation
130 OrganicWasteMineralization
140 AcidBaseMicrobeKill
150 StabilizingAgentSedimentation
160 ProteinPrecipitation
170 LipobondAgentSedimentation
180 AcidBaseNeutralization
190 FantaHeat
200 MethylolHeatReaction
210 Chlorine
220 Boiling
230 AmbientTemperature（环境降温，不是化学反应，最后执行）
```

`BoilingRule` 只读稀疏表 `FlowBoilingTable`：

```text
water 100.0
alcohol 78.37
lipobond_agent 55.0
```

没有登记的液体不会蒸发。

## Formulas

```text
TemperatureChangeReaction
FilterReaction
```

`TemperatureChangeReaction` 由装置传入 `delta`，`FilterReaction` 由过滤装置传入
`properties + speed`；两者都不继承 `FlowReactionRule`，不会被实时 Solver 自动发现。

原版三条萃取反应已经完整实现在 `Realtime/Rules` 的
`04_DreamCapMushReactionRule`、`05_NightmareCapMushReactionRule`、
`06_CoffeeBeanSolventExtractionRule`、`07_CoffeeBeanWaterExtractionRule`
中；旧的简化一次性实现已删除。

## 与 Devices 的边界

```text
Realtime → 不依赖装置、网格、Unity
Formulas → 只依赖 FlowProperties，供后续接入任一触发层
```

触碰式装置（`IFlowDevice` / `IceBlockDevice`）仍然在 `src/Logic/Devices/`，
不搬到 Reactions；它们的调用由 `FlowManager` 在流源经过装置格时发起。

所有文件都在 `FluxForge` 命名空间；目录只表达职责，不改变类型名。
