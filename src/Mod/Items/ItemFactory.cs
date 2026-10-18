using System;
using System.Collections.Generic;
using MelonLoader;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 物品工厂：把一条配置装配成 GameItem。
    ///
    /// 定制优先级：代码注册的工厂 &gt; 数据化装配。
    /// 数据化装配按 base 决定起点：
    ///   template（默认）→ 复制原版模板，继承其功能/标签/属性
    ///   empty           → CreateEmptyItem，从零装配（需自己配 type/tags/value）
    /// 随后统一套用：贴图 + 形状 + 名称 + 说明 + 类型/价值 + 标签值。
    /// </summary>
    public static class ItemFactory
    {
        private static readonly Dictionary<string, Func<FlowItemDefinition, Il2Cpp.GameItem>> _custom
            = new Dictionary<string, Func<FlowItemDefinition, Il2Cpp.GameItem>>();

        /// <summary>注册代码工厂（同一 key 后注册覆盖先注册）。</summary>
        public static void Register(string key, Func<FlowItemDefinition, Il2Cpp.GameItem> factory)
        {
            _custom[key] = factory;
        }

        /// <summary>按配置创建物品：代码工厂优先，否则走数据化装配。</summary>
        public static Il2Cpp.GameItem Create(FlowItemDefinition def)
        {
            Func<FlowItemDefinition, Il2Cpp.GameItem> custom;
            if (_custom.TryGetValue(def.key, out custom))
            {
                return custom(def);
            }

            return CreateFromData(def);
        }

        /// <summary>数据化装配（可单独调用，供代码工厂复用）。</summary>
        public static Il2Cpp.GameItem CreateFromData(FlowItemDefinition def)
        {
            Il2Cpp.GameItem item = CreateBase(def);
            if (item == null)
            {
                return null;
            }

            ApplySprite(item, def);
            ApplyText(item, def);
            ApplyTypeAndValue(item, def);
            ApplyTags(item, def);

            return item;
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

        /// <summary>物品类型（可多个，对应 tooltip 的 "[类型]" 段）+ 价值。类型为空则不显示类目。</summary>
        private static void ApplyTypeAndValue(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            if (def.itemTypes != null)
            {
                for (int i = 0; i < def.itemTypes.Length; i++)
                {
                    string itemType = def.itemTypes[i];
                    if (!string.IsNullOrEmpty(itemType))
                    {
                        item.SetGameItemType(itemType);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(def.type))
            {
                item.SetGameItemType(def.type);
            }

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
                state.valueInt = entry.valueInt.Value;
            }

            if (entry.valueFloat.HasValue)
            {
                state.valueFloat = entry.valueFloat.Value;
            }

            if (entry.valueDouble.HasValue)
            {
                state.valueDouble = entry.valueDouble.Value;
            }

            if (entry.valueBool.HasValue)
            {
                state.valueBool = entry.valueBool.Value;
            }

            if (entry.valueString != null)
            {
                state.valueString = entry.valueString;
            }
        }
    }
}
