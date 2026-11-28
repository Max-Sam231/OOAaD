using System.Text.Json;
using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencySnapshotCodecs : ICommand
    {
        public void Execute()
        {
            var codecs = new SnapshotValueCodecs();

            codecs.Add(
                typeof(int),
                value => JsonSerializer.SerializeToElement((int)value),
                element => element.GetInt32());

            codecs.Add(
                typeof(string),
                value => JsonSerializer.SerializeToElement((string)value),
                element => element.GetString()!);

            codecs.Add(
                typeof(Vector),
                value =>
                {
                    var vector = (Vector)value;
                    var coordinates = new int[vector.Dimension];
                    for (var index = 0; index < coordinates.Length; index++)
                    {
                        coordinates[index] = vector[index];
                    }

                    return JsonSerializer.SerializeToElement(coordinates);
                },
                element => new Vector(element.Deserialize<int[]>()!));

            codecs.Add(
                typeof(Angle),
                value =>
                {
                    var angle = (Angle)value;

                    return JsonSerializer.SerializeToElement(new[] { angle.Numerator, angle.Denominator });
                },
                element =>
                {
                    var parts = element.Deserialize<int[]>()!;

                    return new Angle(parts[0], parts[1]);
                });

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                "Snapshot.Codecs",
                (Func<object[], object>)(args => codecs)
            ).Execute();
        }
    }
}
