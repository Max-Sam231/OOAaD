using App;

namespace SpaceBattle.Lib
{
    public sealed class RegisterGameRulesCommand : ICommand
    {
        private readonly GameRulesConfig _config;

        public RegisterGameRulesCommand(GameRulesConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            _config = config;
        }

        public void Execute()
        {
            Register("Game.Rules.Version", _ => _config.Version);
            Register("Game.Rules.Config", _ => _config.RawJson);

            if (_config.VictoryTarget != null)
            {
                Register("Game.Victory.TargetType", _ => _config.VictoryTarget);
            }

            foreach (var action in _config.Actions)
            {
                var orderedSteps = action.OrderedSteps;
                Register("Specs." + action.Name, _ => orderedSteps);

                var strategy = new CreateMacroCommandStrategy(action.Name);
                Register("Macro." + action.Name, args => Ioc.Resolve<ICommand>("Commands.InTransaction", strategy.Resolve(args)));
            }

            if (_config.Templates.Count > 0)
            {
                foreach (var template in _config.Templates)
                {
                    var properties = template.Value;
                    Register(
                        "Game.Entity.Template." + template.Key,
                        _ => GameSnapshotBuilder.ReadItem(properties, Ioc.Resolve<SnapshotValueCodecs>("Snapshot.Codecs")));
                }

                Register(
                    "Game.Entity.Template.Resolve",
                    args =>
                    {
                        var entityType = (string)args[0];
                        return _config.Templates.TryGetValue(entityType, out var properties)
                            ? GameSnapshotBuilder.ReadItem(properties, Ioc.Resolve<SnapshotValueCodecs>("Snapshot.Codecs"))
                            : new Dictionary<string, object>();
                    });
            }

            foreach (var collision in _config.Collisions)
            {
                var handlerKey = collision.Value;
                Register("Game.Collision.Handle." + collision.Key, args => Ioc.Resolve<ICommand>(handlerKey, args));
            }

            if (_config.Entities.Count > 0)
            {
                var repository = Ioc.Resolve<IWritableGameObjectRepository>("Game.Repository.Writable");
                var codecs = Ioc.Resolve<SnapshotValueCodecs>("Snapshot.Codecs");
                var maxId = int.MinValue;

                foreach (var entity in _config.Entities)
                {
                    var raw = GameSnapshotBuilder.ReadItem(entity.Properties, codecs);
                    var item = Ioc.Resolve<IDictionary<string, object>>("Game.Item.Create", raw);
                    repository.Set(entity.Id, item);
                    maxId = Math.Max(maxId, entity.Id);
                }

                Ioc.Resolve<ICommand>("Game.State.Reindex", maxId + 1).Execute();
            }
        }

        private static void Register(string dependency, Func<object[], object> strategy)
        {
            Ioc.Resolve<ICommand>("IoC.Register", dependency, strategy).Execute();
        }
    }
}
