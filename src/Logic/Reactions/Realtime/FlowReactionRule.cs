namespace FluxForge
{
    /// <summary>
    /// 实时化学反应规则的基类。
    ///
    /// 所有反应规则都必须继承本类，并提供无参构造；<see cref="FlowReactionSolver"/>
    /// 会在静态初始化时扫描本程序集，自动收集全部非抽象子类。
    /// </summary>
    public abstract class FlowReactionRule
    {
        /// <summary>执行顺序，数值小的先执行；同一顺序按类型全名稳定排序。</summary>
        public abstract int Order { get; }

        /// <summary>对一份流源属性执行本规则；不满足条件时应直接返回。</summary>
        public abstract void React(FlowProperties properties);
    }
}
