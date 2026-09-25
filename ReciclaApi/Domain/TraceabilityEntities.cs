namespace ReciclaApi.Domain;

public sealed class Empresa
{
    public Guid EmpresaId { get; set; } = Guid.NewGuid();
    public string Codigo { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string? NombreComercial { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public bool EsGestoraResiduos { get; set; }
    public string? NumeroAutorizacion { get; set; }
    public bool EsActivo { get; set; } = true;
}

public sealed class PuntoResiduo
{
    public Guid PuntoResiduoId { get; set; } = Guid.NewGuid();
    public Guid SedeId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = "GENERACION";
    public bool EsActivo { get; set; } = true;
    public Sede Sede { get; set; } = null!;
}

public sealed class EstadoRetiro
{
    public int EstadoRetiroId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public sealed class EstadoDisposicion
{
    public int EstadoDisposicionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public sealed class TipoTratamiento
{
    public int TipoTratamientoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool EsValorizacion { get; set; }
    public bool EsActivo { get; set; } = true;
}

public sealed class MovimientoResiduo
{
    public Guid MovimientoResiduoId { get; set; } = Guid.NewGuid();
    public Guid RegistroResiduoId { get; set; }
    public DateTime FechaMovimiento { get; set; }
    public Guid? PuntoOrigenId { get; set; }
    public Guid PuntoDestinoId { get; set; }
    public decimal Cantidad { get; set; }
    public int UnidadMedidaId { get; set; }
    public string? Observacion { get; set; }
    public int EstadoSincronizacionId { get; set; }
    public Guid RegistradoPorUsuarioId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoUtc { get; set; }
    public bool Eliminado { get; set; }

    public RegistroResiduo RegistroResiduo { get; set; } = null!;
    public PuntoResiduo? PuntoOrigen { get; set; }
    public PuntoResiduo PuntoDestino { get; set; } = null!;
    public UnidadMedida UnidadMedida { get; set; } = null!;
    public EstadoSincronizacion EstadoSincronizacion { get; set; } = null!;
    public Usuario RegistradoPorUsuario { get; set; } = null!;
}

public sealed class Retiro
{
    public Guid RetiroId { get; set; } = Guid.NewGuid();
    public string Codigo { get; set; } = string.Empty;
    public DateTime FechaRetiro { get; set; }
    public Guid SedeId { get; set; }
    public Guid? PuntoAlmacenamientoId { get; set; }
    public Guid EmpresaGestoraId { get; set; }
    public int EstadoRetiroId { get; set; }
    public string? DocumentoTransporte { get; set; }
    public string? Vehiculo { get; set; }
    public string? Placa { get; set; }
    public string? Conductor { get; set; }
    public string? Observacion { get; set; }
    public int EstadoSincronizacionId { get; set; }
    public Guid CreadoPorUsuarioId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoUtc { get; set; }
    public bool Eliminado { get; set; }

    public Sede Sede { get; set; } = null!;
    public PuntoResiduo? PuntoAlmacenamiento { get; set; }
    public Empresa EmpresaGestora { get; set; } = null!;
    public EstadoRetiro EstadoRetiro { get; set; } = null!;
    public EstadoSincronizacion EstadoSincronizacion { get; set; } = null!;
    public Usuario CreadoPorUsuario { get; set; } = null!;
    public ICollection<RetiroDetalle> Detalles { get; set; } = new List<RetiroDetalle>();
    public ICollection<DisposicionFinal> DisposicionesFinales { get; set; } = new List<DisposicionFinal>();
}

public sealed class RetiroDetalle
{
    public Guid RetiroDetalleId { get; set; } = Guid.NewGuid();
    public Guid RetiroId { get; set; }
    public Guid RegistroResiduoId { get; set; }
    public decimal Cantidad { get; set; }
    public int UnidadMedidaId { get; set; }
    public string? Observacion { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;

    public Retiro Retiro { get; set; } = null!;
    public RegistroResiduo RegistroResiduo { get; set; } = null!;
    public UnidadMedida UnidadMedida { get; set; } = null!;
}

public sealed class DisposicionFinal
{
    public Guid DisposicionFinalId { get; set; } = Guid.NewGuid();
    public Guid RetiroId { get; set; }
    public Guid EmpresaGestoraId { get; set; }
    public DateTime? FechaDisposicion { get; set; }
    public int TipoTratamientoId { get; set; }
    public string? CodigoDocumento { get; set; }
    public int EstadoDisposicionId { get; set; }
    public string? Observacion { get; set; }
    public int EstadoSincronizacionId { get; set; }
    public Guid CreadoPorUsuarioId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoUtc { get; set; }
    public bool Eliminado { get; set; }

    public Retiro Retiro { get; set; } = null!;
    public Empresa EmpresaGestora { get; set; } = null!;
    public TipoTratamiento TipoTratamiento { get; set; } = null!;
    public EstadoDisposicion EstadoDisposicion { get; set; } = null!;
    public EstadoSincronizacion EstadoSincronizacion { get; set; } = null!;
    public Usuario CreadoPorUsuario { get; set; } = null!;
    public ICollection<DisposicionFinalEvidencia> Evidencias { get; set; } = new List<DisposicionFinalEvidencia>();
}

public sealed class DisposicionFinalEvidencia
{
    public Guid EvidenciaId { get; set; } = Guid.NewGuid();
    public Guid DisposicionFinalId { get; set; }
    public string TipoEvidencia { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string? RutaLocal { get; set; }
    public string? UrlNube { get; set; }
    public string? ContentType { get; set; }
    public string? HashArchivo { get; set; }
    public long? TamanoBytes { get; set; }
    public int EstadoSincronizacionId { get; set; }
    public Guid CreadoPorUsuarioId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public bool Eliminado { get; set; }

    public DisposicionFinal DisposicionFinal { get; set; } = null!;
    public EstadoSincronizacion EstadoSincronizacion { get; set; } = null!;
    public Usuario CreadoPorUsuario { get; set; } = null!;
}
