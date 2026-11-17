> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 医院声望 vs 原版帮派声望：复用与差异

## 0. 先纠正

原版帮派不是独立的声望类，它是：

```text
StoreReputation
+ FACTION_CARTEL
+ 化学家开局
+ 固定周合同 NPC
+ 数量/纯度结算
+ 帮派 perk
+ 退出阶段
```

医院声望要复用它的“声望底座”，但不能复用帮派的任务 NPC 表现。

---

## 1. 原版帮派结构

### 1.1 生命周期

```text
化学家开局（DRUG_DEALER）
→ 新游戏就创建 FACTION_CARTEL
→ 声望从 0 开始
→ 每周第 1 天帮派 handler 来
→ 交梦幻粉尘，按数量和纯度结算
→ 声望到达阈值解锁 perk
→ 可走退出阶段
```

关键点：

```text
帮派声望在开局时就已经注册
帮派从第一天起就是玩家身份的一部分
```

### 1.2 NPC

```text
cartelHandlerCheckin
cartelHandlerWeekly
```

特征：

```text
单个任务 NPC
dismissable = false
没有正常商品交易
没有自己的商店交互
只负责帮派合同
```

### 1.3 时间

```text
Day 1 首次
Day % 7 == 1 每周
```

固定周相位，不是“上次来后 7 天”。

### 1.4 合同

```text
cartelRequiredCount
cartelRequiredPurity
```

默认：

```text
count = 10
purity = 40
```

每周增长。交付物是梦幻粉尘类物品，客户端扫描后计算：

```text
数量
平均纯度
是否达标
```

### 1.5 声望

```text
FACTION_CARTEL
```

变化：

```text
数量不足扣声望
纯度不足按差值扣
达标加声望
超额有额外奖励
```

通用入口：

```csharp
StoreReputation.ModCartelReputation(value)
```

### 1.6 Perk

```text
CARTEL_RETIRED
CARTEL_INCREASED_STIPEND
CARTEL_RELIABLE_CHEMIST
CARTEL_AUTHORIZED_EXIT
```

阈值和效果由原版 `StoreReputationPerk` 提供。

---

## 2. 医院声望的目标结构

根据目前需求：

```text
薇拉是上层区医院的成员
玩家与医院签订合同后
才把医院声望注册进游戏
后续会有其他医院 NPC
这些 NPC 有自己的交易和交互
```

这意味着：

```text
医院不是“开局身份”
医院是“玩家签约后加入的关系”
医院可以有多个 NPC
医院 NPC 不应统一变成帮派任务 NPC
```

---

## 3. 复用点

### 3.1 直接复用 `StoreReputation`

不要自己写一套声望数值、UI、存档。

```csharp
StoreReputation.ModReputation(factionId, value, notify)
StoreReputation.GetStoreReputation(factionId)
StoreReputation.IsPerkUnlocked(perkId)
```

可复用：

```text
-200..200
存档
UI 列表
通知
perk 阈值
难度/开局修正
```

### 3.2 复用 faction 结构

医院可以是一个自定义 faction：

```text
FACTION_FLUXFORGE_HOSPITAL
```

或者复用：

```text
FACTION_UPPER_LEVEL
```

区别：

```text
自定义 faction：独立，只由医院剧情改变
上层 faction：和所有上层区交易共享
```

### 3.3 复用 story 框架

医院 NPC 不需要新的 story host：

```text
NpcStoryContent
VisitRule
StoryEvent
StoryAssembly
```

每个医院 NPC 继续有自己的交易和事件。

### 3.4 复用 VisitRule 的周期

如果医院也有固定回访：

```text
CooldownDays / Day 条件
```

但医院不是帮派任务 NPC，不建议强制 `dismissable = false`。

### 3.5 复用声望 UI

添加 faction 后，原版 `ReputationUIManager` 会从 `storeReputations` 读取。

不需要新做声望面板。

---

## 4. 差异点

| 维度 | 原版帮派 | 医院 |
|---|---|---|
| 注册时机 | 新游戏开局 | 签订合同后 |
| 是否属于玩家身份 | 开局即帮派成员 | 合同关系 |
| NPC 数量 | 1 个任务 handler | 多个医院 NPC |
| NPC 行为 | 只做周合同，无交易 | 各自有交易/剧情 |
| 到访方式 | 固定 Day % 7 | 各 NPC 自己的 VisitRule |
| 合同对象 | 帮派 handler | 医院合同/多个 NPC 可能共享 |
| 交付物 | 梦幻粉尘 | 酸碱/酒精/其他医院原料 |
| 声望来源 | 周合同为主 | 合同 + 可能的日常交易 |
| 退出 | 帮派退出阶段 | 合同终止/续签 |
| Perk | 原版帮派 perk | 自定义医院 perk（可暂缓） |

---

## 5. 最小代码方案

### 5.1 不自建声望类

直接用：

```text
StoreReputation
PlayerStore.storeReputations
ReputationUIManager
```

### 5.2 只加一个医院 faction 服务

```text
HospitalReputation.EnsureFaction()
HospitalReputation.Get()
HospitalReputation.Add()
```

职责：

```text
合同签订后确保 faction 存在
读/加医院声望
通知 UI
旧档补 faction
```

### 5.3 `NpcDefinition` 加可选 FactionId

```text
薇拉：FACTION_FLUXFORGE_HOSPITAL
后续医院 NPC：同一个 faction
```

不要让每个 NPC 自己存声望。

### 5.4 不给所有医院 NPC 加特殊控制器

医院 NPC 仍然是：

```text
普通 StoreClient
普通 VisitRule
普通 StoryEvent
```

只是：

```text
clientFaction = 医院 faction
```

如果需要原版自动交易声望，正常交易可以复用 `StoreReputation.OnItemTraded`；自定义交易需要显式 Add。

### 5.5 合同状态先用现有状态

不一定要马上做 `Contract` 类。

可以先用：

```text
modData flags
RequiredCount
RequiredPurity
ContractDay
```

等周合同逻辑稳定后再抽象。

### 5.6 暂不做自定义 perk

医院第一版可以：

```text
只注册 faction
复用原版 UI
perk 列表为空
```

等声望门槛明确后再加医院 perk。

---

## 6. 需要 grill 的问题

下面这些会决定代码量，不能替用户猜：

### Q1. 医院声望是独立 faction 还是上层区声望？

```text
A. 独立 FACTION_FLUXFORGE_HOSPITAL
B. 复用 FACTION_UPPER_LEVEL
```

如果独立：需要补 faction 注册/旧档/UI。
如果复用：零注册代码，但会被所有上层区交易影响。

### Q2. 合同签订前，薇拉的交易算医院声望吗？

```text
A. 不算。签约后才注册 faction，之前的交易不追溯。
B. 算。签约时按历史交易补声望。
C. 签约前用上层区声望，签约后转医院声望。
```

### Q3. 合同签订后，只有合同交货加声望，还是薇拉日常交易也加？

```text
A. 只有合同结算加
B. 薇拉日常交易也按原版交易声望加
C. 所有医院 NPC 的交易都加
```

这决定要不要给 `StoreClient.clientFaction` 接线，以及是否复用 `OnItemTraded`。

### Q4. 医院 NPC 是共享一个合同，还是每个 NPC 有自己的合同？

```text
A. 共享医院合同，NPC 只是不同接口
B. 每个 NPC 独立合同/独立声望
C. 共享声望，但合同由薇拉独占
```

用户目前说“薇拉代表医院”“后续其他医院 NPC”，倾向 A 或 C。

### Q5. 合同是否固定周相位？

```text
A. 签约日为锚点，每 7 天
B. 全站固定 Day % 7 == 1，类似帮派
C. 没有固定日，只按交付触发
```

### Q6. 医院声望需要 perk/等级吗？

```text
A. 先只要数值和 UI
B. 需要等级门槛
C. 需要和帮派一样的 perk
```

自定义 perk 会显著增加 UI/本地化/图标代码。

### Q7. 合同是否能终止/拒绝？

```text
A. 可终止，声望保留
B. 可终止，声望清零
C. 不可终止
```

帮派有 exit stage；医院是否要等价物需要明确。

### Q8. 其他医院 NPC 在合同前是否出现？

```text
A. 合同前不出现
B. 合同前出现，但不加医院声望
C. 合同前出现，使用上层区声望
```

---

## 7. 当前建议

在答案明确前，最小可行方向是：

```text
独立 FACTION_FLUXFORGE_HOSPITAL
签约时 EnsureFaction
复用 StoreReputation / ReputationUIManager
NpcDefinition.FactionId
StoryContext.Reputation
合同状态先用 modData
医院 NPC 不加特殊控制器
暂不做自定义 perk
不照搬帮派 dismissable=false
```

这样新增代码主要集中在：

```text
1 个 faction 服务
1 个 NpcDefinition 字段
1 个 StoryContext 访问器
少量合同结算动作
```

不重写帮派系统，也不重写医院 NPC。

---

## 11. 已确认（2026-11-12）

```text
医院是独立 faction：FACTION_FLUXFORGE_HOSPITAL
签约成功时注册 faction
签约前交易不计入医院声望
签约后：合同结算 + 所有医院 NPC 正常交易加医院声望
合同以签约日为锚点，每 7 天
所有医院 NPC 共享同一份医院声望
```

### 由此确定的最小实现

```text
1. NpcDefinition 增加 FactionId
2. NpcStoryHost.Create 设置 clientFaction
3. HospitalReputation.EnsureFaction()
4. HospitalReputation.Add()
5. StoryContext.Reputation / HasReputation
6. 签约剧情动作调用 EnsureFaction
7. 合同和医院 NPC 交易调用 Add
```

### 仍然不能替用户假设的点

```text
1. 医院声望每点代表多少价值：
   原版交易是 value / 400
   医院合同按数量/纯度给多少

2. 第一版是否需要医院 perk/等级：
   只显示数值，还是 25/50/100 门槛

3. 合同失败/终止：
   声望保留、扣减、还是清零

4. 医院 faction 的显示名、颜色、本地化
```

这四项会直接决定声望服务接口和 UI 显示，仍未写死。

---

## 12. 第二轮确认（2026-11-12）

```text
正常交易沿用原版 value / 400
合同按数量 + 纯度结算
第一版只做数值和原版 UI，不做自定义 perk
合同终止/失败保留医院声望，可重新谈
```

### 因此第一版不需要

```text
自定义声望面板
自定义 perk 图标/本地化
合同终止时清空 faction
```

### 第一版仍需要

```text
HospitalReputation.EnsureFaction
HospitalReputation.Add
NpcDefinition.FactionId
NpcStoryHost 设置 clientFaction
StoryContext 读声望
签约动作注册 faction
合同/交易结算调用 Add
```

### 还需要实测的技术点

```text
1. 运行时向 PlayerStore.storeReputations 添加 faction 后，
   ReputationUIManager 是否需要手动刷新/重建列表。

2. 医院 NPC 走普通交易时，
   clientFaction 是否能正常触发原版 OnItemTraded 的 value/400 声望。

3. 薇拉当前是自定义 AcidBuyerTrade，
   不会走 OnItemTraded；需要显式补医院声望。

4. 旧存档签约状态：
   如果 faction 不存在，EnsureFaction 要能补建且不重复。
```

### 已确认的边界

```text
医院 NPC 共享一个医院声望
医院 NPC 保留自己的交易和剧情，不采用帮派 handler 形态
签约日作为 7 天周期锚点
签约前交易不计入医院声望
```


