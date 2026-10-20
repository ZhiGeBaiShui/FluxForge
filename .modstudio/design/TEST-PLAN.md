# 流源视觉语言 v1 — 测试计划

> 状态：草稿（必须由设计/架构 agent 填实后再通过 design gate）
> 游戏：Probably Stolen · Unity：2021.3.45f2 · 后端：il2cpp · Loader：MelonLoader 0.7.3
> 最后更新：待填 · 负责人：待填

## 1. 测试金字塔

| 层级 | 目的 | 运行环境 | 反馈时间 | 是否阻塞合并 |
|---|---|---|---|---|
| L1 纯核心单元测试 | 规则、计算、状态机 | dotnet test | 秒级 | 是 |
| L2 适配器契约测试 | 游戏类型边界、反射探测 | dotnet test + fake/stub | 秒级 | 是 |
| L3 编译 gate | 所有程序集可编译、无警告升级 | dotnet build | 十秒级 | 是 |
| L4 游戏内 smoke | loader 加载、入口执行、关键路径 | 真游戏 + 日志 | 分钟级 | 是 |
| L5 场景/视觉 | UI、材质、动画、分辨率 | 真游戏 + 截图 | 分钟级 | 发布前 |
| L6 性能 | 帧时间、GC、显存 | Profiler/基准 | 分钟级 | 发布前 |

## 2. 环境矩阵

| 项 | 值 |
|---|---|
| 游戏版本 | 待填 |
| Unity 版本 | 2021.3.45f2 |
| Loader | MelonLoader 0.7.3 |
| 脚本后端 | il2cpp |
| 分辨率 | 1920×1080 / 2560×1440 / 1280×720 |
| 语言 | 简中 / 英文 |
| 其他 mod | 无 / 待填 |

## 3. 用例

| ID | 层级 | 前置 | 步骤 | 预期 | 证据 |
|---|---|---|---|---|---|
| T-001 | L1 | 待填 | 待填 | 待填 | 测试报告 |
| T-002 | L4 | 游戏启动 | 加载 mod，进入主菜单 | 日志出现 ready，无异常 | smoke 日志 |
| T-003 | L5 | 进入场景 | 打开 UI，切换分辨率 | 布局不破，字体可读 | 截图 |

## 4. 命令

~~~powershell
# 纯核心测试
dotnet test tests/Mod.Core.Tests/Mod.Core.Tests.csproj -c Release

# 编译 gate
dotnet build src/Mod.Bootstrap/Mod.Bootstrap.csproj -c Release

# 游戏内 smoke（按实际路径替换）
$game = 'C:\path\to\Game.exe'
$log = Join-Path $PWD 'artifacts\smoke.log'
$p = Start-Process $game -ArgumentList '-logFile', $log -PassThru
Start-Sleep -Seconds 45
if (-not $p.HasExited) { $p.CloseMainWindow() | Out-Null }
Select-String -Path $log -Pattern 'MyMod|Exception|Error'
~~~

## 5. 测试数据与夹具

- 固定随机种子：
- 固定时间/时钟注入：
- 最小存档：
- 最小场景：
- 输入注入方式：
- 截图基线目录：

## 6. 退出标准

- L1-L4 全绿，且失败用例有 issue/任务。
- 关键验收场景 AC-001..N 有 pass 证据。
- 无未处理的异常日志；允许的已知警告列入白名单。
- 性能在预算内；视觉基线无意外差异。
- 发布 gate 通过，证据写入 `mod_evidence`。

## 7. 反模式

- 只跑一个测试文件就宣称全套通过。
- 用增加 timeout 掩盖竞态。
- 在没有真实游戏启动的情况下声称“游戏内验证”。
- 测试私有方法而不是可观察行为。
