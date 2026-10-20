# 流源视觉语言 v1 — 多 Agent 协作契约

> 状态：草稿（必须由设计/架构 agent 填实后再通过 design gate）
> 游戏：Probably Stolen · Unity：2021.3.45f2 · 后端：il2cpp · Loader：MelonLoader 0.7.3
> 最后更新：待填 · 负责人：待填

## 1. 每个 agent 的阅读顺序

1. `.modstudio/context.md`
2. `mod_board action=status` 或 `.modstudio/board.json`
3. 本任务相关的设计文档和源码
4. 本文件的所有权与证据规则

## 2. 角色

| 角色 | 主要职责 | 不应做什么 |
|---|---|---|
| director | 拆解、调度、集成、gate | 不直接改未认领的业务代码 |
| design | GDD/TDD/测试/资产计划 | 不写实现代码 |
| architecture | 模块边界、loader、patch 策略 | 不跳过事实核实 |
| gameplay/systems/ui | 功能实现与自测 | 不跨任务改文件 |
| tech-art | 资产、shader、视觉/性能 | 不未经授权使用资产 |
| qa | 测试、复现、证据 | 不只跑单个测试就宣称通过 |
| review | 对抗式审查、回归风险 | 不直接重写实现（除非获授权） |
| build | 构建、打包、发布 gate | 不绕过失败 gate |
| integration | 合并、冲突解决、全量验证 | 不隐瞒冲突或回退 |

## 3. 文件所有权

- 认领任务时声明 `files`；只改这些文件及其明确依赖。
- 同一文件同一时间只能有一个 writer；read-only reviewer 不受限。
- 需要改所有权外文件时：先 `mod_board action=update status=blocked` 说明，或新建任务，不要静默越界。
- 共享文件（.csproj、场景、prefab、manifest）由 integration 角色统一修改。

## 4. 证据规则

- “完成”不是形容词，是一条 `mod_evidence` 记录。
- 每条证据包含：kind、命令/步骤、结果、日志/截图/报告路径、commit（可选）。
- 失败证据也要记录；失败后修复要再记录 pass。
- 没有证据的 done 会被 `mod_gate` 拒绝。

## 5. 汇报格式

~~~text
STATUS: done | blocked | continue
TASK: T-xxx
CHANGED: path — 一句话
EVIDENCE: E-xxxx (kind/result) — 证据路径
RISKS: ...
NEXT: ...
~~~

## 6. 冲突与升级

1. 发现文件冲突：停止写入，记录对方 task id，向 director 报告。
2. 发现设计矛盾：写入 `.modstudio/decisions.md` 的 Proposed ADR，等待裁决。
3. 发现阻塞超过一个工作轮：标记 blocked，附最小复现和已尝试方案。
4. 不允许用“重写整个模块”解决局部冲突。

## 7. 完成定义（Definition of Done）

- 验收标准逐条可观察地满足。
- 所需证据全部 pass。
- 相关测试/编译通过，日志无未处理异常。
- 变更范围内无调试残留、硬编码路径、临时开关。
- 文档/任务卡已更新，交接信息完整。
