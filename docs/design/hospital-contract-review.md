# 医院声望与周合同：实现评审记录

## 状态

```text
实现：已完成并通过 build/test
独立双程序员 review：未完成
原因：两个实现子代理与两个短 review 子代理均未在时限内返回，已停止
替代：父会话完成 correctness/integration 两轮静态审查
```

这份记录不把父会话审查冒充成两名程序员的独立互审。

## 已实现

```text
独立医院 faction
签约后注册
签约日 +3 首次取货
签约日 +7 首次送原料
之后各自每 7 天
按 FlowPropertyKey 配置化学物
mL × 浓度权值计分
目标 300 分
0.5 信用点/分
单次正声望 +20 上限
原版 StoreReputation/UI/存档
```

## 父会话 correctness 审查

### S1：运行时新增 faction 后 UI 未必自动刷新

`HospitalReputation.EnsureFaction` 在签约对话中把 faction 加入
`PlayerStore.storeReputations`。原版 `ReputationUIManager.OpenUI` 只在打开时
遍历列表创建元素。

影响：如果声望面板此刻已经打开，新 faction 可能不出现，下一次打开才出现。

状态：未做防御性刷新；需要游戏 smoke 验证。没有证据表明玩家会在签约对话中同时打开声望面板，因此没有为它写关闭/重开逻辑。

### S1：错过日期时 pickup 可能压住 material

取货在 +3，送料在 +7。若玩家错过 +7，只在 +10 到访，两段都 due；
`pickup` 优先级更高，会先结算 pickup，material 留在 due 状态等下一次到访。

影响：正常按周到访不会发生；连续错过时可能延后送料。

状态：未改。需要确认是否接受“一次到访只处理一段，下一次再处理另一段”。

### S2：结算与存档之间的中断

`HospitalContractTrade.TrySettle` 先销毁物品、付款、加声望，然后由
`HospitalContractPickupEdge` 调用 `MarkPickup`。如果中途被异常打断，可能重复结算。

状态：按用户要求不处理入侵/异常退出，没有 try/catch 或事务回滚。

### S2：未做游戏内 smoke

验证只覆盖：

```text
build 0 warning / 0 error
test 509/509
```

没有实际进入游戏验证：

```text
签约对话
+3 取货
+7 送料
声望 UI 出现
重载后 faction 不重复
```

## 防御性代码删减记录

### 保留

| 代码 | 证据 |
|---|---|
| `EnsureFaction` 查重 | 签约动作与读档钩子都可能调用 |
| `Start` 已开始直接返回 | 合同锚点不能被重复签约重置 |
| `NpcStoryHost` 空 FactionId 检查 | 奥雅等 NPC 明确不归属 faction |
| `total <= 0` 跳过 | 空容器是正常游戏状态 |
| `active <= 0` 跳过 | 该化学物不存在于当前液体 |
| `concentration + epsilon` | 99%/100% 浮点边界 |
| `ReadInt` 缺 key 返回 0 | 签约前 key 本来不存在 |

### 删除

| 代码 | 删除理由 |
|---|---|
| `Add` 内再次 `EnsureFaction` | 签约/读档已保证；无证据需要 |
| `payment > 0` 才 ModCash | 原版 AcidBuyerTrade 直接 ModCash，0 是合法值 |
| `try/catch` | 无证据 |
| 全局单例 null 检查 | 无证据 |
| 存档损坏修复 | 用户明确不考虑入侵/异常存档 |

## 未完成项

```text
1. 两名程序员的独立互审
2. 游戏内 smoke
3. 声望 UI 运行时刷新验证
4. 错过日期时 pickup/material 的优先级策略确认
```
