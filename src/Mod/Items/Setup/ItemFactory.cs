using MelonLoader;
using ProbablyBagMod.Mod.Rendering.ItemSprites;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 物品工厂：把一条 <see cref="FlowItemDefinition"/> 装配成 GameItem。
    ///
    /// 两步：起点按 base 决定，随后由 <see cref="Initialize"/> 逐段填充内容。
    ///   template（默认）→ 复制原版模板，继承其功能/标签/属性
    ///   empty           → CreateEmptyItem，从零装配（需自己配 type/tags/value）
    /// </summary>
    public static class ItemFactory
    {
        /// <summary>按配置装配物品：造基础物品 → 初始化内容；起点造不出来时返回 null。</summary>
        public static Il2Cpp.GameItem Create(FlowItemDefinition def)
        {
            Il2Cpp.GameItem item = CreateBase(def);
            if (item == null)
            {
                return null;
            }

            Initialize(item, def);
            return item;
        }

        /// <summary>
        /// 内容初始化序列。只对「刚造出、尚未被任何 UI 持有」的物品调用一次。
        ///
        /// 共同约定：每一步都是**增量覆盖**——配置里没写的字段保持模板原样，不会被清空。
        /// 所以 base=template 是「改几个字段」而不是「从零重建」；
        /// 代价是目前没有「显式清空」的表达方式：空串与缺省一样被跳过。
        ///
        /// 步骤之间有顺序依赖：④ 必须排在 ③ 之后，原因见各自注释。
        /// </summary>
        private static void Initialize(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            // ① 外观：贴图三字段 + 占格形状。没配 sprite 时整步跳过，保留模板外观。
            ApplySprite(item, def);

            // ② 文本：名称 / 正文 / 风味是三段独立字段，在原版 tooltip 里落在不同位置
            //    （标题 / 正文段 / 风味段），不是一整块文本，所以逐段判空、逐段覆盖。
            ApplyText(item, def);

            // ③ 类别：显式配了类型就整体替换，没配则保留模板类型。
            //    必须先 RemoveAllGameItemType 再 SetGameItemType——后者只加不清。
            ApplyTypes(item, def);

            //    价值：0 表示沿用模板，所以只在 > 0 时写。
            ApplyValue(item, def);

            // ④ 标签：启用 tag 并只写声明过的槽位，其余槽位保持默认值。
            //    必须最后：类型本质就是 tag，③ 会先清掉全部类型 tag，
            //    提前写的标签可能被那一步清掉。
            ApplyTags(item, def);
        }

        /// <summary>起点物品：模板复制 or 空物品；统一把 identifier 改成配置的 key（存档按它找回）。</summary>
        private static Il2Cpp.GameItem CreateBase(FlowItemDefinition def)
        {
            Il2Cpp.GameItem item;
            if (def.IsEmptyBase)
            {
                item = Il2Cpp.ItemDirectory.CreateEmptyItem(null);
            }
            else if (!string.IsNullOrEmpty(def.template))
            {
                item = Il2Cpp.DirectoryMaster.Item(def.template, true);
            }
            else
            {
                MelonLogger.Error("[工厂] " + def.key + " 既未指定 template 也不是 empty");
                return null;
            }

            if (item == null)
            {
                MelonLogger.Error("[工厂] " + def.key + " 基础物品创建失败（base="
                    + (def.IsEmptyBase ? "empty" : def.template) + "）");
                return null;
            }

            item.identifier = def.key;
            return item;
        }

        /// <summary>贴图三字段 + shape 占位。</summary>
        private static void ApplySprite(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            SpriteEntry sprite = FlowSpriteRegistry.Instance.GetEntry(def.SpriteKey);
            if (sprite == null)
            {
                return;
            }

            item.spriteAtlasPath = sprite.atlas;
            item.spritePath = sprite.key;
            item.spriteChanged = true;

            Il2Cpp.GridShapeBuilder shapeBuilder = new Il2Cpp.GridShapeBuilder();
            shapeBuilder.SetData(sprite.shape, sprite.shapeWidth);
            item.shape = shapeBuilder.shape;
        }

        /// <summary>名称 + 正文说明 + 风味文本（空值不覆盖模板）。</summary>
        private static void ApplyText(Il2Cpp.GameItem item, FlowItemDefinition def)
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
        /// 显式配了类型时先 <c>RemoveAllGameItemType</c>：<c>base: template</c> 会把模板的类型
        /// （比如性能模块的 MODULE）一起复制过来，而 <c>SetGameItemType</c> 只加不清，
        /// 不先清掉的话物品会同时属于两个类型，被原版的模块逻辑当成模组处理。
        ///
        /// <c>itemTypes</c> 优先于单个 <c>type</c>；两者都没配时保留模板类型。
        /// 空数组是合法值——表示「显式不要任何类目」，与「没配」语义不同。
        /// </summary>
        private static void ApplyTypes(Il2Cpp.GameItem item, FlowItemDefinition def)
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
        /// 基础价值。0 表示沿用模板；非 0 时同时写 <c>unitBaseValue</c>，
        /// 让「总价值」与「单价」两个字段保持一致。
        /// </summary>
        private static void ApplyValue(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            if (def.value > 0)
            {
                item.SetValue(def.value);
                item.unitBaseValue = def.value;
            }
        }

        /// <summary>按配置声明写入 tag（GetTag 会自动创建；直接改槽位字段，避开自定义委托）。</summary>
        private static void ApplyTags(Il2Cpp.GameItem item, FlowItemDefinition def)
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
        private static void SetTag(Il2Cpp.GameItem item, TagConfigEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.valueName))
            {
                return;
            }

            item.EnableTag(entry.valueName, true);

            Il2Cpp.TagState state = item.state.GetTag(entry.valueName);
            if (state == null)
            {
                MelonLogger.Warning("[工厂] tag 创建失败：" + entry.valueName);
                return;
            }

            if (entry.valueInt.HasValue)
            {
                state.SetInt(entry.valueInt.Value);
            }

            if (entry.valueFloat.HasValue)
            {
                state.SetFloat(entry.valueFloat.Value);
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
