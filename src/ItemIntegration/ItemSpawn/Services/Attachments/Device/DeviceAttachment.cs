namespace FluxForge.Mod
{
    /// <summary>
    /// 唯一的装置身份装配件：只把「这件物品携带哪种装置」写进 tag。
    ///
    /// 本类**不创建装置实例、不登记装置**。
    /// 装置实例由物品进入容器（或读档补绑）时按 tag 现场构造，见 <see cref="ItemDeviceBinding"/>，
    /// 让「构造 → 初始化 → 注册」落在同一个时刻。
    /// </summary>
    public sealed class DeviceAttachment : ItemAttachment
    {
        public override string Name => "device";

        public override void Attach(GameItem item, FlowItemDefinition def)
        {
            if (string.IsNullOrEmpty(def.device))
            {
                throw new System.InvalidOperationException(
                    "[装置]「" + def.key + "」声明了 device 附件，但缺少 device 字段");
            }

            if (!def.anchorX.HasValue || !def.anchorY.HasValue)
            {
                throw new System.InvalidOperationException(
                    "[装置]「" + def.key + "」缺少中心格 anchorX/anchorY，device=" + def.device);
            }

            // 装置身份：tag 名就是装置 key（= items.json 的 device 字段 =
            // DeviceAdapterCatalog 的 key）。绑定端遍历目录里的全部 key，
            // 命中哪个 tag 就创建对应装置。
            item.EnableTag(def.device, true);

            // 中心格随模板写入 tag；运行时实例沿用这份物品状态。
            // 局部坐标，不是世界坐标；旋转/翻转由 FlowGridAdapter.ForwardTransform 处理。
            item.EnableTag(FlowTags.AnchorX, true);
            item.EnableTag(FlowTags.AnchorY, true);
            item.state.GetTag(FlowTags.AnchorX).SetInt(def.anchorX.Value);
            item.state.GetTag(FlowTags.AnchorY).SetInt(def.anchorY.Value);

            // 批量写 state 后刷新派生快照，再交给目录。
            item.SyncModifiedState();
        }
    }
}
