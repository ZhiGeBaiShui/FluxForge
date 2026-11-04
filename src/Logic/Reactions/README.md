# Logic 反应分层

纯 C# 反应相关代码按**触发方式**分目录。

```text
Reactions/
├─ Realtime/     # 每个流源按自己的等待时间持续检查的反应
└─ Formulas/     # 尚未接入触发器的纯反应公式
```

## Realtime

```text
FlowReactionRule.cs    反应规则基类
FlowReactionSolver.cs  扫描全部规则并顺序执行
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

## Formulas

```text
CoffeeBeanSolventExtractionReaction
DreamCapExtractionReaction
NightmareCapExtractionReaction
TemperatureChangeReaction
```

这些是纯反应公式，目前没有接入实时 Solver 或装置。接入前只作为
测试中的计算函数存在，不代表游戏内已生效。`TemperatureChangeReaction`
需要由装置传入 `delta`，因此保留两个参数的签名。

## 与 Devices 的边界

```text
Realtime → 不依赖装置、网格、Unity
Formulas → 只依赖 FlowProperties，供后续接入任一触发层
```

触碰式装置（`IFlowDevice` / `IceBlockDevice`）仍然在 `src/Logic/Devices/`，
不搬到 Reactions；它们的调用由 `FlowManager` 在流源经过装置格时发起。

所有文件都在 `FluxForge` 命名空间；目录只表达职责，不改变类型名。
