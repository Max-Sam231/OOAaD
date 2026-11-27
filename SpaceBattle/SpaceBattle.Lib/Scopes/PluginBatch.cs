using App;

namespace SpaceBattle.Lib
{
    public sealed class PluginBatch
    {
        public ScopedAssemblyCatalog Catalog { get; }

        public IReadOnlyList<ICommand> RegistrationCommands { get; }

        public PluginBatch(ScopedAssemblyCatalog catalog, IReadOnlyList<ICommand> registrationCommands)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(registrationCommands);

            Catalog = catalog;
            RegistrationCommands = registrationCommands;
        }
    }
}
