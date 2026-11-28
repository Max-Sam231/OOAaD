using System.Collections.Concurrent;
using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyServerThread : ICommand
    {
        public void Execute()
        {
            Register("Server.Thread.Create", args =>
            {
                var inbox = new BlockingCollection<ICommand>();
                var scheduler = new RoundRobinGameScheduler();
                var state = new ServerThreadState(inbox, scheduler);
                var loop = new ServerThreadLoopCommand(inbox, scheduler, state);
                var body = new InSeparateScopeCommand(Ioc.Resolve<object>("IoC.Scope.Current"), loop);

                return new ServerThread(new Thread(body.Execute) { IsBackground = true }, inbox, scheduler, state);
            });

            Register("Server.Thread.Stop.Hard", args =>
            {
                var thread = (ServerThread)args[0];

                return new SendCommand(new HardStopServerThreadCommand(thread.State), thread);
            });

            Register("Server.Thread.Stop.Soft", args =>
            {
                var thread = (ServerThread)args[0];

                return new SendCommand(new SoftStopServerThreadCommand(thread.State), thread);
            });
        }

        private static void Register(string dependency, Func<object[], object> strategy)
        {
            Ioc.Resolve<ICommand>("IoC.Register", dependency, strategy).Execute();
        }
    }
}
