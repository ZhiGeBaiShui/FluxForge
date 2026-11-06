using System.Collections.Generic;
using FluxForge.Mod;

namespace FluxForge.Events
{
    /// <summary>
    /// 事件演示物品的唯一创建/放置入口。
    ///
    /// 放置坐标是「锚点格」：发射器贴图的锚点在未旋转时的最上格，
    /// 因此这里先按朝向算锚点偏移，再反推 modifiedShape 的 minX/minY。
    /// </summary>
    internal static class DemoPlacement
    {
        internal static GameItem Spawn(
            EventSession session,
            GameGridInventory grid,
            string itemKey,
            int anchorCellX,
            int anchorCellY,
            int orientation,
            bool flipped)
        {
            GameItem item = DirectoryMaster.Item(itemKey);
            if (item == null)
            {
                throw new System.InvalidOperationException(
                    "事件物品不存在：" + itemKey);
            }

            GridShape baseShape = item.shape;
            if (baseShape == null)
            {
                throw new System.InvalidOperationException(
                    "事件物品没有形状：" + itemKey);
            }

            int anchorX = 0;
            int anchorY = 0;
            if (item.IsTag(FlowTags.AnchorX) && item.IsTag(FlowTags.AnchorY))
            {
                anchorX = item.GetTagReadonly(FlowTags.AnchorX).GetInt();
                anchorY = item.GetTagReadonly(FlowTags.AnchorY).GetInt();
            }

            PlacementMath.MinForAnchor(
                anchorCellX,
                anchorCellY,
                anchorX,
                anchorY,
                baseShape.width,
                baseShape.height,
                flipped,
                orientation,
                out int minX,
                out int minY);

            GridShapeBuilder desired = new GridShapeBuilder(baseShape)
                .SetTransform(minX, minY, flipped, orientation);

            item.modifiedShape = desired;
            item.modifiedXOrigin = 0;
            item.modifiedYOrigin = 0;

            if (!grid.CheckShapePlacement(item, desired, new List<GameItem>()))
            {
                item.Destroy();
                return null;
            }

            if (!grid.UncheckedAccept(item))
            {
                throw new System.InvalidOperationException(
                    "事件物品被网格拒绝：" + itemKey);
            }

            // UncheckedAccept 内部会 ValidateShapeState，可能按原 shape 重算位置。
            // 事件物品没有后续玩家拖拽，直接在入格后把最终形状钉回目标位置。
            item.modifiedShape = new GridShapeBuilder(baseShape)
                .SetTransform(minX, minY, flipped, orientation);
            item.modifiedXOrigin = 0;
            item.modifiedYOrigin = 0;
            item.Validate();

            FlowRuntime runtime = GridRegistry.Get(grid);
            runtime.Grid.Register(item);

            DeviceRegistry.SetFacing(
                item,
                FlowFacingAdapter.FromOrientation(orientation),
                flipped);

            GridLock.Track(item);
            session.TrackItem(item);
            return item;
        }

        internal static void FillWater(GameItem item, int microlitres)
        {
            FlowProperties properties = FlowStock.Read(item);
            properties.Set(FlowProps.Water, microlitres);
            FlowStock.Write(item, properties);
        }
    }
}
