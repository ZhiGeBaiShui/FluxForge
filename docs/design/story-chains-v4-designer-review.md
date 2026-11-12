# 剧情设计师评审：story-chains-v4

## Verdict

**CONCERNS，但可以进入实现前细化。**

v4 已经修掉 v3 的三个硬伤：

```text
日历排期 → 行为触发
全局一条 active → 每 NPC 一条 + pending
拒绝流血 → 拒绝无惩罚
```

但仍有一个关键问题：

```text
v4 有 12 条链，质量不齐；
其中 former_intro、former_order、custom_order、blackout 仍更像“机制/事件”，不是完整剧情链。
```

建议最终 **10 条链 + 2 个环境事件**，而不是为了数量保留 12 条空壳。

---

## 1. v4 是否修好了玩家评审的问题

| 玩家问题 | v4 状态 | 结论 |
|---|---|---|
| 仍是 Day 3/4/5 日历 | §4 改成行为触发，日期只做地板 | 已修 |
| 一条 active 挤掉其他链 | 每 NPC 一条，pending 不用过期 | 已修 |
| 5 条链拒绝有隐性惩罚 | 大部分删除；upper_contract 改为概率/价格，保留“到此为止” | 基本修好 |
| 0 条玩家发起 | §7 列了 5 条 | 方向对，但 3 条依赖新 UI |
| 空表格 chain.shortage/rival | shortage 降为环境事件；rival 仍有独立链 | 部分修好 |
| 道德按钮 | return 改“按合同走”；black_market 删除举报 | 已修 |
| 用病人当筹码 | v4 删除“诊所需要货” | 已修 |
| 关系/风险/信息无数值 | §8 给 0–3 档位 | 已修 |

---

## 2. 12 条链逐条判断

| 链 | 判断 | 理由 |
|---|---|---|
| chain.procurement | **保留** | 最像玩家真实能力变现的一条 |
| chain.vera_supply | **保留** | 入口已有“不做”，节点 2 有经营三岔口 |
| chain.former_intro | **合并进库存** | 商品介绍不是剧情链；保留为奥雅的一次上架对话即可 |
| chain.former_order | **保留但缩短** | 成型机唯一的产品出口；2 个节点足够 |
| chain.return | **保留** | 唯一由玩家卖出的货自动回来的链 |
| chain.inspection | **保留** | 阶级工具最强；需要把 500/人情/停业写进选项 |
| chain.oya_past | **保留** | 世界观钥匙；但不能只给“以后解锁” |
| chain.upper_contract | **保留** | 唯一改变经营路线；v4 已修掉“失去准入” |
| chain.black_market | **保留** | 中间人选项让玩家不是只能当骡子 |
| chain.rival | **并入 upper_contract** | 价格战和独家合同是同一轴；独立成链会重复 |
| chain.blackout | **降级为环境事件** | 一次处置，不是链；不占链槽 |
| chain.custom_order | **P1，依赖终端** | 当前没有柜台挂单 UI；先用 NPC 对话替代 |

最终建议：

```text
P0 剧情链 8 条：
  procurement
  vera_supply
  former_order（合并 former_intro）
  return
  inspection
  oya_past
  upper_contract
  black_market

P1 玩家发起：
  custom_order（需要终端/挂单 UI）
  rival（并入 upper_contract 的分支）

环境事件，不占链：
  blackout
  shortage
```

这不是“只有 7 条”；是 **8 条完整链 + 2 个环境事件 + 2 个 P1 扩展**。

---

## 3. 玩家发起入口的可实现性

v4 §7 的 5 条：

```text
柜台挂单 → 需要新 UI
成型机找买家 → 需要机器按钮
主动问奥雅旧厂 → 现有对话可实现
主动给薇拉报价 → 现有对话可实现
主动查记录 → 需要站务窗口/新 UI
```

问题：

```text
3/5 依赖当前不存在的新 UI；
这会把“剧情设计”变成“先做三个界面”。
```

修正：

```text
P0 只保留两条玩家发起：
  主动问奥雅旧厂
  主动给薇拉报价

把“成型机找买家”改成：
  购买成型机后，奥雅下一次到访主动说“有人问成品”，
  玩家仍可选择接/不接；这不是玩家发起，但不需要新 UI。

把“柜台挂单/查记录”放入 P1，
在终端 UI 存在之前不要写进主线。
```

玩家自由度仍然成立：

```text
不问奥雅 → 旧厂链不启动
不向薇拉报价 → 低价供货链不启动
买了成型机也可以不接成品单
```

---

## 4. 因果与机制合理性

### 4.1 procurement

```text
条件：玩家交付过 99% 低残留酸
机制：净化装置去除化学/重金属
问题：需要新增“采购员收购”内容，不能只靠 Vera 交易
结论：可行，但需要独立 trade 内容
```

### 4.2 vera_supply

```text
条件：批量交易/主动报价
机制：薇拉低价收 99% 酸/碱
问题：薇拉现有收购只看浓度，不看“低价合同”
结论：需要给薇拉一个合同 flag，不能只改台词
```

### 4.3 former_order

```text
条件：玩家有成型机
机制：heavy_metal + former → flux_agent
问题：v4 写“助熔剂成品”但没有写配方/数量
结论：明确 100% 重金属 4 mL → 1 个 flux_agent；
      采购员按成品价收，不替代现有 heavy metal flux 配方
```

### 4.4 return

```text
条件：玩家卖出含沉淀/化学残留的货
机制：薇拉现有交易只收 >=99%，不会收含沉淀的货
问题：为什么会有“卖出去的坏货”？
结论：需要“赶工/未复检”内容动作写入 `batch.tainted`，
      否则 return 链永远不会触发
```

### 4.5 inspection

```text
条件：风险交易
机制：原版有安全检查/记录
问题：Mod 目前没有“风险值”系统
结论：用现有 flag 累计风险 0–3，不要新增检查模拟
```

### 4.6 oya_past / upper_contract / black_market

```text
条件：成型机/采购次数/受限化学品
机制：现有对话和物品 tag 可实现
问题：需要“受限化学品”tag 和采购次数 flag
结论：可行
```

### 4.7 rival / blackout

```text
rival：并入 upper_contract 的定价分支，不要独立
blackout：环境事件，不占链槽；可作为 shortage 的变体
```

---

## 5. 语气与阶级

v4 已经避免：

```text
病人筹码
“拒绝负责”
假举报按钮
```

仍需检查：

```text
1. 薇拉的“预算低”不能变成哀求；她是买家，不是受害者。
2. inspection 的“给站长打电话”要明确是关系/钱，不是道德选择。
3. black_market 的“中间人”要写风险归属，不能只写抽成。
4. upper_contract 的“不签”不能写成“你选择了自由”这种旁白；
   只写价格和概率变化。
5. 所有拒绝台词用交易性收尾，不用“行。”这种赌气句。
```

---

## 6. 七个必修

1. **最终链数定为 8 条 P0 + 2 个环境事件 + 2 个 P1 扩展。**
   不要为了“12 条”保留空壳。

2. **former_intro 合并进奥雅上架对话。**
   商品页不是剧情链。

3. **rival 并入 upper_contract。**
   价格战和独家合同是同一决策轴。

4. **P0 玩家发起只保留两条：问奥雅旧厂、给薇拉报价。**
   柜台终端/查记录放 P1，避免三个新 UI 拖死剧情。

5. **return 的前置必须可产生。**
   在赶工/未复检内容里写 `batch.tainted`；否则这条链永远不触发。

6. **所有拒绝选项统一为“到此为止/这批不做”，禁止“拒绝负责/举报”这类道德按钮。**
   拒绝后台词必须交易性收尾。

7. **每条链的奖励/惩罚写明现金、库存、风险 0–3、关系 0–3。**
   v4 部分做到，former_order/oya_past 还要补数字。

---

## 7. 结论

```text
v4 可以进入实现前细化，但不建议直接按 12 条全部实现。
先做 8 条 P0 链：
  procurement
  vera_supply
  former_order（含 former_intro）
  return
  inspection
  oya_past
  upper_contract
  black_market

环境事件：
  blackout
  shortage

P1：
  custom_order（需要终端）
  rival（并入 upper_contract）
```

这样比“12 条半成品”更完整，也比原来的 7 条多。
