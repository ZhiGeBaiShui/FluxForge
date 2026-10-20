# 运行时实现状态（A 方案 · 每机器视图 · 2026-10-21）

> 旧粒子框架与静态视觉桥**已全部删除**。当前实现：每台机器一个 `FlowAtlasView`，
> 颜色由「3 层蒙版 × 运行时染色」产生，成分混合由 `FlowComposition` 决定。
> 契约：`REFACTOR-A.md`。

## 1. 架构

```
FlowItemSetup          机器创建时 new FlowAtlasView(grid, inventory, flows) → machine.AtlasView
FlowDriver.Update      逻辑推进（不变）
FlowDriver.LateUpdateVisuals(dt)   逐台 machine.AtlasView.Tick(dt)（单台 try/catch）
PBModEntry.OnLateUpdate            → FlowDriver.LateUpdateVisuals(Time.deltaTime)
FlowDriver.Retire/Reset            → machine.AtlasView.Dispose()
```

- `FlowAtlasView`（每机器一个）：订阅本机 `FlowSpawner`、实体表、blocked 计数、collect 残影、12fps 时钟、`FlowAtlasLayer`；**没有静态网格字典**。
- 静态共享仅剩 `FlowAtlasTexture`（4 张纹理 + Sprite 缓存）。
- 已删除：`src/Presentation/Particles/**`（8 文件）、`FlowParticleLayer/Graphic`、`FlowVisual`、`FlowAtlasVisual`。

## 2. 资产

| 文件 | 内容 |
|---|---|
| `assets/flow_mask_body.png` | 本体/后缘/蒸汽/抽吸流亮度蒙版 |
| `assets/flow_mask_edge.png` | 1px 张力边 + 后缘暗边（染色 = 温度边色） |
| `assets/flow_mask_accent.png` | 亮核/前脸高光/霜点/白沫（近白） |
| `assets/flow_mask_wall.png` | 墙（固定 `#C9C2B8`，运行时 tint 必须白） |
| `assets/flow_mask.json` | `{"tiles":{key:[x,y]}}`，1040 key（272 张唯一图 + 非 flow 帧别名） |

key：`content|state|dir`，flow 追加 `|f0..f11`（**无 comp/temp 维度**）。

## 3. 染色

```csharp
FlowColor body   = FlowTint.Body(composition);              // 六色归一化加权
FlowColor edge   = FlowTint.Edge(composition, temp);        // shade(-0.52) + 冷/热边
FlowColor accent = FlowTint.Accent(composition);            // mix(body, white, 0.62)
// wall 图自带颜色，Image.color = white
```

Tier-0：`composition = PureWater`，温度/含量/方向/状态照旧映射。

## 4. 每流源节点

| 节点 | 数量 | 说明 |
|---|---|---|
| body / edge / accent / wall | 各 1 | 4 层蒙版，各自 tint；wall 仅 blocked |
| ghost | 0–6（按含量 1/2/4/6） | 同一 body sprite，alpha 阶梯，只 flow |
| dot（常驻粒子） | 每流源 ~4–5 个 | 1px 方块，池 512；体内 70% / 边缘 30%，径向 + 30% 后偏，0.4–0.65s 淡出 |
| 死亡 | 无贴图切换 | 本体立即隐藏，粒子停止生成后自然淡完（~0.6s） |

池化：64×4 + 384 + 192；懒创建、SetActive 复用、稳态 0 分配。

## 5. 验证（2026-10-21）

```
dotnet build -c Release --no-incremental   → 0 warning / 0 error
dotnet test tests/...                      → 78 / 78
rg "FlowParticle|IEmissionPolicy|...|FlowVisual\b" src → 0 命中
rg "Dictionary<IFlowGrid" src\Mod          → 0 命中
verify_mask_atlas.py                       → exit 0；alpha 272/272 key 一致
                                             颜色 Δmax=59 / Δavg=6.1（accent 已随成分；S 130→4、~ 119→15、C 81→0；剩余偏差来自 edge/body 近似，§6）
```

蒙版抽样：`docs/flow-visual/final/png/mask-sample.png`（12 形状 × 3 混色）。

## 6. 已知取舍

1. **成分专属纹理已删**（毒泡/油膜/血絮）：成分只靠混合色区分，边缘只随温度。
2. **蒙版是近似**：亮核/高光为近白（accent），与旧"成分色高亮核"有色调差；方向仍靠钝头+亮核。
3. **Linear 色彩空间**下 `Image.color × 蒙版` 与离线 sRGB 可能不逐字节一致。
4. **墙**只在 `large|blocked` 有像素；小档没有墙图。
5. 面板关闭期间粒子场清空（不会积压）；重开从零开始，不会闪旧帧。
6. 常驻粒子参数 = 探针 B 档（8/s、0.6–1.4 格/s、0.40–0.65s、alpha 0.45）；温度只影响扩散速度（cold ×0.6 / hot ×1.6），1× 下 cold↔normal 可辨、hot 略弱。
7. 扩展点：`IFlowParticleModifier` + `FlowParticleRules.Register`；以后毒物改色/概率只需加一个无状态 modifier。

## 7. 复跑

```powershell
$py = "C:\Users\Administrator\.dsh\dsh-runtimes\dsh-primary-runtime\dependencies\python\python.exe"
& $py tools/flow-visual/build_mask_atlas.py
& $py tools/flow-visual/verify_mask_atlas.py
dotnet build -c Release
dotnet test tests\ProbablyBagMod.Tests\ProbablyBagMod.Tests.csproj -c Release
```
