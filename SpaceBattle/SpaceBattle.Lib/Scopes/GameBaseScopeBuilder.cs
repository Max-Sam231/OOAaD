using App;

namespace SpaceBattle.Lib
{
    public sealed class GameBaseScopeBuilder
    {
        public object Build(
            object rootScope,
            ICommand baseInitializer,
            ScopedAssemblyCatalog catalog,
            IReadOnlyList<ICommand> registrationCommands)
        {
            ArgumentNullException.ThrowIfNull(rootScope);
            ArgumentNullException.ThrowIfNull(baseInitializer);
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(registrationCommands);

            var baseScope = Ioc.Resolve<object>("IoC.Scope.Create", rootScope);

            new InSeparateScopeCommand(
                baseScope,
                new DelegatingCommand(() =>
                {
                    new RegisterIoCDependencyAdapterFactory(catalog).Execute();
                    new RegisterIoCDependencyCommandFactory(catalog).Execute();
                    baseInitializer.Execute();

                    if (registrationCommands.Count > 0)
                    {
                        var dict = (IDictionary<string, Func<object[], object>>)baseScope;
                        dict["IoC.Register"] = args => new DelegatingCommand(
                            () => dict[(string)args[0]] = (Func<object[], object>)args[1]);
                        try
                        {
                            foreach (var command in registrationCommands)
                            {
                                command.Execute();
                            }
                        }
                        finally
                        {
                            dict.Remove("IoC.Register");
                        }
                    }
                })).Execute();

            return baseScope;
        }
    }
}
