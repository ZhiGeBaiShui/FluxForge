namespace FluxForge.Mod
{
    /// <summary>
    /// 唯一的装置身份装配件：只把「这件物品携带哪种装置」写进 tag。
    ///
    /// 本类**不创建装置实例、不登记装置**。
    /// 装置实例由物品挂上容器时按 tag 现场构造，让「构造 → 初始化 → 注册」落在同一个时刻。
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

            // 装置身份：tag 名就是装置类型名（= items.json 的 device 字段 = FlowDeviceCatalog 的 key）。
            // 绑定端遍历目录里的全部类型名，命中哪个 tag 就用哪个类型构造。
            item.EnableTag(def.device, true);

            // 中心格随模板写入 tag；运行时实例沿用这份物品状态。
            // 局部坐标，不是世界坐标；旋转/翻转由 FlowGridAdapter.ForwardTransform 处理。
            item.EnableTag("FLUXFORGE_DEVICE_ANCHOR_X", true);
            item.EnableTag("FLUXFORGE_DEVICE_ANCHOR_Y", true);
            item.state.GetTag("FLUXFORGE_DEVICE_ANCHOR_X").SetInt(def.anchorX.Value);
            item.state.GetTag("FLUXFORGE_DEVICE_ANCHOR_Y").SetInt(def.anchorY.Value);

            // 批量写 state 后刷新派生快照，再交给目录。
            item.SyncModifiedState();
        }
    }
}
