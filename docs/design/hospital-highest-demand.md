> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 医院60声望：最高需求与信任报酬

## 玩家流程

声望达到60后，下一次正常送料时追加最高需求提议：

1. 医院的制药师们一直在抱怨纯净的化学试剂根本弄不到。
2. 他们说最缺的就是酸、碱、酒精。
3. 能再给医院提供1000ml的100%纯度试剂吗？
4. 酸、碱、酒精，三种各个都要1000ml。

不增加接取选项，不占新的主剧情事件，不改变周合同目标、送料/取货日程及声望结算。
正常送料和里程碑代送料共享 MaterialEdge.Append，所以两条路径都能提出需求。
最后一句结束时写 Started，之后正常医院取货开始累计。此前交过的货不追溯。

每周最后追加：已经交了酸 x ml，碱 y ml，酒精 z ml。
每一种分别累计至少1000ml后，追加：非常感谢你的帮助，试剂已经足够了。
感谢结束写 Completed，停止进度汇报与提议；周合同继续。
任务本身无现金、声望、原料或物品奖励，后续剧情用 HospitalHighestDemandState.Completed 解锁。

## 交付口径和存档

- 只统计医院周交货实际收走的货物，不统计薇拉普通散货收购。
- 仅该化学品含量等于容器物质总量时算100%；不将99.999%四舍五入成100%。
- 体积在唯一一次桌面检查中连同Qualified和Score保存于Inspection；结算销毁货物后才累计。
- 有不合格试剂返回、询问不足量、玩家返回重放货阶段均不累计。
- 不足量确认后实际交出的纯试剂照常计入。明确零交付不计入。
- 酸/碱/酒精各自记录实际累计微升，超过目标不裁掉，汇报时除1000显示ml。
- Started/Completed及三种累计量保存至modData，双精度数按InvariantCulture的R格式保存。
- 进入任务后声望降低不清除进度；任务完成后也不再重复记数或感谢。

键前缀：hospital.highest_demand.，后缀 started / completed / acid / base / alcohol。

## 信任报酬（独立的60声望奖励）

新增医院正向perk HOSPITAL_TRUST_PAYMENT，阈值60，名称“信任报酬”。
描述：交付给医院的货物，现在会提供1.2倍的金额报酬奖励。
风味：上层人的医院居然会为一位供货商提供额外嘉奖，这事听起来太过奇妙。

沿用HospitalReputation.EnsurePerk与原版声望UI，不把[未激活]/60/200写死在名称里。
原版ReputationUIManager.UpdateToolTipContent根据isUnlocked追加激活状态和perkThreshold/200。
源码依据：.tmp-decomp/ReputationUIManager.cs，UpdateToolTipContent；
StoreReputation.UpdateReputation按当前amount与阈值决定激活状态。

付款 = 原周合同整数报酬 × 1.2，最终按游戏整数货币向下取整。
按本次取货结算前的医院声望是否>=60判断；本次交货才达到60时，从下一次付款开始生效。
声望跌回60以下不再享受溢价，与原版perk激活状态一致；普通散货收购价格不变。

## 代码组织与分支依据

| 代码 | 职责/依据 |
|---|---|
| Events/HospitalHighestDemand | 追加提议、最后进度与完成对白；SayNpc/Do沿用StorySequence机制 |
| HospitalHighestDemandState | 唯一任务状态与累计；后续只读取Completed |
| HospitalContractInspection新增三个纯试剂体积 | 复用同一次扫描结果，避免为了任务重新扫描或读取已销毁物品 |
| HospitalContractTrade.Settle的Active判断 | 任务提出前和完成后不记交付，来自任务起止要求 |
| AppendOffer的声望与Started判断 | 60后下一次送料提出，且只能提出一次，不增加重复保护标记 |
| AppendProgress的Active判断 | 只在任务期间报告，已完成后不重复 |
| TargetMet三项>=1000000 | 三种分别达到1000ml，不能合并成总计3000ml |
| 状态Read缺失为0 | 任务开始时尚未交任何货，正常初始状态；无坏存档捕获或猜测修复 |
| HospitalContractScore.Payment(score,reputation) | 在原统一结算出口应用倍率，正常/不足量/零交付都同一口径 |
| HospitalReputation/Texts | 沿用20/40声望的注册与本地化机制；无新UI框架 |

没有新增异常吞掉、空值回退、重试、迁移猜测或跨NPC统计。

## 验证

测试直接编译最高需求、付款、HospitalReputation、HospitalReputationTexts及原框架。
仅原版声望数据与本地化表仍用边界替身，不把替身通过说成实际游戏UI通过。

覆盖：59到60后的下一次送料、里程碑送料路径、提议只一次、99.999%不计入、
三种分别累计、跨周与读档、部分/零交付、不合格与重试不重复统计、
普通收购和提议前交货不计入、完成后不重复与无额外任务奖励、
60阈值perk注册文本与付款倍率、旧剧情分支回归。

验证结果：主工程编译通过，0警告0错误；剧情测试69项通过，原医院/剧情选择器测试36项通过；git diff --check通过。
游戏内UI和真实存读档尚未实测，部署目录尚未更新。
