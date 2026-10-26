namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 物品行为装配件：一件物品可以在 items.json 里声明多个，按声明顺序执行。
    ///
    /// 硬性规约（实施新装配件时逐条遵守）：
    ///   1. 无实例字段——实例启动时创建、全局共享；
    ///   2. 无静态可变状态——不要缓存 GameItem / IntPtr / PixelWindow，读档后原生指针会复用；
    ///   3. 顶层 public sealed class，public 无参构造，不嵌套；
    ///   4. 不得覆盖 <see cref="ItemFactory"/> 五步写入的内容（identifier / 贴图 / shape /
    ///      名称 / 说明 / 风味 / itemTypes / value / 配置声明的 tags）；调用游戏原生装配 API
    ///      建立行为所需状态（如 <c>WaterHelper.InitLiquidContainerItem</c>）不算越界；
    ///   5. 一件物品挂哪些装配件、组合是否互斥（两个面板 / 两个装置 / 两组激活委托），
    ///      由作者在 items.json 里决定并保证；运行期不做检查，同一字段被写两次时后者生效；
    ///   6. 装配件之间不得互相依赖，需要依赖就该合并；<see cref="FlowItemDefinition"/> 是唯一配置入口；
    ///   7. 装配件必须无参——需要参数就说明该参数应成为 def 的顶层字段，或该装配件应被拆分。
    /// </summary>
    public abstract class ItemAttachment
    {
        /// <summary>装配件名，items.json 用它引用。子类硬编码，全局唯一。</summary>
        public abstract string Name { get; }

        /// <summary>只挂行为。</summary>
        public abstract void Attach(Il2Cpp.GameItem item, FlowItemDefinition def);
    }
}
