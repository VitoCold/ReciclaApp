namespace ReciclaApp.Navigation;

public static class AppRoutes
{
    // Rutas absolutas: reemplazan toda la pila de navegación.
    public const string Login = "//login";
    public const string ControlesGeneracion = "//controles-generacion";
    public const string StockResiduos = "//stock-residuos";
    public const string Registros = "//registros";
    public const string Perfil = "//perfil";

    // Rutas relativas: se apilan sobre la pantalla actual.
    public const string NuevoControlGeneracion = "nuevo-control-generacion";
    public const string DetalleControlGeneracion = "detalle-control-generacion";
    public const string AsignarUsuarioControl = "asignar-usuario-control";
    public const string DetalleRegistroControl = "detalle-registro-control";
    public const string DetalleRegistro = "detalle-registro";
    public const string DetalleResiduoDetalle = "detalle-residuo-detalle";
    public const string DetalleResiduoFotos = "detalle-residuo-fotos";
    public const string InicioRegistro = "inicio-registro";
    public const string RegistrarResiduo = "registrar-residuo";
}
