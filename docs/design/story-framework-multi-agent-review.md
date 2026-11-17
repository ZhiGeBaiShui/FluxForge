> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 剧情框架多身份迭代记录

## 参与者与迭代方式

本轮实际启动三个子代理，均直接检查当前源码：

- `architecture_review`：执行语义、依赖方向、调用返回、插入、取消及卸载。
- `defensive_review`：防御性代码删除员，逐项追溯真实调用与原版代码依据。
- `directory_review`：目录设计及项目规范，核对 Logic / LogicIntegration 模式与文档。

第一轮独立审查后，主代理与三位子代理交叉核对发现。主代理实现修复并运行测试，
第二轮重新阅读补丁、核查新增判断；目录审查员负责同步使用说明。
没有用“代理同意”代替代码依据。

## 已发现并修复

### 1. 选项执行回调被跳过

上一轮清理错误地把选项的两个 Dialogue 节点当成重复显示并删除其中一个。

真实执行路径：

1. `.tmp-decomp/DialogUIManager.cs:486` 的 DisplayChoice 从选项目标读取菜单文字。
2. `:521` 的 SelectChoice 把 currentDialog 设为这个菜单目标。
3. `:523` 安排 ContinueConversation；`:421` 先取 currentDialog.nextDialogue。
4. `:424` 才执行下一行 startAction。

因此菜单载体必须指向执行对白，RunOption 必须在执行对白的 startAction。
当前修复位于 `src/Story/Runtime/Playback/Dialogue/StoryDialogueAdapter.cs:60`。
载体不显示为玩家对白，不属于重复内容或防御代码。

之前测试直接进入选项目标，未模拟第 3 步，错误地让缺陷版本通过。
现两个测试驱动都从 `choice.Next` 进入实际对白。先只改驱动，80 项中 54 项失败、26 项通过；
恢复必要节点后 80 项全部通过。子图选项测试断言完整文字序列，确认只有一次玩家回答。

### 2. 卸载遗留当前或排队的不可解散访客

`NpcStoryHost.Create` 把剧情访客设为 dismissable=false；正常结束才由 ApplyOutcome 恢复。
取消不执行正常结束回调，因此仅取消播放器不能释放客户。

原版 `.tmp-decomp/ModLoader.cs:653`、`:661`、`:682` 支持运行中调用 OnDisable。
`.tmp-decomp/PlayerStore.cs:1790` 检查队列，`:1797` 检查当前客户；
任一客户不可解散都会令 CanEndDay 返回 false。

修复由拥有注册身份和客户创建职责的 `NpcStoryHost.Dispose` 承担：

- 移除队列内已注册 NPC 的客户，避免卸载后继续执行 OnArrival。
- 当前客户属于注册 NPC 时，调用原版 DismissCurrentClient。
- 组合根先取消播放器，再释放客户；不执行待处理赠礼、剧情 Complete 或完成计数。
- 保留普通 NPC 与其对白，不新增客户管理服务或第二份注册表。

新判断的直接依据：

- HasInstance：原版 PlayerStore.cs:671 不创建实例，Instance getter 在 :679–682 懒建实例，
  ResetData 在 :3609 清空实例。菜单卸载须跳过客户清理，不能创建新存档对象。
- current 非空及注册 ID 匹配：无当前访客、原版访客都是正常状态，只有本框架注册客户应被释放。

Dismiss 仍遵循游戏原有行为；其 autoCallNext 设置可覆盖传入的 callNextWhenPossible=false，
本次没有改游戏设置或额外阻止普通客户来访。

### 3. SayPlayer 未使用原版说话人标识

原版 StartDialogue (`DialogUIManager.cs:274`) 和 ContinueConversation (`:430`)
只把 title=="PLAYER" 交给玩家气泡。CreateLine 原先使用玩家姓名，会落入 NPC 显示路径。

现改用 PLAYER，并添加适配契约测试。当前剧情没有 SayPlayer 生产调用，
此项是框架接口缺陷，不能声称玩家已在现有剧情中遇到。

## 没有采纳的改动

- 不再拆分 StageAssembly / StageEdge；它们是私有实现，项目 DeviceHost 同文件辅助类型已有先例。
- 不新增独立客户清理服务；Host 只有一个消费位置且已有客户身份注册。
- 没有找到生产路径触发无前置对白的空选项占位问题，不增添猜测性修复。
- 不新增递归上限、异常吞噬、参数兜底、重试或动作去重集合。
- `_cancelled`、状态机空值和卸载时活动播放器检查仍有真实路径依据，不能为减少判断而删除。

防御审查员独立复核 ModLoader 停止与卸载条件，确认目前唯一接线不要求重复 Shutdown 兜底；
EndStory 也只有活动根播放器回调调用，保留删除无效客户参数的清理。

## 最终验证

- Release 编译：0 警告、0 错误。
- 剧情及框架测试：86 项全部通过。
- 完整逻辑测试：518 项全部通过。
- `git diff --check`：通过。

新增 6 项检查玩家标识，以及 Host 的队列清理、当前访客释放、不影响普通访客、
菜单无存档卸载和两个到访钩子退订。Host 使用生产代码，客户对象与 UI 仍是边界替身。
没有部署到游戏目录，没有进行真实 Unity UI 实测。
