using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyAdapterTemplates : ICommand
    {
        private static readonly (string Dependency, string FileName)[] Templates =
        [
            ("Adapters.Template.Class", "AdapterClass.txt"),
            ("Adapters.Template.Setter", "AdapterSetter.txt"),
            ("Adapters.Template.CommandFactory", "CommandFactory.txt"),
        ];

        private static readonly (string Dependency, string Text)[] BuiltInTemplates =
        [
            ("Adapters.Template.Property", "        public {{PropertyType}} {{PropertyName}}\n        {\n{{Accessors}}\n        }"),
            ("Adapters.Template.Getter", "            get { return global::App.Ioc.Resolve<{{PropertyType}}>(\"{{GetterKey}}\", _gameItem); }")
        ];

        private readonly string _directory;

        public RegisterIoCDependencyAdapterTemplates()
            : this(Path.Combine(AppContext.BaseDirectory, "Adapters", "Templates"))
        {
        }

        public RegisterIoCDependencyAdapterTemplates(string directory)
        {
            _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        }

        public void Execute()
        {
            var texts = Templates
                .Select(template => (template.Dependency, Text: File.ReadAllText(Path.Combine(_directory, template.FileName))))
                .ToList();

            foreach (var (dependency, text) in texts)
            {
                Ioc.Resolve<ICommand>("IoC.Register", dependency, (Func<object[], object>)(args => text)).Execute();
            }

            foreach (var (dependency, text) in BuiltInTemplates)
            {
                Ioc.Resolve<ICommand>("IoC.Register", dependency, (Func<object[], object>)(args => text)).Execute();
            }
        }
    }
}
