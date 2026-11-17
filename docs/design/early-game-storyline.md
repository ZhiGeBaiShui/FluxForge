> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 前期剧情线与日常剧情设计

> 方向已废弃：本文的“诊所/姐姐/揭发/道德结局”不符合当前太空站近赛博朋克基调。当前设计见 [dark-tone-storyline.md](dark-tone-storyline.md)。

## 0. 设计目标

玩家刚完成奥雅的 95% 酸教程，身上有：

```text
med_bottle_red（约 90% 酸 + 10% 化学污染）
2 个发射器
1 个升温装置
1 个净化装置
1 张入门便条
```

目标是在**不新增装置、不新增液体物品、不要求建造大型产线**的前提下，让玩家在 30 分钟内完成第一笔真实交易，并在前 3～7 天形成：

```text
处理原料 → 达到客户要求的浓度 → 交付 → 获得钱/新装置 → 接到下一单
```

长线版本扩展到：

```text
14 个游戏日 / 三幕 / 约 2.5–4 小时
第一幕：拿到第一笔钱，建立两个客户
第二幕：发现“纯度”背后的代价
第三幕：选择站在谁的标准一边
```

## 0.1 参考作品与吸收点

| 作品 | 吸收的优点 | 在本设计中的具体做法 |
|---|---|---|
| Stardew Valley | 日常节奏、关系里程碑、每天都有小期待 | 薇拉每天来，奥雅按周期来；日常台词随任务 flag 变化 |
| Papers, Please | 检查、污染、道德压力 | 纯度不是数字，而是“这批货能不能救人”的选择 |
| Pentiment | 时间有限、选择不可逆、社会后果 | 14 天时间窗；帮助一方会失去另一方的信任 |
| Disco Elysium | 对话即人物，选项暴露价值观 | 玩家选择“诚实/效率/忠诚”，NPC 会记住并改变语气 |
| Hades | 可重复的短循环中推进长线 | 每天 30–90 秒的薇拉交易，也会推进关系台词 |
| The Witcher 3 | 道德灰度的委托，准备影响结果 | 提前测试/留样/赶工，会改变后续事件 |
| Citizen Sleeper | 资源压力下的安静日常 | 不靠战斗，靠现金、原料、信任和当天时间做取舍 |

不吸收：

```text
不引入对话轮盘/属性检定/新战斗
不做开放世界
不做多结局大分支树导致内容量爆炸
```

## 1. 三条支柱

### 支柱一：把“脏原料”变成“可卖的纯品”

- 玩家感受：我不是在刷材料，而是在控制温度、污染和浓度。
- 机制：升温装置 + 净化装置；净化一次达到温度支持的纯度上限。
- 可观察验证：玩家能在教程后用同一瓶酸做出 99% 酸并卖给薇拉。

### 支柱二：两个客户，一个导师

- 玩家感受：奥雅教我技术，薇拉给我第一笔真实订单。
- 机制：奥雅按周期来访，负责装置和长期目标；薇拉每日来访，负责收购和短期压力。
- 可观察验证：玩家能说清“为什么做 99% 酸”和“做完卖给谁”。

### 支柱三：纯度是有代价的

- 玩家感受：把货做得更纯，不只是为了多赚钱，也决定谁信我。
- 机制：赶工/留样/如实记录/隐瞒污染，会改变薇拉和奥雅后续对话与结局。
- 可观察验证：同一批货选择“赶工”或“复检”，后续日常台词和终局不同。

## 2. 非目标

```text
不新增 NPC 立绘
不新增装置物品
不新增液体定义
不要求大型自动化工厂
不引入订单 UI / 新交易系统
不要求玩家记住复杂配方
```

所有剧情只使用：

```text
StoryEvent + StoryAssembly
VisitRule
现有设备、液体、Vera 交易、Oya 商店
```

## 3. 前 30 分钟体验

```text
0–5 分钟：
  奥雅教程，做出 95% 酸

5–10 分钟：
  第二天薇拉到访，提出 99% 酸收购
  玩家把教程酸继续升温、净化，达到 99%
  第一次交付，拿到钱

10–20 分钟：
  奥雅下一次到访，带来一台旧离心机
  玩家第一次处理薇拉赠送的受污染原料

20–30 分钟：
  薇拉收购 99% 碱
  玩家拿到第一张“酸 + 碱”的长期合同
```

## 4. 前期任务线（第一幕）

### Q1：第一笔真订单

```text
ID: quest.first_sale
NPC: 薇拉
触发: Vera TradeCompleted
优先级: 100
条件: 交易成功 && 尚未完成 Q1
```

剧情：

```text
薇拉收下酸后，语气稍微缓和：
“第一次就能做到这个浓度，比我想的强。”
“以后我每天都会来。你只要保证浓度，我就收。”
“诊所那边最近也缺货……算了，先把你这批做好。”
```

目标：

```text
向薇拉成功交付任意一瓶 >=99% 的酸
```

奖励：

```text
基础收购款
额外 25 块“稳定供货”奖金
flag: quest.first_sale_done
```

教学点：

```text
交易成功本身就是任务完成条件
不需要新增订单 UI
```

### Q2：分离第一课

```text
ID: quest.separation
NPC: 奥雅
触发: Oya VisitEnding（奥雅二次剧情结束后）
优先级: 80
条件: HasSecondVisit && quest.first_sale_done && !quest.separation_done
```

剧情：

```text
奥雅看过薇拉留下的受污染酸液：
“净化装置拿得掉化学污染，拿不掉塑料、矿物和碎屑。”
“这台旧离心机借你用。按粒径分开，剩下的才是你要的酸。”
```

目标：

```text
第一步：只用净化装置处理脏酸，交给薇拉，被拒收
第二步：奥雅拿出旧离心机
第三步：用离心机把同一批脏酸处理到 >=99%
```

奖励：

```text
一台破旧 flow_centrifuge（剧情赠送）
flag: quest.separation_done
```

教学点：

```text
先让玩家踩到“净化不够用”的坑，
再让工具需求从失败里长出来，而不是先送工具再解释。
```

### Q3：碱也要

```text
ID: quest.base_sample
NPC: 薇拉
触发: Vera VisitStarted
优先级: 90
条件: quest.separation_done && !quest.base_sample_done
```

剧情：

```text
“酸能稳定了，就试试碱。”
“我这里有两瓶受污染的碱，处理到 99%，我照样收。”
“诊所最近催得紧，碱也要备一点。”
```

目标：

```text
向薇拉交付 >=99% 的碱
```

奖励：

```text
薇拉的碱收购款
额外 30 块
flag: quest.base_sample_done
```

教学点：

```text
酸和碱共用同一套处理思路，但污染组成不同。
```

### Q4：第一张长期合同

```text
ID: quest.first_contract
NPC: 薇拉
触发: Vera VisitStarted
优先级: 95
条件: quest.base_sample_done && !quest.first_contract_done
```

剧情：

```text
“我有个固定的客户，要一批酸和一批碱。”
“不用多，各 100 mL，浓度 99% 以上。”
“做到这一次，我就把你当成固定供货商。”
```

玩家选择：

```text
A. 接受固定低价
   稳定收入，薇拉信任+
B. 要求涨价
   每单多 40，薇拉信任-
C. 要求先付材料费
   立刻拿 60 预付款；需要之前有如实记录/未伪造的 flag
```

目标：

```text
交付 100 mL >=99% 的酸
交付 100 mL >=99% 的碱
```

奖励：

```text
两张订单的收购款
基础额外 80 块，按谈判选择调整
flag: quest.first_contract_done
```

后续：

```text
薇拉的每日收购对话进入“固定供货商”版本
奥雅的下一次到访引出 Q5
```

### Q5：把流程固定下来

```text
ID: quest.stable_line
NPC: 奥雅
触发: Oya VisitStarted
优先级: 70
条件: quest.first_contract_done && !quest.stable_line_done
```

剧情：

```text
“你现在不是在做实验，是在做生意。”
“把升温、净化、分离摆成一条顺手的路线。以后每天跑一遍。”
```

目标：

```text
交付 180 mL >=99.5% 的酸
可以分两天交付
```

奖励：

```text
200 块
flag: quest.stable_line_done
```

教学点：

```text
从“能做出来”过渡到“能稳定重复”；
用适中的体积避免把剧情高潮变成重复操作。
```

## 4.1 第二幕：干净的货

第二幕从“会做”转向“做给谁、要不要说谎”。

### Q6：加急单

```text
ID: act2.rush_order
NPC: 薇拉
触发: Vera VisitStarted
优先级: 100
条件: quest.stable_line_done && !act2.rush_done
```

剧情：

```text
薇拉这次没有寒暄：
“250 mL，99.5%，明天要。”
“诊所那边等不了。你能做，我就加钱；不能做，我现在去找别人。”
```

玩家选择：

```text
A. 赶工交付
   flag: act2.rushed
   当天能交，+120 加急费
   下一次薇拉到访时触发“沉淀被退回”的后续对话
B. 先复检再交
   flag: act2.careful
   晚一天交，+40，薇拉不耐烦
   后续不触发退货，但 Q9 仍有近失事件
```

目标：

```text
交付 250 mL >=99.5% 的酸
```

设计意图：

```text
第一次让“速度”和“可靠性”冲突。
选择要在下一幕有可见后果，而不只是剧情 flag。
```

### Q7：奥雅的旧样品

```text
ID: act2.old_sample
NPC: 奥雅
触发: Oya VisitStarted
优先级: 90
条件: act2.rush_done && !act2.old_sample_done
```

剧情：

```text
奥雅带来一瓶封得很严的旧酸：
“北环水处理厂当年给我的样品。”
“他们说是工业级，不该进食品和医疗线。”
“你帮我看看，里面到底有什么。”
```

玩家选择：

```text
A. 认真检测
   flag: act2.tested_sample
   发现异常重金属，解锁后续“揭发”路线
B. 不碰旧账
   flag: act2.ignored_sample
   奥雅不再提，但关系变冷
```

奖励：

```text
检测路线：奥雅给 80 块实验补贴
回避路线：无
```

设计意图：

```text
把“检测”从数字变成道德选择；
玩家可以拒绝主线，但关系会变化。
```

### Q8：薇拉的审核

```text
ID: act2.audit
NPC: 薇拉
触发: Vera VisitStarted
优先级: 95
条件: act2.old_sample_done && !act2.audit_done
```

剧情：

```text
“诊所那边要看出货记录。”
“你只要写‘纯度合格’，他们不会追问。你要是写太多，他们反而会怕。”
```

玩家选择：

```text
A. 如实写记录
   flag: act2.honest_records
   薇拉不满，但信任+
B. 按薇拉的意思写
   flag: act2.forged_records
   现金+60，埋下泄漏事件
C. 拒绝签字
   flag: act2.refused_audit
   薇拉暂时取消订单
```

设计意图：

```text
让玩家第一次感到“文件名”和“货”一样有重量。
```

### Q9：泄漏

```text
ID: act2.spill
NPC: 薇拉
触发: Vera VisitStarted
优先级: 110
条件: act2.audit_done && !act2.spill_done
```

剧情：

```text
无论前面是否赶工/伪造记录，这一批原料都会出问题：
- 赶工/伪造：问题货已经发到诊所
- 复检/如实记录：你在出货前拦下了异常批次

薇拉脸色很差：
“诊所那边有一批货出了问题。不是你的名字，但批次号是你的。”
“现在有两个选择：召回，还是当没看见。”
```

玩家选择：

```text
A. 召回并复检
   flag: act2.recalled
   损失 150 块，薇拉信任+
B. 压下消息
   flag: act2.covered_up
   保留现金，奥雅信任-
C. 直接去找诊所
   flag: act2.went_to_clinic
   解锁第三幕“诊所线”
```

设计意图：

```text
不惩罚玩家“赶工”，而是让选择产生可承担/不可承担的后果。
即使一路谨慎，也会有近失事件，保证第二幕和第三幕不断线。
```

### Q10：北环的报价

```text
ID: act3.northloop_offer
NPC: 奥雅
触发: Oya VisitStarted
优先级: 100
条件: act2.spill_done && !act3.offer_done
```

剧情：

```text
奥雅把一封没有署名的报价放在桌上：
“北环想买你所有 99.5% 以上的货。”
“价格很好。条件是——别再问原料从哪来。”
```

玩家选择：

```text
A. 接受独家合同
   flag: act3.signed_exclusive
   +500，后续每日收入提高
B. 拒绝
   flag: act3.refused_exclusive
   奥雅明显松了口气
C. 假装接受，暗中留样
   flag: act3.double_agent
   高风险，需要 act2.tested_sample
```

### Q11：薇拉的选择

```text
ID: act3.vera_truth
NPC: 薇拉
触发: Vera VisitStarted
优先级: 105
条件: act3.offer_done && !act3.vera_truth_done
```

剧情：

```text
“诊所是我姐姐开的。”
“她用的酸，有一半是从你这里买的。”
“你可以继续卖给北环，也可以帮我撑过这个月。”
```

玩家选择：

```text
A. 优先供诊所
   flag: act3.clinic_first
   收入降低，薇拉信任++
B. 按合同优先北环
   flag: act3.contract_first
   收入提高，薇拉信任-
C. 两边都供，自己加班
   flag: act3.both_sides
   需要 act2.careful/act2.recalled，否则触发事故
```

### Q12：最后一批

```text
ID: act3.final_batch
NPC: 薇拉
触发: Vera VisitStarted
优先级: 120
条件: act3.vera_truth_done && !act3.final_batch_done
```

剧情：

```text
“最后一批。300 mL，99.9%。”
“这次不是测试，是交货。”
```

目标：

```text
交付 300 mL >=99.9% 的酸
允许分两批交付
完成任意一个终局选择的前置条件
```

奖励：

```text
基础货款
根据终局路线追加 200–500
```

### Q13：结账

```text
ID: act3.ending
NPC: 薇拉（Q12 结束后）
触发: Vera VisitEnding
优先级: 200
条件: act3.final_batch_done && !act3.ending_seen
```

根据 flag 进入三个主要结局：

```text
结局 A：稳定供货商
  act3.signed_exclusive
  → 钱多、订单稳定；奥雅保持距离；薇拉成为纯商业伙伴

结局 B：独立工匠
  act3.refused_exclusive + act3.clinic_first
  → 收入较低；奥雅留下；薇拉成为长期客户

结局 C：揭发者
  act2.tested_sample + act3.double_agent
  → 北环合同取消；薇拉诊所受牵连；奥雅离开/重新出现
  → 解锁“城市水质”后续伏笔

最佳变体：
  诚实记录 + 召回 + 拒绝独家 + 优先诊所
  → “公开实验室”结局，后续日常剧情使用合作版本
```

设计意图：

```text
结局不是“好/坏”按钮，而是之前选择的汇总。
每个结局都保留继续经营和日常剧情的空间。
```

## 4.2 奥雅到访节奏

14 天弧线不能只靠 7 天一次奥雅。增加一条**仅剧情期间生效**的规则：

```text
flow_emitter.story_visit
Priority 20
Cooldown 3 天
Condition: arc.active
VisitKind.Sell
```

现有 `flow_emitter.every_7_days` 保持 Priority 10。剧情期间奥雅约每 3 天来一次；弧线结束后移除/关闭该规则，恢复每 7 天。

设计理由：

```text
第二幕和第三幕需要奥雅多次出现；
不能用“等七天”消耗玩家耐心；
到访规则本身支持Priority和Cooldown，不需要改NPC控制器。
```

## 5. 日常剧情

日常事件全部低于任务事件；任务条件不成立时，至少保留一条日常对话，避免玩家空等。

| ID | 标题 | NPC | Trigger | Priority | MaxTimes | 条件 | Outcome | 玩家动作 | 奖励 |
|---|---|---|---|---:|---:|---|---|---|---|
| daily.vera.standard | 日常收购 | 薇拉 | VisitStarted | 10 | 0 | 总是 | Leave | 卖 99% 酸/碱 | 基础价 |
| daily.vera.two_bottles | 今天要两瓶 | 薇拉 | VisitStarted | 30 | 0 | separation_done | Leave | 卖 2 瓶酸 | +10 |
| daily.vera.base | 碱也要 | 薇拉 | VisitStarted | 30 | 0 | base_sample_done | Leave | 卖 99% 碱 | +10 |
| daily.vera.high_purity | 更纯一点 | 薇拉 | VisitStarted | 40 | 1 | first_contract_done | Leave | 卖 99.5%+ | +30 |
| daily.vera.contract | 固定供货商 | 薇拉 | VisitStarted | 50 | 0 | first_contract_done | Leave | 自由交易 | 基础价 |
| daily.vera.clinic | 诊所的订单 | 薇拉 | VisitStarted | 55 | 0 | act3.clinic_first | Leave | 优先供诊所 | +20 |
| daily.vera.aftermath | 余波 | 薇拉 | VisitStarted | 60 | 0 | act3.ending_seen | Leave | 听后续 | 无 |
| daily.oya.mentor | 奥雅看看货 | 奥雅 | VisitStarted | 40 | 0 | base_sample_done | OpenShop | 对话后开店 | 无 |
| daily.oya.new_stock | 新装置 | 奥雅 | VisitStarted | 35 | 0 | separation_done | OpenShop | 查看库存 | 无 |
| daily.oya.advice | 一句提醒 | 奥雅 | VisitStarted | 20 | 0 | 总是 | OpenShop | 听建议 | 无 |
| daily.oya.memory | 旧厂的事 | 奥雅 | VisitStarted | 55 | 0 | act2.tested_sample | OpenShop | 追问北环 | 无 |
| daily.oya.warning | 合同的代价 | 奥雅 | VisitStarted | 55 | 0 | act3.signed_exclusive | OpenShop | 听警告 | 无 |
| daily.oya.contract | 长期合作 | 奥雅 | VisitEnding | 60 | 1 | first_contract_done | Leave | 听总结 | 50 |
| daily.vera.rush | 加急单 | 薇拉 | VisitStarted | 60 | 1 | stable_line_done | Leave | 当天交 100% 酸 | +80 |

互斥建议：

```text
daily.vera.* 同组 daily_vera
daily.oya.* 同组 daily_oya
quest.* / act*.* 高优先级，选中后消费对应互斥组
```

日常设计原则：

```text
每日事件不超过 30–90 秒
不要求玩家每天交付大量货物
主线期间也保留低优先级日常，避免任务条件卡住时玩家完全无事可做
同一句日常随着 flag 改变称呼/语气，而不是写十条独立长对话
```

## 6. 剧情事件注册草案

只展示新增事件，现有 arrival/repeat 保持不变。

```csharp
// 薇拉：第一笔交易后的固定供货邀约
new StoryEventDefinition(
    new StoryEvent(
        "quest.first_sale",
        "fluxforge_acid_buyer",
        StoryTrigger.TradeCompleted,
        100,
        1,
        "quest_vera",
        ctx => ctx.TradeResult.Success
            && !ctx.HasFlag("quest.first_sale_done")),
    new VeraFirstSaleQuestAssembly(),
    StoryOutcome.Leave)

// 奥雅：分离第一课
new StoryEventDefinition(
    new StoryEvent(
        "quest.separation",
        "fluxforge_flow_emitter_demo",
        StoryTrigger.VisitEnding,
        80,
        1,
        "quest_oya",
        ctx => FlowEmitterMerchantProgress.HasSecondVisit
            && ctx.HasFlag("quest.first_sale_done")
            && !ctx.HasFlag("quest.separation_done")),
    new OyaSeparationQuestAssembly(),
    StoryOutcome.OpenShop)

// 薇拉：碱样品
new StoryEventDefinition(
    new StoryEvent(
        "quest.base_sample",
        "fluxforge_acid_buyer",
        StoryTrigger.VisitStarted,
        90,
        1,
        "quest_vera",
        ctx => ctx.HasFlag("quest.separation_done")
            && !ctx.HasFlag("quest.base_sample_done")),
    new VeraBaseSampleQuestAssembly(),
    StoryOutcome.Leave)

// 第二幕：加急单
new StoryEventDefinition(
    new StoryEvent(
        "act2.rush_order",
        "fluxforge_acid_buyer",
        StoryTrigger.VisitStarted,
        100,
        1,
        "act2_vera",
        ctx => ctx.HasFlag("quest.stable_line_done")
            && !ctx.HasFlag("act2.rush_done")),
    new VeraRushOrderAssembly(),
    StoryOutcome.Leave)

// 第二幕：奥雅的旧样品
new StoryEventDefinition(
    new StoryEvent(
        "act2.old_sample",
        "fluxforge_flow_emitter_demo",
        StoryTrigger.VisitStarted,
        90,
        1,
        "act2_oya",
        ctx => ctx.HasFlag("act2.rush_done")
            && !ctx.HasFlag("act2.old_sample_done")),
    new OyaOldSampleAssembly(),
    StoryOutcome.OpenShop)

// 第三幕：北环报价
new StoryEventDefinition(
    new StoryEvent(
        "act3.northloop_offer",
        "fluxforge_flow_emitter_demo",
        StoryTrigger.VisitStarted,
        100,
        1,
        "act3_oya",
        ctx => ctx.HasFlag("act2.spill_done")
            && !ctx.HasFlag("act3.offer_done")),
    new OyaNorthLoopOfferAssembly(),
    StoryOutcome.OpenShop)
```

推荐 flag：

```text
quest.first_sale_done
quest.separation_done
quest.base_sample_done
quest.first_contract_done
quest.stable_line_done

act2.rush_done
act2.rushed
act2.careful
act2.old_sample_done
act2.tested_sample
act2.ignored_sample
act2.audit_done
act2.honest_records
act2.forged_records
act2.spill_done
act2.recalled
act2.covered_up
act2.went_to_clinic

act3.offer_done
act3.signed_exclusive
act3.refused_exclusive
act3.double_agent
act3.vera_truth_done
act3.clinic_first
act3.contract_first
act3.both_sides
act3.final_batch_done
act3.ending_seen
```

所有 flag 都写入玩家 modData，随存档保存。

## 7. 对话样本

### Q1 薇拉

```text
薇拉：“第一次就能做到这个浓度，比我想的强。”
薇拉：“以后我每天都会来。你只要保证浓度，我就收。”
玩家选项：
  1. “可以，我继续做。”
  2. “我先看看还能不能更纯。”
```

### Q2 奥雅

```text
奥雅：“净化装置拿得掉化学污染，拿不掉塑料、矿物和碎屑。”
奥雅：“这台旧离心机借你用。按粒径分开，剩下的才是你要的酸。”
玩家选项：
  1. “我试试。”
  2. “离心和净化有什么区别？”
```

### Q3 薇拉

```text
薇拉：“酸能稳定了，就试试碱。”
薇拉：“两瓶受污染的碱，处理到 99%，我照样收。”
玩家选项：
  1. “碱和酸有什么不同？”
  2. “我回去处理。”
```

### 日常：薇拉·更纯一点

```text
薇拉：“今天不要 99%，要 99.5%。”
薇拉：“多出来的纯度，我按高价收。”
玩家选项：
  1. “我回去处理。”
  2. “今天先交 99%。”
```

### 日常：奥雅·新装置

```text
奥雅：“你已经开始处理真正的原料了。”
奥雅：“冷萃和离心机，能帮你处理净化装置拿不掉的东西。”
玩家选项：
  1. “我看看货。”
  2. “下次再说。”
```

### Q6 薇拉·加急单

```text
薇拉：“250 mL，99.5%，明天要。”
薇拉：“诊所那边等不了。你能做，我就加钱；不能做，我现在去找别人。”
玩家选项：
  1. “我赶一批出来。”
  2. “先让我复检一次。”
```

### Q7 奥雅·旧样品

```text
奥雅：“北环水处理厂当年给我的样品。”
奥雅：“他们说是工业级，不该进食品和医疗线。”
奥雅：“你帮我看看，里面到底有什么。”
玩家选项：
  1. “我测。”
  2. “旧账就别翻了。”
```

### Q10 奥雅·北环报价

```text
奥雅：“北环想买你所有 99.5% 以上的货。”
奥雅：“价格很好。条件是——别再问原料从哪来。”
玩家选项：
  1. “我签。”
  2. “我不卖。”
  3. “我签，但我留样。”
```

## 8. 设计与实现风险

```text
1. 净化装置只去除化学污染和重金属，不能单独把高污染原料做到 99%。
   因此 Q2 必须让玩家拿到离心/冷萃，不能写成“净化一次就完成”。

2. 当前 StoryContext 没有直接读取玩家背包液体浓度的能力。
   任务验收优先使用 TradeCompleted 的交易成功/金额，
   复杂配方验收需要后续增加“交付物品检查”动作，不应在本期伪造。

3. 薇拉当前是每日 100% 到访的测试配置。
   上线前要恢复实际概率，否则日常任务没有呼吸感。

4. 奥雅 7 天间隔会让 Q2 等待较久。
   Q2 使用 VisitEnding，挂在奥雅二次剧情之后，避免再等一个七天周期；
   薇拉的日常对话负责维持存在感。

5. 所有奖励先使用现有金钱/装置，不新增货币或任务道具。
```

## 9. 游戏内 smoke test

```text
1. 完成奥雅 95% 酸教程后，修改酸到 99%，薇拉收购成功。
2. 触发 quest.first_sale，关闭对话后不再重复触发。
3. 奥雅二次剧情结束后优先播放 quest.separation，而不是普通通用剧情。
4. 拿到旧离心机后，能把受污染酸液处理到 99%。
5. 薇拉下一次到访播放碱样品任务，交付成功后写 flag。
6. 不满足条件时，日常对话仍可正常进入。
7. 强制遣散正在播放任务的 NPC，剧情停止且不写完成 flag。
8. 存档读档后，已完成任务不重复，未完成任务仍可继续。
9. Q6 赶工路线下一次薇拉到访出现退货后续；复检路线不出现。
10. Q12 允许分两批交付，终局 flag 正确汇总。
```

## 10. 玩家评审后的修订

玩家视角评审见 [player-review-v1.md](player-review-v1.md)。已吸收：

```text
Q2 改成先净化失败、再给离心机
Q3/Q6 提前提及诊所，给薇拉的姐姐线埋钩子
Q4 增加低价/涨价/预付款三种谈判选择
Q5 的 250 mL 降到 180 mL，允许分两天
Q6 赶工选择增加“下一日退货”的可见后果
Q12 的 500 mL 降到 300 mL，允许分两批
```

暂不采纳：

```text
新增出货单 UI / 检测小游戏
新增第三个 NPC
把旧样品检测做成独立玩法
```

原因：

```text
这些会扩大实现范围；
当前先用现成 Dialogue、TradeCompleted、VisitEnding 和 flag 完成叙事。
```
