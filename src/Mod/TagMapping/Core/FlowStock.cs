namespace FluxForge.Mod
{
    /// <summary>
    /// 流源 / 容器物品的运行期 <see cref="FlowProperties"/> ↔ 原生液体 tag 读写点。
    ///
    /// 只服务两类物品：发射器（流源进入运动层前的快照）和液体容器（倒水后的存量和
    /// tooltip）。装置私有状态不走这里，由各自的 <see cref="DeviceStateAdapter"/>
    /// 通过 <see cref="ITagStore"/> 读写。
    ///
    /// tag 名来自 <see cref="FlowPropertyTags"/> 的只读缓存（由
    /// <see cref="FlowTagNaming"/> 在启动期生成）：液体是
    /// <c>CURRENT_PART_&lt;ID&gt;</c>（int，µL），标量是 <c>&lt;ID&gt;_TAG</c>（double）。
    /// 这是原版协议，不是本 mod 的持久化格式，所以不用额外的映射表。
    ///
    /// 读写都经过 <see cref="GameItemTagStore"/>：读 <c>modifiedState</c>、写 <c>state</c>，
    /// 零值 / 等值写入由 store 跳过，因此只有真正有值的 tag 才会落盘。
    /// </summary>
    public static class FlowStock
    {
        /// <summary>读出一份完整的流源属性；不存在的 tag 按 0 处理。</summary>
        public static FlowProperties Read(GameItem item)
        {
            ITagStore tags = new GameItemTagStore(item);
            FlowProperties properties = new FlowProperties();

            foreach (FlowPropertyTag property in FlowPropertyTags.All)
            {
                properties.Set(property.Key, ReadValue(tags, property.Kind, property.Tag));
            }

            return properties;
        }

        /// <summary>
        /// 把一份完整的流源属性写回物品 tag，然后交给 <see cref="ItemTagCommit"/> 提交。
        ///
        /// 零值不会创建新 tag；已有的零值 tag 会保留（液体 tag 由游戏创建，删不得）。
        /// </summary>
        public static void Write(GameItem item, FlowProperties source)
        {
            ITagStore tags = new GameItemTagStore(item);

            foreach (FlowPropertyTag property in FlowPropertyTags.All)
            {
                double value = source.Get(property.Key);

                if (property.Kind == FlowPropertyKind.Liquid)
                {
                    tags.WriteInt(property.Tag, (int)value);
                }
                else
                {
                    tags.WriteDouble(property.Tag, value);
                }
            }

            ItemTagCommit.Commit(item);
        }

        /// <summary>总存量 = 全部液体 tag 之和（µL）；温度不计入。</summary>
        public static double Total(GameItem item)
        {
            ITagStore tags = new GameItemTagStore(item);
            double total = 0d;

            foreach (FlowPropertyTag property in FlowPropertyTags.All)
            {
                if (property.Kind != FlowPropertyKind.Liquid)
                {
                    continue;
                }

                total += tags.ReadInt(property.Tag);
            }

            return total;
        }

        private static double ReadValue(ITagStore tags, FlowPropertyKind kind, string tag)
        {
            return kind == FlowPropertyKind.Liquid ? tags.ReadInt(tag) : tags.ReadDouble(tag);
        }
    }
}
