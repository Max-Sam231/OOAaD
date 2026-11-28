using System.Text.Json;

namespace SpaceBattle.Lib
{
    public static class GameSnapshotSerializer
    {
        public static string Serialize(GameSnapshot snapshot)
        {
            return JsonSerializer.Serialize(snapshot);
        }

        public static GameSnapshot Deserialize(string json)
        {
            try
            {
                var snapshot = JsonSerializer.Deserialize<GameSnapshot>(json);

                if (snapshot == null
                    || snapshot.Rules == null
                    || snapshot.Rules.Length == 0
                    || snapshot.CommandLimit <= 0
                    || snapshot.Items == null
                    || snapshot.Commands == null
                    || snapshot.Items.Values.Any(item => item == null || item.Values.Any(value => value == null))
                    || snapshot.Commands.Any(command => command == null))
                {
                    throw new InvalidOperationException("The game snapshot is incomplete.");
                }

                return snapshot;
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException("The game snapshot is not valid JSON.", exception);
            }
        }
    }
}
