using ReciclaApp.Data;

namespace ReciclaApp
{
    public partial class App : Application
    {
        public static AppDatabase Database { get; private set; } = null!;

        public App(AppDatabase database)
        {
            InitializeComponent();
            Database = database;
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            _ = InitializeDatabaseAsync();
            return new Window(new AppShell());
        }

        private static async Task InitializeDatabaseAsync()
        {
            try
            {
                await Database.InitializeAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error inicializando la base local: {ex}");
            }
        }
    }
}
