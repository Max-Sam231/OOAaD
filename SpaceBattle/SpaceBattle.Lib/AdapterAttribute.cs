using System;

namespace SpaceBattle.Lib
{
    [AttributeUsage(AttributeTargets.Interface | AttributeTargets.Assembly, AllowMultiple = true)]
    public class AdapterAttribute : Attribute
    {
        public Type InterfaceType { get; }
        public string PropertyName { get; }
        public string StrategyName { get; }

        public AdapterAttribute(Type interfaceType, string propertyName, string strategyName)
        {
            InterfaceType = interfaceType;
            PropertyName = propertyName;
            StrategyName = strategyName;
        }
    }
}
