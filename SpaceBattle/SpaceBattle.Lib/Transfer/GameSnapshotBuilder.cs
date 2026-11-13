using System.Text.Json;

namespace SpaceBattle.Lib
{
    public static class GameSnapshotBuilder
    {
        public static GameSnapshot Build(TransferableGame game, SnapshotValueCodecs codecs)
        {
            return new GameSnapshot
            {
                Rules = game.Rules,
                CommandLimit = game.CommandLimit,
                Items = game.Repository.Ids.ToDictionary(id => id, id => WriteItem(game.Repository.Get(id), codecs)),
                Commands = game.Journal.Pending.ToList(),
            };
        }

        public static IDictionary<string, object> ReadItem(
            IReadOnlyDictionary<string, Dictionary<string, JsonElement>> written,
            SnapshotValueCodecs codecs)
        {
            return written.ToDictionary(property => property.Key, property => codecs.Read(property.Value));
        }

        private static Dictionary<string, Dictionary<string, JsonElement>> WriteItem(
            IDictionary<string, object> item,
            SnapshotValueCodecs codecs)
        {
            var written = new Dictionary<string, Dictionary<string, JsonElement>>();

            foreach (var property in item)
            {
                if (codecs.TryWrite(property.Value, out var value))
                {
                    written[property.Key] = value;
                }
            }

            return written;
        }
    }
}
