using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencySendCommand : ICommand
    {
        public void Execute()
        {
            var cmd = Ioc.Resolve<App.ICommand>("IoC.Register", "Commands.Send", (object[] args) =>
            {
                var send = Ioc.Resolve<ICommand>("Adapters.ICommand", args);
                var receiver = Ioc.Resolve<ICommandReceiver>("Adapters.ICommandReceiver", args);

                return new SendCommand(send, receiver);
            });

            cmd.Execute();
        }
    }
}
