# 流源视觉 — 实现契约（设计 → 工程的交接）

> 状态：设计阶段产出；**本轮不写任何 C#**。
> 依据：`research/engine-constraints.md`（617 行，含 file:line 与复算脚本）。
> 读者：后续接手的实现者。本文只描述"怎么画"，不描述"画什么"（画什么见 `SPEC.md`）。

## 1. 范围与非目标

**做**：在现有网格 UI 之下新增一层流源视觉；每帧从流源实体读取只读数据，画成 16×16 格内像素图。
**不做**：不改 `src/Logic/**`；不改装置行为、tag、存档；不引入自定义 shader、后处理、AssetBundle。

## 2. 数据契约

### 2.1 主轨（设计目标，需用户批准后加字段）

| 视觉输入 | 需要的字段 | 现状 |
|---|---|---|
| 成分占比（6 分量） | `FlowProperties.Composition`（6 个 0..1） | **不存在**；`FlowProperties` 只有 Moisture/Temperature，且 `Validate()` 强制规则表声明 |
| 剩余射程 | `FlowEntity.RangeRemaining`（double/格） | **不存在**；全项目无 Range/Remaining |
| 状态枚举 | `FlowEntity.State`（Flowing/Blocked/Split/Merged/Collected） | **不存在**；分流/汇聚无事件 |
| 温度 | `Properties.Temperature` | 有（double，无常量范围，需 clamp -20..100） |
| 含量 | `Properties.Moisture`（µL） | 有 |
| 方向 | `Motion.Direction`（0=上 1=右 2=下 3=左） | 有 |
| 速度 | `Motion.Speed`（格/秒） | 有 |
| 位置 | `Position`（连续 double，整数=格边界） | 有 |

> 三个缺失字段是**独立的前置任务**，需要用户批准后由逻辑层负责人添加；本轮设计不碰。

### 2.2 Tier-0 降级（今天就能画）

只用 Temperature / Moisture / Direction / Speed+位置差 / OnEntityAdded-Removed：

- 能表达：温度（冷/常温/热）、含量（空/少/中/多）、方向（4 向）、流动 vs 静止、出生/消失。
- 不能表达：6 成分（只有水）、剩余射程、受阻 vs 静止的区分、分流/汇聚。
- 退化策略：成分固定水色；射程通道整条留空（不占像素）；状态只画"流动/静止/被收集"。

**通道预留原则**：Tier-0 不得占用将来主轨要用的像素位置（射程段、状态肋），否则加数据时要重排整套映射。

## 3. 渲染路径（三选一，按优先级）

### 3.1 主路径：自绘 `MaskableGraphic`（1 次重建 / 1 draw call）

- 每块网格一个 Graphic，一次 `OnPopulateMesh` 画完全部流源。
- **必须显式注册**：`ClassInjector.RegisterTypeInIl2Cpp<FlowVisualGraphic>()`（先 `IsTypeRegisteredInIl2Cpp` 探测）。
  不注册时 `AddComponent` 不可依赖（可能抛，也可能返回 null），当前代码靠异常兜底 → 大概率走回退。
- `OnPopulateMesh` 必须是 `public override`（本游戏程序集里是 public virtual；`protected override` 会 CS0507）。
- 顶点色逐 quad；同色同 alpha 的相邻像素**必须合并**成一段，否则 quad 数翻倍。

### 3.2 回退路径：分层 `Image`（每源 2–4 层）

- 每源：本体 1 + 温度覆层 0/1 + 含量填充 1 + 射程点 1（可为 0/1/2 个独立 Image）。
- **只在档位变化时**改 sprite/color；改 `anchoredPosition` 不触发重建。
- 需要一张 ≤64×64 的图集（16×16 tile × 16 格）+ `Sprite.Create` 切片；`Texture2D.SetPixels32 + Apply + Sprite.Create` 在 IL2CPP 下可用（元数据已核验）。
- 图集放 `assets/flow_atlas.png` 即自动被 `<EmbeddedResource Include="assets\*.png" />` 收编，不用改 csproj。
- 预算：≤12 quads/源、≤2 源/网格 是安全线；超过就应优先修好 3.1。

### 3.3 纯贴图路径（可选，追求"手绘像素画"时）

- 一张图集，每个状态/朝向/帧一个 16×16 tile，1 quad + UV 画出；仍走 3.1 的 Graphic。
- 优点：16×16 逐像素 = 1 quad，而不是 256 quads。
- 代价：需要离线烘图（Python 生成），并保证 `FilterMode.Point`、`hideFlags = DontSave`、ppu 正确。

## 4. 坐标与像素对齐

```
格坐标 → 局部坐标：
  localX = area.xMin + (cellX + 0.5) * 16      // 格中心
  localY = area.yMax - (cellY + 0.5) * 16      // Y 向下
格内像素 (px, py)（0..15，左上原点）→ 局部：
  localX = area.xMin + cellX*16 + px
  localY = area.yMax - cellY*16 - py
屏幕像素对齐（四条边）：
  screen = local * ppu
  left = round(screenLeft); right = round(screenRight)
  localLeft = left / ppu; localRight = right / ppu
```

- `ppu = rectTransform.lossyScale.x`（屏幕空间画布下等于缩放）。
- **不要**用小数坐标"抹平"非整数缩放；接受相邻格 ±1px 的边宽差。
- 实机 1 格 ≈ 113 屏幕像素（2560×1440 实测），即 16 单位 ≈ 7px/单位；非整数缩放会让 1px 细节出现 1–2px 抖动，这是可接受的，不要改逻辑坐标。

## 5. 预算

| 项 | 值 |
|---|---|
| 1 quad | 4 顶点 + 6 索引 = 444 B |
| 单流源目标 | **≤24 quads（静态）/ ≤32（动画峰值）/ 硬顶 64** |
| 每网格 | 1 个 Graphic、1 次重建、1 draw call；20 源 × 32 quads 仍只有 1 次重建 |
| 顶点数据 | 32 quads ≈ 14 KiB；64 quads ≈ 27.8 KiB |
| GC | 稳态 0 分配/帧；初始化允许一次性图集/材质/List |
| 半透明叠加 | ≤2 层（UI/Default 直通 alpha，第 3 层开始洗色） |

## 6. 更新顺序与生命周期

1. `OnUpdate`：逻辑推进（已有）。
2. `OnLateUpdate`：同步位置 → 逐源计算视觉参数 → 逐网格 `Submit` → `SetVerticesDirty()` 一次。
3. 面板关闭（`activeInHierarchy == false`）：停止发射/推进新动画，但不销毁；重开时补一次 `Submit`。
4. 对象销毁：`Destroy` 延迟到帧末；需要消失动画就在视觉层自己延迟 N 帧再销毁。
5. `OnPopulateMesh` 只读：数据必须**拷贝快照**，不能在里面对游戏对象发命令。

## 7. 性能红线

- 禁止每帧 `new Material` / `Sprite.Create` / `new Texture2D` / 字符串拼接 / LINQ / 闭包。
- 禁止每帧 `Shader.Find`（打包后会被裁剪，返回 null）。
- 不要改 `Graphic.defaultGraphicMaterial` 全局实例；需要贴图时克隆一次并在销毁时释放。
- 单源 quad 数超 32 时先查"同色相邻像素是否合并"。

## 8. 错误处理与回退

| 失败 | 行为 |
|---|---|
| `ClassInjector` 注册失败 | 记一次日志，走 3.2 分层 Image |
| 图集加载失败 | 退化为纯色 quad（无纹理），仍可表达通道 |
| 网格 rect 取不到 | 推迟到下一帧；不要把流源画在世界坐标 |
| 单个流源数据异常（NaN/负值） | 跳过该源，不抛到 `LateUpdate` 外层 |

## 9. 兼容性检查表

- [ ] 面板重建（rectTransform 变化）后重新挂层
- [ ] 读档 `FlowDriver.Reset()` 后清空视觉状态
- [ ] UI 缩放在 16:9 / 21:9 / 1440p 下不糊
- [ ] 与至少一个其它 UI mod 共存（ProbablyMouseTweaks）
- [ ] 暂停（`Time.timeScale=0`）时动画冻结
- [ ] 无流源时不提交空层（省重建）

## 10. 后续实施任务（建议）

| 任务 | 前置 | 验收 |
|---|---|---|
| T-IMPL-1 数据字段（Composition/Range/State） | 用户批准 | 字段可读、单测覆盖 |
| T-IMPL-2 视觉层骨架 + Graphic 注册探测 | T-IMPL-1 或 Tier-0 | 游戏内日志出现"使用自定义 Graphic" |
| T-IMPL-3 静态绘制（本体/温度/含量/方向） | T-IMPL-2 | 对照 `SPEC.md` 逐像素一致 |
| T-IMPL-4 射程/状态通道 | T-IMPL-1 | 5 状态静止可读 |
| T-IMPL-5 12 帧动画 | T-IMPL-3 | 0 GC、12fps 相位正确 |
| T-IMPL-6 视觉基线截图 | 全部 | 5 档属性矩阵与设计稿一致 |
