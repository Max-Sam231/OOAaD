using App;

namespace SpaceBattle.Lib
{
    public interface ICommandFactory
    {
        ICommand Create(IDictionary<string, object> gameItem);
    }
}
