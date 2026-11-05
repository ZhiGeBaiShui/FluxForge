# Logic 反应

## 要做什么

### 新增一条实时规则

1. 在 `src/Logic/Reactions/Realtime/Rules/` 新建 `NN_XxxRule.cs`。类必须 `public sealed`，继承 `FlowReactionRule`，并提供 public 无参构造。
2. 实现顺序和反应：

   ```csharp
   public sealed class XxxRule : FlowReactionRule
   {
       public override int Order => 25;

       public override void React(FlowProperties properties)
       {
           if (!条件)
           {
               return;
           }

           properties.Set(FlowProps.Xxx, 新值);
       }
   }
   ```

   `Order` 小的先执行；同值按类型全名排序。新规则插在已有编号之间（如 15、25），不要改旧值；需要最后执行才用大于 230 的编号。
3. 不需要登记到 `FlowReactionSolver`。它在静态初始化时扫描本程序集，自动收集全部具体 `FlowReactionRule` 子类；抽象类会被跳过，具体类必须有无参构造，构造失败会在启动时抛异常。
4. 用 `FlowProps` 具名 Key 读写；需要原版 int 语义时用 `FlowReactionMath`。规则不引用装置、网格或 Unity。
5. 补测试并运行：

   ```powershell
   dotnet test tests\FluxForge.Tests\FluxForge.Tests.csproj -c Release
   ```

现有规则 Order 从 10 到 230；`220` 是 `BoilingRule`，`230` 是最后执行的环境降温 `AmbientTemperatureRule`。

### 新增一条纯公式

1. 在 `src/Logic/Reactions/Formulas/` 新建静态类。
2. 方法只接收 `FlowProperties` 和本公式需要的参数（如 `speed`、`delta`），原地修改属性。
3. 由装置或其它调用方显式调用；`FlowReactionSolver` 不会发现公式。
4. 需要每个流源按等待时间自动执行时，改写成 `FlowReactionRule` 放 `Realtime/Rules/`。

### 新增可蒸发液体

在 `src/Logic/Reactions/Realtime/Core/FlowBoilingTable.cs` 的 `_entries` 里加 `FlowProps.Xxx` 和沸点；没有登记的液体不会蒸发。当前只登记了 `water`、`alcohol`、`lipobond_agent`。

## 常用 API

| API | 提供 |
|---|---|
| `FlowReactionRule.Order` / `React(FlowProperties)` | 实时规则的执行顺序与反应逻辑 |
| `FlowReactionSolver.React(FlowProperties)` | 按 Order 执行全部自动发现的规则 |
| `FlowBoilingTable.Entries` | 液体 → 沸点的稀疏表；不在表里 = 不蒸发 |
| `FlowReactionMath.GetInt` / `TotalInt` / `SetInt` / `Round` / `Ceiling` / `Min` / `Max` | 移植原版 int µL 语义 |
| `FilterReaction.React(FlowProperties, double speed)` | 按温度和速度去除化学污染与重金属 |
| `FilterReaction.ContaminantFloorAt(double temperature)` | 当前温度下的污染占比下限 |
| `FilterReaction.RemovalFractionAt(double speed)` | 单次过滤比例 |
| `TemperatureChangeReaction.React(FlowProperties, double delta)` | 施加一次温度增减 |
| `BoilingRule.EvaporationRateAt(double temperature, double boilingPoint)` | 过热度对应的每轮蒸发比例 |

## 为什么 / 不做会怎样

- 规则没有 public 无参构造，`FlowReactionSolver` 会在静态初始化阶段抛异常，整个 mod 起不来。
- 规则里不判断条件就直接改属性，所有流源每轮都会被执行，属性被反复改写；条件不满足时必须直接返回。
- 新规则改了已有 `Order`，所有依赖顺序的反应可能一起错位；只在空位插入。
- 公式不会被自动执行。只放进 `Formulas/` 而没有调用方，等于没有实现。
- 新液体不登记 `FlowBoilingTable`，温度再高也不会蒸发；表里没有的项默认「不蒸发」。
- 规则引用 `GameItem` / Unity 或写 tag，会破坏 Logic 的纯 C# 边界，测试也无法覆盖。
