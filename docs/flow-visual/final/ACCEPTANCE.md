# 流源视觉 — 验收标准（可观察、可复跑）

> 设计是否成立，不看"好不好看"，看下面 12 条能不能被**第三方**复现。
> 每条给：场景、判据、证据文件、复跑命令。

## 0. 盲测协议（所有感知类验收共用）

1. 从最终 `tiles/manifest.json` 用 `tools/flow-visual/qa.py blind` 生成**无标注**接触表（1x 与 4x 各一份）。
2. 评审是被单独派出的 agent，**只拿到图**，不看 `spec.md`、不看文件名、不看本文件。
3. 评审逐格写出：成分 / 温度 / 含量 / 射程 / 状态 / 方向。
4. 与 `key.json` 对比，统计每项准确率；同一属性至少 3 个样本。
5. 至少跑两轮：正常视觉 + `deutan` 模拟（`qa.py colorblind`）。

**通过线**：任何属性 < 80% 即该通道不通过，必须回去改设计，不许改判据。

---

## AC-01 1x 可读性

- 场景：16×16 原始像素，深色网格背景，无 4x 放大。
- 判据：3 名独立评审中 ≥2 名在 3 秒内说出"这是一个在流动的东西"，而不是"图标/UI/方块"。
- 证据：`final/png/hero-1x.png` + 盲测记录。
- 复跑：`python tools/flow-visual/verify.py --check readable`

## AC-02 温度静态可读

- 场景：冷 / 常温 / 热 三张 1x 图，打乱顺序，**不提供颜色说明**。
- 判据：盲测准确率 ≥ 80%；灰度化后仍 ≥ 60%（形状线索必须存在）。
- 证据：`final/png/temp-1x.png`、`temp-gray.png`、盲测 key。
- 复跑：`qa.py sheet` + `qa.py colorblind`（灰度用 `--kind gray`）

## AC-03 成分可分（含色盲）

- 场景：6 种成分各 1 张 1x；正常视觉与 deutan 各一轮。
- 判据：正常 ≥ 90%；deutan ≥ 4 组可分且组内不混（准确率 ≥ 75%）。
- 证据：`attrs-1x.png`、`attrs-deutan.png`、`contrast.png`。
- 备注：血红在 protan 下亮度趋零，必须靠**常量 1px 轮廓**兜底（见 CHANNELS）。

## AC-04 含量 4 档

- 场景：空 / 少 / 中 / 多 四张 1x，随机顺序。
- 判据：4 档两两可分，盲测 ≥ 80%；"空"必须读成"干了"而不是"消失了"。
- 证据：`content-1x.png`、`content-area.csv`（脚本量面积，档间 ≥30%）。

## AC-05 射程 × 含量正交

- 场景：3 含量 × 3 射程 = 9 张，随机顺序。
- 判据：射程判读 ≥ 75%，且"含量变化不影响射程判读"（交叉表无系统性偏移）。
- 证据：`range-x-content.png`、交叉混淆矩阵。

## AC-06 方向与 L 拐角

- 场景：4 向各 1 张 + 一条 L 路径连续 4 帧。
- 判据：4 向包围盒中心偏差 **0px**；拐角帧无 1px 错台（叠像素网格目检 + 脚本比对中心）。
- 证据：`dirs-1x.png`、`lpath-4x.png`、`bbox-center.csv`。

## AC-07 状态 5 种

- 场景：流动 / 受阻 / 分流 / 汇聚 / 被收集 五张静态帧。
- 判据：盲测 ≥ 80%（至少 4 种能分）。
- 证据：`states-1x.png`。

## AC-08 动效可信

- 场景：12 帧条（1x + 4x）与 0.5s 循环说明。
- 判据：
  - 任何单帧包围盒面积变化 ≤ 2px²（否则读成含量变化）；
  - 受阻抖动 ≤ 0.5s 且之后静止；
  - 3 名评审中 ≥2 名不认为"像卡顿/掉帧/穿模"。
- 证据：`loop-1x.png`、`loop-4x.png`、`area-per-frame.csv`。

## AC-09 预算

- 判据：单源 ≤ 32 quads（峰值），≤ 24（静态）；1 次重建 / 网格；0 分配/帧（实现后测）。
- 证据：`quads.csv`（逐部件）、`verify.py --check budget` 输出。
- 备注：设计阶段用脚本从 `rect[]` 表统计；实现阶段用 Profiler 复核。

## AC-10 遮挡

- 场景：流源叠在 3 种真实物品图标（深/中/浅底）上，1x。
- 判据：3 种里 ≥2 种仍能看出图标轮廓；流源自身轮廓不断。
- 证据：`occlusion-1x.png`、`occlusion-light-4x.png`。

## AC-11 色盲与对比度

- 判据：6 色在 deutan/protan 模拟下，分组数 ≥4；关键轮廓对比度 ≥ 4.5:1。
- 证据：`contrast.png`、`palette.csv`（含 WCAG 与模拟值）。

## AC-12 实现边界

- 判据：不依赖自定义 shader；纯 UI/Default 材质；每流源 ≤4 层蒙版 + ≤6 残影（A 方案，已无粒子）；像素对齐公式与实现契约一致。
- 证据：`IMPLEMENTATION.md` 对照表 + 实现后的一次游戏内截图。

---

## 复跑命令（全部）

```powershell
$py = "C:\Users\Administrator\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
& $py tools/flow-visual/verify.py --all
& $py tools/flow-visual/qa.py blind --manifest docs/flow-visual/final/tiles/manifest.json --out docs/flow-visual/final/png/blind.png --key docs/flow-visual/final/tiles/key.json
& $py tools/flow-visual/qa.py colorblind --in docs/flow-visual/final/png/attrs-1x.png --out docs/flow-visual/final/png/attrs-deutan.png --kind deutan --scale 4
```

## 失败处理

任何一条不通过 → 记录在 `final/REVIEW.md` 的"未通过项"，回到设计改**通道**，不改判据；
改完重跑本节全部命令，旧图归档到 `final/png/archive/`。
