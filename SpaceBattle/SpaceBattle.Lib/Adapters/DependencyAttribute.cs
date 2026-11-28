namespace SpaceBattle.Lib
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public class DependencyAttribute : Attribute
    {
        public Type InterfaceType { get; }
        public string PropertyName { get; }
        public Type StrategyType { get; }
        public string StrategyMethodName { get; }

        public DependencyAttribute(Type interfaceType, string propertyName, Type strategyType, string strategyMethodName)
        {
            InterfaceType = interfaceType;
            PropertyName = propertyName;
            StrategyType = strategyType;
            StrategyMethodName = strategyMethodName;
        }
    }
}
