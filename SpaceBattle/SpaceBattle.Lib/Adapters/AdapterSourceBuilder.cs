using System.Text;

namespace SpaceBattle.Lib
{
    public class AdapterSourceBuilder
    {
        private const string GameItemType = "global::System.Collections.Generic.IDictionary<string, object>";

        public string Build(Type interfaceType)
        {
            if (!interfaceType.IsInterface)
            {
                throw new ArgumentException($"{interfaceType.FullName} is not an interface.", nameof(interfaceType));
            }

            if (interfaceType.GetInterfaces().Length > 0)
            {
                throw new ArgumentException($"{interfaceType.FullName} inherits other interfaces.", nameof(interfaceType));
            }

            var properties = interfaceType.GetProperties();
            var accessors = properties.SelectMany(property => property.GetAccessors());

            if (interfaceType.GetMethods().Except(accessors).Any())
            {
                throw new ArgumentException($"{interfaceType.FullName} declares members other than properties.", nameof(interfaceType));
            }

            var adapterName = AdapterDependencyNaming.AdapterTypeName(interfaceType);
            var source = new StringBuilder();

            source.AppendLine("namespace SpaceBattle.Generated");
            source.AppendLine("{");
            source.AppendLine($"    public class {adapterName} : {TypeName(interfaceType)}");
            source.AppendLine("    {");
            source.AppendLine($"        private readonly {GameItemType} _gameItem;");
            source.AppendLine($"        public {adapterName}({GameItemType} gameItem) {{ _gameItem = gameItem; }}");

            foreach (var property in properties)
            {
                var propertyType = TypeName(property.PropertyType);

                source.AppendLine($"        public {propertyType} {property.Name}");
                source.AppendLine("        {");

                if (property.CanRead)
                {
                    var getter = AdapterDependencyNaming.Getter(interfaceType, property.Name);
                    source.AppendLine($"            get {{ return global::App.Ioc.Resolve<{propertyType}>(\"{getter}\", _gameItem); }}");
                }

                if (property.CanWrite)
                {
                    var setter = AdapterDependencyNaming.Setter(interfaceType, property.Name);
                    source.AppendLine($"            set {{ global::App.Ioc.Resolve<global::App.ICommand>(\"{setter}\", _gameItem, value).Execute(); }}");
                }

                source.AppendLine("        }");
            }

            source.AppendLine("    }");
            source.AppendLine($"    public class {adapterName}Factory : global::SpaceBattle.Lib.IAdapterFactory");
            source.AppendLine("    {");
            source.AppendLine($"        public object Create({GameItemType} gameItem) {{ return new {adapterName}(gameItem); }}");
            source.AppendLine("    }");
            source.AppendLine("}");

            return source.ToString();
        }

        private static string TypeName(Type type)
        {
            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition().FullName!.Split('`')[0];
                var arguments = string.Join(", ", type.GetGenericArguments().Select(TypeName));

                return $"global::{definition.Replace('+', '.')}<{arguments}>";
            }

            return $"global::{type.FullName!.Replace('+', '.')}";
        }
    }
}
