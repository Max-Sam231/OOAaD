using System;
using System.Collections.Generic;
using App;

namespace SpaceBattle.Lib
{
    public class AutoRegisterCommandDependency : ICommand
    {
        private readonly string _dependencyName;
        private readonly Type _commandType;

        public AutoRegisterCommandDependency(string dependencyName, Type commandType)
        {
            _dependencyName = dependencyName;
            _commandType = commandType;
        }

        public void Execute()
        {
            var constructors = _commandType.GetConstructors();
            if (constructors.Length != 1)
                throw new InvalidOperationException("Команда должна иметь ровно один конструктор.");

            var constructor = constructors[0];
            var parameters = constructor.GetParameters();
            if (parameters.Length != 1)
                throw new InvalidOperationException("Конструктор команды должен принимать ровно один параметр-интерфейс.");

            var interfaceType = parameters[0].ParameterType;

            var sourceCode = AdapterCodeGenerator.Generate(interfaceType);
            var adapterType = AdapterCompiler.Compile(sourceCode, interfaceType);

            Func<object[], object> commandStrategy = (args) =>
            {
                var rawObject = (IDictionary<string, object>)args[0];
                var adapterInstance = Activator.CreateInstance(adapterType, rawObject);
                return Activator.CreateInstance(_commandType, adapterInstance)!;
            };

            Ioc.Resolve<ICommand>("IoC.Register", _dependencyName, commandStrategy).Execute();
        }
    }
}
