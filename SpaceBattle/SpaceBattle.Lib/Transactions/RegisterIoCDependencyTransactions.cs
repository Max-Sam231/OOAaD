using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyTransactions : ICommand
    {
        public void Execute()
        {
            var transactions = new GameTransactions();

            Register("Game.Transactions", args => transactions);

            Register(
                "Game.Item.Transactional",
                args => new TransactionalGameItem((IDictionary<string, object>)args[0], transactions));

            Register(
                "Commands.InTransaction",
                args => new InTransactionCommand((ICommand)args[0], transactions));
        }

        private static void Register(string dependency, Func<object[], object> strategy)
        {
            Ioc.Resolve<ICommand>("IoC.Register", dependency, strategy).Execute();
        }
    }
}
