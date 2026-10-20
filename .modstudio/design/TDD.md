# 流源视觉语言 v1 — 技术设计文档（TDD）

> 状态：草稿（必须由设计/架构 agent 填实后再通过 design gate）
> 游戏：Probably Stolen · Unity：2021.3.45f2 · 后端：il2cpp · Loader：MelonLoader 0.7.3
> 最后更新：待填 · 负责人：待填

## 1. 已核实事实

| 事实 | 值 | 证据来源 |
|---|---|---|
| 游戏 Unity 版本 | 2021.3.45f2 | 待填（文件/日志/命令） |
| 脚本后端 | il2cpp | 待填 |
| Mod loader | MelonLoader 0.7.3 | 待填 |
| 游戏程序集目录 | 待填 | 待填 |
| 目标平台 | Windows x64（默认） | 待填 |
| 游戏是否使用 Addressables | 待填 | 待填 |
| 渲染管线 | Built-in / URP / HDRP / 待确认 | 待填 |

> 未核实的事实不能进入架构决策。先写证据，再写结论。

## 2. 约束与非目标

- 不修改游戏原始文件；只通过 loader / 补丁 / 资源加载扩展。
- 不分发游戏程序集、原始资产或破解材料。
- 不绕过 DRM、反作弊或在线公平性限制。
- 兼容目标：游戏版本、loader 版本、至少一个共存 mod。
- 性能预算：待填（例如主线程 +0.3 ms、常驻内存 +32 MB）。

## 3. 架构

~~~mermaid
flowchart TD
  Bootstrap[Bootstrap / Loader entry] --> Host[ModuleHost]
  Host --> FeatureA[Feature module]
  Host --> FeatureB[Feature module]
  FeatureA --> Adapter[Game adapters]
  FeatureB --> Adapter
  Adapter --> Game[Game assemblies / scene objects]
  Host --> Core[Pure C# core]
  FeatureA --> Core
  FeatureB --> Core
  Host --> Patch[Patch plan / Harmony]
  Patch --> Game
~~~

分层规则：

1. `Core` 不引用 UnityEngine，可在 dotnet test 中独立运行。
2. `Adapters` 是唯一直接接触游戏类型/反射/场景对象的层。
3. `Features` 只依赖 Core 接口和 Adapter 契约。
4. `Patches` 只做转发与状态同步，业务逻辑回到 Feature。
5. `Bootstrap` 只做组合根：配置、日志、生命周期、模块注册。

## 4. 目录与程序集

~~~text
src/
  Mod.Core/                 # 纯 C#，无 Unity 依赖
  Mod.Adapters/             # 游戏类型适配，引用游戏程序集
  Mod.Features/             # 功能模块
  Mod.Patches/              # Harmony patch，尽量薄
  Mod.Bootstrap/            # loader 入口
tests/
  Mod.Core.Tests/           # dotnet test
  Mod.Integration.Tests/    # 契约/编译/加载冒烟
~~~

## 5. 模块与生命周期

| 模块 | 职责 | 初始化顺序 | 可禁用 | 失败隔离 |
|---|---|---|---:|---|
| 待填 | 待填 | 待填 | 是 | 记录并禁用该模块 |

定义 `IModModule` 的 `Initialize / Start / Tick? / Dispose` 语义；场景切换时只重置场景级状态。

## 6. Harmony 补丁计划

| Patch ID | 目标类型/方法 | 类型 | 目的 | 失败时的行为 | 兼容风险 |
|---|---|---|---|---|---|
| P-001 | 待填 | Prefix/Postfix/Transpiler/Finalizer | 待填 | 跳过并记录 | 待填 |

规则：

- 每个 patch 必须验证目标存在；签名变化要 fail loud 并禁用相关功能。
- Prefix 返回 `false` 时必须明确跳过原方法后的替代行为。
- Transpiler 只做最小 IL 改动，必须有 IL 日志/反编译对照。
- `PatchAll` 只用于受控的 patch 程序集；禁止无差别扫描。
- 提供卸载/禁用路径，避免热重载后重复打补丁。

## 7. 数据、配置、存档

- 配置来源与优先级：
- 配置 schema 版本与迁移：
- 存档路径、原子写入、备份、损坏恢复：
- 禁止存档引用 UnityEngine.Object；使用稳定 ID。
- 版本升级迁移策略：

## 8. 资源与技美

- AssetBundle 构建 Unity 版本/平台/SRP：
- Shader 兼容与 fallback：
- 资源加载失败时的降级：
- 显存/包体预算：

## 9. 错误处理与日志

- 日志前缀、级别、文件位置：
- 模块级 error boundary：
- 用户可见错误提示：
- 诊断 bundle（日志、版本、mod 列表、patch 列表）：

## 10. 测试与验收

- 单元测试范围：
- 契约测试范围：
- 编译 gate：
- 游戏内 smoke：
- 视觉/性能 gate：
- 详见 `.modstudio/design/TEST-PLAN.md`。

## 11. 构建与发布

- 目标框架（例如 netstandard2.1 / net472）：
- 引用游戏程序集的方式（Private=false）：
- 输出目录与 loader 插件目录：
- 版本号、changelog、校验和：
- 发布包不包含哪些文件：

## 12. 风险与 ADR

- 风险见 `.modstudio/design/RISK.md`。
- 决策见 `.modstudio/decisions.md`。
