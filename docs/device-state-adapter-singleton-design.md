# 装置适配器单例化与 Create/Load 迁移设计

> 状态：**已实施**（2026-11-05）
> 基线：commit `2a4c2fa`；实施后 build 0 warning / 0 error，test 290 / 290

## 1. 结论

后续采用：

```text
key → 无状态 Adapter 单例
Adapter.Create(GameItem)   → 完整初始化的 Device
Adapter.Load(GameItem, device) → 已有 Device 更新
Adapter.Save(GameItem, device) → Device 写 tag
```

`DeviceHost` 保存：

```text
Key + Device + Adapter
```

适配器不持有 `GameItem`、`Device`、`DeviceHost`。

## 2. 已确认的设计目标

```text
适配器单例化
创建装置由适配器执行
Load / Save 的字段映射由适配器执行
删除 ITagStore / GameItemTagStore / FakeTagStore
使用 GameItem + 静态 tag 工具
Host 保存 device key 和 device 实例
不重新创建已有装置
```

说明：游戏类型 `TagState` 不能删除；删除的是 Mod 自己的 `ITagStore` / `GameItemTagStore` 封装。

## 3. 目标契约

### 3.1 静态 tag 工具

```csharp
public static class GameItemTags
{
    public static int ReadInt(GameItem item, string tag);
    public static double ReadDouble(GameItem item, string tag);

    public static bool WriteInt(GameItem item, string tag, int value);
    public static bool WriteDouble(GameItem item, string tag, double value);
}
```

语义与当前 `GameItemTagStore` 相同：

```text
读 item.modifiedState，缺失为 0
写 item.state
值相同 → false
tag 不存在且值为 0 → 不创建
否则 GetTag / Enable / Set → true
```

### 3.2 Adapter 基类

```csharp
public abstract class DeviceStateAdapter
{
    public abstract object Create(GameItem item);
    public abstract void Load(GameItem item, object device);
    public abstract bool Save(GameItem item, object device);
}

public abstract class DeviceStateAdapter<TDevice> : DeviceStateAdapter
    where TDevice : class
{
    protected abstract TDevice CreateDevice(GameItem item);
    protected abstract void LoadDevice(GameItem item, TDevice device);
    protected abstract bool SaveDevice(GameItem item, TDevice device);

    public sealed override object Create(GameItem item)
    {
        return CreateDevice(item);
    }

    public sealed override void Load(GameItem item, object device)
    {
        LoadDevice(item, (TDevice)device);
    }

    public sealed override bool Save(GameItem item, object device)
    {
        return SaveDevice(item, (TDevice)device);
    }
}
```

约束：

```text
具体 Adapter 无实例字段
public 无参构造
只做映射，不保存 GameItem / Device
```

### 3.3 Catalog

```csharp
public sealed class DeviceDefinition
{
    public string Key;
    public DeviceStateAdapter Adapter;
}

public static class DeviceAdapterCatalog
{
    public static IReadOnlyCollection<string> Keys;
    public static bool TryGet(string key, out DeviceDefinition definition);
    public static DeviceDefinition Get(string key);
    public static List<DeviceDefinition> FindFor(GameItem item);
}
```

启动扫描：

```text
[DeviceAdapter(key)]
  → 装置类型由 Adapter 泛型确定
  → 校验 Adapter 无实例字段
  → 实例化一个 Adapter 单例
  → key → DeviceDefinition
```

不再有：

```text
CreateDevice
CreateAdapter
不再保存装置类型字典
```

### 3.4 Host

```csharp
public readonly struct DeviceBinding
{
    public readonly string Key;
    public readonly DeviceStateAdapter Adapter;
    public readonly object Device;
}

public sealed class DeviceHost
{
    public static DeviceHost Create(GameItem item, List<DeviceDefinition> definitions);

    public GameItem Item { get; }
    public IReadOnlyList<DeviceBinding> Bindings { get; }
    public IFlowRouter Router { get; }
    public IFlowDevice Reactor { get; }
    public bool IsDirty { get; set; }

    public void LoadAll();
    public bool SaveAll();

    public void OnDeviceChanged(IFlowNotifier sender);
    public void OnDeviceExhausted(IFlowNotifier sender);
}
```

`DeviceHost.Create`：

```text
for definition in definitions
    device = definition.Adapter.Create(item)
    binding = (key, adapter, device)
```

这是唯一创建装置的位置。

### 3.5 生命周期

```text
Bind:
  已有 Host
    → if (!IsDirty) host.LoadAll()
  无 Host
    → definitions = Catalog.FindFor(item)
    → host = DeviceHost.Create(item, definitions)
    → Registry.Register(host)

外部 tag 变化:
  DeviceStateReconcilePatch
    → if (!host.IsDirty) host.LoadAll()

发射后:
  FlowRuntime.Emit
    → host.LoadAll()

装置 Changed:
  OnDeviceChanged
    → DeviceStateSync.MarkDirty(host)

帧末:
  DeviceStateSync.Flush
    → changed = host.SaveAll()
    → changed ? ItemTagCommit.Commit(item)

Exhausted:
  host.SaveAll()
  Registry.Unregister
  Expel
```

`Load` 只更新已有 device，禁止重新创建。

## 4. Create / Load 的映射去重

每个有状态适配器提取私有读取方法：

```csharp
private readonly struct IceBlockState
{
    public readonly int Water;
    public readonly double Temperature;
}

private static IceBlockState Read(GameItem item)
{
    return new IceBlockState(
        GameItemTags.ReadInt(item, FlowTagNaming.Water),
        GameItemTags.ReadDouble(item, FlowTagNaming.ContainerTemperature));
}
```

`Create`：

```csharp
protected override IceBlockDevice CreateDevice(GameItem item)
{
    IceBlockState state = Read(item);
    return new IceBlockDevice(state.Water, state.Temperature);
}
```

`Load`：

```csharp
protected override void LoadDevice(GameItem item, IceBlockDevice device)
{
    IceBlockState state = Read(item);
    device.Water = state.Water;
    device.Temperature = state.Temperature;
}
```

`Save` 反向写。

Collector：

```text
ReadStock(item) → FlowProperties
Create → new CollectorDevice(stock)
Load → device.ReplaceStock(stock)
Save → 遍历 FlowPropertyTags 写回
```

需要新增：

```csharp
public CollectorDevice(FlowProperties stock)
public void ReplaceStock(FlowProperties stock)
```

Temperature：

```text
Create → new TemperatureDevice(GameItemTags.ReadDouble(...))
Load → device.Temperature = ...
```

需要新增：

```csharp
public TemperatureDevice(double temperature)
```

Filter / 四个分流适配器：

```text
Create → new XxxDevice()
Load → 空
Save → false
```

## 5. 已知风险与约束

1. **不能重建 device**
   - `DeviceRegistry` 按 device 引用反查 item。
   - 音频/动画按 device 引用反查 item。
   - Load 必须只更新已有实例。

2. **不要按 key 二次查找 adapter**
   - `DeviceBinding` 直接保存 `(Key, Adapter, Device)`。
   - Load/Save 使用 binding 里的 Adapter，避免 key/device 不匹配和向下转型错误。

3. **Bind 的 dirty 处理**
   - 当前已有 Host 会无条件 Load。
   - 新设计在 `IsDirty` 时跳过 Load，避免旧 `modifiedState` 覆盖尚未 Flush 的装置状态。

4. **多装置顺序**
   - `FindFor` 返回顺序必须稳定（按 key 排序，或保持目录顺序）。
   - Host 的 Router/Reactor 仍按 Binding 顺序选第一个。

5. **Exhausted 顺序**
   - 保持 `Save → Unregister → Expel`。
   - 不能先注销，否则音频反查失败。

6. **测试**
   - 删除 `ITagStore` / `GameItemTagStore` / `FakeTagStore` 后，适配器直接依赖 `GameItem`，现有 game-free 测试项目不能编译适配器。
   - 需要决定测试策略：
     - 方案 A：测试项目增加最小 `GameItem` / `TagSystem` / `TagState` stub；
     - 方案 B：删除适配器单测，只保留 Logic/Presentation 测试和实机 smoke；
     - 方案 C：新增独立 game-aware 测试项目。
   - 推荐方案 A，保留 tag 映射与 sparse-write 覆盖；stub 只实现 `GameItemTags` 用到的成员。

7. **删除范围**
   - 删除 Mod 的 `ITagStore`、`GameItemTagStore`、`FakeTagStore`。
   - 不删除游戏 `TagState` 类型。

## 6. 迁移顺序

### 阶段 1：静态 tag 工具

```text
新增 GameItemTags
让 GameItemTagStore / FlowStock 暂时委托它
保持 build/test 绿
```

### 阶段 2：Logic 构造函数

```text
IceBlockDevice(water, temperature) 已有
TemperatureDevice(temperature) 新增
CollectorDevice(stock) / ReplaceStock 新增
保留现有无参构造，直到 cutover
```

### 阶段 3：原子切换

```text
Adapter 基类改为 Create/Load/Save
8 个 Adapter 改为无状态单例
Catalog 改为 key → DeviceDefinition
Host 改为 DeviceBinding
ItemDeviceBinding / Registry / Sync 改接线
删除 ITagStore / GameItemTagStore / FakeTagStore
更新测试 csproj 与测试
```

### 阶段 4：文档

```text
docs/device-state-architecture.md
README.md §7.3 / §8.3
src/Mod/README.md
```

## 7. 待确认

```text
D1 测试策略：A stub / B 删除适配器测试 / C game-aware 测试项目
D2 DeviceAdapterAttribute 是否保留 typeof(Device)：
   保留 = 重复但可启动校验；删除 = 泛型已足够
D3 Collector 使用 ReplaceStock 还是原地 Stock.Set
D4 是否删除 IceBlock / Temperature / Collector 的无参构造
D5 Bind 遇到 dirty host 时跳过 Load，还是先 Flush
```

## 8. 不建议的做法

```text
Load 里重新 Create
Adapter 保存 GameItem / Device
按 key 二次查 Adapter 后再强转
Create 和 Load 各写一份 tag 读取
把 TagState 当成 Mod 封装删除
```


## 9. 已确认决策

| 编号 | 决策 |
|---|---|
| D1 | 测试项目增加最小 `GameItem` / `TagSystem` / `TagState` 测试桩，保留适配器映射与 sparse-write 测试。 |
| D2 | 已废止：attribute 只保留 key；装置类型由适配器泛型参数确定。 |
| D3 | Collector 使用 `CollectorDevice(FlowProperties stock)` + `ReplaceStock`，Create 直接完整初始化。 |
| D4 | 切换后删除 `IceBlockDevice` / `TemperatureDevice` / `CollectorDevice` 的无参构造。 |
| D5 | 已有 Host 重新 Bind 且 `IsDirty` 时跳过 `LoadAll`。 |
| D6 | 已废止：字段表使用 `TagFields.Int/Double(tag, getter, setter)` 显式委托，避免 `Expression.Compile` 触发 dynamic-code。 |
| D7 | 删除 Mod 的 `ITagStore` / `GameItemTagStore` / `FakeTagStore`；游戏自身 `TagState` 作为内部实现保留。 |
| D8 | 接受首次 Create 后的 ValidateShapeState 再 Load 一次；不在 Host 上增加“首次 reconcile 跳过”状态。 |
| D9 | CreateDevice 保留显式 `GameItemTags.Read...` 构造参数读取；字段表负责 Load/Save，接受这一处构造读取。 |

因此当前没有设计级未决项；剩余工作是按计划实施、测试和文档同步。
