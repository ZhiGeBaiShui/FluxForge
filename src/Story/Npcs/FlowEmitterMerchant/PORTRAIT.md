# NPC 人物贴图机制设计

## 1. 目标

```text
保留原始 864×1152 全图
不裁掉人物
不横向拉宽人物
不降低原始像素
显示范围和原版人物完全一致
底部对齐原版
水平居中
不超出交易窗口
```

## 2. 原版链路

```text
StoreClient.spriteName
→ StoreClient.GetSprite()
→ SpriteDict.Instance.ShowClientSprite(key)
→ SpriteDict.GetSprite(key)
→ spriteDictionary[key]
→ StoreClientMono.image.sprite
```

原版人物 Sprite：

```text
288 × 211
pivot = (0.5, 0.5)
pixelsPerUnit = 100
世界尺寸 = 2.88 × 2.11
```

`SpriteDict` 没有公开注册 API，但 `spriteDictionary` 是 public，可以在初始化后插入自定义 key。

官方 ModLoader 会把：

```text
Textures/*.png       读入 LoadedMod.Textures
Textures/Items/*.png 读入 LoadedMod.Sprites
Textures/Paper/*.png 读入 LoadedMod.PaperSprites
```

人物立绘应放在：

```text
FluxForge/Textures/npc_saleswoman.png
```

不要放到 `Textures/Items`，那里会按物品图标处理。

## 3. 核心方案：透明画布补边

不裁剪、不拉伸原图，而是把原图完整放进一张符合原版比例的高分辨率透明画布，再按原版尺寸创建 Sprite。

原图：

```text
864 × 1152
```

原版比例：

```text
288 / 211 = 1.36493
```

透明画布：

```text
高度 = 1152
宽度 = round(1152 × 1.36493) = 1572
```

把原图完整贴入画布中央：

```text
左右各补 354 像素透明边
人物本身不缩放、不裁切
```

创建 Sprite：

```csharp
float ppu = canvasWidth / 2.88f;
// 1572 / 2.88 ≈ 545.83

Sprite.Create(
    paddedTexture,
    new Rect(0, 0, canvasWidth, canvasHeight),
    new Vector2(0.5f, 0.5f),
    ppu);
```

结果：

```text
世界宽度 = 1572 / 545.83 ≈ 2.88
世界高度 = 1152 / 545.83 ≈ 2.11
```

与原版完全一致：

```text
2.88 × 2.11
```

因为 Sprite 的 pivot 是中心，且高度一致，所以底部自然与原版底部对齐，水平也自然居中。

## 4. 组件设计

```text
NpcPortraitRegistry
  管理 NPC key → Sprite
  负责加载 PNG、补透明画布、创建 Sprite
  负责注入 SpriteDict.spriteDictionary

NpcPortraitDefinition
  NpcId
  SpriteKey
  TextureName
  ReferenceSpriteKey = "merchant"

NpcPortraitPatch
  SpriteDict.Awake postfix
  确保每次字典重建后重新注入
```

### 注册流程

```text
FFModEntry.Init
→ NpcPortraitRegistry.Initialize()
→ 读取 LoadedMod.Textures["npc_saleswoman"]
→ 生成 paddedTexture
→ Sprite.Create
→ 等待 SpriteDict.Awake
→ spriteDictionary["fluxforge_saleswoman"] = sprite
```

### NPC 绑定

`FlowEmitterMerchantNpc.Create()`：

```csharp
StoreClient client = new StoreClient
{
    ...
    spriteName = "fluxforge_saleswoman",
    ...
};
```

不要再用：

```csharp
spriteName = SpriteDict.GetRandomMerchantSpriteName(),
```

## 5. 为什么不直接使用完整 864×1152

完整原图比例：

```text
864 / 1152 = 0.75
```

原版比例：

```text
1.36493
```

如果让高度对齐：

```text
PPU = 1152 / 2.11 ≈ 546
世界宽度 = 864 / 546 ≈ 1.58
```

结果是人物比原版窄，左右会空；虽然不裁切，但显示范围不等于原版。

透明补边方案：

```text
人物保持完整
画布补到原版比例
世界尺寸完全是 2.88×2.11
```

这是唯一同时满足：

```text
不裁人物
不横向变形
显示范围和原版一致
```

的方案。

## 6. 参考系选择

优先使用原版 Sprite 的几何尺寸：

```csharp
Sprite reference = SpriteDict.Instance.GetSprite("merchant");
Vector2 size = reference.bounds.size; // 2.88×2.11
```

如果以后游戏改了显示缩放，使用 `StoreClientMono.image.bounds` 作为实际参考：

```text
referenceMinY = image.bounds.min.y
referenceCenterX = image.bounds.center.x
```

但常规情况下不必改 transform：只要 Sprite 的世界尺寸严格等于 `2.88×2.11`，中心 pivot 就能自动底部对齐并居中。

## 7. 时序

```text
ModLoader.LoadModAssets
→ Textures/npc_saleswoman.png 进入 LoadedMod.Textures

FFModEntry.Init
→ NpcPortraitRegistry.Initialize()
→ 加载/合成 Sprite

SpriteDict.Awake
→ 原版字典构建完成
→ NpcPortraitPatch 注入自定义 Sprite

商人到达
→ StoreClient.spriteName = "fluxforge_saleswoman"
→ ShowClientSprite 命中自定义 Sprite
```

不能只在 `OnGameLoadedLate` 注入一次，因为 `SpriteDict` 可能在场景重建时重新 Awake；用 postfix 保证每次重建后都存在。

## 8. 失败处理

```text
PNG 不存在
→ 记录 Error
→ 不修改 spriteName，回落原版 merchant

画布创建失败
→ 记录 Error
→ 回落到 merchant

SpriteDict 尚未初始化
→ 保留待注册状态
→ Awake 后重试
```

不允许因为人物贴图失败导致商人无法到店。

## 9. 资源约定

```text
源文件保留：
assets/推销员.png

部署文件：
Textures/npc_saleswoman.png

Sprite key：
fluxforge_saleswoman
```

源文件与部署文件名分开，避免中文文件名在不同平台/打包链路下产生编码问题。

## 10. 验收标准

```text
[ ] 原图 864×1152 完整保留
[ ] 人物没有横向拉伸
[ ] 人物没有裁切
[ ] Sprite 世界尺寸约 2.88×2.11
[ ] 底部和原版人物对齐
[ ] 水平居中
[ ] 不超出交易窗口
[ ] 不依赖物品工厂
[ ] 换场景/读档后仍然生效
```
