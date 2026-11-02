namespace FluxForge
{
    /// <summary>
    /// 注册表里每一个属性的固定槽位键。
    ///
    /// 逻辑代码用这些字段读写 <see cref="FlowProperties"/>，
    /// 不要依赖数字下标，也不要重复 <see cref="FlowPropertyRegistry"/> 的字符串。
    /// </summary>
    public static class FlowProps
    {
        public static readonly FlowPropertyKey Water = FlowPropertyRegistry.Key("water");
        public static readonly FlowPropertyKey HeavyMetal = FlowPropertyRegistry.Key("heavy_metal");
        public static readonly FlowPropertyKey OrganicWaste = FlowPropertyRegistry.Key("organic_waste");
        public static readonly FlowPropertyKey Microplastic = FlowPropertyRegistry.Key("microplastic");
        public static readonly FlowPropertyKey Microbe = FlowPropertyRegistry.Key("microbe");
        public static readonly FlowPropertyKey ChemicalContaminant = FlowPropertyRegistry.Key("chemical_contaminant");
        public static readonly FlowPropertyKey PhysicalContaminant = FlowPropertyRegistry.Key("physical_contaminant");
        public static readonly FlowPropertyKey Mineral = FlowPropertyRegistry.Key("mineral");
        public static readonly FlowPropertyKey DreamCapMush = FlowPropertyRegistry.Key("dream_cap_mush");
        public static readonly FlowPropertyKey DreamCapExtract = FlowPropertyRegistry.Key("dream_cap_extract");
        public static readonly FlowPropertyKey PlantMatter = FlowPropertyRegistry.Key("plant_matter");
        public static readonly FlowPropertyKey Alcohol = FlowPropertyRegistry.Key("alcohol");
        public static readonly FlowPropertyKey Caffeine = FlowPropertyRegistry.Key("caffeine");
        public static readonly FlowPropertyKey Protein = FlowPropertyRegistry.Key("protein");
        public static readonly FlowPropertyKey Fat = FlowPropertyRegistry.Key("fat");
        public static readonly FlowPropertyKey Starch = FlowPropertyRegistry.Key("starch");
        public static readonly FlowPropertyKey Sugar = FlowPropertyRegistry.Key("sugar");
        public static readonly FlowPropertyKey CoffeeMush = FlowPropertyRegistry.Key("coffee_mush");
        public static readonly FlowPropertyKey Contraceptive = FlowPropertyRegistry.Key("contraceptive");
        public static readonly FlowPropertyKey Zyanide = FlowPropertyRegistry.Key("zyanide");
        public static readonly FlowPropertyKey NightmareExtract = FlowPropertyRegistry.Key("nightmare_extract");
        public static readonly FlowPropertyKey NightmareCapMush = FlowPropertyRegistry.Key("nightmare_cap_mush");
        public static readonly FlowPropertyKey Chlorine = FlowPropertyRegistry.Key("chlorine");
        public static readonly FlowPropertyKey Synaprest = FlowPropertyRegistry.Key("synaprest");
        public static readonly FlowPropertyKey Fanta = FlowPropertyRegistry.Key("fanta");
        public static readonly FlowPropertyKey Methylol = FlowPropertyRegistry.Key("methylol");
        public static readonly FlowPropertyKey ReducingAgent = FlowPropertyRegistry.Key("reducing_agent");
        public static readonly FlowPropertyKey Acid = FlowPropertyRegistry.Key("acid");
        public static readonly FlowPropertyKey Base = FlowPropertyRegistry.Key("base");
        public static readonly FlowPropertyKey StabilizingAgent = FlowPropertyRegistry.Key("stabilizing_agent");
        public static readonly FlowPropertyKey Debris = FlowPropertyRegistry.Key("debris");
        public static readonly FlowPropertyKey LipobondAgent = FlowPropertyRegistry.Key("lipobond_agent");
        public static readonly FlowPropertyKey LivestockSteroid = FlowPropertyRegistry.Key("livestock_steroid");
        public static readonly FlowPropertyKey LivestockImmunity = FlowPropertyRegistry.Key("livestock_immunity");
        public static readonly FlowPropertyKey WineExtract = FlowPropertyRegistry.Key("wine_extract");
        public static readonly FlowPropertyKey Nomore = FlowPropertyRegistry.Key("nomore");
        public static readonly FlowPropertyKey Temperature = FlowPropertyRegistry.Key("container_temp");
    }
}
