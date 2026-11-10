namespace FluxForge.Story
{
    internal sealed class VisitDecision
    {
        internal VisitRule Rule { get; }
        internal VisitKind Kind
        {
            get { return Rule.Kind; }
        }

        internal VisitDecision(VisitRule rule)
        {
            Rule = rule;
        }
    }
}
