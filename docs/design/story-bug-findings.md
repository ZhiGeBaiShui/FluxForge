> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 当前剧情框架与医院线的缺陷检查

后续处理：按用户决定，仅修复第 2 项退出存档清理；第 1、3 项不处理，对应探索性复现测试已移除。
第 2 项现由剧情层自己的 StoryMainMenuExitPatch、StoryQuitToMenuPatch 前置补丁清理，两个入口均有回归用例，当前剧情测试 94 项通过。适配层 SaveExited 保持原样。下文保留发现时的历史证据。

本轮三个子代理分别检查运行时、医院交易、NPC 接线；根代理重新核对原版代码与复现输入。
以下 3 项已由具体源码路径及真实生产类的测试支持。尚未修改生产实现。

## 1. 恰好 300 分的医院交货可能被算成不足

三瓶酸液的总体积分别为 29,852、236,938、33,210 微升，合计正好 300,000 微升，即 300mL。
每瓶酸为 `volume * 95 / 100` 整数微升，其余为污染物，浓度在 90% 至 99% 之间，权值应为 1。
每瓶都小于 250mL。这些输入符合真实 FlowStock.Read 的整数液体 tag 协议，不依赖替身允许的小数微升。

证据链：

- `LogicIntegration/TagMapping/Core/FlowStock.cs:37` 读取整数液体量。
- `HospitalContractTrade.cs:98` 逐瓶先除 1000 再累加，结果为 **299.99999999999994**。
- `HospitalContractPickupEdges.cs:62` 因分数小于 300，进入不足交付确认。
- 确认结算后，`HospitalContractScore.cs:42` 得到声望 **-1**，应为 **+3**。
- 同文件 `:55` 得到付款 **29**，应为 **30**。
- 结算提示 `PickupEdges.cs:106` 还会显示 **299 / 300**。

这是同一个分数累计误差造成的多处后果，不分别计作三个缺陷。
建议修复方向：统一分数计算与边界语义，避免每瓶换算后的浮点误差改变达标、声望和付款；
不能只修改对话判断而留下错误结算，也不应无依据加入任意浓度容差。

复现：`tests/FluxForge.Story.Tests/StoryHospitalBugReproTests.cs:11`。
先用当前错误结果断言验证了实际结算 -1/29，再改为正确结果的回归断言；当前失败为期望 300、实际 299.99999999999994。

## 2. 退出存档未清理活动剧情，可能影响后续存档的原版对白

可达流程：旧存档正在进行 Mod 剧情时返回主菜单；进入另一个存档，在新的 Mod 剧情尚未接管前，
普通 NPC 正开始打字显示对白，此时停用 Mod。

原版与接线依据：

- `.tmp-decomp/EscapeUIManager.cs:107–111` 调用 Reset/ResetData 后切换菜单场景。
- `.tmp-decomp/PlayerStore.cs:3761–3771` 的 Reset 直接清空当前客户和锁，不调用 OnDismiss。
- `src/LogicIntegration/Bootstrap/SaveExited.cs:54–58` 没有清理 StoryNpcRuntime。
- `StoryNpcRuntime.cs:37–45` 因而仍持有并取消旧播放器。
- `StoryPlayer.cs:36` 的 Cancel 操作使用全局当前 DialogUIManager，作用于新存档 UI。
- 原版 `DialogUIManager.cs:529` 在普通 NPC 刚开始显示时设置 displayingUncompletedDialog；
  `:720–723` 中断时会执行这条原版对白的 endAction。

测试观察：原版对白结束动作期望 **0 次**，实际被执行 **1 次**，当前对白也被清除。
确定存在的是跨存档活动状态残留以及误用当前 UI；没有把旧 Unity 对象一定抛 MissingReference 当作已验证结果。

建议在真实退出存档入口清理剧情播放状态，不能仅在停用 Mod 时检查 UI 是否为空。
复现：`tests/FluxForge.Story.Tests/StoryRuntimeBugReproTests.cs:26`。
测试模拟原版 Reset/换 UI 的边界，不加载真实 Unity 场景。

## 3. 薇拉首次赠料后中断，恢复来访会重复赠料

可达流程：首次到访领到四瓶赠料后，尚未结束对话时停用 Mod；将已获得的物品收入背包并保存该状态，
重新启用后继续游戏，满足下次来访条件时再次进入初遇。不能把单纯返回菜单、未保存物品的流程说成必然重复领取。

证据链：

- `AcidBuyerArrival/Edges/AcidBuyerArrivalEdges.cs:23` 中途执行 GiveRaw，发放四瓶。
- `AcidBuyerStock.cs:64` 起将物品标为玩家所有后放上前台。
- `AcidBuyerArrivalAssembly.cs:11–14` 直到整场 Complete 才写 MarkMet。
- 原版 `ModLoader.cs:682` 支持运行中停用，取消不执行 Complete，也不回滚已经发放的物品。
- 原版 `PlayerStore.cs:1633` 起离场仅清理非玩家所有物品；玩家可以保留赠品。
- `AcidBuyerStory.cs:83` 仍按 HasMet=false 选择初遇，重新发放。

测试第二次通过真实事件选择器获取剧情，没有硬编码强行重跑初遇；预期累计 **4 瓶**，实际 **8 瓶**。
测试验证状态和发放逻辑，不模拟完整保存文件及重新启用流程。
建议独立记录赠料是否已发，与发料同一动作提交，避免把“介绍未完成”和“赠品未领取”混为同一个标志。

复现：`tests/FluxForge.Story.Tests/StoryNpcBugReproTests.cs:11`。

## 实际执行结果

执行剧情测试共 95 项：既有 92 项通过，新增 3 项正确行为断言失败，分别复现以上问题。
新增用例保留为后续修复的回归要求，因此当前剧情测试套件处于失败状态。
生产代码没有修改；没有部署，也没有进行真实游戏 UI、存档与贴图运行实测。

撤回的候选：用小数微升 Split 输入制造最高需求累计不足，不符合实际液体整数 tag 边界，未列入缺陷。
报酬先取整再乘 1.2、零交付确认仍不收货均遵循已有设计，未按个人偏好改判为 bug。
