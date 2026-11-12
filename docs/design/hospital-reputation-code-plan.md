# 医院声望代码设计方案

> 只做设计，不写实现。
> 已确认：独立医院 faction、签约时注册、签约前不计、合同 + 所有医院 NPC 正常交易加声望、签约日锚点每 7 天、共享声望、第一版不做自定义 perk、合同终止保留声望。

## 1. 直接复用

### 1.1 声望数据/存档/UI

```csharp
PlayerStore.Instance.storeReputations   // [ES3Serializable] List<StoreReputation>
StoreReputation.GetStoreReputation(id)
StoreReputation.IsPerkUnlocked(perkId)
StoreReputation.UpdateReputation()
ReputationUIManager.Instance.OpenUI()
ReputationUIManager.Instance.NotifyReputation(factionId, value)
```

不自己写：

```text
声望存档文件
声望数值 UI
声望增减的存档结构
perk 阈值表
```

### 1.2 声望变化

原版实例方法支持小数：

```csharp
StoreReputation fame = StoreReputation.GetStoreReputation(FactionId);
fame.ModReputation(value / 400d);
ReputationUIManager.Instance.NotifyReputation(FactionId, (int)value);
```

原因：

```text
静态 StoreReputation.ModReputation(string,int) 只收 int
原版交易 value/400 是小数
直接用 int 会把小于 400 的交易全部截成 0
```

### 1.3 普通交易自动接入

如果医院 NPC 走原版交易：

```csharp
StoreClient.clientFaction = HospitalFactionId;
```

则原版：

```text
PlayerStore.OnItemTraded
→ StoreReputation.OnItemTraded
→ value / 400
```

自动加声望。

### 1.4 剧情框架

```text
NpcDefinition
NpcStoryContent
VisitRule
StoryEvent
StoryContext
StoryAssembly
player.Do(...)
```

不新增 StoryHost，不新增 NPC 控制器。

---

## 2. 要设计的代码

### 2.1 `NpcDefinition.FactionId`

```csharp
internal string FactionId { get; }
```

建议可选构造参数，默认 null：

```csharp
NpcDefinition(..., string factionId = null)
```

使用：

```text
薇拉：FACTION_FLUXFORGE_HOSPITAL
奥雅：null
后续医院 NPC：FACTION_FLUXFORGE_HOSPITAL
```

### 2.2 `NpcStoryHost.Create`

创建客户时：

```csharp
if (!string.IsNullOrEmpty(content.Definition.FactionId))
{
    client.clientFaction = content.Definition.FactionId;
}
```

作用：

```text
普通医院 NPC 走原版交易时自动加声望
薇拉自定义交易仍需显式加法
```

### 2.3 `HospitalReputation` 服务

职责：

```text
EnsureFaction()
Get()
Add(double)
AddTrade(double cashValue)
Notify(...)
```

`EnsureFaction()`：

```text
查 StoreReputation.GetStoreReputation(FactionId)
存在 → 直接返回
不存在 → 创建 StoreReputation
        factionDisplay = 医院名
        factionId = FACTION_FLUXFORGE_HOSPITAL
        amount = 0
        colors = 医院配色
        perks = 空列表（第一版）
        加入 PlayerStore.storeReputations
        调用 UpdateReputation()
```

调用时机：

```text
签订医院合同成功时
读档后若合同已开始但 faction 缺失时补建
```

### 2.4 合同状态

第一版用 modData，不新增存档类：

```text
hospital.contract.started
hospital.contract.startDay
hospital.contract.requiredVolume
hospital.contract.requiredPurity
hospital.contract.lastDeliveryDay
```

或者包一个很薄的 `HospitalContractState` 静态类：

```text
Start(day)
IsActive
IsDeliveryDay(day)
MarkDelivered(day)
RequiredVolume/Purity
```

不新增 `Contract` 引擎。

### 2.5 签约动作

在合同接受的剧情内容中：

```text
HospitalReputation.EnsureFaction();
HospitalContractState.Start(currentDay);
```

然后：

```text
医院 faction 出现在 PlayerStore.storeReputations
原版 UI 可读取
```

### 2.6 声望结算

#### 正常交易

如果医院 NPC 走原版交易：

```text
clientFaction 自动处理
```

如果是薇拉这种自定义交易：

```text
交易成功后
HospitalReputation.AddTrade(reward);
```

#### 合同结算

```text
读取本次合同数量/纯度
按合同公式计算声望
HospitalReputation.Add(delta);
```

### 2.7 `StoryContext`

增加只读：

```csharp
internal int HospitalReputation { get; }
internal bool HasHospitalReputation(int threshold);
```

用途：

```text
医院合同分级
后续医院 NPC 剧情门槛
```

### 2.8 合同周期

```text
签约日作为 anchor
每 7 天一个合同节点
```

实现优先复用：

```text
VisitRule.Condition 读 HospitalContractState
StoryEvent.Condition 读合同状态
```

不需要新 scheduler。

---

## 3. 明确不设计

```text
自定义声望面板
自定义 perk 图标/本地化
帮派 handler NPC
帮派退出阶段
全局 Day % 7 帮派相位
自建声望存档文件
```

---

## 4. 风险

```text
1. 运行时新增 faction 后，原版 ReputationUIManager 如果已经打开，
   列表可能不会自动重建；需要验证 OpenUI 的刷新行为。

2. 医院 NPC 如果都像薇拉一样走自定义交易，
   clientFaction 不会自动触发 OnItemTraded；
   必须统一走 HospitalReputation.AddTrade。

3. StoreReputation 的 amount 是 double，
   但静态 ModReputation 只收 int；
   不能把 value/400 直接转 int。

4. 签约前不能提前创建 faction，
   否则玩家会看到未签约的医院声望。

5. 旧存档签约后 faction 缺失时，
   需要 EnsureFaction，且不能重复添加。

6. 合同按数量+纯度给声望的具体数值仍未定。
```

## 5. 待确认

已确认：

```text
医院 faction 名称：上层区中心医院
合同按液体 mL × 浓度权值计分
目标：每周独立 300 分
90% 以上可计入
90–98.9%：0.9 权值
99%：1.0 权值
99% 以上每 0.1 个百分点 +0.1 权值
100%：3.0 权值
达标按 score/100 加声望
未达标按缺口/100 扣声望
付款按 score 折算
薇拉现有交易暂不加医院声望
只保留 AddTrade 接口给后续医院 NPC
第一版不做自定义 perk
合同终止保留声望
```

### 尚未定的数值

```text
1 分 = 多少信用点
单次合同正声望上限
负声望是否有下限
```

这些应做成可配置常量，不写死在业务逻辑里。

---

## 6. 最终代码分层

### 6.1 复用层（不改）

```text
PlayerStore.storeReputations
StoreReputation.GetStoreReputation
StoreReputation.ModReputation(double)
StoreReputation.UpdateReputation
ReputationUIManager.NotifyReputation
ReputationUIManager.OpenUI
PlayerStore.modData
NpcStoryHost
StoryEvent / VisitRule / StoryContext
FlowStock / FlowProperties / FlowProps
```

### 6.2 需要新增

```text
HospitalReputation
HospitalContractState
ChemicalRequest
HospitalContractTrade
HospitalContractOfferAssembly
HospitalContractSettlementAssembly
NpcDefinition.FactionId
StoryContext.HospitalReputation
```

### 6.3 不需要新增

```text
自定义声望存档
自定义声望 UI
自定义 perk
帮派 handler
医院 NPC 专用到访控制器
```

## 7. 合同计分

```text
对每件可收液体：
  concentration = 目标 FlowProperty / SubstanceTotal
  mL = SubstanceTotal / 1000

  weight(concentration):
    90–98.9%      0.9
    99%           1.0
    99% + Δ      1.0 + Δ×100
    100%          3.0

  score += mL × weight
```

目标：

```text
300 分 / 周
```

声望：

```text
score >= 300:
  +floor(score / 100)，受单次上限限制

score < 300:
  -ceil((300 - score) / 100)
```

付款：

```text
cash = floor(score × paymentRate)
```

即使未达标，也按已交货物付款，再扣声望。

## 8. 化学物配置

`ChemicalRequest`：

```csharp
internal sealed class ChemicalRequest
{
    internal FlowPropertyKey Key;
    internal double MinimumConcentration = 0.90;
}
```

剧情可以新增：

```text
Acid
Base
Alcohol
其他 FlowProps
```

交易扫描时按请求匹配，不把酸/碱写死在合同代码里。

不匹配的物品留在后柜台。

---

## 9. 待确认

```text
1 分 = 多少信用点
单次合同正声望上限
负声望是否有下限
是否允许一次合同提交多种化学物混合计分
```

