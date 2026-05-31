using App;

namespace SpaceBattle.Lib
{
    public class AuthorizeCommand : ICommand
    {
        private readonly ICommand _command;
        private readonly string _subject;
        private readonly string _objectOwner;

        public AuthorizeCommand(ICommand command, string subject, string objectOwner)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
            _subject = subject ?? throw new ArgumentNullException(nameof(subject));
            _objectOwner = objectOwner ?? throw new ArgumentNullException(nameof(objectOwner));
        }

        public void Execute()
        {
            if (_subject != _objectOwner)
            {
                throw new InvalidOperationException("Authorization failed: subject is not the owner of the object.");
            }

            _command.Execute();
        }
    }
}
