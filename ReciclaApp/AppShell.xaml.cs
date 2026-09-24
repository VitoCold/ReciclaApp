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

        public AppShell()
        {
            InitializeComponent();
            RegistrarRutas();
            Loaded += OnShellLoaded;
        }

        private async void OnShellLoaded(object? sender, EventArgs e)
        {
            if (_sessionChecked)
                return;

            _sessionChecked = true;

            var services = Handler?.MauiContext?.Services;
            if (services is null)
                return;

            try
            {
                var session = services.GetRequiredService<IAuthSessionService>();
                if (await session.RestoreSessionAsync())
                    await AppNavigator.IrARegistrosAsync();
            }
            catch (Exception ex)
            {
                services.GetService<ILogger<AppShell>>()?
                    .LogWarning(ex, "No se pudo restaurar la sesión al iniciar la aplicación.");
            }
        }

        private static void RegistrarRutas()
        {
            Routing.RegisterRoute(AppRoutes.DetalleRegistroDetalle, typeof(DetalleRegistroDetallePage));
            Routing.RegisterRoute(AppRoutes.DetalleRegistroResiduos, typeof(DetalleRegistroResiduosPage));
            Routing.RegisterRoute(AppRoutes.DetalleRegistroDisposicion, typeof(DetalleRegistroDisposicionPage));
            Routing.RegisterRoute(AppRoutes.DetalleResiduoDetalle, typeof(DetalleResiduoDetallePage));
            Routing.RegisterRoute(AppRoutes.DetalleResiduoFotos, typeof(DetalleResiduoFotosPage));
            Routing.RegisterRoute(AppRoutes.InicioRegistro, typeof(InicioRegistroPage));
            Routing.RegisterRoute(AppRoutes.RegistrarResiduo, typeof(RegistrarResiduoPage));
        }
    }
}
