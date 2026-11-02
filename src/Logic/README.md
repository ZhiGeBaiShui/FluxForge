# 如何添加一个属性

属性 = 流源携带的一种数值。液体成分如 `water`，标量如 `container_temp`。

## 一、最简操作

只改两个文件。

### 1. 登记属性：`src/Logic/Flow/FlowPropertyRegistry.cs`

打开 `Build()`：

- **加液体**：把 id 加进 `liquids` 数组，位置跟游戏 `Liquid.InitLiquid()` 的顺序一致。
- **加标量**：在 `defs` 末尾加一行；数组长度和槽位号也要跟着 +1。

```csharp
// 液体
string[] liquids = { "water", /* … */ "new_liquid" };

// 标量
defs[liquids.Length + 1] = new FlowPropertyDef(
    "new_scalar", FlowPropertyKind.Scalar,
    SplitRule.Keep, MergeRule.WeightedAverage, liquids.Length + 2);
```

### 2. 加一个 Key：`src/Logic/Flow/FlowProps.cs`

在文件末尾加一行（按你加的属性选对应那行），字段名是 id 的 PascalCase：

```csharp
public static readonly FlowPropertyKey NewLiquid = FlowPropertyRegistry.Key("new_liquid");
public static readonly FlowPropertyKey NewScalar = FlowPropertyRegistry.Key("new_scalar");
```

### 3. 标量额外确认 tag

标量的 tag 名固定是 `<ID大写>_TAG`（例如 `new_scalar` → `NEW_SCALAR_TAG`）。
这个 tag 必须是游戏本来就有的；不是的话见第三节。

### 4. 跑测试

```powershell
dotnet test tests\FluxForge.Tests\FluxForge.Tests.csproj -c Release
```

测试里有硬编码的数量/顺序断言，需要同步：

- `Count_IsThirtySeven`
- `All_HasThirtySixLiquidsAndOneScalar`
- `Order_MatchesGameLiquidInit_AndEndsWithTemperature`
- `FlowProps_FieldNamesMatchRegistryIds`

需要显示时，再到 `assets/items.json` 的 `statusLines` 加 `"valueType": "double"`。

## 二、每一步为什么

1. **为什么在 `FlowPropertyRegistry` 加**
   它是唯一的属性清单：`FlowProperties` 按 `Count` 分配数组、按顺序定槽位，`FlowTagMap` 也遍历它生成 tag。只改这一处，不用再到别处维护第二份清单。

2. **为什么顺序不能乱**
   顺序就是槽位，槽位从 1 开始（`Key.Index - 1` 才是数组下标）。在中间插入会让后面属性的槽位号整体后移，所以代码里一律用 `FlowProps` / `Registry.Key(id)`，不要写死数字下标。

3. **为什么还要加 `FlowProps`**
   逻辑代码靠具名 Key 读写属性。测试会检查 `FlowProps` 的字段数、字段名与注册表一一对应，漏加会直接失败。

4. **为什么测试要改**
   当前测试把「37 条」「36 液体 + 1 标量」「温度在第 37 槽」都写死了；加属性后这些数字必然变化，不改测试就会误报。

5. **为什么液体和标量的规则不一样**
   规则由 `Kind` 决定：液体是物质，分流 `Divide`、汇聚 `Add`，计入 `SubstanceTotal`；标量不是物质，分流 `Keep`、汇聚按物质的量加权平均。`Build()` 按 Kind 统一发放，不单独写规则。

6. **为什么 tag 名不用手写**
   `FlowTagMap` 按固定规则自动生成：

   - 液体：`CURRENT_PART_<ID大写>`，int，单位 µL。原版液体系统按这个名字读，**前缀不能换**。
   - 标量：`<ID大写>_TAG`，double。

   名字对上了，`FlowStock` 才能把物品上的 tag 和属性正确对应起来。

## 三、非游戏属性（未实现）

上面能加的只是游戏本来就有的属性（36 种液体 + `container_temp`）。

如果要加游戏没有的属性，光登记不够：得先把它注册进原版游戏的注册表（液体走 `Liquid` 注册表，标量走原版 tag 的注册/初始化路径），否则 `FlowTagMap` 会生成一个游戏不认的孤立 tag，读档和显示都不可控。

**这部分目前没有实现**，仓库里没有注册代码；在此之前请只使用游戏已有属性。

## 四、总结

- 新增属性 = `FlowPropertyRegistry.Build()` 登记一行 + `FlowProps` 加一个 Key。液体加进 `liquids` 数组，标量加一条 `FlowPropertyDef`。
- tag 名自动生成，`CURRENT_PART_` 前缀 / `_TAG` 后缀是固定约定，不要手写。
- 液体和标量的分合规则由 `Kind` 决定，Logic 层不碰游戏类型。
- 游戏没有的 tag，要先注册原版注册表；当前未实现。
