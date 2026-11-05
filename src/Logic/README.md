# Logic 模块

## 要做什么

### 新增一个流源属性

1. 在 `src/Logic/Flow/FlowPropertyRegistry.cs` 的 `Build()` 里登记：
   - 液体：加进 `liquids` 数组，位置必须与游戏 `Liquid.InitLiquid()` 的登记顺序一致。
   - 标量：扩容 `defs`，在末尾追加 `FlowPropertyDef`；槽位号按「下标 + 1」写。
2. 在 `src/Logic/Flow/FlowProps.cs` 末尾加具名 Key，字段名用 id 的 PascalCase：

   ```csharp
   public static readonly FlowPropertyKey NewLiquid = FlowPropertyRegistry.Key("new_liquid");
   public static readonly FlowPropertyKey NewScalar = FlowPropertyRegistry.Key("new_scalar");
   ```

3. 标量的 tag 名由 `FlowTagNaming` 自动生成为 `<ID大写>_TAG`；只能用游戏已有的 tag。游戏没有的属性当前不支持，必须先注册原版注册表（尚未实现）。
4. 需要显示时，在 `assets/items.json` 的 `statusLines` 里加 `tag`；double 值加 `"valueType": "double"`。
5. 同步测试并运行：

   ```powershell
   dotnet test tests\FluxForge.Tests\FluxForge.Tests.csproj -c Release
   ```

   至少同步 `Count_IsThirtySeven`、`All_HasThirtySixLiquidsAndOneScalar`、`Order_MatchesGameLiquidInit_AndEndsWithTemperature`、`FlowProps_FieldNamesMatchRegistryIds` 里的数量与顺序断言。

### 新增装置、反应或表现

| 要加什么 | 放哪 | 下一步 |
|---|---|---|
| 路由 / 反应装置 | `src/Logic/Devices/` | 按 [`Devices/README.md`](Devices/README.md) 写类，再到 Mod 层加适配器 |
| 实时反应规则 | `src/Logic/Reactions/Realtime/Rules/` | 按 [`Reactions/README.md`](Reactions/README.md) 继承 `FlowReactionRule` |
| 纯反应公式 | `src/Logic/Reactions/Formulas/` | 写成静态方法，由装置显式调用 |
| 表现事件 | `src/Logic/Presentation/` | 只发语义 ID（`FlowAudio.Play` / `FlowAnimation.Play`），不碰 Unity |

### 保持分层

`src/Logic` 和 `src/Presentation` 都是纯 C#。写这两层时不要引用 `UnityEngine`、`Assembly-CSharp`、`HarmonyLib`，也不要出现 `GameItem` / `TagState`；游戏类型属于 `src/Mod`。

## 常用 API

| API | 提供 |
|---|---|
| `FlowPropertyRegistry.All` / `Count` / `Get` / `Key` | 属性清单、稳定 id 与槽位 |
| `FlowProps` | 具名 `FlowPropertyKey`；逻辑代码只用它，不写死数字下标 |
| `FlowProperties.Get` / `Set` | 按 Key 读写一份属性 |
| `FlowProperties.Split` / `Merge` / `MergeFrom` / `SubstanceTotal` / `Clone` | 守恒分合、物质总量与深拷贝 |
| `FlowEntity` | `Position` / `Motion` / `Properties` / `NextReactionTime` |
| `FlowManager.Tick` / `IFlowGrid` | 每帧推进与「某格有什么装置」的查询边界 |
| `FlowSpawner.Accept` / `Kill` / `KillAll` / `Active` | 流源名单与生死广播 |
| `FlowAudio.Play` / `FlowAnimation.Play` | 语义表现事件，由 Mod 层订阅 |
| `FlowReactionSolver.React` | 执行全部实时反应规则 |

## 为什么 / 不做会怎样

- 顺序即槽位。新增液体必须按游戏 `Liquid.InitLiquid()` 的位置放；插错或重排旧项会让后续槽位、tag 和存档值错位。
- 漏加 `FlowProps` 或不同步测试，编译或测试会直接失败，逻辑代码也没有 Key 可用。
- Logic 引用游戏或 Unity 类型后，`tests\FluxForge.Tests` 无法脱离游戏编译，分层和可测性一起丢失。
- tag 名是固定协议：液体 `CURRENT_PART_<ID>`（int，µL），标量 `<ID>_TAG`（double）。手写错前缀或后缀，游戏读不到或写不进。
- 游戏里不存在的属性没有原版注册表，强行加只会生成孤立 tag，读档和 tooltip 都不可控。
