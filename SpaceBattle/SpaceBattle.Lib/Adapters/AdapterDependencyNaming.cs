namespace SpaceBattle.Lib
{
    public static class AdapterDependencyNaming
    {
        public static string Adapter(Type interfaceType) => "Adapters." + interfaceType.Name;

        public static string Getter(Type interfaceType, string propertyName) => interfaceType.FullName + "." + propertyName;

        public static string Setter(Type interfaceType, string propertyName) => Getter(interfaceType, propertyName) + ".Set";

        public static string AdapterTypeName(Type interfaceType) => interfaceType.Name + "Adapter";
    }
}
