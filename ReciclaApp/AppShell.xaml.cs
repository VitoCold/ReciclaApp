using ReciclaApp.Navigation;
using ReciclaApp.Pages;

namespace ReciclaApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            RegistrarRutas();
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
