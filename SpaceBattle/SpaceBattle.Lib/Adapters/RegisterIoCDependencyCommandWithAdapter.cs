using System.Linq.Expressions;
using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyCommandWithAdapter : ICommand
    {
        private readonly string _dependencyName;
        private readonly Type _commandType;

        public RegisterIoCDependencyCommandWithAdapter(string dependencyName, Type commandType)
        {
            _dependencyName = dependencyName ?? throw new ArgumentNullException(nameof(dependencyName));
            _commandType = commandType ?? throw new ArgumentNullException(nameof(commandType));
        }

        public void Execute()
        {
            if (!typeof(ICommand).IsAssignableFrom(_commandType))
            {
                throw new ArgumentException($"{_commandType.FullName} does not implement ICommand.");
            }

            var constructors = _commandType.GetConstructors();

            if (constructors.Length != 1)
            {
                throw new ArgumentException($"{_commandType.FullName} must have exactly one public constructor.");
            }

            var parameters = constructors[0].GetParameters();

            if (parameters.Length != 1 || !parameters[0].ParameterType.IsInterface)
            {
                throw new ArgumentException($"The constructor of {_commandType.FullName} must take exactly one interface parameter.");
            }

            var interfaceType = parameters[0].ParameterType;
            var adapter = Expression.Parameter(typeof(object));
            var create = Expression.Lambda<Func<object, object>>(
                Expression.New(constructors[0], Expression.Convert(adapter, interfaceType)),
                adapter).Compile();
            var adapterDependency = AdapterDependencyNaming.Adapter(interfaceType);

            Ioc.Resolve<ICommand>(
                "IoC.Register",
                _dependencyName,
                (Func<object[], object>)(args => create(Ioc.Resolve<object>(adapterDependency, args)))
            ).Execute();
        }
    }
}
