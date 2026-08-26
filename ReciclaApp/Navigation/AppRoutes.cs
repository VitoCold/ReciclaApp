namespace ReciclaApp.Navigation;

public static class AppRoutes
{
    // Rutas absolutas: reemplazan toda la pila de navegación.
    public const string Login = "//login";
    public const string Registros = "//registros";

    // Rutas relativas: se apilan sobre la pantalla actual.
    public const string DetalleRegistroDetalle = "detalle-registro-detalle";
    public const string DetalleRegistroResiduos = "detalle-registro-residuos";
    public const string DetalleRegistroDisposicion = "detalle-registro-disposicion";
    public const string DetalleResiduoDetalle = "detalle-residuo-detalle";
    public const string DetalleResiduoFotos = "detalle-residuo-fotos";
    public const string InicioRegistro = "inicio-registro";
    public const string RegistrarResiduo = "registrar-residuo";
}
