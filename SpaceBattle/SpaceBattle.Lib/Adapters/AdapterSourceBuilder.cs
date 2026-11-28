using App;

namespace SpaceBattle.Lib
{
    public class AdapterSourceBuilder
    {
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

            var classTemplate = Ioc.Resolve<string>("Adapters.Template.Class");
            var propertyTemplate = Ioc.Resolve<string>("Adapters.Template.Property");
            var getterTemplate = Ioc.Resolve<string>("Adapters.Template.Getter");
            var setterTemplate = Ioc.Resolve<string>("Adapters.Template.Setter");

            var renderedProperties = properties.Select(property =>
            {
                var propertyType = TypeName(property.PropertyType);
                var accessorTexts = new List<string>();

                if (property.CanRead)
                {
                    accessorTexts.Add(Render(
                        getterTemplate,
                        ("PropertyType", propertyType),
                        ("GetterKey", AdapterDependencyNaming.Getter(interfaceType, property.Name))));
                }

                if (property.CanWrite)
                {
                    accessorTexts.Add(Render(
                        setterTemplate,
                        ("SetterKey", AdapterDependencyNaming.Setter(interfaceType, property.Name))));
                }

                return Render(
                    propertyTemplate,
                    ("PropertyType", propertyType),
                    ("PropertyName", property.Name),
                    ("Accessors", string.Join(Environment.NewLine, accessorTexts)));
            });

            return Render(
                classTemplate,
                ("AdapterName", AdapterDependencyNaming.AdapterTypeName(interfaceType)),
                ("InterfaceType", TypeName(interfaceType)),
                ("Properties", string.Join(Environment.NewLine, renderedProperties)));
        }

        private static string Render(string template, params (string Name, string Value)[] values)
        {
            return values.Aggregate(template, (text, value) => text.Replace("{{" + value.Name + "}}", value.Value));
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
