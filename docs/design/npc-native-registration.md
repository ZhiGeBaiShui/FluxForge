> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 原版 NPC 目录接入

## 原版依据

检查对象是当前安装游戏的 Assembly-CSharp.dll，反编译记录保存在 artifacts/npc-registration-native。

- StoreClientListDict.decompiled.cs:7：公开的 storeClientDict 保存 ID 到 Func<StoreClient> 的映射。
- 同文件:1118：CreateStoreClient 查字典、调用工厂，再标记来源。
- 同文件:1131：GetClientDisplayName 也调用工厂并缓存名字；因此创建不能推进剧情或发放物品。
- 同文件:1147：ClearDisplayNameCache 是原版提供的缓存清理方法。
- StoreClientManager.decompiled.cs:490：AddNextClient 接收已经创建的 StoreClient；入队不等于登记到目录。

## 实现

NpcStoryHost.Add 登记客户工厂。工厂沿用现有 Create，不访问存档；OnArrival 才执行 StartVisit，根据当前存档选择剧情。

NpcStoryContent.DirectVisitRule 明确指定直接创建入口，两位 NPC 均使用各自原有的日常来访规则。直接创建不执行规则的排队条件、概率和冷却；回调、访客类型、到访计数仍取自该规则。医院到访时的事件优先级保持原有选择流程。自然排队继续由 VisitRuleSelector 检查条件、概率和冷却。

- 薇拉：fluxforge_acid_buyer。
- 奥雅：fluxforge_flow_emitter_demo。

Dispose 移除本控制器登记的工厂，并清空原版名字缓存，避免移除后仍返回旧名字。没有添加重复启动兼容、冲突覆盖、异常吞噬或额外空值防御。

## 验证

### 防御性代码复核

本次生产代码新增内容逐项复核：

- 字典 Add、创建委托：实现原版目录登记，没有 ContainsKey、覆盖旧工厂、异常捕获或重试。
- DirectVisitRule 字段、构造参数及两位 NPC 的传参：直接创建的配置，不是异常回退。两位 NPC 显式传入原有日常规则，不从数组首项猜测，不以 null 替代规则。
- Dispose 中 Remove 循环：释放登记到静态字典的工厂；工厂委托捕获剧情配置，不移除会在禁用后仍能创建客户。实际卸载链为 FFModEntry.OnDisable → StoryBootstrap.Shutdown → NpcStoryHost.Dispose。
- ClearDisplayNameCache：有明确原版依据。GetClientDisplayName 第 1133—1135 行先返回缓存，第 1137 行才检查工厂字典。仅 Remove 工厂不会删除缓存，因此注销后名字仍能被查到。原版第 1147—1149 行提供公开缓存清理入口，使用它完成注销收尾。

本次没有新增 if、空值检查、try/catch、异常回退、重复初始化兼容或工厂恢复逻辑。NpcStoryHost 原有的 HasInstance 与 current 空值检查不属于本次新增代码。无需删除本次实现；保留全部目录登记与注销代码。

主项目编译通过，0 警告、0 错误。剧情测试 98 项通过。新增 4 项覆盖无存档名字查询、直接创建与自然排队的区别、到达时选择当前剧情、注销与名字缓存清理。

测试使用原版目录接口的边界替身，未实测第三方控制台的名单刷新和实际召唤。现有控制台若只在启动时缓存目录，需要重新启动游戏后验证。
