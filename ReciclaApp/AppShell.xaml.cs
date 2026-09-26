using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReciclaApp.Navigation;
using ReciclaApp.Pages;
using ReciclaApp.Services;

namespace ReciclaApp
{
    public partial class AppShell : Shell
    {
        private bool _sessionChecked;
        private bool _checkingProtectedRoute;

        public AppShell()
        {
            InitializeComponent();
            RegistrarRutas();
            Loaded += OnShellLoaded;
            Navigated += OnShellNavigated;
        }

        private async void OnShellLoaded(object? sender, EventArgs e)
        {
            if (_sessionChecked)
                return;

            _sessionChecked = true;
            var services = AppServices.Services;

            try
            {
                var session = services.GetRequiredService<IAuthSessionService>();
                if (await session.RestoreSessionAsync())
                    await AppNavigator.IrAControlesGeneracionAsync();
            }
            catch (Exception ex)
            {
                services.GetService<ILogger<AppShell>>()?
                    .LogWarning(ex, "No se pudo restaurar la sesión al iniciar la aplicación.");
            }
        }

        private async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
        {
            var location = e.Current.Location.OriginalString;
            var isLogin = location.Contains("login", StringComparison.OrdinalIgnoreCase);

            FlyoutBehavior = isLogin
                ? Microsoft.Maui.FlyoutBehavior.Disabled
                : Microsoft.Maui.FlyoutBehavior.Flyout;

            if (isLogin || _checkingProtectedRoute)
                return;

            var services = AppServices.Services;
            var session = services.GetRequiredService<IAuthSessionService>();
            if (session.IsAuthenticated)
                return;

            _checkingProtectedRoute = true;
            try
            {
                if (!await session.RestoreSessionAsync())
                    await AppNavigator.IrAlLoginAsync();
            }
            finally
            {
                _checkingProtectedRoute = false;
            }
        }

        private static void RegistrarRutas()
        {
            Routing.RegisterRoute(AppRoutes.NuevoControlGeneracion, typeof(NuevoControlGeneracionPage));
            Routing.RegisterRoute(AppRoutes.DetalleControlGeneracion, typeof(ControlGeneracionDetallePage));
            Routing.RegisterRoute(AppRoutes.AsignarUsuarioControl, typeof(AsignarUsuarioControlPage));
            Routing.RegisterRoute(AppRoutes.DetalleRegistroControl, typeof(RegistroControlDetallePage));
            Routing.RegisterRoute(AppRoutes.NuevoRetiro, typeof(NuevoRetiroPage));
            Routing.RegisterRoute(AppRoutes.DetalleRetiro, typeof(RetiroDetallePage));
            Routing.RegisterRoute(AppRoutes.DetalleRegistro, typeof(DetalleRegistroDetallePage));
            Routing.RegisterRoute(AppRoutes.DetalleResiduoDetalle, typeof(DetalleResiduoDetallePage));
            Routing.RegisterRoute(AppRoutes.DetalleResiduoFotos, typeof(DetalleResiduoFotosPage));
            Routing.RegisterRoute(AppRoutes.InicioRegistro, typeof(InicioRegistroPage));
            Routing.RegisterRoute(AppRoutes.RegistrarResiduo, typeof(RegistrarResiduoPage));
        }
    }
}
