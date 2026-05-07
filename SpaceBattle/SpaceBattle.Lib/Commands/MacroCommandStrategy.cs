using App;

namespace SpaceBattle.Lib
{
    public class CreateMacroCommandStrategy
    {
        private readonly string _commandSpec;

        public CreateMacroCommandStrategy(string commandSpec)
        {
            _commandSpec = commandSpec;
        }

        public ICommand Resolve(object[] args)
        {
            var commandNames = Ioc.Resolve<IEnumerable<string>>($"Specs.{_commandSpec}");

            var commands = commandNames.Select(name => Ioc.Resolve<ICommand>(name, args)).ToList();

            return Ioc.Resolve<ICommand>("Commands.Macro", commands);
        }
    }
}
