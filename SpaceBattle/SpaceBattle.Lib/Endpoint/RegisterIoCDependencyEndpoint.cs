using App;

namespace SpaceBattle.Lib
{
    public sealed class RegisterIoCDependencyEndpoint : ICommand
    {
        public void Execute()
        {
            var router = new GameRouter();
            var decoder = new CommandMessageFactory();
            var paused = 0;

            Register("Server.Router", _ => router);
            Register(
                "Server.Ingress.Paused",
                args => args.Length == 0
                    ? (Func<bool>)(() => Volatile.Read(ref paused) != 0)
                    : new DelegatingCommand(() => Volatile.Write(ref paused, (bool)args[0] ? 1 : 0)));
            Register(
                "Server.Thread.TryEnqueue",
                args =>
                {
                    var receiver = (ICommandReceiver)args[0];
                    return (Func<ICommand, bool>)(cmd =>
                    {
                        receiver.Receive(cmd);
                        return true;
                    });
                });
            Register("Endpoint.Message.Decoder", _ => decoder);
            Register(
                "Endpoint.Route.Create",
                args =>
                {
                    var tryEnqueue = (Func<ICommand, bool>)args[0];
                    var registry = (IGameReceiverRegistry)args[1];
                    return new ThreadGameEndpointRoute(
                        tryEnqueue,
                        msg => new DeliverMessageCommand(registry, msg));
                });
            Register(
                "Endpoint.Target.Local.Create",
                args =>
                {
                    var receiver = (ICommandReceiver)args[0];
                    var factory = args.Length > 1
                        ? (Func<CommandDescription, ICommand>)args[1]
                        : (Func<CommandDescription, ICommand>)(desc => new DelegatingCommand(
                            () => Ioc.Resolve<ICommand>("Commands.InterpretMessage", desc).Execute()));
                    return new LocalEndpointGameSending(receiver, factory);
                });
        }

        private static void Register(string dependency, Func<object[], object> strategy)
        {
            Ioc.Resolve<ICommand>("IoC.Register", dependency, strategy).Execute();
        }
    }
}
