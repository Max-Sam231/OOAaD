using System;
using App;

namespace SpaceBattle.Lib
{
    public class InSeparateScopeCommand : ICommand
    {
        private readonly object _targetScope;
        private readonly ICommand _innerCommand;

        public InSeparateScopeCommand(object targetScope, ICommand innerCommand)
        {
            _targetScope = targetScope ?? throw new ArgumentNullException(nameof(targetScope));
            _innerCommand = innerCommand ?? throw new ArgumentNullException(nameof(innerCommand));
        }

        public void Execute()
        {
            var previousScope = Ioc.Resolve<object>("IoC.Scope.Current");
            var restoreScope = Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", previousScope);
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", _targetScope).Execute();

            try
            {
                _innerCommand.Execute();
            }
            finally
            {
                restoreScope.Execute();
            }
        }
    }
}
