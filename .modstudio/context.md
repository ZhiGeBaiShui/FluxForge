# 流源视觉语言 v1 — 执行上下文包

> 状态：设计阶段（**不改任何源码**）
> 游戏：Probably Stolen v0.46D · Unity：2021.3.45f2 · 后端：il2cpp · Loader：MelonLoader 0.7.3 · 目标框架 net6.0
> 最后更新：2026-10-20 · 负责人：director

## 项目事实（已核实）

- 游戏安装：`E:\SteamLibrary\steamapps\common\Probably Stolen Demo`（MelonLoader 0.7.3 / Il2Cpp / x64 / net6）
- 网格：`UISettings.gridSize = 16` UI 单位/格；物品贴图按 `ppu = 宽 ÷ (列数 × 0.16)` 落格
- 渲染：UGUI；mod 的视觉层挂在网格 `RectTransform` 下，`SetAsLastSibling()` 画在物品之上
- 实机分辨率 2560×1440（PlayerPrefs 实测）；截图量得 1 格约 66–80 屏幕像素（≈4–5× 设计单位，需在实现阶段复核）
- 流源：一个承担全部属性的游戏对象，占 1 格，逐帧 4 向移动（非持续水流）

## 不可违反的约束

1. **逻辑层 `src/Logic/**` 冻结**；本阶段连 `src/**` 全部不碰。
2. 视觉设计只用 UGUI 顶点矩形 / 程序化纹理 / 默认 UI 材质；不依赖自定义 shader。
3. 一属性一视觉通道；禁止用宽度/粗细编码属性（历史两次否决）。
4. 不复制游戏或 Backpack Hero 的任何素材；参考只取理念。
5. 任何"设计完成"必须能落到可观察的验收证据（1x 判读、静态温度判读、色盲模拟）。

## 代码地图（只读参考）

| 路径 | 作用 | 本阶段 |
|---|---|---|
| `src/Logic/` | 流源/装置/运动/反应（纯 C#） | 冻结，只读 |
| `src/Mod/Flow/` | 网格适配、流源驱动、视觉接线 | 只读 |
| `src/Mod/Flow/Visual/` | 现有粒子渲染（**旧实现，作废不继承**） | 只读 |
| `src/Presentation/Particles/` | 现有粒子内核（**旧实现，作废不继承**） | 只读 |
| `docs/flow-visual/` | 本轮设计工作区 | 可写 |
| `tools/flow-visual/` | 渲染与 QA 工具 | 可写 |

## 当前状态

- Phase：design（多代理概念迭代）
- 任务：`T-VIS-001` 研究 / `T-VIS-002` 概念 / `T-VIS-003` 评审收敛 / `T-VIS-004` 最终规格
- 设计简报：`docs/flow-visual/BRIEF.md`（所有子代理的唯一上下文）
- 最近决策：见 `.modstudio/decisions.md`

## 生成最新 brief

~~~text
mod_board action=brief
~~~

把 brief 的 markdown 原样放入子 agent 的任务提示，不要依赖子 agent 继承本会话。
