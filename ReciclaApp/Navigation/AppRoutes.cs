namespace ReciclaApp.Navigation;

public static class AppRoutes
{
    // Rutas absolutas: reemplazan toda la pila de navegación.
    public const string Login = "//login";
    public const string Registros = "//registros";
    public const string Perfil = "//perfil";

    // Rutas relativas: se apilan sobre la pantalla actual.
    public const string DetalleRegistro = "detalle-registro";
    public const string DetalleResiduoDetalle = "detalle-residuo-detalle";
    public const string DetalleResiduoFotos = "detalle-residuo-fotos";
    public const string InicioRegistro = "inicio-registro";
    public const string RegistrarResiduo = "registrar-residuo";
}
