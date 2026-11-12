# 医院声望与周合同：实现评审记录

## 状态

```text
实现：已完成
build/test：通过
独立双程序员 review：已完成（两个独立 review lane）
防御性代码证据审查：已完成
游戏内 smoke：尚未执行
```

## 已实现

```text
独立医院 faction
签约后注册
签约日 +3 首次送原料
签约日 +7 首次取货
之后各自每 7 天
按 FlowPropertyKey 配置化学物
mL × 浓度权值计分
目标 300 分
0.5 信用点/分
单次正声望 +20 上限
原版 StoreReputation/UI/存档
```

## 程序员 review 发现与处理

### 已修复

| 发现 | 处理 |
|---|---|
| 错过日期时 pickup 长期压住 material | `ShouldRunPickup/ShouldRunMaterial` 改为先处理更久未处理的一边；新增纯测试 |
| 高分显示四舍五入后与判定不一致 | 通知改用 `Math.Floor(score)` |
| 材料部分收取的 `Math.Max(1, n/2)` 是死检查 | 删除；`WeeklyCount` 最小为 2 |
| 读档钩子顺序可能让 offer 错过本次 QueueAll | 先订阅 `EnsureHospitalFaction`，再 `_host.Subscribe()` |

### 保留为设计选择

| 发现 | 保留理由 |
|---|---|
| 零交付扣 3 声望并消耗本周取货 | 用户明确选择“零交付算未达标，按缺口扣声望” |
| 合同拒绝后不自动重提 | 设计师稿明确“拒绝后不自动再提”；v1 不写重开入口 |
| 结算先付款/加声望，后 MarkPickup | 用户明确不考虑中途退出/路径异常；不写事务回滚 |
| `AddTrade` 无调用方 | 用户要求给后续医院 NPC 预留接口 |

### 未解决/需实测

```text
1. 运行时新增 faction 后，已打开的声望面板是否立即出现
2. 游戏内 smoke：
   - 首次高浓度后的合同提议
   - +3 送料
   - +7 取货
   - 声望 UI 出现/重载不重复
3. 跨午夜时 StoreStation.GetDayCounter 与事件选择日的偏差
```

## 防御性代码审查

### 保留（有证据）

| 代码 | 证据 |
|---|---|
| `EnsureFaction` 查重 | 签约和读档两个调用方；重复会生成重复 UI 行 |
| `Start` 幂等 | 合同锚点不能被重复签约重置 |
| `NpcStoryHost` 空 FactionId 检查 | 奥雅等 NPC 明确不归属 faction；`clientFaction` 默认 `NONE` |
| `total <= 0` 跳过 | 空容器正常，且避免除零 |
| `active <= 0` 跳过 | 该化学物不存在于当前液体 |
| `concentration + epsilon` | 99%/100% 浮点边界 |
| `ReadInt` 缺 key 返回 0 | 签约前 key 本来不存在；`Started &&` 防止未签约误判 due |

### 删除（无证据/冗余）

| 代码 | 删除理由 |
|---|---|
| `Add` 内再次 `EnsureFaction` | 签约/读档已保证；无证据 |
| `payment > 0` 才 ModCash | 原版 AcidBuyer 直接 ModCash，0 合法 |
| `Math.Max(1, WeeklyCount()/2)` | `WeeklyCount` 最小 2，永不触发 |
| `try/catch` | 范围内为 0 |
| 全局单例 null 检查 | 无证据 |
| 存档损坏修复 | 用户明确不考虑入侵/异常存档 |

### 明确不删除

```text
AddTrade：用户要求保留的后续接口，不是当前死代码清理对象
ExclusionGroup 检查：现有框架代码，不在本次医院功能范围
```

## 结论

```text
实现通过构建/测试；
两个独立 review 已完成并修正真实问题；
防御审查结论为 KEEP/DELETE 均带证据；
剩余风险是游戏内 smoke 与 UI 刷新，不是编译/逻辑测试。
```
