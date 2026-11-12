# 化学家开局的帮派声望体系分析

## 0. 结论

原版帮派不是一套独立的声望类，而是：

```text
通用 StoreReputation
+ FACTION_CARTEL
+ 化学家开局专用客户端
+ 每周公事
+ 数量/纯度指标
+ 帮派 perk
+ 退出阶段
```

我上一份文档只讲通用 faction，漏掉了后半部分。

---

## 1. 谁能加入

开局类型：

```csharp
NewGameData.StartType.DRUG_DEALER
```

游戏里显示为：

```text
chemist（化学家）
```

判断：

```csharp
PlayerStore.Instance.IsChemistStart()
{
    return startType == NewGameData.StartType.DRUG_DEALER;
}
```

帮派存在条件：

```csharp
IsChemistStart() && !isCartelExited
```

`FACTION_CARTEL` 只在化学家开局时加入声望列表：

```csharp
if (PlayerStore.Instance.startType == StartType.DRUG_DEALER)
{
    // 创建 FACTION_CARTEL
    // factionDisplay = 帮派名
    // perks = CreatePerks("FACTION_CARTEL")
}
```

---

## 2. 帮派客户什么时候来

`StoreClientManager` 的帮派调度：

```csharp
if (startType == StartType.DRUG_DEALER && !isCartelExited)
{
    if (dayCounter == 1)
    {
        // 首次接触/check-in
    }
    else if (dayCounter % 7 == 1)
    {
        AddClient(StoreClientListCartel.CreateCartelHandlerWeekly());
    }

    if (dayCounter == 9 || dayCounter == 18 || dayCounter == 27)
    {
        // 帮派相关剧情客户
    }
}
```

日历 UI 也明确标：

```csharp
dayCounter % 7 == 1
&& !storeEventManager.IsEventActive("cartel_visit")
```

所以：

```text
首次：Day 1
之后：每 7 天，day % 7 == 1
固定周事件：cartel_visit
```

这正是“每周固定时间来”的原版依据。

---

## 3. 周合同

`StoreClientListCartel.CreateCartelHandlerWeekly()`：

```text
cartelHandlerWeekly
FACTION_CARTEL
dismissable = false
```

它不是简单收钱，而是检查：

```csharp
PlayerStore.Instance.cartelRequiredCount
PlayerStore.Instance.cartelRequiredPurity
```

默认：

```text
cartelRequiredCount = 10
cartelRequiredPurity = 40
```

合同目标每周上升：

```text
count += 5
purity += 5（上限约 70）
```

玩家交的是：

```text
dream dust / 梦幻粉尘类物品
```

客户端会：

```text
统计数量
计算平均纯度
检查是否达到本周指标
```

---

## 4. 声望怎么变化

帮派声望仍然是：

```csharp
StoreReputation.ModCartelReputation(value)
```

等价于：

```csharp
StoreReputation.ModReputation("FACTION_CARTEL", value)
```

周合同中的具体变化：

```text
数量不足：
  -(缺少数量 × 4)

数量刚好：
  +count

数量超出：
  按超出量给额外奖励/声望

纯度低于要求：
  按差值扣声望

纯度达标/超出：
  +少量声望
```

所以帮派声望不是“做一次任务固定加 5”，而是由：

```text
数量
纯度
是否达标
```

共同计算。

---

## 5. 报酬

基础周薪：

```csharp
int num = 150;
```

如果解锁：

```text
CARTEL_INCREASED_STIPEND
```

则：

```csharp
num += StoreReputation.GetCartelReputation() * 4;
```

也就是：

```text
声望越高，周薪越高
```

这属于帮派 perk，不是通用声望 UI 的普通效果。

---

## 6. 帮派 perk

`StoreReputation.CreatePerks("FACTION_CARTEL")` 生成：

```text
CARTEL_RETIRED
  阈值 -100
  低声望/退出相关

CARTEL_INCREASED_STIPEND
  阈值 25
  周薪增加

CARTEL_RELIABLE_CHEMIST
  阈值 50
  可靠化学家，相关交易/成本效果

CARTEL_AUTHORIZED_EXIT
  阈值 100
  合法退出资格
```

这些 perk 会通过：

```csharp
StoreReputation.UpdateReputation()
```

按 amount 阈值自动解锁/锁定。

---

## 7. 退出

帮派不是永久绑定：

```text
PlayerStore.isCartelExited
PlayerStore.cartelExitStage
```

退出流程由 `StoreClientListCartel` 的 exit 链处理：

```text
cartelExitStage 0..3
CARTEL_AUTHORIZED_EXIT（声望 100）
```

不是简单把 `FACTION_CARTEL` 删掉。

---

## 8. 当前框架还差什么

### 8.1 没有 faction 归属

`NpcDefinition`：

```text
没有 FactionId
```

帮派客户需要：

```text
clientFaction = FACTION_CARTEL
```

自定义 `NpcStoryHost.Create` 创建客户时也没有设置 `clientFaction`。

### 8.2 没有帮派合同状态

当前框架只有：

```text
VisitCount
LastVisitDay
flags
tags
SeenCount
```

没有：

```text
本周要求数量
本周要求纯度
当前周数
是否达标
周薪
退出阶段
```

这些目前只能塞 modData，或复用原版 `PlayerStore.cartelRequiredCount/Purity` 字段。

### 8.3 没有固定周事件

`VisitRule` 能做 7 天冷却：

```csharp
CooldownDays = 7
```

但“固定星期几来”和“送料/收货分开”还没有：

```text
Day 1 首次接触
Day % 7 == 1 周合同
其他日期交料/收货
```

需要自己加日期条件或复用 `StoreClientManager` 的周调度。

### 8.4 没有交物品/算纯度的剧情动作

薇拉的 `AcidBuyerTrade` 是自定义交易，可以扫描后柜台。

帮派周合同需要另一套：

```text
扫描梦幻粉尘
数量统计
平均纯度计算
消耗物品
结算报酬
增减 FACTION_CARTEL 声望
```

当前 `StoryAssembly` 可以 `Do` 动作，但没有通用合同交易 API。

### 8.5 没有声望上下文

`StoryContext` / `VisitContext` 没有：

```text
Reputation
HasPerk
GangTier
```

剧情条件只能闭包直接读原版静态方法。

### 8.6 没有声望结果

`StoryOutcome` 只有：

```text
Default
Leave
OpenShop
```

帮派周合同的：

```text
+声望
-声望
+周薪
解锁 perk
```

只能写在内容动作里，没有统一结果类型。

### 8.7 没有帮派 perk/UI 接线

原版 `FACTION_CARTEL` perks 已经存在，但如果你的 mod 不是化学家开局，默认不会创建这个 faction。

需要：

```text
确保 FACTION_CARTEL 存在
补 perks
旧档补 faction
调用 ReputationUIManager.NotifyReputation
```

---

## 9. 推荐落地结构

### 9.1 复用原版 `FACTION_CARTEL`

不要自己再造一套帮派声望。

```text
StoreReputation.GetCartelReputation()
StoreReputation.ModCartelReputation()
StoreReputation.IsPerkUnlocked("CARTEL_...")
```

这样直接获得：

```text
存档
UI
perk
通知
-200..200 范围
```

### 9.2 增加帮派合同状态

建议放在 modData 或复用原版字段：

```text
gang.requiredCount
gang.requiredPurity
gang.week
gang.stage
gang.lastDeliveryDay
gang.stipend
```

如果 mod 只在化学家开局生效，可以直接使用：

```text
PlayerStore.cartelRequiredCount
PlayerStore.cartelRequiredPurity
PlayerStore.cartelExitStage
```

但要注意：这些字段属于原版化学家开局，不应该污染其他开局。

### 9.3 增加合同动作

新增：

```text
GangContractTrade.TrySubmit(
    out int count,
    out double averagePurity,
    out int reward)
```

负责：

```text
扫描后柜台梦幻粉尘
统计数量/平均纯度
消耗物品
计算报酬
调用 ModCartelReputation
```

### 9.4 增加剧情上下文

```csharp
StoryContext.CartelReputation
StoryContext.RequiredCount
StoryContext.RequiredPurity
StoryContext.HasCartelPerk(perkId)
```

### 9.5 周调度

```text
首次：Day 1 / 加入后第一次开门
之后：Day % 7 == 1
```

与薇拉的 7 天冷却不同，帮派是**固定周相位**：

```text
不是“上次来后 7 天”
而是“每个第 1 天”
```

---

## 10. 还差的清单

```text
[ ] 帮派 faction 的创建/迁移
[ ] 化学家开局或玩家主动加入的条件
[ ] 固定周相位调度（day % 7 == 1）
[ ] 周合同状态（数量/纯度/周数）
[ ] 后柜台粉尘扫描与消耗
[ ] 平均纯度计算
[ ] 周薪和声望结算
[ ] 帮派 perk 解锁
[ ] 退出阶段
[ ] StoryContext 的帮派声望/门槛访问
[ ] 旧档补 faction
[ ] UI 通知
```

其中前四项是 P0；没有它们，只能算“给帮派加了一点声望数值”，还不算原版那套帮派周合同。
