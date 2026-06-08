using System;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SpaceBattle.Lib
{
    public static class AdapterCodeGenerator
    {
        public static string Generate(Type interfaceType)
        {
            if (!interfaceType.IsInterface)
                throw new ArgumentException($"{interfaceType.FullName}");

            var attributes = interfaceType.GetCustomAttributes<AdapterAttribute>()
                .Concat(interfaceType.Assembly.GetCustomAttributes<AdapterAttribute>())
                .Where(a => a.InterfaceType == interfaceType)
                .ToList();

            var className = $"{interfaceType.Name}Adapter";
            var sb = new StringBuilder();

            sb.AppendLine("using System;");
            sb.AppendLine("using App;");
            sb.AppendLine($"using {interfaceType.Namespace};");
            sb.AppendLine();
            sb.AppendLine("namespace SpaceBattle.Lib.Generated");
            sb.AppendLine("{");
            sb.AppendLine($"    public class {className} : {interfaceType.Name}");
            sb.AppendLine("    {");
            sb.AppendLine("        private readonly System.Collections.Generic.IDictionary<string, object> _obj;");
            sb.AppendLine();
            sb.AppendLine($"        public {className}(System.Collections.Generic.IDictionary<string, object> obj)");
            sb.AppendLine("        {");
            sb.AppendLine("            _obj = obj;");
            sb.AppendLine("        }");
            sb.AppendLine();

            foreach (var prop in interfaceType.GetProperties())
            {
                var propTypeName = GetFriendlyTypeName(prop.PropertyType);
                sb.AppendLine($"        public {propTypeName} {prop.Name}");
                sb.AppendLine("        {");

                var customAttr = attributes.FirstOrDefault(a => a.PropertyName == prop.Name);

                if (prop.CanRead)
                {
                    var strategy = customAttr != null ? customAttr.StrategyName : $"{interfaceType.Name}:{prop.Name}.Get";
                    sb.AppendLine("            get");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                return Ioc.Resolve<{propTypeName}>(\"{strategy}\", _obj);");
                    sb.AppendLine("            }");
                }

                if (prop.CanWrite)
                {
                    sb.AppendLine("            set");
                    sb.AppendLine("            {");
                    sb.AppendLine($"                Ioc.Resolve<ICommand>(\"{interfaceType.Name}:{prop.Name}.Set\", _obj, value).Execute();");
                    sb.AppendLine("            }");
                }

                sb.AppendLine("        }");
                sb.AppendLine();
            }

            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GetFriendlyTypeName(Type type)
        {
            if (type.IsGenericType)
            {
                var genericArgs = type.GetGenericArguments().Select(GetFriendlyTypeName);
                return $"{type.Name.Split('`')[0]}<{string.Join(", ", genericArgs)}>";
            }
            return type.FullName ?? type.Name;
        }
    }
}
