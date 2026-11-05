# FluxForge

《Probably Stolen》0.46 的 Mono mod。
当前提供流源属性、装置、发射器、收集器和基础视觉层。

> 状态：Logic / Presentation 与装置适配器有单元测试；游戏生命周期和 Harmony patch 需要进游戏验证；视觉层仍是实验性内容。

## 1. 先做什么

### 只想安装来玩

```text
1. 完全退出游戏
2. 构建 Release
3. 把 FluxForge.dll、manifest.xml、Textures/ 复制到
   <游戏目录>/Mods/FluxForge/
4. 启动游戏，确认 Mods 菜单中 FluxForge 已启用
```

构建命令见 §5。安装后如果装置不出现，先看 F8 控制台和 `MelonLoader/Latest.log`。

### 想新增一个装置

按这个顺序做：

```text
1. Logic：在 src/Logic/Devices/ 写装置类
2. Mod：在 src/Mod/DeviceState/Adapters/ 写适配器，加 [DeviceAdapter("key")]
3. 数据：在 assets/items.json 声明 device / anchor / attachments
4. 贴图：在 assets/sprites.json 登记，PNG 放到 mod/Textures/Items/
5. 测试：补装置类与适配器测试，进游戏验证读档和拖拽
```

对应文件：

| 要改什么 | 文件 |
|---|---|
| 装置行为与状态 | `src/Logic/Devices/<分类>/<名称>/` |
| 状态 ↔ tag | `src/Mod/DeviceState/Adapters/<名称>StateAdapter.cs` |
| 物品定义 | `assets/items.json` |
| 贴图定义 | `assets/sprites.json` + `mod/Textures/Items/*.png` |
| 目录契约测试 | `tests/FluxForge.Tests/DeviceCatalogTests.cs` |

### 想新增液体 / 标量属性

只改 Logic 的注册表和 `FlowProps`：

```text
1. src/Logic/Flow/FlowPropertyRegistry.cs
2. src/Logic/Flow/FlowProps.cs
3. 同步测试里的属性数量与顺序断言
4. 需要显示时再改 assets/items.json 的 statusLines
```

完整步骤见 [`src/Logic/README.md`](src/Logic/README.md)。

### 想新增普通物品或视觉

```text
items.json / sprites.json → 编译期嵌入
PNG → mod/Textures/Items/
行为 → ItemAttachment
视觉 → src/Presentation 与 src/Mod/Visual
```

## 2. 工程结构

```text
src/
  Logic/          纯 C#：属性、装置语义、反应规则
  Presentation/   纯 C#：颜色、粒子、图集等表现计算
  Mod/            游戏适配：Bootstrap、Harmony patch、GameItem/Tag、Unity

tests/
  FluxForge.Tests/   Logic + Presentation + Mod 纯逻辑部分的测试

assets/            items.json / sprites.json 等源数据
mod/               构建时复制的运行时资源
docs/              设计、架构、评审记录
```

### 分层

```text
src/Mod  →  src/Logic
src/Mod  →  src/Presentation
```

反方向不允许：

```text
Logic / Presentation 不引用 UnityEngine
Logic / Presentation 不引用 Assembly-CSharp
```

原因：Logic 和 Presentation 要能脱离游戏跑测试；把游戏类型带进去，测试、移植和阅读都会被破坏。

### 顶级目录职责

| 目录 | 负责 | 不负责 |
|---|---|---|
| `src/Logic` | 流源属性、装置行为、反应规则 | GameItem、tag、Unity、存档 |
| `src/Presentation` | 颜色、粒子、渲染数据 | 游戏对象、生命周期、存档 |
| `src/Mod` | 游戏入口、patch、tag 映射、装置登记 | 业务规则 |
| `tests` | 纯 C# 行为与契约测试 | 游戏内集成验证 |
| `assets` | 物品/贴图源数据 | 运行时加载逻辑 |
| `mod` | 随 DLL 分发的资源 | 源数据编辑 |

## 3. 必须知道的生命周期

这些不是“实现细节”，是新增装置时容易踩的边界。

### 读档

游戏读档时先 `AddParent`，之后才应用最终形状。
所以除了正常的挂载钩子，读档完成还要额外做一次：

```text
ItemDeviceBinding.Bind(item)
Grid.Rebuild()
```

不做会怎样：

```text
装置实例存在，但格表按旧形状登记
→ 发射器/收集器位置错误，读档后需要重新拖动才恢复
```

### 单件拿起 / 批量框选拖拽

游戏拿起物品时不一定调用 `RemoveParent`：

```text
单件：ItemMouseDragHandler.StartDrag
批量：ItemMultiSelectHandler.StartGroupDrag
```

这两种情况都要在“拿起”时先从格表注销。

批量拖拽结束不需要额外补丁：

```text
Expel → RemoveParent → ItemDetached
UncheckedAccept → AddParent → ItemAttached
```

落点会自然恢复登记。

不做会怎样：

```text
物品已经被拿在手上，格表仍认为它在原格
→ 流源继续穿过已经拿起的装置
```

### 外部 tag 变化

游戏或其它系统直接改 `modifiedState` 后，装置不会自动知道。
统一入口是：

```text
ValidateShapeState 后 → DeviceStateReconcilePatch
发射写回后 → FlowRuntime
```

它们只对未脏的 Host 调 `LoadAll()`。

不做会怎样：

```text
tooltip 显示新值，装置仍用旧状态
```

### 装置状态回写

装置变化只标脏，帧末统一：

```text
Changed → DeviceStateSync.MarkDirty
帧末 → host.SaveAll()
有变化 → ItemTagCommit.Commit
```

不要在 `ItemAttachment` 或 patch 里手写 tag 同步。

不做会怎样：

```text
同一帧多次写 tag、漏 SyncModifiedState、tooltip 与存档不一致
```

## 4. 重要公开 API

### Logic

| API | 作用 |
|---|---|
| `FlowProperties` | 一份流源属性值；按 `FlowPropertyKey` 读写 |
| `FlowPropertyRegistry` / `FlowProps` | 属性注册表与具名 Key |
| `FlowEntity` | 运动层传给装置的反应输入 |
| `IFlowRouter` | 装置如何路由流源 |
| `IFlowDevice` | 装置如何反应/改属性 |
| `IFlowNotifier` | 装置状态变化与耗尽通知 |
| `FlowReactionSolver` | 实时反应规则入口 |

### Mod

| API | 作用 |
|---|---|
| `ItemDeviceBinding.Bind(GameItem)` | 确保物品的装置实例存在；重复调用安全 |
| `DeviceHost.Create(...)` | 唯一装置出生路径；不会在 Load 时重建 |
| `DeviceHost.LoadAll()` / `SaveAll()` | 从 tag 回灌 / 回写全部装置 |
| `DeviceStateAdapter` | 单例适配器：`Create` / `Load` / `Save` |
| `DeviceAdapterCatalog` | key → 装置定义；启动扫描 `[DeviceAdapter]` |
| `DeviceRegistry` | 物品 ↔ 装置、Router / Reactor 反查 |
| `FlowStock` | 流源/容器整份属性 ↔ 原生 tag |
| `GameItemTags` | 单 tag 静态读写；读 `modifiedState`，写 `state` |
| `DeviceStateSync` | Changed 标脏、帧末 flush、Exhausted 处理 |

各模块的完整 API 与扩展步骤见：

- [`src/Logic/README.md`](src/Logic/README.md)
- [`src/Logic/Devices/README.md`](src/Logic/Devices/README.md)
- [`src/Mod/README.md`](src/Mod/README.md)
- [`src/Mod/GridBuilding/Lifecycle/README.md`](src/Mod/GridBuilding/Lifecycle/README.md)

## 5. 构建、测试、安装

### 构建

```powershell
dotnet build FluxForge.csproj -c Release -p:GameManagedDir="<Game>/Probably Stolen_Data/Managed"
```

本机示例：

```powershell
dotnet build FluxForge.csproj -c Release -p:GameManagedDir="E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed"
```

产物：

```text
bin/Release/
  FluxForge.dll
  manifest.xml
  Textures/Items/*.png
```

### 测试

```powershell
dotnet test tests\FluxForge.Tests\FluxForge.Tests.csproj -c Release
```

当前基线：**290 / 290**。
测试覆盖 Logic、Presentation、装置适配器、Catalog 和挂载编排；不替代游戏内读档/拖拽验证。

### 安装

```text
1. 完全退出游戏
2. 新建 Mods/FluxForge/
3. 复制 FluxForge.dll、manifest.xml、Textures/
4. 启动游戏
```

启动日志应出现：

```text
[FluxForge]: FluxForge 已加载
[FluxForge]: FluxForge 已启用
[注册] <成功数>/<总数> 个物品
```

### 实机验收

```text
1. 冰块、收集器、发射器 tooltip 数值正常
2. 发射纯水只生成一个流源，读档不复活
3. 冰块融化/耗尽后读档状态一致
4. 单件拖拽、批量框选拖拽后格表正确
5. 排序后格表正确
6. 装置耗尽后不再被 Router / Reactor 查询
7. 普通退档/继续游戏后发射器可用
```

## 6. 约束与后果

| 约束 | 不遵守会怎样 |
|---|---|
| Logic / Presentation 不引用游戏类型 | 单测无法编译，逻辑与游戏绑定 |
| 一个装置一个 key，`device` 同时是持久化 tag | 改名等于换物品，旧存档状态丢失 |
| 装置状态走 Adapter / DeviceHost | 手写同步容易重复写、漏 SyncModifiedState |
| 不在 Load 中重建装置 | 音频/动画/反查引用失效 |
| 批量拖拽只在 StartGroupDrag 注销 | 结束再补会重复处理，落点已由 Add/Remove 覆盖 |
| `manifest.xml` 保持 `<Capabilities />` | 引入 Expression.Compile 或计算 Type 反射会触发权限拒绝 |
| tag 读 `modifiedState`、写 `state` | 读到旧值或写完不生效 |
| 液体单位是 µL | 与游戏原版协议不一致，数值差 1000 倍 |

## 7. 许可

见仓库许可文件。FluxForge 不是游戏官方项目。
