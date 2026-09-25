namespace ReciclaApi.Domain;

public sealed class EstadoControlGeneracion
{
    public int EstadoControlGeneracionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public sealed class ControlGeneracion
{
    public Guid ControlGeneracionId { get; set; } = Guid.NewGuid();
    public string Codigo { get; set; } = string.Empty;
    public Guid SedeId { get; set; }
    public Guid EmpresaResponsableId { get; set; }
    public Guid ProyectoId { get; set; }
    public Guid ActividadId { get; set; }
    public Guid? PuntoGeneracionId { get; set; }
    public string? DescripcionTrabajo { get; set; }
    public DateTime FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public int EstadoControlGeneracionId { get; set; }
    public string? Observacion { get; set; }
    public string? MotivoUltimoCambio { get; set; }
    public Guid CreadoPorUsuarioId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoUtc { get; set; }
    public Guid? AprobadoPorUsuarioId { get; set; }
    public DateTime? AprobadoUtc { get; set; }
    public bool Eliminado { get; set; }

    public Sede Sede { get; set; } = null!;
    public Empresa EmpresaResponsable { get; set; } = null!;
    public Proyecto Proyecto { get; set; } = null!;
    public Actividad Actividad { get; set; } = null!;
    public PuntoResiduo? PuntoGeneracion { get; set; }
    public EstadoControlGeneracion EstadoControlGeneracion { get; set; } = null!;
    public Usuario CreadoPorUsuario { get; set; } = null!;
    public Usuario? AprobadoPorUsuario { get; set; }
    public ICollection<ControlGeneracionUsuario> Usuarios { get; set; } = new List<ControlGeneracionUsuario>();
    public ICollection<Registro> Registros { get; set; } = new List<Registro>();
}

public sealed class ControlGeneracionUsuario
{
    public Guid ControlGeneracionUsuarioId { get; set; } = Guid.NewGuid();
    public Guid ControlGeneracionId { get; set; }
    public Guid UsuarioId { get; set; }
    public string RolControl { get; set; } = string.Empty;
    public bool EsPrincipal { get; set; }
    public DateTime FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public bool EsActivo { get; set; } = true;
    public Guid AsignadoPorUsuarioId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoUtc { get; set; }

    public ControlGeneracion ControlGeneracion { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
    public Usuario AsignadoPorUsuario { get; set; } = null!;
}
