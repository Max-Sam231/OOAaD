using App;

namespace SpaceBattle.Lib
{
    public sealed class RegisterIoCDependencyScopeHierarchy : ICommand
    {
        private readonly object _rootScope;
        private readonly ICommand _baseInitializer;

        public RegisterIoCDependencyScopeHierarchy(object rootScope, ICommand baseInitializer)
        {
            ArgumentNullException.ThrowIfNull(rootScope);
            ArgumentNullException.ThrowIfNull(baseInitializer);

            _rootScope = rootScope;
            _baseInitializer = baseInitializer;
        }

        public void Execute()
        {
            var loaderFactory = new PluginLoaderFactory();
            var baseBuilder = new GameBaseScopeBuilder();
            var proxyFactory = new ProxyScopeFactory();

            var initialBaseScope = baseBuilder.Build(
                _rootScope,
                _baseInitializer,
                ScopedAssemblyCatalogFactory.CreateDefault(),
                Array.Empty<ICommand>());

            var holder = new ScopeParentHolder(initialBaseScope);
            var proxyScope = proxyFactory.Create(holder);

            Register("Server.Scope.Root", _ => _rootScope);
            Register("Server.Scope.Holder", _ => holder);
            Register("Server.Scope.Proxy", _ => proxyScope);
            Register("Scopes.Base.Initializer", _ => _baseInitializer);
            Register("Scopes.Plugins.Load", args => loaderFactory.Load((string)args[0]));
            Register(
                "Scopes.Base.Build",
                args => baseBuilder.Build(
                    args[0],
                    (ICommand)args[1],
                    (ScopedAssemblyCatalog)args[2],
                    (IReadOnlyList<ICommand>)args[3]));
            Register("Scopes.Game.Create", _ => Ioc.Resolve<object>("IoC.Scope.Create", proxyScope));
            Register("Scopes.HotReload", args => new HotReloadGameBaseScopeCommand((string)args[0]));
            Register(
                "Game.Create.Scoped",
                args =>
                {
                    var innerCommand = (ICommand)args[0];
                    var gameInitializer = (ICommand)args[1];
                    var gameScope = Ioc.Resolve<object>("IoC.Scope.Create", proxyScope);
                    new InSeparateScopeCommand(gameScope, gameInitializer).Execute();
                    return new InSeparateScopeCommand(gameScope, innerCommand);
                });
        }

        private static void Register(string dependency, Func<object[], object> strategy)
        {
            Ioc.Resolve<ICommand>("IoC.Register", dependency, strategy).Execute();
        }
    }
}
