using App;

namespace SpaceBattle.Lib
{
    public class GameItemPropertySetting : ICommand
    {
        private readonly IDictionary<string, object> _gameItem;
        private readonly string _key;
        private readonly object _value;

        public GameItemPropertySetting(IDictionary<string, object> gameItem, string key, object value)
        {
            _gameItem = gameItem ?? throw new ArgumentNullException(nameof(gameItem));
            _key = key ?? throw new ArgumentNullException(nameof(key));
            _value = value;
        }

        public void Execute()
        {
            _gameItem[_key] = _value;
        }
    }
}
