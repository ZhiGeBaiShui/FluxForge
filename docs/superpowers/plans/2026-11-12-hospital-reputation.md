# 医院声望与周合同 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use subagent-driven-development (recommended) or executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在现有剧情框架中加入独立、签约后才注册的医院声望，以及按“液体 mL × 浓度权值”结算的医院周合同。

**Architecture:** 复用原版 `StoreReputation` / `ReputationUIManager` 作为声望存档和 UI，只新增医院 faction 的注册封装、合同状态、纯计分逻辑和交付扫描。医院 NPC 继续走现有 `NpcDefinition` / `VisitRule` / `StoryEvent`，不新增 NPC 控制器。

**Tech Stack:** Unity 2021.3.45f2、C#、Mono、Harmony、xUnit 测试项目。

**Spec:**
- [hospital-vs-cartel-reputation.md](../design/hospital-vs-cartel-reputation.md)
- [hospital-reputation-code-plan.md](../design/hospital-reputation-code-plan.md)
- [cartel-gang-reputation-analysis.md](../design/cartel-gang-reputation-analysis.md)

## Global Constraints

```text
不实现自定义声望 UI
不实现自定义 perk
不做帮派式任务 handler
不统一设计其他医院 NPC 的到访节奏
薇拉现有自定义交易暂不加医院声望，只保留 AddTrade 接口
医院 faction 只在合同签订成功后注册
合同以签约日为锚点，每 7 天一次
第一版目标固定 300 分
签约后合同结算加声望
医院声望范围沿用 StoreReputation 的 -200..200
合同付款默认 0.5 信用点 / 分
单次合同正声望上限 +20
```

## Review Focus

- 运行时新增 faction 后，原版 `ReputationUIManager` 已打开时可能仍是旧列表。
- `StoreReputation.ModReputation` 有 int/double 两个入口，错误使用 int 会把小额声望截成 0。
- 高浓度回答后才允许合同提议；拒绝后不自动重复。
- 合同周期以签约日为锚点，不是全站 `Day % 7`。
- 后柜台为空、浓度不足、没有匹配化学物时，不能吞物品或重复结算。

---

## File Structure

```text
src/
  Story/
    Hospital/
      HospitalReputation.cs
      HospitalContractState.cs
      HospitalContractScore.cs
      HospitalContractSchedule.cs
      HospitalContractDefinition.cs
      ChemicalRequest.cs
      HospitalContractTrade.cs
      HospitalContractSettlement.cs
      Events/
        HospitalContractOffer/
          HospitalContractOfferAssembly.cs
          HospitalContractOfferEdges.cs
          HospitalContractOfferTexts.cs
        HospitalContractSettlement/
          HospitalContractSettlementAssembly.cs
          HospitalContractSettlementEdges.cs
          HospitalContractSettlementTexts.cs

    Runtime/
      Model/NpcDefinition.cs                 (modify)
      Model/StoryContext.cs                  (modify)
      Direction/NpcStoryHost.cs              (modify)
      Bootstrap/StoryBootstrap.cs            (modify)

    Npcs/AcidBuyer/
      AcidBuyerStory.cs                      (modify)
      Events/AcidBuyerFirstHighPurity/
        Edges/AcidBuyerFirstHighPurityEdges.cs (modify)

tests/
  FluxForge.Tests/
    HospitalContractScoreTests.cs
    HospitalContractScheduleTests.cs
    HospitalContractStateTests.cs
```

职责边界：

```text
HospitalReputation      只负责医院 faction 的注册/读取/加减
HospitalContractState   只负责合同状态和周相位
HospitalContractScore   纯函数，不碰 Unity/PlayerStore
HospitalContractTrade   只负责扫描、计分、消耗、付款
HospitalContractDefinition 只放当前合同的化学物配置
Events                  只做对话和调用状态/交易服务
```

---

### Task 1: 纯计分与周相位

**Files:**
- Create: `src/Story/Hospital/HospitalContractScore.cs`
- Create: `src/Story/Hospital/HospitalContractSchedule.cs`
- Test: `tests/FluxForge.Tests/HospitalContractScoreTests.cs`
- Test: `tests/FluxForge.Tests/HospitalContractScheduleTests.cs`

**Interfaces:**
- Produces:
  - `HospitalContractScore.Weight(double concentration) -> double`
  - `HospitalContractScore.ReputationDelta(double score) -> int`
  - `HospitalContractScore.Payment(double score) -> int`
  - `HospitalContractSchedule.IsDue(int startDay, int lastSettledDay, int today) -> bool`

- [ ] **Step 1: 写失败测试**

断言：

```text
Weight(0.90)  == 1.0
Weight(0.989) == 1.0
Weight(0.99)  == 1.0
Weight(0.995) == 1.5
Weight(1.0)   == 3.0

ReputationDelta(300) == 3
ReputationDelta(299) == -1
ReputationDelta(0)   == -3
ReputationDelta(900) == 9
Payment(300) == 150
```

- [ ] **Step 2: 运行测试确认失败**

```powershell
dotnet test tests\FluxForge.Tests\FluxForge.Tests.csproj -c Release --filter HospitalContract
```

预期：类型不存在，编译失败。

- [ ] **Step 3: 实现纯函数**

`Weight`：

```text
concentration < 99%      -> 1.0
99% <= concentration < 100% -> 1 + (concentration - 0.99) * 100
concentration >= 100%    -> 3.0
```

`ReputationDelta`：

```text
score >= 300 -> min(floor(score / 100), 20)
score < 300  -> -ceil((300 - score) / 100)
```

`Payment`：

```text
floor(score * 0.5)
```

- [ ] **Step 4: 运行测试确认通过**

- [ ] **Step 5: 提交**

```powershell
git add src/Story/Hospital/HospitalContractScore.cs src/Story/Hospital/HospitalContractSchedule.cs tests/FluxForge.Tests/HospitalContractScoreTests.cs tests/FluxForge.Tests/HospitalContractScheduleTests.cs
git commit -m "feat: add hospital contract score and schedule"
```

---

### Task 2: NPC faction 字段与客户接线

**Files:**
- Modify: `src/Story/Runtime/Model/NpcDefinition.cs`
- Modify: `src/Story/Runtime/Direction/NpcStoryHost.cs`
- Modify: `src/Story/Npcs/AcidBuyer/AcidBuyerStory.cs`
- Test: compile-only + existing story tests

**Interfaces:**
- Consumes: existing `NpcDefinition` constructor.
- Produces:
  - `NpcDefinition.FactionId { get; }`
  - `NpcDefinition(..., bool queueOnGameLoad, string factionId = null)`
  - `NpcStoryHost.Create` sets `client.clientFaction`.

- [ ] **Step 1: 写一个定义测试**

测试只验证可选参数不破坏旧调用：

```csharp
Assert.Null(new NpcDefinition("a", "A", "sprite", null, false).FactionId);
Assert.Equal("FACTION_FLUXFORGE_HOSPITAL",
    new NpcDefinition("a", "A", "sprite", null, false, "FACTION_FLUXFORGE_HOSPITAL").FactionId);
```

- [ ] **Step 2: 运行测试确认失败**

- [ ] **Step 3: 修改 `NpcDefinition`**

增加只读 `FactionId`，构造参数放在最后并默认 null。

- [ ] **Step 4: 修改 `NpcStoryHost.Create`**

```text
if (!string.IsNullOrEmpty(definition.FactionId))
    client.clientFaction = definition.FactionId;
```

- [ ] **Step 5: 在 `AcidBuyerStory` 设置薇拉 faction**

```text
FactionId = "FACTION_FLUXFORGE_HOSPITAL"
```

- [ ] **Step 6: 构建/测试**

- [ ] **Step 7: 提交**

---

### Task 3: 医院声望 faction 服务

**Files:**
- Create: `src/Story/Hospital/HospitalReputation.cs`
- Modify: `src/Story/Runtime/Bootstrap/StoryBootstrap.cs`
- Test: 手动游戏 smoke

**Interfaces:**
- Consumes:
  - `PlayerStore.Instance.storeReputations`
  - `StoreReputation.GetStoreReputation`
  - `StoreReputation.ModReputation(double)`
  - `ReputationUIManager.NotifyReputation`
- Produces:
  - `HospitalReputation.FactionId`
  - `HospitalReputation.EnsureFaction()`
  - `HospitalReputation.IsRegistered -> bool`
  - `HospitalReputation.Get() -> int`
  - `HospitalReputation.Add(double amount)`
  - `HospitalReputation.AddTrade(double cashValue)`（只留接口，当前无调用方）

- [ ] **Step 1: 写一个 faction 常量/幂等测试**

测试至少覆盖：

```text
EnsureFaction 两次后，storeReputations 中只有一个医院 faction
新创建 faction 的 factionDisplay == "上层区中心医院"
perks 为空
amount == 0
```

如果测试环境无法构造 `PlayerStore`，把幂等检查提取为纯函数：

```text
HospitalReputation.HasFaction(IEnumerable<StoreReputation>)
```

- [ ] **Step 2: 运行测试确认失败**

- [ ] **Step 3: 实现 `EnsureFaction`**

顺序：

```text
GetStoreReputation(FactionId)
存在 -> 返回
不存在 -> 创建 StoreReputation
        factionId/display/colors
        perks = new List<StoreReputationPerk>()
        storeReputations.Add
        UpdateReputation()
```

`Add`：

```text
找到医院 faction
调用实例 ModReputation(double)
NotifyReputation(FactionId, (int)Math.Round(amount))
```

不能用静态 `ModReputation(string,int)`。

- [ ] **Step 4: 加读档补注册**

在 `StoryBootstrap.Initialize` 注册一个加载钩子：

```text
if (HospitalContractState.Started)
    HospitalReputation.EnsureFaction();
```

只在合同已开始时补建；签约前不创建。`StoryBootstrap.Shutdown` 必须注销这个钩子。

- [ ] **Step 5: 运行构建**

- [ ] **Step 6: 游戏 smoke**

手动验证：

```text
未签约：原版声望 UI 中没有“上层区中心医院”
签约：UI 出现医院 faction，初始 0
重载存档：不重复出现
```

- [ ] **Step 7: 提交**

---

### Task 4: 合同状态与提议触发

**Files:**
- Create: `src/Story/Hospital/HospitalContractState.cs`
- Modify: `src/Story/Npcs/AcidBuyer/Events/AcidBuyerFirstHighPurity/Edges/AcidBuyerFirstHighPurityEdges.cs`
- Test: `tests/FluxForge.Tests/HospitalContractStateTests.cs`

**Interfaces:**
- Produces:
  - `HospitalContractState.Started`
  - `HospitalContractState.Start(int day)`
  - `HospitalContractState.IsDue(int day)`
  - `HospitalContractState.MarkSettled(int day)`
  - `HospitalContractState.QueueOffer()`
  - `HospitalContractState.OfferReady`
  - `HospitalContractState.DeclineOffer()`
  - `HospitalContractState.OfferDeclined`

状态 key：

```text
hospital.contract.started
hospital.contract.start_day
hospital.contract.last_settled_day
hospital.contract.offer_ready
hospital.contract.offer_declined
```

- [ ] **Step 1: 写状态测试**

覆盖：

```text
Start(100) -> Started
Start(100) 再 Start(101) -> start_day 仍为 100
IsDue(106) == false
IsDue(107) == true
MarkSettled(107) -> IsDue(113) == false
IsDue(114) == true
QueueOffer -> OfferReady
DeclineOffer -> OfferDeclined 且 OfferReady=false
Accept/Start -> OfferReady=false
```

- [ ] **Step 2: 运行测试确认失败**

- [ ] **Step 3: 实现状态**

`Start` 必须幂等；重复调用不能重置 `start_day`。

`IsDue`：

```text
Started && today - lastSettledDay >= 7
```

`Start` 时：

```text
StartDay = day
LastSettledDay = day
OfferReady = false
```

- [ ] **Step 4: 高浓度回答后排队合同提议**

在 `AcidBuyerHighPurityAnswerEdge.Execute` 写：

```text
AcidBuyerProgress.MarkHighPurityDialogueDone();
HospitalContractState.QueueOffer();
player.SayNpc(_response);
```

只在正常回答路径调用，不为中途退出增加防护。

- [ ] **Step 5: 运行测试**

- [ ] **Step 6: 提交**

---

### Task 5: 化学物请求与合同交付

**Files:**
- Create: `src/Story/Hospital/ChemicalRequest.cs`
- Create: `src/Story/Hospital/HospitalContractDefinition.cs`
- Create: `src/Story/Hospital/HospitalContractSettlement.cs`
- Create: `src/Story/Hospital/HospitalContractTrade.cs`
- Test: `tests/FluxForge.Tests/HospitalContractScoreTests.cs`（补充匹配/计分纯函数）

**Interfaces:**
- Consumes:
  - `FlowStock.Read`
  - `FlowProperties.Get(FlowPropertyKey)`
  - `FlowProps.Acid/Base/Alcohol`
- Produces:
  - `ChemicalRequest(FlowPropertyKey key, double minimumConcentration)`
  - `HospitalContractDefinition.Chemicals -> IReadOnlyList<ChemicalRequest>`
  - `HospitalContractTrade.TrySettle(...) -> HospitalContractSettlement`
  - `HospitalContractSettlement.Score/ReputationDelta/Payment/ItemCount`

- [ ] **Step 1: 写纯匹配测试**

构造 `ChemicalRequest(FlowProps.Acid, 0.90)`，验证：

```text
90% 酸 -> 可收，权重 1.0
99% 酸 -> 可收，权重 1.0
99.5% 酸 -> 可收，权重 1.5
100% 酸 -> 可收，权重 3.0
89.9% 酸 -> 不可收
不含 Acid 的液体 -> 不可收
```

- [ ] **Step 2: 实现 `ChemicalRequest`**

字段：

```csharp
FlowPropertyKey Key;
double MinimumConcentration = 0.90;
```

- [ ] **Step 3: 实现 `HospitalContractDefinition`**

第一版：

```text
Acid
Base
Alcohol
```

都使用 `MinimumConcentration = 0.90`。

以后新增化学物时只在这里追加，不改扫描代码。

- [ ] **Step 4: 实现扫描/结算**

`TrySettle` 只在后柜台扫描：

```text
跳过 FlowDevice
读取 FlowStock
SubstanceTotal <= 0 -> 跳过
对每个 request 计算 concentration
concentration >= request.Minimum
选权重最高的匹配项
score += mL * weight
收集物品
```

多个 request 同时匹配同一物品时：

```text
只计一次
选权重最高的一项
```

扫描结束后：

```text
无匹配物品 -> score 0、payment 0、rep -3
有匹配物品 -> 销毁已收物品、ModCash(payment)
```

- [ ] **Step 5: 结果模型**

`HospitalContractSettlement`：

```csharp
int Score;
int ReputationDelta;
int Payment;
int ItemCount;
```

`ReputationDelta` 和 `Payment` 调用 Task 1 的纯函数。

- [ ] **Step 6: 运行测试**

- [ ] **Step 7: 提交**

---

### Task 6: 合同提议/结算剧情

**Files:**
- Create: `src/Story/Hospital/Events/HospitalContractOffer/HospitalContractOfferTexts.cs`
- Create: `src/Story/Hospital/Events/HospitalContractOffer/HospitalContractOfferEdges.cs`
- Create: `src/Story/Hospital/Events/HospitalContractOffer/HospitalContractOfferAssembly.cs`
- Create: `src/Story/Hospital/Events/HospitalContractSettlement/HospitalContractSettlementTexts.cs`
- Create: `src/Story/Hospital/Events/HospitalContractSettlement/HospitalContractSettlementEdges.cs`
- Create: `src/Story/Hospital/Events/HospitalContractSettlement/HospitalContractSettlementAssembly.cs`
- Modify: `src/Story/Npcs/AcidBuyer/AcidBuyerStory.cs`

**Interfaces:**
- Consumes:
  - `HospitalContractState`
  - `HospitalReputation`
  - `HospitalContractTrade`
- Produces:
  - `hospital.contract_offer` StoryEvent
  - `hospital.contract_settlement` StoryEvent
  - 高优先级合同到访 VisitRule

- [ ] **Step 1: 写故事图测试**

如果现有测试支持，至少测试：

```text
OfferReady && !Started && !Declined -> offer event 可选中
Started && IsDue -> settlement event 优先于 repeat
OfferDeclined -> offer 不再选中
```

- [ ] **Step 2: 实现提议**

`HospitalContractOfferAssembly`：

```text
greeting -> accept / decline
```

`Accept`：

```text
HospitalContractState.Start(day)
HospitalReputation.EnsureFaction()
```

`Decline`：

```text
HospitalContractState.DeclineOffer()
```

- [ ] **Step 3: 实现结算**

`HospitalContractSettlementAssembly`：

```text
执行 HospitalContractTrade.TrySettle
按 Score 分成：未达标 / 达标 / 超额
调用 HospitalReputation.Add(result.ReputationDelta)
HospitalContractState.MarkSettled(day)
```

- [ ] **Step 4: 注册到 `AcidBuyerStory`**

新增：

```text
contractOffer StoryEvent:
  priority 90
  maxTimes 1
  condition: OfferReady && !Started && !Declined
  assembly: HospitalContractOfferAssembly

contractSettlement StoryEvent:
  priority 200
  maxTimes 0
  condition: Started && IsDue(day)
  assembly: HospitalContractSettlementAssembly
```

新增 VisitRule：

```text
id: acid_buyer.contract_due
priority: 100
chance: 1
cooldown: 0
condition: Started && IsDue(day)
```

- [ ] **Step 5: 保留现有薇拉规则**

原有 `acid_buyer.seven_days` 保留；合同到期规则优先级更高。

- [ ] **Step 6: 运行构建/测试**

- [ ] **Step 7: 提交**

---

### Task 7: 声望上下文

**Files:**
- Modify: `src/Story/Runtime/Model/StoryContext.cs`
- Test: compile + story condition tests

**Interfaces:**
- Produces:
  - `StoryContext.HospitalReputationValue -> int`
  - `StoryContext.HasHospitalReputation(int threshold) -> bool`

- [ ] **Step 1: 写测试**

```text
HasHospitalReputation(0) 在未签约时为 false
签约并加声望后为 true
```

- [ ] **Step 2: 实现只读属性**

不要在 `StoryContext` 构造器加参数；直接读取 `HospitalReputation.Get()`，避免修改所有调用点。

`HasHospitalReputation` 在未注册时返回 false：

```text
HospitalReputation.IsRegistered && Get() >= threshold
```

- [ ] **Step 3: 运行测试**

- [ ] **Step 4: 提交**

---

### Task 8: 完整验证

**Files:**
- Modify: `docs/design/story-v5-all-text.md`（只改合同数值描述）
- Modify: `docs/design/hospital-reputation-code-plan.md`（标记已实现）
- Test: full test + game smoke

- [ ] **Step 1: 完整构建**

```powershell
dotnet build FluxForge.csproj -c Release -p:GameManagedDir="E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed"
```

- [ ] **Step 2: 全部测试**

```powershell
dotnet test tests\FluxForge.Tests\FluxForge.Tests.csproj -c Release
```

- [ ] **Step 3: 游戏 smoke**

```text
1. 未签约：无医院 faction
2. 首次高浓度对话后下一次到访：出现合同提议
3. 拒绝：不重复提议
4. 接受：医院 faction 出现在原版声望 UI
5. 签约后第 7 天：合同结算到访
6. 交付 300mL 99%：+3 声望，付款 150
7. 交付 100mL 100%：score 300，同样达标
8. 空交付：-3 声望，不付款
9. 重载：声望和合同状态保留，faction 不重复
```

- [ ] **Step 4: 打包验证**

包中只需要：

```text
FluxForge.dll
manifest.xml
Textures/
AssetBundles/
Localization/
```

- [ ] **Step 5: 提交**

---

## 防御性代码删减员审阅

### 保留

| 防御代码 | 理由 |
|---|---|
| `EnsureFaction` 查重 | 签约/读档可能重复调用；重复 faction 会破坏 UI 和存档 |
| `HospitalContractState.Start` 幂等 | 重复签约事件不能重置合同锚点 |
| `MarkSettled` 只在 due 时调用 | 防止重复结算、重复付款、重复加声望 |
| `SubstanceTotal <= 0` 跳过 | 空瓶/空容器是正常游戏状态 |
| 浓度边界 epsilon | 99%/100% 是浮点边界，必须稳定判定 |
| `FactionId` 为空不设置 `clientFaction` | 奥雅等无 faction NPC 的正常路径 |
| 无匹配物品时返回 score 0 | 空合同是合法业务结果，不是异常 |

### 删除

| 过度防御 | 删除理由 |
|---|---|
| `try/catch` 包住 `ModReputation` | 游戏内 API，失败应暴露而不是吞掉 |
| 每次 `Add` 都检查 `PlayerStore.Instance == null` | 剧情运行时必然存在 |
| `ReputationUIManager.Instance == null` 检查 | 原版管理器存在；UI 刷新单独测试 |
| `storeReputations == null` 检查 | 原版初始化和存档保证 |
| 对自写 modData 使用 `int.TryParse` + 回退 | 数据由本 mod 写入；只检查 key 是否存在 |
| 对固定 `FlowPropertyKey` 做 null/非法 key 检查 | 配置是编译期常量 |
| 自动修复被修改的存档 | 用户已明确不考虑入侵/异常存档 |
| 为“中途退出”专门回滚合同/声望 | 正常路径不处理异常退出 |

### 审阅结论

```text
通过，但必须保证：
- EnsureFaction 幂等
- 合同只在 due 时结算一次
- 空交付是正常失败，不抛异常
- 不引入全局单例 null 检查
```

---

## 程序员评审

### 结论

```text
有条件通过
```

### 认可

```text
- 复用 StoreReputation 而不是自建声望存档
- 使用实例 ModReputation(double) 处理小数声望
- 医院 NPC 继续用现有 StoryEvent/VisitRule
- 合同计分拆成纯函数，可单测
- 第一版不做自定义 perk/UI，范围合理
```

### 必改

```text
1. 不能用静态 ModReputation(string,int)
   否则 value/400 和 score/100 的小数会丢失。

2. 合同结算必须只调用一次
   story event 条件和 MarkSettled 双重保证，不做 try/catch。

3. 运行时 EnsureFaction 后，
   原版 ReputationUIManager 如果已经打开，列表可能不刷新。
   必须先做手动验证，再决定是否需要关闭/重开 UI。

4. 合同到期 VisitRule 的优先级必须高于
   acid_buyer.seven_days，否则可能被普通到访抢先。

5. 合同结算 StoryEvent 的优先级必须高于
   repeat_visit，否则玩家只会看到日常收货。

6. NpcDefinition 的 FactionId 必须是可选参数，
   不能破坏奥雅现有构造调用。

7. 医院 faction 只在签约后创建；
   不要为了“以后可能有合同”提前注册。
```

### 未解决

```text
- 合同提议的对白是否直接复用 story-v5-all-text.md 的旧稿
  （旧稿仍写 300mL/99%，需要按新计分改写）
- 医院合同 UI 是否需要单独的“声望未解锁”提示
- 其他医院 NPC 的实际交易方式尚未设计，因此 AddTrade 只留接口
```

---

## 执行顺序

```text
Task 1 纯计分/相位
Task 2 NpcDefinition faction
Task 3 医院声望服务
Task 4 合同状态/提议
Task 5 化学物交付
Task 6 合同剧情接线
Task 7 StoryContext
Task 8 验证/打包
```

Task 1 和 Task 2 可以并行；Task 3 依赖 Task 2；Task 4 依赖 Task 1；Task 5 依赖 Task 1；Task 6 依赖 Task 3/4/5；Task 8 最后。
