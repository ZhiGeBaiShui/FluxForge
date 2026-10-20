# 重构 + A 方案契约（蒙版染色 · 每机器视图）

> 一次到位：删掉旧粒子框架、视觉视图归机器、颜色从图集里解耦（蒙版 + 运行时染色）。
> 旧粒子设计**整份作废**，允许删除，不保留兼容层。

## 1. 目标文件树

```
src/Presentation/Flow/Visual/          ← 纯 C#（零 Unity，可单测）
  FlowComposition.cs      新增：6 个权重 + 归一化 + 混合
  FlowTint.cs             新增：BodyColor / EdgeColor / AccentColor / WallColor
  FlowVisualState.cs      改：去掉 Comp/Temp 维度，带 Composition + Temp（温度仍驱动边缘）
  FlowAtlasIndex.cs       改：key = content|state|dir[|fN]
  FlowTierMap.cs          保留：温度/含量/状态/方向/拖尾层数
  FlowPixelMath.cs        保留

src/Mod/Rendering/FlowAtlas/           ← UnityEngine 层
  FlowAtlasTexture.cs     改：加载 3 张蒙版 + 1 张墙 + 索引；三套 Sprite 缓存
  FlowAtlasLayer.cs       改：每流源 body/edge/accent + 墙 + 6 残影 + 3 射程点

src/Mod/Flow/View/                     ← 每机器视觉编排
  FlowAtlasView.cs        新增：**每台机器一个**（替代静态 FlowAtlasVisual）

src/Mod/Flow/Visual/                   ← 旧目录（已删除）
  FlowParticleLayer.cs    删
  FlowParticleGraphic.cs  删

src/Presentation/Particles/            ← 整目录删除
src/Mod/Flow/FlowVisual.cs             ← 删除

src/Mod/Flow/FlowMachine.cs            改：+ public FlowAtlasView AtlasView
src/Mod/Flow/FlowDriver.cs             改：+ LateUpdateVisuals(dt)；Retire/Reset 里 Dispose 视图
src/Mod/Items/FlowItemSetup.cs         改：创建机器时 new FlowAtlasView(...)
src/Mod/PBModEntry.cs                  改：OnLateUpdate → FlowDriver.LateUpdateVisuals(dt)
tests/ProbablyBagMod.Tests/*           改：删粒子测试；新增 FlowTint/Composition/Key 测试
```

## 2. 蒙版图集（构建期，Python）

| 项 | 值 |
|---|---|
| 输出 | `assets/flow_mask_body.png` / `flow_mask_edge.png` / `flow_mask_accent.png` / `flow_mask_wall.png` + `flow_mask.json` |
| 尺寸 | 每张 256×1040（1040 个 key = 80 静态 + 960 非 flow 帧别名；唯一图 272 张，运行时只请求 272 个 key） |
| key | `content|state|dir`，flow 追加 `|f0..f11`（无 comp/temp） |
| 格式 | RGBA：**RGB = 亮度蒙版**（染色时相乘），**A = 形状 alpha** |
| 墙 | 单独一张彩色小图（固定 `#C9C2B8`），key 同样 `content|state|dir`，仅在 blocked 有意义 |

**角色 → 蒙版/亮度/alpha**（角色定义见 `render_final.role_rgba`）：

| 角色 | 蒙版 | RGB 亮度 | Alpha |
|---|---|---|---|
| `B` 本体 | body | 按行：上 255 / 中 235 / 下 215 | 208 |
| `C` 亮核、`G` 前脸高光 | accent | 255 | 255 |
| `R` 后缘、`u` | body | 75 / 166 | 130 / 170 |
| `S`、`~` 蒸汽/抽吸流 | body | 255 | 255 / 200 |
| `E` 张力边、`M`/`L` 缝与唇 | edge | 255 | 255 |
| `r` 后缘暗边 | edge | 38 | 130 |
| `P` 干痕芯 | edge | 210 | 200 |
| `s` / `S2` | edge | 255 | 200 / 120 |
| `I` 霜点、`F` 白沫 | accent | 255 | 255 |
| `W` 墙 | wall 图 | 固定色 | 255 |
| `X/Y/Z/K/N` 成分纹理 | **删除**（A 方案不保留成分专属纹理） | — | — |

确定性：同输入两次构建字节一致；`verify_mask_atlas.py` 抽 20 key，用「蒙版 × 预期色」重建，和 `render_final.render()` 的对应像素比较（alpha 逐像素一致；颜色按 verify 的 Δ 预算（当前 Δmax≈90、Δavg<10，见 verify 输出））。

## 3. 染色公式（纯 C#，`FlowTint`）

```
BodyColor(c)   = Σ(w_i · comp_i) / Σw_i        // 全 0/NaN → #27C7E6
EdgeColor(c,t) = mix(shade(BodyColor,-0.52), tempTint(t), k)   // 与 render_final.edge_color 同式
                 cold: #D6ECFF, k=0.88；hot: #FF964E, k=0.86；normal: 不混
AccentColor(c) = mix(Body(c), #FFFFFF, 0.62)（亮核/高光/霜/沫/蒸汽/抽吸流共用）
WallColor      = #C9C2B8
```

## 4. C# 接口（冻结）

```csharp
public readonly struct FlowColor            // 纯逻辑层不能引 UnityEngine.Color32
{
    public readonly byte R, G, B;
    public FlowColor(byte r, byte g, byte b);
}

public readonly struct FlowComposition
{
    public readonly float Water, Poison, Oil, Blood, Acid, Energy;
    public static FlowComposition PureWater { get; }
    public bool IsEmpty { get; }                 // 全 0 或含 NaN
    public FlowColor Blend();                    // 归一化加权；空 → 水色
}

public static class FlowTint
{
    public static FlowColor Body(in FlowComposition c);
    public static FlowColor Edge(in FlowComposition c, FlowTemp temp);
    public static FlowColor Accent { get; }
    public static FlowColor Wall { get; }
}

public readonly struct FlowVisualState
{
    public readonly FlowComposition Composition;
    public readonly FlowTemp Temp;
    public readonly FlowContent Content;
    public readonly FlowState State;
    public readonly FlowDir Dir;
    public readonly int Frame;                   // -1 = 静态
    public string ToKey();                       // content|state|dir[|fN]
}
```

## 5. 生命周期（每机器视图）

```
FlowItemSetup            new FlowAtlasView(grid, inventory, flows)  → machine.AtlasView
FlowDriver.Update        （逻辑推进，不变）
FlowDriver.LateUpdateVisuals(dt)   逐台 machine.AtlasView.Tick(dt)
PBModEntry.OnLateUpdate  FlowDriver.LateUpdateVisuals(Time.deltaTime)
FlowDriver.Retire/Reset  machine.AtlasView.Dispose()
```

`FlowAtlasView` 自己订阅 `FlowSpawner` 的生死广播、持有 `FlowAtlasLayer`、实体表、collect 残影、12fps 时钟；**没有任何静态可变的网格字典**。共享的只有 `FlowAtlasTexture`（纹理 + Sprite 缓存）。

## 6. 验收

1. `dotnet build -c Release` 0 warning / 0 error。
2. `dotnet test` 全绿；新增测试覆盖 `FlowComposition` 归一化/空、`FlowTint` 纯水与混合、key 格式、拖尾层数。
3. `rg` 全仓无 `FlowParticle`、`IEmissionPolicy`、`ParticleStyle`、`FlowVisual` 的引用。
4. 蒙版对拍：纯水/常温 20 个 key 与 `render_final.render()` 逐像素一致。
5. 视觉：`assets/flow_mask_*` 抽 12 张 × 3 色混合（纯水 / 50% 毒 / 30% 油+70% 酸）拼图，肉眼确认边缘温度色正确。
6. 无静态网格字典：`FlowAtlasView` 之外不出现 `Dictionary<IFlowGrid, ...>`。

## 7. 风险

- 蒙版把亮核变成近白（accent），比原设计的"成分色高亮核"更白；盲测方向依赖"头最亮"，仍需保留钝头 + 2px 高光。
- 成分专属纹理被删（毒泡/油膜/血絮），成分只靠颜色区分；这是 A 方案的代价，需在 README 记录。
- 每流源 4 层（body/edge/accent/wall）+ 0–6 残影（按含量）；射程点暂不渲染（无 Range 数据），满网格时节点数约为旧版 2 倍，需实测帧时间。
