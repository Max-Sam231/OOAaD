using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyMoveTorpedoCommand : ICommand
    {
        public void Execute()
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.Torpedo.Move",
                (Func<object[], object>)(args =>
                {
                    var torpedoId = (int)args[0];
                    var repository = Ioc.Resolve<IReadOnlyGameObjectRepository>("Game.Repository.ReadOnly");
                    return new MoveTorpedoCommand(torpedoId, repository);
                })
            ).Execute();
        }
    }
}
