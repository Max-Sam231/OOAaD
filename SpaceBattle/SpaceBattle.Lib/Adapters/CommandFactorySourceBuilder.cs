using App;

namespace SpaceBattle.Lib
{
    public class CommandFactorySourceBuilder
    {
        public string Build(Type commandType)
        {
            if (!typeof(ICommand).IsAssignableFrom(commandType))
            {
                throw new ArgumentException($"{commandType.FullName} does not implement ICommand.", nameof(commandType));
            }

            var constructors = commandType.GetConstructors();

            if (constructors.Length != 1)
            {
                throw new ArgumentException($"{commandType.FullName} must have exactly one public constructor.", nameof(commandType));
            }

            var parameters = constructors[0].GetParameters();

            if (parameters.Length != 1 || !parameters[0].ParameterType.IsInterface)
            {
                throw new ArgumentException(
                    $"The constructor of {commandType.FullName} must take exactly one interface parameter.",
                    nameof(commandType));
            }

            var interfaceType = parameters[0].ParameterType;

            return Ioc.Resolve<string>("Adapters.Template.CommandFactory")
                .Replace("{{FactoryName}}", commandType.Name + "Factory")
                .Replace("{{CommandType}}", "global::" + commandType.FullName!.Replace('+', '.'))
                .Replace("{{InterfaceType}}", "global::" + interfaceType.FullName!.Replace('+', '.'))
                .Replace("{{AdapterKey}}", AdapterDependencyNaming.Adapter(interfaceType));
        }
    }
}
