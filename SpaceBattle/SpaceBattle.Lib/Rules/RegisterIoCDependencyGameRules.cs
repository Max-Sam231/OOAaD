using App;

namespace SpaceBattle.Lib
{
    public sealed class RegisterIoCDependencyGameRules : ICommand
    {
        public void Execute()
        {
            var sorter = new TopologicalOrderBuilder();
            var parser = new GameRulesParserBuilder(sorter);

            Register("Game.Rules.Parse", args => parser.Parse((string)args[0]));
            Register("Game.Rules.Apply", args => new RegisterGameRulesCommand((GameRulesConfig)args[0]));
            Register(
                "Game.Rules.RegisterNamed",
                args =>
                {
                    var ruleName = (string)args[0];
                    var rulesJson = (string)args[1];
                    return new DelegatingCommand(() =>
                    {
                        ArgumentException.ThrowIfNullOrWhiteSpace(ruleName);
                        var parsed = parser.Parse(rulesJson);
                        Register("Game.Rules." + ruleName, _ => new RegisterGameRulesCommand(parsed));
                    });
                });
            Register("Game.Item.Create", args => new Dictionary<string, object>((IDictionary<string, object>)args[0]));
            Register("Game.Entity.Template.Resolve", _ => new Dictionary<string, object>());
            Register("Game.Victory.TargetType", _ => "Ship");
            Register("Commands.InTransaction", args => (ICommand)args[0]);
            Register("Game.State.Reindex", _ => Ioc.Resolve<ICommand>("Commands.Empty"));
        }

        private static void Register(string dependency, Func<object[], object> strategy)
        {
            Ioc.Resolve<ICommand>("IoC.Register", dependency, strategy).Execute();
        }
    }
}
