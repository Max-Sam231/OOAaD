using App;

namespace SpaceBattle.Lib
{
    public class RegisterIoCDependencyAdapterTemplates : ICommand
    {
        private static readonly (string Dependency, string FileName)[] Templates =
        [
            ("Adapters.Template.Class", "AdapterClass.txt"),
            ("Adapters.Template.Property", "AdapterProperty.txt"),
            ("Adapters.Template.Getter", "AdapterGetter.txt"),
            ("Adapters.Template.Setter", "AdapterSetter.txt"),
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
        }
    }
}
