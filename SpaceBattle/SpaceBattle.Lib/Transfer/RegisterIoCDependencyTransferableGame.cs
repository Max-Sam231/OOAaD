using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyTransferableGame : ICommand
    {
        public void Execute()
        {
            Register("Game.Create.Transferable", args => Create((ServerThread)args[0], (int)args[1], (string)args[2], new GameSnapshot(), false));

            Register("Game.Restore", args =>
            {
                var snapshot = GameSnapshotSerializer.Deserialize((string)args[0]);

                return Create((ServerThread)args[1], snapshot.CommandLimit, snapshot.Rules, snapshot, true);
            });

            Register("Game.Command.Send", args =>
            {
                var game = (TransferableGame)args[0];
                var description = new CommandDescription
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Type = (string)args[1],
                    GameItemId = (int)args[2],
                };

                return new DelegatingCommand(() => game.Sending.Send(description));
            });

            Register("Game.Transfer", args =>
            {
                var game = (TransferableGame)args[0];
                var target = (ServerThread)args[1];

                return new DelegatingCommand(() => game.Thread.Receive(new TransferGameCommand(game, target)));
            });
        }

        private static TransferableGame Create(ServerThread thread, int commandLimit, string rules, GameSnapshot snapshot, bool restoring)
        {
            var codecs = Ioc.Resolve<SnapshotValueCodecs>("Snapshot.Codecs");
            var quantum = new GameQuantumState(commandLimit);
            var repository = new EnumerableGameObjectRepository();
            var journal = new PendingCommandJournal();
            var gameScope = Ioc.Resolve<object>("IoC.Scope.Create");

            new InSeparateScopeCommand(gameScope, new DelegatingCommand(() =>
            {
                new RegisterIoCDependencyGameQuantum(quantum).Execute();
                Register("Game.Repository.ReadOnly", args => repository);
                Register("Game.Repository.Writable", args => repository);
                Ioc.Resolve<ICommand>("Game.Rules." + rules).Execute();
            })).Execute();

            if (restoring)
            {
                repository.Ids.Except(snapshot.Items.Keys).ToList().ForEach(repository.Remove);
            }

            foreach (var item in snapshot.Items)
            {
                var restored = GameSnapshotBuilder.ReadItem(item.Value, codecs);

                if (repository.Ids.Contains(item.Key))
                {
                    var existing = repository.Get(item.Key);

                    restored.ToList().ForEach(property => existing[property.Key] = property.Value);
                }
                else
                {
                    repository.Set(item.Key, restored);
                }
            }

            var game = new Game();
            var turn = new GameQuantumCommand(new InSeparateScopeCommand(gameScope, game), quantum, thread.Scheduler);
            var receiver = new SchedulingCommandReceiver(game, thread.Scheduler, turn);
            var local = new LocalGameCommandSending(
                receiver,
                journal,
                description => new JournaledCommand(
                    new DelegatingCommand(() =>
                    {
                        var item = Ioc.Resolve<IReadOnlyGameObjectRepository>("Game.Repository.ReadOnly").Get(description.GameItemId);

                        Ioc.Resolve<ICommand>(description.Type, item).Execute();
                    }),
                    journal,
                    description.Id));
            var location = new RedirectingGameCommandSending(local);
            var sending = new DelegatingGameCommandSending(
                description => thread.Receive(new DelegatingCommand(() => location.Send(description))));

            snapshot.Commands.ForEach(sending.Send);

            return new TransferableGame(thread, sending, location, journal, repository, rules, commandLimit);
        }

        private static void Register(string dependency, Func<object[], object> strategy)
        {
            Ioc.Resolve<ICommand>("IoC.Register", dependency, strategy).Execute();
        }
    }
}
