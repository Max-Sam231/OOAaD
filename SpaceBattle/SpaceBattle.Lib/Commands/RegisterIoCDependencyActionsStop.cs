using App;
using SpaceBattle.Lib;
using System;
using System.Collections.Generic;

namespace SpaceBattle.Lib
{
    public class StopLongRunningOperationCommand : ICommand
    {
        private readonly ICommandInjecting _injectable;
        private readonly ICommand _emptyCommand;

        public StopLongRunningOperationCommand(ICommandInjecting injectable, ICommand emptyCommand)
        {
            _injectable = injectable;
            _emptyCommand = emptyCommand;
        }

        public void Execute()
        {
            _injectable.Inject(_emptyCommand);
        }
    }

    public class RegisterIoCDependencyActionsStop : ICommand
    {
        public void Execute()
        {
            Ioc.Resolve<ICommand>("IoC.Register", "Actions.Stop", new Func<object[], object>((args) =>
            {
                var order = (IDictionary<string, object>)args[0];

                var injectable = (ICommandInjecting)order["Injectable"];

                var emptyCommand = Ioc.Resolve<ICommand>("Commands.Empty");

                return new StopLongRunningOperationCommand(injectable, emptyCommand);
            })).Execute();
        }
    }
}
