using App;

namespace SpaceBattle.Lib
{
    public sealed class RegisterIoCDependencyEndpointGameBase : ICommand
    {
        private readonly IGameConnectionRegistry _connections;
        private readonly Action<ICommand, Exception> _onException;

        public RegisterIoCDependencyEndpointGameBase(
            IGameConnectionRegistry connections,
            Action<ICommand, Exception> onException)
        {
            ArgumentNullException.ThrowIfNull(connections);
            ArgumentNullException.ThrowIfNull(onException);

            _connections = connections;
            _onException = onException;
        }

        public void Execute()
        {
            Register(
                "Exception.Handle",
                args => new DelegatingCommand(() => _onException((ICommand)args[0], (Exception)args[1])));
            Register("Server.Connections", _ => _connections);
            Register("Commands.InterpretMessage", args => new InterpretMessageCommand((CommandDescription)args[0]));
            Register(
                "Game.Item.Owner",
                args => ((IDictionary<string, object>)args[0]).TryGetValue("OwnerId", out var owner)
                    && owner is string ownerId
                    ? ownerId
                    : string.Empty);
            Register(
                "Game.Message.Authorize",
                args =>
                {
                    var command = (ICommand)args[0];
                    var message = (CommandDescription)args[1];
                    if (string.IsNullOrEmpty(message.PlayerId))
                    {
                        return command;
                    }

                    var owner = Ioc.Resolve<string>("Game.Item.Owner", args[2]);
                    return Ioc.Resolve<ICommand>("Commands.Authorize", command, message.PlayerId, owner);
                });
            Register(
                "Game.Message.Command",
                args =>
                {
                    var message = (CommandDescription)args[0];
                    var repo = Ioc.Resolve<IReadOnlyGameObjectRepository>("Game.Repository.ReadOnly");
                    var item = repo.Get(message.GameItemId);
                    var codecs = Ioc.Resolve<SnapshotValueCodecs>("Snapshot.Codecs");
                    var decoded = CommandMessageFactory.DecodeArguments(message, codecs);
                    var command = decoded is null
                        ? Ioc.Resolve<ICommand>(message.Type, item)
                        : Ioc.Resolve<ICommand>(message.Type, item, decoded);
                    var authorized = Ioc.Resolve<ICommand>("Game.Message.Authorize", command, message, item);
                    return Ioc.Resolve<ICommand>("Commands.InTransaction", authorized);
                });
            Register(
                "Game.State.Serialize",
                args =>
                {
                    var gameId = (string)args[0];
                    var step = (long)args[1];
                    var repo = Ioc.Resolve<IReadOnlyGameObjectRepository>("Game.Repository.ReadOnly");
                    var codecs = Ioc.Resolve<SnapshotValueCodecs>("Snapshot.Codecs");
                    return CommandMessageFactory.EncodeTickState(
                        gameId,
                        step,
                        "running",
                        null,
                        repo,
                        (IGameObjectIdSource)repo,
                        codecs);
                });
            Register("Game.State.Broadcast", args => new BroadcastGameStateCommand((string)args[0]));
        }

        private static void Register(string dependency, Func<object[], object> strategy)
        {
            Ioc.Resolve<ICommand>("IoC.Register", dependency, strategy).Execute();
        }
    }
}
