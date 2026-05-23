using App;
using SpaceBattle.Lib;
using System;
using System.Collections.Generic;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyActionsStart : ICommand
    {
        public void Execute()
        {
            Ioc.Resolve<ICommand>("IoC.Register", "Actions.Start", new Func<object[], object>((args) =>
            {
                var order = (IDictionary<string, object>)args[0];

                var receiver = (ICommandReceiver)order["Receiver"];
                var targetCommand = (ICommand)order["Target"];

                var injectable = Ioc.Resolve<ICommandInjectable>("Commands.CommandInjectable");

                injectable.Inject(targetCommand);
                return Ioc.Resolve<ICommand>("Commands.Send", injectable, receiver);
            })).Execute();
        }
    }
}
