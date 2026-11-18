# 单件拖拽后偏移：静态检查结论

检查对象：当前源码、当前原版 Assembly-CSharp.dll、dist/FluxForge-0.2.0-release-20261117-2045/FluxForge/FluxForge.dll。未取得反馈玩家的 DLL、复现存档及现场日志。

结论：目前未找到足以确定该玩家问题根因的内部错误；不能证明无 bug。确认一项有条件的坐标兼容问题。

## 单件拖拽落地与注册

原版调用链：

1. ItemMouseDragHandler.EndDrag → lastSlot.TryAcceptOnce。
2. SlotMarker.TryAcceptOnce 在调用 UncheckedAccept **之前**，已用 itemGridShape 更新 item.modifiedShape 的变换。
3. GameGridInventory.UncheckedAccept 调用 AddParent，最后 ValidateShapeState。
4. 我们的 ItemAttached 在 AddParent 后登记；ItemDragEnded 在整个 EndDrag 返回后再次按当前 modifiedShape 登记。
5. FlowGridAdapter.Register 先 Unregister 再 RegisterCore，清除该物品旧格。

所以，仅凭“AddParent 在 ValidateShapeState 之前”不能推出单件拖拽登记必定读取旧落点。此前对此时序的怀疑需要收窄。当前发版 DLL 也包含 ItemDragEnded，不能把玩家问题直接归因于当前源码缺少补登记。

原版反编译证据在 output/diagnostic-SlotMarker.cs、diagnostic-GameGridInventory.cs、diagnostic-ItemMouseDragHandler.cs；对应 mod 代码在 GridBuilding/Lifecycle/ItemDropped.cs、ItemAttached.cs 和 Services/FlowGridAdapter.cs。

## 发射点与装置逻辑格

FlowRuntime.ResolveEmit 和 FlowGridAdapter.RegisterCore 都读取 item.modifiedShape，并对同一 anchor 标签调用 shape.ForwardTransform。没有一处使用原始定义 shape、另一处使用落地 modifiedShape 的明显分歧。FlowSpawner.TryEmit 原样复制 origin；转向后 FlowManager.StopAt 使用格中心坐标。

## 流源中心换算

FlowGameSpaceAdapter.ToGameCenter(L) = L + 0.5。
FlowPixelMath.TileLeftFromGameCenter(G) = G - 0.5。
FlowAtlasLayer.PositionFor 以所得 L 为图块左上格坐标，再加图块半宽；CreateImage 的 pivot 为 (0.5, 0.5)。FlowAtlasTexture.SpriteAt 创建的 Sprite pivot 也是 (0.5, 0.5)。

在原版 16 像素格、物品容器与网格原点一致的前提下，流源中心为 ((Lx+0.5)*16, -(Ly+0.5)*16)，没有发现固定多加或少加半格的错误。该结论不涵盖素材内部透明留白造成的视觉偏心。

## 确认的有条件问题

原版 GameItemElement.ApplyRectTransform 和 GameGridInventory.GetGridPosition 使用 UISettings.current.gridSize；流源 FlowAtlasLayer.PositionFor 和 QuadSizePx 使用 FlowPixelMath.CellPixels=16。

若实际 gridSize 变成 20，逻辑 X=3 时原版格中心为 70，流源中心仍约为 56，产生随坐标变化的偏差。这是源码能够证明的条件性问题。

但当前原版 UISettings.gridSize 默认是 16；没有证据表明反馈玩家的值不是 16，因此不能认定这就是本次 bug，也不能因此直接断言是其它 mod 导致。

## 仍需现场证据的部分

实际安装版本、Harmony 后置补丁执行顺序、拖拽后 inner 与网格节点的相对变换、实际 gridSize、素材内容在 Image 内是否偏心。临时诊断版用于补齐这些数据。静态检查没有修改业务逻辑，也没有以测试通过替代游戏内复现。
