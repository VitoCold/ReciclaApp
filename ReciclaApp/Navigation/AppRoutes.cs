namespace ReciclaApp.Navigation;

public static class AppRoutes
{
    // Rutas absolutas: reemplazan toda la pila de navegación.
    public const string Login = "//login";
    public const string ControlesGeneracion = "//controles-generacion";
    public const string StockResiduos = "//stock-residuos";
    public const string Retiros = "//retiros";
    public const string Reportes = "//reportes";
    public const string Registros = "//registros";
    public const string Administracion = "//administracion";
    public const string Perfil = "//perfil";

    // Rutas relativas: se apilan sobre la pantalla actual.
    public const string NuevoControlGeneracion = "nuevo-control-generacion";
    public const string DetalleControlGeneracion = "detalle-control-generacion";
    public const string AsignarUsuarioControl = "asignar-usuario-control";
    public const string DetalleRegistroControl = "detalle-registro-control";
    public const string NuevoRetiro = "nuevo-retiro";
    public const string DetalleRetiro = "detalle-retiro";
    public const string NuevaDisposicionFinal = "nueva-disposicion-final";
    public const string TrazabilidadResiduo = "trazabilidad-residuo";
    public const string DetalleRegistro = "detalle-registro";
    public const string DetalleResiduoDetalle = "detalle-residuo-detalle";
    public const string DetalleResiduoFotos = "detalle-residuo-fotos";
    public const string InicioRegistro = "inicio-registro";
    public const string RegistrarResiduo = "registrar-residuo";
    public const string AdministracionUsuarios = "administracion-usuarios";
    public const string EditarAdministracionUsuario = "editar-administracion-usuario";
    public const string AdministracionSedes = "administracion-sedes";
    public const string EditarAdministracionSede = "editar-administracion-sede";
    public const string AdministracionPuntosResiduo = "administracion-puntos-residuo";
    public const string EditarAdministracionPuntoResiduo = "editar-administracion-punto-residuo";
    public const string AdministracionProyectos = "administracion-proyectos";
    public const string EditarAdministracionProyecto = "editar-administracion-proyecto";
    public const string AdministracionActividades = "administracion-actividades";
    public const string EditarAdministracionActividad = "editar-administracion-actividad";
    public const string AdministracionTiposResiduo = "administracion-tipos-residuo";
    public const string EditarAdministracionTipoResiduo = "editar-administracion-tipo-residuo";
    public const string AdministracionResiduos = "administracion-residuos";
    public const string EditarAdministracionResiduo = "editar-administracion-residuo";
}
