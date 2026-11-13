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

                    return JsonSerializer.SerializeToElement(
                        Enumerable.Range(0, vector.Dimension).Select(index => vector[index]).ToArray());
                },
                element => new Vector(element.Deserialize<int[]>()!));

            codecs.Add(
                typeof(Angle),
                value => JsonSerializer.SerializeToElement(new[] { ((Angle)value).Numerator, Angle.Denominator }),
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
