using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyGameCreate : ICommand
    {
        public void Execute()
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Game.Create",
                (Func<object[], object>)(args =>
                {
                    var thread = (ServerThread)args[0];
                    var state = new GameQuantumState((int)args[1]);
                    var gameScope = Ioc.Resolve<object>("IoC.Scope.Create");

                    new InSeparateScopeCommand(gameScope, new RegisterIoCDependencyGameQuantum(state)).Execute();

                    var game = new Game();
                    var turn = new GameQuantumCommand(new InSeparateScopeCommand(gameScope, game), state, thread.Scheduler);

                    var receiver = new SchedulingCommandReceiver(game, thread.Scheduler, turn);

                    return new DelegatingCommandReceiver(command => thread.Receive(new SendCommand(command, receiver)));
                })
            ).Execute();
        }
    }
}
