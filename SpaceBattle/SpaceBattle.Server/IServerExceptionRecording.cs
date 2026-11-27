using App;

namespace SpaceBattle.Server
{
    public interface IServerExceptionRecording
    {
        void Record(ICommand command, Exception exception);
        IReadOnlyCollection<Exception> Snapshot();
    }
}
