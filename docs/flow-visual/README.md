# 流源视觉语言 — 工作区导览

> 这是一次**从零开始**的视觉设计（旧粒子实现整份作废）。
> 本目录只放设计与验证产物；**没有改动任何游戏代码**。
>
> 注：中间渲染图（各写手的 png/tiles、盲测中间表）已清理，只保留 `final/`、每个概念一张主图、
> 全部 `.md` 与渲染脚本。需要时用各自的 `render_*.py` 重新生成（例如 `final/render_final.py`）。

## 先看什么

| 顺序 | 文件 | 看什么 |
|---|---|---|
| 1 | [`preview.html`](preview.html) | 交互预览（舞台 + 12 帧动效 + 成分×温度矩阵 + 状态条） |
| 2 | [`final/png/channel-card.png`](final/png/channel-card.png) | **六通道总览卡**：一张图看懂整套设计 |
| 3 | [`final/png/showcase-8x.png`](final/png/showcase-8x.png) | 门面图：终稿在 8× 下的样子 |
| 4 | [`final/README.md`](final/README.md) | 最终方案一页纸 + 交付清单 |
| 5 | [`final/SPEC.md`](final/SPEC.md) | 逐像素规格（工程师照这个实现） |
| 6 | [`final/CHANNELS.md`](final/CHANNELS.md) | 属性→通道表与色盲/对比度实测 |
| 7 | [`final/ACCEPTANCE.md`](final/ACCEPTANCE.md) | 怎么验收 |
| 8 | [`final/IMPLEMENTATION.md`](final/IMPLEMENTATION.md) | 怎么落到 UGUI |

## 目录

| 路径 | 内容 |
|---|---|
| `BRIEF.md` | 第一轮设计简报：硬约束、历史否决清单、6 条信息、rubric |
| `final/` | 最终规格、通道表、实现契约、验收、决策 |

> 旧阶段目录（research / concepts / round2）已清理，历史版本见 git 提交 `459051e` 及更早。
| `ref/` | 参考图（**仅分析用，不随包发布**） |

## 已经被否决的东西（不要回头）

- 宽度/粗细编码任何属性（外宽、芯宽、亮带厚度都算）
- 温度 = 垂直位移（四向定义冲突、L 拐角断带）
- 温度 = 散开度（读成"散"不是"冷"）
- 头部纯白（抹掉成分色；本作纯白还是"玻璃/液体"语义）
- 8px 光束（压 12×12 图标）
- 每格 1px 虚线表射程（与边框/温度抢同一圈）
- "少" = 稀疏长尾 / 低透明度 / 粒子数量
- 弧长 16px 的头部（像素尺度下=平台，7px/45° 才读得出）
- 16×16 逐像素画成 256 quads（必须走图集，1 quad/tile）

## 当前状态

- **终稿方向**：「液滴电荷」——B 的流体质感 + W4 的钝头/亮核（方向）+ W4 的物理状态 + 格底三点射程。
- 设计阶段：**未写任何 C#**；`src/**` 只读。
- 数据前置：成分 / 射程 / 状态枚举在逻辑层不存在，属"需用户批准的独立任务"。
- 旧粒子代码（`src/Presentation/Particles/**`、`FlowVisual`）**已删除**。

## 已跑过的盲测（关键教训）

| 设计 | 温度 | 含量 | 射程 | 方向 | 状态 |
|---|---|---|---|---|---|
| B 原稿 | 3/3 | 4/4 | — | **0/4** | **0/5** |
| W2 核+环 | 3/3 | 4/4 | 3/3 | **0/4** | 2/5 |
| **W4 修正（终稿采用）** | — | — | — | **4/4** | **5/5** |
| **终稿（调色后）** | **3/3** | **4/4** | **3/3** | **4/4** | **5/5** |

**方向**：两次 0/4 都是 180° 反读——16px 下「尖 = 前」的先验极强。终稿必须删除所有 1px 尖角。
**温度/含量**：多次 3/3、4/4，通道稳定。
**状态**：缝/谷/肋这类细微拓扑不可读，必须物理直喻（平脸/两行空/亮缝/连续流）。


## 复跑

```powershell
$py = "C:\Users\Administrator\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
& $py docs/flow-visual/final/render_final.py          # 重建全部图版 + tiles + atlas
& $py docs/flow-visual/final/render_final.py --dump   # 自检：quads / 面积 / bbox，末行 ALL OK
```
