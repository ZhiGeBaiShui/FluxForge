> 维护校对：2026-11-16。当前实现以[项目状态](current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 装置事件层

`src/Events` 为手册和独立装置演示服务，剧情层不调用它。公共入口为 `FlowEventApi`；返回true表示启动，`EventCompleted`按网格通知结束。

## 编写新演示

1. 在 `Demos/` 编写静态事件类，创建 `EventSession`。
2. 使用 `DemoPlacement.Spawn` 按锚点格放置，成功物品由session持有；Spawn实际返回null表示放置失败，调用Fail结束。
3. 使用session协程等待物品生成、流源接收或实际位置，不以固定延时猜测进入装置。
4. 明确调用Complete或CompleteAfter；持续回路也必须设定结束时间。
5. 在FlowEventApi增加入口，再由对应手册页绑定事件和网格尺寸。

## 当前事件

| 入口 | 行为 |
|---|---|
| RunBoundaryEmitter | 发射后流源离开边界 |
| RunTwoEmitterRelay | 两侧发射器接力 |
| RunFilterLoop | 左侧发射一次、右侧接收 |
| RunColdTrapLoop | 基础冷萃，往返3轮 |
| RunColdTrapAdvancedLoop | 进阶，右侧一次发射、左侧接收后2秒清理 |
| RunColdTrapAutomation | 按图闭环，进入冷萃后补左下转角，闭环后10秒清理 |
| RunCentrifuge | 单次离心演示 |

## 生命周期和几何

EventSession拥有物品、协程和取消清理；手册关闭时ManualDemoPage停止事件、退役对应流源并销毁网格。GridLock单独提供可释放的交互锁，演示网格通过CanvasGroup禁止操作。

游戏orientation与逻辑Facing相反，统一经 `FlowFacingAdapter.FromOrientation` 转换。不要把逻辑顺时针方向直接填入Spawn的orientation。

自动化的转角在(3,2)、(5,4)、(5,0)、(7,0)、(7,2)、延迟放置(3,4)，orientation分别0、2、0、3、2、1。升温位于(3,3)/(6,0)，降温位于(4,4)/(7,1)，冷萃中心(5,2)，发射器锚点(1,4)。这些坐标相对于9×5布局左上角。

演示物品图片设Simple：原版RenderNodePrefab的Image是Tiled，Unity Image.GenerateTiledSprite用像素对齐后的区域向上取整计算平铺数；缩放时会出现多绘窄边的路径。它是显示配置修复，不修改PNG，不添加异常兜底；尚待游戏内视觉验证。
