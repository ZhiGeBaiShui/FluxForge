# 流源视觉语言 v1 — 架构决策记录（ADR）

> 状态：草稿（必须由设计/架构 agent 填实后再通过 design gate）
> 游戏：Probably Stolen · Unity：2021.3.45f2 · 后端：il2cpp · Loader：MelonLoader 0.7.3
> 最后更新：待填 · 负责人：待填

## ADR-001：选择 loader 与脚本后端适配策略

- 状态：Proposed
- 背景：待填（游戏版本、Mono/IL2CPP、现有 loader）
- 决策：待填
- 替代方案：待填
- 后果：待填
- 证据：待填

## ADR-002：Core / Adapter / Patch 分层

- 状态：Accepted（脚手架默认）
- 背景：游戏类型难以单元测试，patch 目标随版本变化。
- 决策：Core 不引用 UnityEngine；Adapters 隔离游戏类型；Patches 只做转发。
- 后果：需要写接口和 fake；换取可测试性与可迁移性。
- 证据：`.modstudio/design/TDD.md`

## ADR-003：完成必须有证据

- 状态：Accepted（脚手架默认）
- 背景：长任务中 agent 容易把“看起来完成”当成完成。
- 决策：任务 done 需要 requiredEvidence 的 latest pass；发布需要 gate。
- 后果：增加记录成本；换取可审计与可恢复。
- 证据：`.modstudio/AGENTS.md`

## ADR-004：本阶段只做视觉设计，不碰任何源码

- 状态：Accepted（用户 2026-10-20 20:11 明确）
- 背景：用户要求「代码全部丢掉」，从零设计流源视觉；同时禁止改逻辑层与其它代码。
- 决策：
  1. 本阶段只写 `docs/flow-visual/**` 与 `tools/flow-visual/**`；
  2. `src/**`、`assets/**`、`*.csproj` 一律只读；
  3. 现有粒子实现（`src/Presentation/Particles/**`、`src/Mod/Flow/Visual/**`）**作废、不继承**，
     但也不删除（删除需单独授权）；
  4. 设计交付物是「规格 + 预览 + 渲染矩阵 + 验收判据」，不是 C# 实现。
- 替代方案：直接边设计边写 C#（被否：先把视觉语言定死，避免又一次参数返工）。
- 后果：设计通过后需要单独一轮实现任务与文件所有权划分。
- 证据：`docs/flow-visual/BRIEF.md`、`docs/flow-visual/final/**`

## ADR-005：参考素材只做分析，不随包发布

- 状态：Accepted
- 背景：用户允许「参考背包英雄的贴图」，但项目不得分发第三方素材。
- 决策：参考图仅存放于 `docs/flow-visual/ref/**` 用于观感分析，不进入 `assets/`、不嵌入 DLL、
  不随发布包分发；所有交付图形由本项目脚本生成。
- 后果：若后续要「像背包英雄」的具体贴图质感，只能自行重绘。
- 证据：`docs/flow-visual/ref/`（分析用途）

## 新 ADR 模板

~~~text
## ADR-00N：标题
- 状态：Proposed / Accepted / Superseded
- 背景：
- 决策：
- 替代方案：
- 后果：
- 证据：
~~~
