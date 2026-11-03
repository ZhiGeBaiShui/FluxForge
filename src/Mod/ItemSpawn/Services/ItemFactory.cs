using FluxForge.Mod.Rendering.ItemSprites;

namespace FluxForge.Mod
{
    /// <summary>
    /// 物品工厂：把一条 <see cref="FlowItemDefinition"/> 装配成 GameItem。
    ///
    /// 起点是 CreateEmptyItem，随后由 <see cref="Initialize"/> 按 items.json 逐段填充内容。
    /// </summary>
    public static class ItemFactory
    {
        /// <summary>manifest.xml 的 &lt;ID&gt;；贴图管理者按它从 ModLoader 找到本模组的 Textures/Items。</summary>
        internal const string ModId = "fluxforge";

        /// <summary>
        /// 按配置装配物品：从空物品开始，按 items.json 逐项加工；造不出来时返回 null。
        ///
        /// 原版自己的物品工厂也是这个形态（例如 ModuleDirectory.SystemPerformanceModule：
        /// CreateEmptyItem → SetSpriteAndShape → SetName → SetGameItemType → InitModuleItem），
        /// 所以这里不复制任何模板，一切外观 / 文本 / 类型 / 价值 / 标签都由配置决定。
        /// </summary>
        public static GameItem Create(FlowItemDefinition def)
        {
            GameItem item = ItemDirectory.CreateEmptyItem(null);
            if (item == null)
            {
                FFModEntry.Log.Error("[工厂] 「" + def.key + "」空物品创建失败");
                return null;
            }

            item.identifier = def.key;
            Initialize(item, def);
            return item;
        }

        /// <summary>
        /// 内容初始化序列。只对「刚造出、尚未被任何 UI 持有」的物品调用一次。
        ///
        /// 每一项都是**显式覆盖**：配置里没写的字段保持空物品默认值。
        ///
        /// 步骤之间有顺序依赖：④ 必须排在 ③ 之后，原因见各自注释。
        /// </summary>
        private static void Initialize(GameItem item, FlowItemDefinition def)
        {
            // ① 外观：自定义图集贴图（sprites.json）+ 声明的占格形状。没配 sprite 时保持空物品外观。
            ApplySprite(item, def);

            // ② 文本：名称 / 正文 / 风味是三段独立字段，在原版 tooltip 里落在不同位置
            //    （标题 / 正文段 / 风味段），不是一整块文本，所以逐段判空、逐段覆盖。
            ApplyText(item, def);

            // ③ 类别：显式配了类型就整体替换，没配则保持无类型。
            //    必须先 RemoveAllGameItemType 再 SetGameItemType——后者只加不清。
            ApplyTypes(item, def);

            //    价值：0 表示无价，所以只在 > 0 时写。
            ApplyValue(item, def);

            // ④ 标签：启用 tag 并只写声明过的槽位，其余槽位保持默认值。
            //    必须最后：类型本质就是 tag，③ 会先清掉全部类型 tag，
            //    提前写的标签可能被那一步清掉。
            ApplyTags(item, def);

            // ApplyTags 用 skipValidate: true 批量写 state，不会走 ValidateShapeState，
            // 这里统一刷新 modifiedState，保证交给目录的物品快照是完整的。
            item.SyncModifiedState();
        }

        /// <summary>
        /// 贴图三字段 + shape 占位。
        ///
        /// 形如 spriteAtlasPath=ice_block_atlas / spritePath=ice_block / spriteChanged=true，
        /// 游戏解析贴图时由 <see cref="LoadFromAtlasPatch"/> 命中我们自建的 Sprite。
        /// </summary>
        private static void ApplySprite(GameItem item, FlowItemDefinition def)
        {
            SpriteEntry sprite = FlowSpriteRegistry.Instance.GetEntry(def.SpriteKey);
            if (sprite == null)
            {
                return;
            }

            // spriteChanged 是 protected；SetSprite 是公开的“设两字段 + 标记重取”入口，语义相同。
            item.SetSprite(sprite.atlas, sprite.key);

            GridShapeBuilder shapeBuilder = new GridShapeBuilder();
            shapeBuilder.SetData(sprite.shape, sprite.shapeWidth);
            item.shape = shapeBuilder.shape;
        }

        /// <summary>名称 + 正文说明 + 风味文本（空值跳过）。</summary>
        private static void ApplyText(GameItem item, FlowItemDefinition def)
        {
            if (!string.IsNullOrEmpty(def.name))
            {
                item.SetName(def.name);
            }

            if (!string.IsNullOrEmpty(def.desc))
            {
                item.shortDescription = def.desc;
            }

            if (!string.IsNullOrEmpty(def.flavor))
            {
                item.flavorText = def.flavor;
            }
        }

        /// <summary>
        /// 物品类型（可多个，对应 tooltip 的 "[类型]" 段）。
        ///
        /// 显式配了类型时先 <c>RemoveAllGameItemType</c>：<c>SetGameItemType</c> 只加不清，
        /// 不先清掉的话，空物品可能带着默认类型被当成别的物品处理。
        private static void ApplyTypes(GameItem item, FlowItemDefinition def)
        {
            if (def.itemTypes != null)
            {
                item.RemoveAllGameItemType();

                for (int i = 0; i < def.itemTypes.Length; i++)
                {
                    string itemType = def.itemTypes[i];
                    if (!string.IsNullOrEmpty(itemType))
                    {
                        item.SetGameItemType(itemType);
                    }
                }

                return;
            }

            if (!string.IsNullOrEmpty(def.type))
            {
                item.RemoveAllGameItemType();
                item.SetGameItemType(def.type);
            }
        }

        /// <summary>
        /// 基础价值。0 表示无价；非 0 时同时写 <c>unitBaseValue</c>，
        /// 让「总价值」与「单价」两个字段保持一致。
        /// </summary>
        private static void ApplyValue(GameItem item, FlowItemDefinition def)
        {
            if (def.value > 0)
            {
                item.SetValue(def.value);
                item.unitBaseValue = def.value;
            }
        }

        /// <summary>按配置声明写入 tag（GetTag 会自动创建；直接改槽位字段，避开自定义委托）。</summary>
        private static void ApplyTags(GameItem item, FlowItemDefinition def)
        {
            if (def.tags == null)
            {
                return;
            }

            for (int i = 0; i < def.tags.Length; i++)
            {
                SetTag(item, def.tags[i]);
            }
        }

        /// <summary>
        /// 写一条 tag：先启用，再按声明的字段名写入对应槽位。
        /// 声明了哪些槽位就写哪些，没声明的保持默认值（0 / null），互不影响。
        /// </summary>
        private static void SetTag(GameItem item, TagConfigEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.valueName))
            {
                return;
            }

            item.EnableTag(entry.valueName, true);

            TagState state = item.state.GetTag(entry.valueName);
            if (state == null)
            {
                FFModEntry.Log.Warning("[工厂] tag 创建失败：" + entry.valueName);
                return;
            }

            if (entry.valueInt.HasValue)
            {
                state.SetInt(entry.valueInt.Value);
            }

            if (entry.valueDouble.HasValue)
            {
                state.SetDouble(entry.valueDouble.Value);
            }

            if (entry.valueBool.HasValue)
            {
                state.SetBool(entry.valueBool.Value);
            }

            if (entry.valueString != null)
            {
                state.SetString(entry.valueString);
            }
        }
    }
}
