using App;
using System;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyAuthorizeCommand : ICommand
    {
        public void Execute()
        {
            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Commands.Authorize",
                (Func<object[], object>)(args =>
                {
                    var command = (ICommand)args[0];
                    var subject = (string)args[1];
                    var objectOwner = (string)args[2];
                    return new AuthorizeCommand(command, subject, objectOwner);
                })
            ).Execute();
        }
    }
}
