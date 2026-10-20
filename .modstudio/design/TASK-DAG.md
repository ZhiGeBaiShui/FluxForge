# 流源视觉语言 v1 — 任务 DAG 与迭代计划

> 状态：草稿（必须由设计/架构 agent 填实后再通过 design gate）
> 游戏：Probably Stolen · Unity：2021.3.45f2 · 后端：il2cpp · Loader：MelonLoader 0.7.3
> 最后更新：待填 · 负责人：待填

## 1. 规则

- 每个任务必须有：可观察的验收标准、文件所有权、所需证据类型、依赖。
- 任务粒度：一个 agent 一次工作会话（约 30–90 分钟）能完成并验证。
- 不允许“实现整个系统”这类任务；拆成垂直切片。
- 用 `mod_board action=add` 建任务，用 `mod_board action=claim` 认领。

## 2. 推荐任务序列

| 顺序 | 任务 | 角色 | 依赖 | 交付物 | 证据 |
|---:|---|---|---|---|---|
| 1 | 核实游戏/Unity/loader/后端事实 | architecture | — | 事实表 | design |
| 2 | 完成 GDD/TDD/测试/资产计划 | design | 1 | 四份文档 | design |
| 3 | 搭建可编译的 loader 插件骨架 | build | 2 | DLL + 日志 ready | build |
| 4 | 建立配置/日志/模块生命周期 | architecture | 3 | 模块宿主 | build/test |
| 5 | 建立纯核心测试与契约测试 | qa | 3 | dotnet test 绿 | test |
| 6 | 垂直切片：一个玩家可见功能 | gameplay | 4,5 | 可玩路径 | smoke/playtest |
| 7 | 场景切换/存档/异常边界 | systems | 6 | 稳定生命周期 | test/smoke |
| 8 | UI/技美/本地化 | ui,tech-art | 6 | 可用界面/资产 | visual |
| 9 | 兼容性与 patch 审查 | review | 6 | 审查报告 | review |
| 10 | 性能与发布 gate | qa,build | 7,8,9 | 发布包 | perf/smoke |

## 3. 任务卡模板

~~~text
ID:
标题:
角色:
优先级:
依赖:
文件所有权:
验收标准:
  1.
  2.
所需证据: build / test / smoke / review / visual / perf
风险:
~~~

## 4. 并行化边界

可以并行：不同目录、不同功能、只读审查、独立资产。
不要并行：同一文件、同一场景/prefab、同一 patch 目标、需要先确定接口的上下游。

## 5. 阶段 gate

| Gate | 通过条件 |
|---|---|
| design | 四份设计文档 + AGENTS 完成，design 证据 pass |
| foundation | 编译 + 单元/契约测试 pass |
| feature | 至少一个任务完成，build/test/review 证据 pass |
| release | 全部任务完成，build/test/smoke/review/perf pass，无 blocked/stale claim |
