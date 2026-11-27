using App;

namespace SpaceBattle.Lib
{
    public sealed class HotReloadGameBaseScopeCommand : ICommand
    {
        private readonly string _pluginsDirectory;

        public HotReloadGameBaseScopeCommand(string pluginsDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pluginsDirectory);
            _pluginsDirectory = pluginsDirectory;
        }

        public void Execute()
        {
            var rootScope = Ioc.Resolve<object>("Server.Scope.Root");
            var holder = Ioc.Resolve<ScopeParentHolder>("Server.Scope.Holder");
            var baseInitializer = Ioc.Resolve<ICommand>("Scopes.Base.Initializer");
            var batch = Ioc.Resolve<PluginBatch>("Scopes.Plugins.Load", _pluginsDirectory);
            var nextBaseScope = Ioc.Resolve<object>(
                "Scopes.Base.Build",
                rootScope,
                baseInitializer,
                batch.Catalog,
                batch.RegistrationCommands);

            holder.Swap(nextBaseScope);
        }
    }
}
