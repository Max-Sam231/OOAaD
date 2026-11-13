using System.Collections.Concurrent;
using App;
using App.Scopes;
using SpaceBattle.Lib;
using Xunit;

namespace SpaceBattle.Test
{
    public class GameTransferTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly ConcurrentQueue<(string Entry, int ThreadId)> _log = new ConcurrentQueue<(string, int)>();
        private readonly SemaphoreSlim _logged = new SemaphoreSlim(0);
        private readonly ServerThread _first;
        private readonly ServerThread _second;

        public GameTransferTests()
        {
            new InitCommand().Execute();

            var scope = Ioc.Resolve<object>("IoC.Scope.Create");

            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", scope).Execute();

            Register("Exception.Handle", args => new TestActionCommand(() => Record("exception: " + args[1])));
            Register("Game.Rules.Standard", args => new TestActionCommand(RegisterStandardRules));

            new RegisterIoCDependencyServerThread().Execute();
            new RegisterIoCDependencySnapshotCodecs().Execute();
            new RegisterIoCDependencyTransferableGame().Execute();

            _first = Ioc.Resolve<ServerThread>("Server.Thread.Create");
            _second = Ioc.Resolve<ServerThread>("Server.Thread.Create");
        }

        private static void Register(string dependency, Func<object[], object> strategy)
        {
            Ioc.Resolve<ICommand>("IoC.Register", dependency, strategy).Execute();
        }

        private void Record(string entry)
        {
            _log.Enqueue((entry, Environment.CurrentManagedThreadId));
            _logged.Release();
        }

        private void RegisterStandardRules()
        {
            Register("Rule", args => "standard");

            Register("Commands.Test.Burn", args => new TestActionCommand(() =>
            {
                var item = (IDictionary<string, object>)args[0];
                item["Fuel"] = (int)item["Fuel"] - 1;
                Record("burn");
            }));

            Register("Commands.Test.Spawn", args => new TestActionCommand(() =>
            {
                var repository = Ioc.Resolve<IWritableGameObjectRepository>("Game.Repository.Writable");
                repository.Set(2, new Dictionary<string, object> { ["Name"] = "torpedo", ["Fuel"] = 0 });
                repository.Remove(3);
            }));

            Register("Commands.Test.Log", args => new TestActionCommand(() =>
            {
                var item = (IDictionary<string, object>)args[0];
                var ids = Ioc.Resolve<IGameObjectIdSource>("Game.Repository.ReadOnly").Ids.Order();
                Record($"{item["Name"]} fuel={item["Fuel"]} rule={Ioc.Resolve<string>("Rule")} engine={item.ContainsKey("Engine")} items={string.Join(",", ids)}");
            }));

            var seeded = Ioc.Resolve<IWritableGameObjectRepository>("Game.Repository.Writable");
            seeded.Set(3, new Dictionary<string, object> { ["Name"] = "decoy", ["Fuel"] = 0 });
            seeded.Set(1, new Dictionary<string, object>
            {
                ["Name"] = "ship",
                ["Fuel"] = 10,
                ["Position"] = new Vector(1, 2),
                ["Engine"] = new TestActionCommand(() => { }),
            });
        }

        private static ICommand QueueOnServerThread(TransferableGame game, params (string Type, int ItemId)[] commands)
        {
            return new TestActionCommand(() => commands.ToList().ForEach(command => game.Location.Send(new CommandDescription
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = command.Type,
                GameItemId = command.ItemId,
            })));
        }

        private (string Entry, int ThreadId)[] WaitForEntries(int count)
        {
            Enumerable.Range(0, count).ToList().ForEach(_ => Assert.True(_logged.Wait(Timeout)));

            return _log.ToArray();
        }

        private void StopBoth()
        {
            Ioc.Resolve<ICommand>("Server.Thread.Stop.Soft", _first).Execute();
            Ioc.Resolve<ICommand>("Server.Thread.Stop.Soft", _second).Execute();

            Assert.True(_first.Join(Timeout));
            Assert.True(_second.Join(Timeout));
        }

        [Fact]
        public void Transfer_MovesItemsRulesAndPendingCommandsToTheTargetThread_AndForwardsLateCommands()
        {
            var game = Ioc.Resolve<TransferableGame>("Game.Create.Transferable", _first, 2, "Standard");
            _first.Receive(QueueOnServerThread(
                game,
                ("Commands.Test.Burn", 1),
                ("Commands.Test.Spawn", 1),
                ("Commands.Test.Log", 1),
                ("Commands.Test.Log", 2)));
            Ioc.Resolve<ICommand>("Game.Transfer", game, _second).Execute();
            Ioc.Resolve<ICommand>("Game.Command.Send", game, "Commands.Test.Log", 1).Execute();
            _first.Start();
            _second.Start();

            WaitForEntries(4);
            Ioc.Resolve<ICommand>("Game.Command.Send", game, "Commands.Test.Log", 2).Execute();
            WaitForEntries(1);
            StopBoth();
            var log = _log.ToArray();

            Assert.Equal(
                new[]
                {
                    "burn",
                    "ship fuel=9 rule=standard engine=True items=1,2",
                    "torpedo fuel=0 rule=standard engine=False items=1,2",
                    "ship fuel=9 rule=standard engine=True items=1,2",
                    "torpedo fuel=0 rule=standard engine=False items=1,2",
                },
                log.Select(entry => entry.Entry));
            Assert.NotEqual(log[0].ThreadId, log[1].ThreadId);
            Assert.Single(log.Skip(1).Select(entry => entry.ThreadId).Distinct());
        }

        [Fact]
        public void Transfer_ThroughTheOldHandle_MovesTheGameFromItsCurrentLocation()
        {
            var game = Ioc.Resolve<TransferableGame>("Game.Create.Transferable", _first, 2, "Standard");
            _first.Receive(QueueOnServerThread(game, ("Commands.Test.Burn", 1)));
            Ioc.Resolve<ICommand>("Game.Transfer", game, _second).Execute();
            Ioc.Resolve<ICommand>("Game.Command.Send", game, "Commands.Test.Burn", 1).Execute();
            Ioc.Resolve<ICommand>("Game.Transfer", game, _first).Execute();
            Ioc.Resolve<ICommand>("Game.Command.Send", game, "Commands.Test.Log", 1).Execute();
            _first.Start();
            _second.Start();

            WaitForEntries(3);
            StopBoth();
            var log = _log.ToArray();

            Assert.Equal(new[] { "burn", "burn", "ship fuel=8 rule=standard engine=True items=1,3" }, log.Select(entry => entry.Entry));
            Assert.Equal(log[0].ThreadId, log[2].ThreadId);
        }

        [Fact]
        public void Restore_FromInvalidSnapshot_ThrowsInvalidOperationException()
        {
            Assert.Throws<InvalidOperationException>(() => Ioc.Resolve<TransferableGame>("Game.Restore", "{", _first));
        }
    }
}
