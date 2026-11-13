using App;

namespace SpaceBattle.Lib
{
    public interface ICommandInjecting
    {
        void Inject(ICommand command);
    }
}
