using System.Reflection;
using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyAdapter : ICommand
    {
        private readonly Type _interfaceType;

        public RegisterIoCDependencyAdapter(Type interfaceType)
        {
            _interfaceType = interfaceType ?? throw new ArgumentNullException(nameof(interfaceType));
        }

        public void Execute()
        {
            var factory = Ioc.Resolve<IAdapterFactory>("Adapters.Factory", _interfaceType);

            Register(
                AdapterDependencyNaming.Adapter(_interfaceType),
                args => factory.Create((IDictionary<string, object>)args[0]));

            foreach (var property in _interfaceType.GetProperties())
            {
                var name = property.Name;

                if (property.CanRead)
                {
                    Register(AdapterDependencyNaming.Getter(_interfaceType, name), GetterStrategy(name));
                }

                if (property.CanWrite)
                {
                    Register(
                        AdapterDependencyNaming.Setter(_interfaceType, name),
                        args => new GameItemPropertySetting((IDictionary<string, object>)args[0], name, args[1]));
                }
            }
        }

        private Func<object[], object> GetterStrategy(string propertyName)
        {
            var declared = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetCustomAttributes<DependencyAttribute>())
                .Where(dependency => dependency.InterfaceType == _interfaceType && dependency.PropertyName == propertyName)
                .ToList();

            if (declared.Count > 1)
            {
                throw new InvalidOperationException($"More than one [Dependency] is declared for {_interfaceType.FullName}.{propertyName}.");
            }

            if (declared.Count == 1)
            {
                var method = declared[0].StrategyType.GetMethod(declared[0].StrategyMethodName)
                    ?? throw new InvalidOperationException($"Strategy method {declared[0].StrategyType.FullName}.{declared[0].StrategyMethodName} is not found.");

                return method.CreateDelegate<Func<object[], object>>();
            }

            return args => ((IDictionary<string, object>)args[0])[propertyName];
        }

        private static void Register(string dependency, Func<object[], object> strategy)
        {
            Ioc.Resolve<ICommand>("IoC.Register", dependency, strategy).Execute();
        }
    }
}
