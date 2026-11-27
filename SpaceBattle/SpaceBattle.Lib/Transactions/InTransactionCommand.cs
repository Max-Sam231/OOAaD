using App;

namespace SpaceBattle.Lib
{
    public class InTransactionCommand : ICommand
    {
        private readonly ICommand _command;
        private readonly GameTransactions _transactions;

        public InTransactionCommand(ICommand command, GameTransactions transactions)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _transactions = transactions ?? throw new ArgumentNullException(nameof(transactions));
        }

        public void Execute()
        {
            _transactions.Begin();

            try
            {
                _command.Execute();
            }
            catch
            {
                _transactions.Rollback();

                throw;
            }

            _transactions.Commit();
        }
    }
}
