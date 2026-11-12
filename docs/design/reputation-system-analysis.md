# 原版声望体系与当前框架差距

## 1. 原版声望怎么走

### 1.1 数据

`PlayerStore`：

```csharp
[ES3Serializable]
public List<StoreReputation> storeReputations;
```

`StoreReputation`：

```csharp
[ES3Serializable] private double amount;
[ES3Serializable] public string factionId;
[ES3Serializable] public string factionDisplay;
[ES3Serializable] public List<StoreReputationPerk> perks;
```

范围：

```text
-200 .. 200
```

### 1.2 初始化

`StoreReputation.InitReputation()` 在新游戏时建立：

```text
FACTION_LOWER_LEVEL
FACTION_UPPER_LEVEL
FACTION_SECURITY
FACTION_BLACK_MARKET
FACTION_REVOLUTION
```

另外根据开局/条件：

```text
FACTION_TOURIST
FACTION_CHURCH
FACTION_CARTEL（化学家开局）
```

每个 faction 带：

```text
显示名
颜色
perks
特殊参数
```

### 1.3 声望变化

统一入口：

```csharp
StoreReputation.ModReputation(
    string factionId,
    int value,
    bool notify = true)
```

内部流程：

```text
找到 faction
→ 应用开局/难度/派系修正
→ storeReputation.ModReputation(value)
→ 夹到 -200..200
→ UpdateReputation() 刷新 perk 解锁
→ ReputationUIManager.NotifyReputation(...)
→ AchievementTracker.CheckPerfectMediator()
```

`ModReputationRaw` 绕过部分修正。

### 1.4 交易自动加声望

`PlayerStore.OnItemTraded` 会调用：

```csharp
StoreReputation.OnItemTraded(
    clientFaction,
    value,
    ...);
```

核心换算：

```text
value / 400
```

再乘：

```text
客户交易声望倍率
派系倍率
perk 倍率
偷窃/违禁品修正
```

所以原版交易声望不是每条剧情手写，而是交易系统自动接入。

### 1.5 读取与门槛

常用读取：

```csharp
StoreReputation.GetStoreReputation(factionId)
StoreReputation.GetSecReputation()
StoreReputation.GetRevReputation()
StoreReputation.GetBMReputation()
StoreReputation.GetCartelReputation()
StoreReputation.IsPerkUnlocked(perkId)
```

剧情/客户/事件通过 factionId 读声望，再决定是否出现、是否解锁 perk。

### 1.6 显示

`ReputationUIManager` 从 `PlayerStore.storeReputations` 生成列表：

```text
faction 名称
声望数值 / 200
状态
perk 列表
```

变化时：

```csharp
ReputationUIManager.Instance.NotifyReputation(factionId, value);
```

---

## 2. 当前剧情框架缺什么

| 能力 | 当前状态 | 缺口 |
|---|---|---|
| NPC 所属阵营 | `NpcDefinition` 没有 `FactionId` | 薇拉/奥雅无法声明代表哪个声望 |
| 剧情条件读声望 | `StoryContext` 只有 flags/tags/trade/day | 只能闭包直接调原版静态方法，没有统一字段 |
| 到访条件读声望 | `VisitContext` 没有声望 | 同上 |
| 声望变化作为结果 | `StoryOutcome` 只有 Leave/OpenShop | 没有 `ReputationDelta` 或等价效果 |
| 声望变化作为动作 | `StoryAssembly` 可以 `player.Do` | 没有统一 Reputation API，容易散落 |
| 自动交易声望 | 薇拉用自定义 `AcidBuyerTrade` | 绕过了 `PlayerStore.OnItemTraded`，不会自动加声望 |
| 自定义 faction | 原版固定初始化列表 | 需要加载/新游戏时补进 `storeReputations`，并去重 |
| 自定义 perk | 原版 `StoreReputationPerk` 有本地化/图标依赖 | 需要额外定义 perk 和 UI 文案 |
| 声望通知 | 原版 `ReputationUIManager` 已有 | 修改声望时需主动调用 notify |
| 存档 | `storeReputations` 已序列化 | 自定义 faction 在旧存档中不存在，需要补 |
| 周合同 | 当前没有 | 声望若绑定周供货，需要合同状态和周结算 |

---

## 3. 两条实现路线

### 路线 A：复用 `FACTION_UPPER_LEVEL`

适合：

```text
薇拉的医院声望 = 上层区声望
不要求独立 faction
```

优点：

```text
原版 UI、存档、perk、通知直接可用
不需要新增 faction
```

缺点：

```text
所有上层区交易都会改变医院声望
无法单独表现“医院关系”
```

### 路线 B：自定义 `FACTION_FLUXFORGE_HOSPITAL`

适合：

```text
薇拉代表独立医院声望
只由薇拉的供货/合同改变
```

需要补：

```text
新游戏/读档后把 faction 加进 PlayerStore.storeReputations
避免重复添加
自定义显示名/颜色
可选自定义 perk
旧存档补 faction
修改声望时通知 UI
```

推荐：

```text
如果医院声望只由薇拉产生，用自定义 faction。
如果只是借用上层区声望，用 FACTION_UPPER_LEVEL。
```

---

## 4. 当前框架建议补的最小接口

### 4.1 `NpcDefinition`

增加可选：

```csharp
internal string FactionId { get; }
```

薇拉：

```text
FACTION_FLUXFORGE_HOSPITAL
```

或：

```text
FACTION_UPPER_LEVEL
```

### 4.2 `StoryContext` / `VisitContext`

增加：

```csharp
internal int Reputation { get; }
internal bool HasReputation(int min);
internal bool HasPerk(string perkId);
```

不要在每个条件里直接写原版静态调用。

### 4.3 声望结果

新增类似：

```csharp
internal sealed class StoryEffect
{
    internal int ReputationDelta;
    internal string FactionId;
}
```

或给 `StoryEventDefinition` 加：

```text
OnResolved(reputationDelta)
```

### 4.4 声望服务

新增：

```text
StoryReputation.Get(factionId)
StoryReputation.Add(factionId, amount, notify)
StoryReputation.EnsureFaction(...)
StoryReputation.IsUnlocked(perkId)
```

### 4.5 交易接入

薇拉自定义交易成功后：

```csharp
StoryReputation.Add(
    VeraHospitalFaction,
    calculateReputation(reward, purity),
    notify: true);
```

不要依赖 `PlayerStore.OnItemTraded`，因为自定义交易没有走原版交易路径。

### 4.6 存档/旧档

`storeReputations` 原版已序列化，但自定义 faction：

```text
新游戏 InitReputation 后补
读档后检查缺失并补
重复检查 factionId
```

---

## 5. 结论

原版已经有完整的声望底座：

```text
数据/存档
faction
增减
perk 阈值
UI
通知
交易自动接入
```

当前剧情框架缺的不是“声望数值存储”，而是：

```text
1. NPC/剧情上下文里的声望访问
2. 声望效果和统一增减入口
3. 自定义 faction 的补建/迁移
4. 自定义交易的声望接入
5. 周合同的声望结算
```

如果下一步实现，建议顺序：

```text
P0 先确定用 FACTION_UPPER_LEVEL 还是自定义医院 faction
P1 加 StoryReputation 服务和 StoryContext.Reputation
P2 把薇拉的交货/合同结算接入声望
P3 再接 perk/UI 门槛
P4 补旧档 faction 迁移
```
