using App;

namespace SpaceBattle.Lib
{
    public sealed class ProxyScopeFactory
    {
        public object Create(ScopeParentHolder holder)
        {
            ArgumentNullException.ThrowIfNull(holder);

            var scope = Ioc.Resolve<object>("IoC.Scope.Create.Empty");
            ((IDictionary<string, Func<object[], object>>)scope)["IoC.Scope.Parent"] = _ => holder.Current;

            return scope;
        }
    }
}
