namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 冰块物品的代码工厂（mod 层）。
    ///
    /// 目前只做数据化装配（贴图 / shape / 名称 / 说明 / 类型 / tags）——
    /// 装置实例与物品 tag 的对接留给适配层（ProbablyBagMod.Adapter）实现。
    /// </summary>
    public static class IceBlockFactory
    {
        /// <summary>物品 key（须与 items.json / sprites.json 一致）。</summary>
        public const string ItemKey = "ice_block";

        /// <summary>注册到 ItemFactory（在物品目录注册之前调用一次）。</summary>
        public static void Register()
        {
            ItemFactory.Register(ItemKey, Create);
        }

        private static Il2Cpp.GameItem Create(FlowItemDefinition def)
        {
            return ItemFactory.CreateFromData(def);
        }
    }
}
