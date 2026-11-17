> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 低频剧情链设计 v3

> 依据：[原版剧情/访客逻辑核对](original-story-logic.md)
> 基调：[太空站店主：黑暗基调与前期剧情设计](dark-tone-storyline.md)
> 旧版道德向设计不再使用。

## 0. 结论先行

原版不是“每天一个新剧情”。

原版是：

```text
条件/日期门控的临时客户
+ 少量不可遣散的教学/剧情客户
+ 随机普通客户
```

本 Mod 改成：

```text
低频剧情链
+ 玩家主动接受
+ 每个节点都可拒绝
+ 拒绝后不自动续接
+ 不扣钱、不扣声望、不训斥
```

核心调整：

```text
不做每日新剧情
同一时间最多一条主动剧情链
两条链开始之间至少间隔 3 个游戏日
同一天最多一个剧情链节点
每个链节点都有“到此为止”选项
拒绝整条链不会影响其他链、日常客户或经济
```

12 条链不是 10 天全部塞完。它们分布在前 30–40 天，部分由玩家行为触发，不按日历排队。

---

## 1. 原版依据

### 1.1 条件/日期门控

`StoreClientManager.HandleAlwayClient()` 中：

```csharp
if (12 == StoreStation.Instance.dayCounter
    && !PlayerStore.Instance.isAugIntroduced)
{
    AddClient(StoreClientListStory.CreateMentorShotgun());
}

if (24 == StoreStation.Instance.dayCounter
    && !PlayerStore.Instance.hasBusinessPermit)
{
    AddClient(StoreClientListStory.CreateMentorPermit());
}

if (38 <= StoreStation.Instance.dayCounter
    && StoreStation.Instance.dayCounter % 7 == 3
    && !PlayerStore.Instance.hasBusinessPermit)
{
    AddClient(StoreClientList.CreateBusinessPermit());
}
```

不是每天一条。

### 1.2 原版强制剧情

`StoreClientListStory.CreateMentorClient()`：

```csharp
new StoreClient
{
    identifier = "mentor",
    clientIntent = StoreClient.ClientIntent.DIALOGUE,
    dismissable = false,
    mainDialogue = StoreClientDialogList.MentorIntroDialog()
};
```

原版用 `dismissable = false` 保证教学送达。

本 Mod 明确不照搬：

```text
首次接触可以只有“接受 / 拒绝”两句
拒绝后立刻结束
不把玩家锁在剧情里
```

### 1.3 临时访客

`StoreClientListAppraisal.CreateAppraisalRandom()`：

```csharp
storeClient.identifier = "scavAppraisalRandom";
storeClient.clientIntent = StoreClient.ClientIntent.APPRAISAL_SERVICE;
storeClient.mainDialogue
    .SetText(...)
    .NextDialogue()
    .SetText(...)
    .SetEndAction(() =>
    {
        // 随机生成 2–5 件物品
    });
```

这是“偶发临时访客”，不是固定主线。

---

## 2. 剧情链调度模型

### 2.1 一条链的生命周期

```text
未出现
  ↓ 条件满足 + 至少间隔 2–4 天 + 无其他主动链
出现一次“是否要谈”的邀请
  ├─ 玩家拒绝 → ended，彻底结束（不惩罚）
  └─ 玩家接受 → active
        ↓ 下一节点在下一/两次到访
        ├─ 玩家继续 → 下一节点
        └─ 玩家退出 → ended
```

### 2.2 链不是新引擎

使用现有字段即可：

```text
StoryEvent.Condition
StoryEvent.MaxTimes
StoryEvent.ExclusionGroup
StoryEventDefinition.Outcome
StoryEvent 内容里的 flag 写入
```

链状态：

```text
chain.<id>.offered
chain.<id>.started
chain.<id>.step1
chain.<id>.step2
chain.<id>.ended
```

事件条件只依赖：

```text
上一步 flag
未 ended
无其他 active 链
```

不需要新增 StoryChain 运行时。

### 2.3 频率

```text
普通客户：每天照常
薇拉日常：每天可来，但只做短交易
剧情链：每 3–4 天最多出现一个新入口
同一天最多一个剧情链节点
临时采购员：低概率，独立于剧情链
```

硬规则：

```text
lastStoryStartDay + 3 <= 当前天数
否则不出现新的剧情链入口
```

“每天来一个新剧情”明确禁止。

---

## 3. 玩家自由

### 3.1 每个链的入口都有拒绝

```text
“有件事想跟你谈。”
选项：
  1. 听
  2. 不感兴趣
```

拒绝：

```text
设置 chain.<id>.ended
NPC 只说一句“行。”
不扣钱
不扣声望
不影响其他链
不写后续 flag
```

### 3.2 链中随时退出

每个节点至少有一个：

```text
“到此为止。”
```

退出后：

```text
当前链 ended
不自动生成后续事件
NPC 不再追问
```

### 3.3 拒绝不会变成“错过永久内容”的焦虑

```text
拒绝的是这条链，不是整个游戏
其他链仍按自己的条件出现
日常客户、采购、薇拉交易不受影响
玩家不会因为拒绝而被系统标记为“坏人”
```

### 3.4 玩家仍可回头

如果玩家之后主动想谈：

```text
NPC 的普通日常可以出现一次“再谈”入口
只有玩家选择“再谈”才重开
不会自动重开
```

重开条件：

```text
chain.<id>.ended
且距离 ended 至少 3 天
且玩家在普通对话中主动选择
```

### 3.5 玩家发起，不是 NPC 每天派任务

```text
NPC 只负责“出现一次邀请”
玩家负责“要不要开始”
```

具体做法：

```text
邀请只有一次
拒绝后 NPC 不再追问
玩家可以在之后的日常对话里主动问“之前那件事”
玩家不问，链就保持 ended
不显示任务列表
不弹窗
不写“你错过了内容”
```

链的启动优先由玩家动作触发：

```text
玩家去奥雅货架看机器 → former_intro
玩家把高纯度酸放到后柜台 → procurement
玩家卖出有问题的货 → return
玩家接了黑市货 → inspection
玩家主动问奥雅旧厂 → oya_past
```

而不是：

```text
第 N 天自动塞给玩家一个新的故事
```

---

## 4. 剧情链目录

共 12 条新链 + 既有奥雅教程。每条 3–4 个节点。

“最早出现”不是固定日程，而是条件上限；大多数链必须由玩家行为或前序选择触发。

| ID | 名称 | 最早出现 | 长度 | NPC | 机制 | 玩家选择 | 拒绝后果 |
|---|---|---|---:|---|---|---|---|
| chain.procurement | 上层采样 | Day 3 | 3 | 站务采购 | 净化/化学残留 | 接单/拒绝/压价 | 不来，无惩罚 |
| chain.former_intro | 新机器 | 奥雅二次到访（约 Day 8） | 2 | 奥雅 | 成型机 | 看/买/不买 | 机器仍在库存，不催 |
| chain.shortage | 短缺 | Day 12 | 3 | 薇拉/采购 | 净化/库存 | 涨价/保供/停卖 | 价格恢复，无惩罚 |
| chain.vera_supply | 低价供货 | 采购链后 | 3 | 薇拉 | 批量酸/碱 | 供货/留给自己/抬价 | 薇拉继续日常，无惩罚 |
| chain.former_order | 成品订单 | 买成型机后 +2 天 | 3 | 奥雅/采购 | 成型机产品 | 接/不接/转卖 | 订单消失，不影响原料线 |
| chain.return | 退货 | 卖出问题货后 | 3 | 薇拉 | 化学残留 | 赔/拒赔/换货 | 关系变化，但链可结束 |
| chain.inspection | 抽检 | 有风险交易后 | 3 | 站务 | 记录/库存 | 配合/贿赂/关门 | 风险自担，不强制 |
| chain.rival | 同行 | 完成 2 次采购后 | 3 | 采购/薇拉 | 价格战 | 跟价/不跟/合作 | 不追，无惩罚 |
| chain.oya_past | 旧厂 | 买成型机后 | 4 | 奥雅 | 设备来源 | 追问/不追问/买断 | 不影响主线 |
| chain.upper_contract | 上层合同 | 完成 2 次采购后 | 4 | 采购 | 高纯度 | 签/不签/谈条件 | 失去准入，但可继续经营 |
| chain.black_market | 黑市 | oya_past 后 | 4 | 薇拉 | 受限化学品 | 接/不接/举报 | 关系/风险自担 |
| chain.blackout | 停电 | Day 20 | 3 | 奥雅/薇拉 | 设备/库存 | 囤货/保供/关门 | 事件过去，无惩罚 |

“拒绝后果”不是惩罚，而是这条链不再推进；其他内容继续。

---

## 5. 前 10 天节奏

```text
Day 1  奥雅教程（既有）
Day 2  普通营业，无新剧情
Day 3  采购链入口（可拒绝）
Day 4  普通营业，无新剧情
Day 5  普通营业，无新剧情
Day 6  普通营业，无新剧情
Day 7  普通营业，无新剧情
Day 8  奥雅二次到访：成型机介绍（可拒绝/不买）
Day 9  普通营业，无新剧情
Day 10 普通营业，无新剧情
```

规则：

```text
前 10 天只有 2 个剧情入口，不是 10 个
每个链只在自己的条件满足时出现
同一天最多一个剧情链节点
普通日常客户不占剧情槽位
```

后续链按行为/天数解锁，例如：

```text
买了成型机 → former_order / oya_past
卖出问题货 → return
有风险交易 → inspection
完成采购 → rival / upper_contract
Day 12 之后 → shortage
```

---

## 6. 关键链设计

### 6.1 chain.procurement：上层采样

**入口**

```text
采购员：“99% 酸，化学残留低于 1%。你有这个设备。”
选项：
  1. “可以谈。”
  2. “不做。”
  3. “你先说价格。”
```

**节点 2**

```text
玩家交付后：
采购员：“这批可以。以后我可能还会来。”
选择：
  1. 建立长期供应
  2. 只做这一次
```

**节点 3**

```text
长期供应 vs 单次高价：
长期：每次价格略低，但固定机会
单次：这次多拿钱，之后概率降低
```

拒绝：

```text
采购员不劝，转身离开。
不扣声望，不影响薇拉。
```

### 6.2 chain.vera_supply：低价供货

**入口**

```text
薇拉：“诊所预算低。你卖给上层是卖，卖给我也是卖。”
选项：
  1. 接低价单
  2. 留给上层
  3. 要求预付
```

**节点 2**

```text
如果玩家接了：
薇拉要求稳定数量，价格仍低
如果拒绝：
薇拉不再提，继续日常收购
```

**节点 3**

```text
短缺时，薇拉要求优先供货
玩家可以：
  保供（关系+，现金少）
  涨价（现金+，关系-）
  停卖（关系-，库存保留）
```

没有“正确”选项。

### 6.3 chain.former_intro：新机器

**入口**

```text
奥雅把机器放在柜台上：
“成分成型机。我自己改的。”
“纯化后的液体按配方压成成品。耗电，配方在机器里。”
“今天起放我货里。买不买随你。”
选项：
  1. “多少钱？”
  2. “能做什么？”
  3. “不买。”
```

**节点 2**

```text
如果玩家问功能：
奥雅只讲事实：
  “耗电。”
  “需要纯化后的液体。”
  “能做助熔剂。”
如果玩家拒绝：
机器仍在奥雅库存，之后可买。
```

**节点 3（可选）**

```text
玩家购买后：
奥雅：“配方别乱改。炸了别说是我的机器。”
```

### 6.4 chain.former_order：成品订单

**入口条件**

```text
玩家已有 fluxforge_chemical_former
且完成过一次采购链
```

**内容**

```text
采购员：“上次的酸不错。有没有助熔剂成品？”
选项：
  1. 接单
  2. 只卖原料
  3. 问规格
```

**奖励**

```text
成品单价高于原料
但需要重金属原料和电量
```

**拒绝**

```text
采购员只收原料，价格低一档。
不催，不惩罚。
```

### 6.5 chain.return：退货

**入口条件**

```text
玩家曾把含沉淀/化学残留的货卖给薇拉
```

**内容**

```text
薇拉把瓶子放回柜台：
“这批有沉淀。诊所不要。”
选项：
  1. 退款
  2. 换货
  3. 拒绝负责
```

后果：

```text
退款：现金-，关系不变
换货：库存-，关系+
拒绝：现金保留，关系-
```

没有道德评价。

### 6.6 chain.inspection：抽检

**入口条件**

```text
玩家有未记录的化学品交易，或触发原版检查条件
```

**内容**

```text
站务检查员：“把最近三天的出货记录拿出来。”
选项：
  1. 配合
  2. 贿赂
  3. 关门，改天再说
```

后果：

```text
配合：可能罚款/放行
贿赂：现金-，风险 flag
关门：当天不营业，检查推迟
```

玩家可以拒绝整条链，关门即可。

### 6.7 chain.oya_past：旧厂

**入口条件**

```text
玩家已购买成型机，且完成过采购链
```

**内容**

```text
奥雅：“机器是从旧厂拆出来的。”
“有些原料，他们不让人问。”
选项：
  1. 追问
  2. 不追问
  3. 只谈买卖
```

后续：

```text
追问：获得旧厂信息，解锁 black_market/upper_contract 的额外选项
不追问：链结束，不影响经营
```

不是“揭发邪恶”主线。

### 6.8 chain.upper_contract：上层合同

**入口条件**

```text
玩家完成过 2 次采购链
且净化/分离达到要求
```

**内容**

```text
采购员：“上层要签独家供应。”
“签了，你的货只能走我们。”
选项：
  1. 签
  2. 不签
  3. 谈非独家
```

后果：

```text
签：稳定高价，失去薇拉/黑市部分订单
不签：保留自由，采购概率下降
谈：需要之前有稳定交付记录
```

没有“正确”选择，只有经营路线。

### 6.9 chain.black_market：黑市

**入口条件**

```text
玩家有受限化学品
或完成过 oya_past 的追问
```

**内容**

```text
薇拉：“有个客户要一批不登记的酸。”
选项：
  1. 接
  2. 不接
  3. 问用途
```

后果：

```text
接：现金高，检查风险+
不接：薇拉不劝
问用途：信息+，但薇拉警惕
```

### 6.10 chain.blackout：停电

**入口条件**

```text
Station day >= 18，随机/事件触发
```

**内容**

```text
奥雅：“上层把电切了。冷萃和离心都停了。”
薇拉：“诊所需要货。”
选项：
  1. 用手头库存保供
  2. 涨价
  3. 关门
```

不是灾难救援，是库存和价格选择。

---

## 7. 临时采购员

不进入剧情链槽位。

```text
独立 VisitRule
低概率
3 天冷却
收购 99% 酸/碱
要求低化学残留
价格高
```

它可以在任何一天出现，也可以很多天不出现。

与剧情链的区别：

```text
剧情链：玩家选择是否推进
临时采购：玩家选择卖不卖
```

两者都不强制。

---

## 8. 成型机

```text
Oya 介绍 → 加入库存 → 玩家可买
不购买不影响任何链的启动
购买后 former_order 才会出现
```

对话原则：

```text
只讲功能/消耗/产物
不讲“这是为了教你”
不评价玩家买不买
```

---

## 9. flag 与条件

```text
chain.<id>.offered
chain.<id>.started
chain.<id>.step1
chain.<id>.step2
chain.<id>.ended
chain.active
```

约束：

```text
同一时间 chain.active 只能有一个 id
开始新链前检查 chain.active 为空或 ended
拒绝后写 ended，不写 step
普通日常不读写这些 flag
```

如果现有框架只能存 key：

```text
用 chain.active=<id> 和 chain.<id>.step=<n>
```

不需要改运行时。

---

## 10. 验收

```text
1. 第 1 天只有奥雅教程，没有第二个新剧情。
2. 第 2 天没有新剧情链。
3. 第 3 天采购链入口出现，玩家可以拒绝。
4. 拒绝后第 4 天不会再自动追问。
5. 拒绝采购链不影响薇拉日常。
6. 同时只能有一条 chain.active。
7. 每条链的每个节点都有退出选项。
8. 退出后不写后续 step。
9. 玩家可以整天不接任何剧情，只做普通买卖。
10. 成型机介绍后不加到玩家背包，只加奥雅库存。
11. 玩家不买成型机，仍能完成采购/薇拉链。
12. 临时采购员不每天出现。
```

---

## 11. 与旧版区别

```text
旧版：7 个事件 + 每日推进 + 道德选择
新版：12 条链 + 低频节点 + 商业选择 + 可拒绝
```

旧版文件：

```text
early-game-storyline.md  （已废弃）
dark-tone-storyline.md   （基调参考）
```

新版以本文为准。
