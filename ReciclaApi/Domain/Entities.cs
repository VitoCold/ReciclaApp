namespace ReciclaApi.Domain;

public sealed class Rol
{
    public int RolId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool EsActivo { get; set; } = true;
    public ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();
}

public sealed class Usuario
{
    public Guid UsuarioId { get; set; } = Guid.NewGuid();
    public string UsuarioNombre { get; set; } = string.Empty;
    public string Nombres { get; set; } = string.Empty;
    public string? Apellidos { get; set; }
    public string? Email { get; set; }
    public byte[]? PasswordHash { get; set; }
    public byte[]? PasswordSalt { get; set; }
    public bool DebeCambiarClave { get; set; }
    public bool EsActivo { get; set; } = true;
    public DateTime? UltimoAccesoUtc { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoUtc { get; set; }
    public ICollection<UsuarioRol> UsuarioRoles { get; set; } = new List<UsuarioRol>();
    public ICollection<UsuarioSede> UsuarioSedes { get; set; } = new List<UsuarioSede>();
}

public sealed class UsuarioRol
{
    public Guid UsuarioId { get; set; }
    public int RolId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public Usuario Usuario { get; set; } = null!;
    public Rol Rol { get; set; } = null!;
}

public sealed class Sede
{
    public Guid SedeId { get; set; } = Guid.NewGuid();
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool EsActivo { get; set; } = true;
}

public sealed class PuntoResiduo
{
    public Guid PuntoResiduoId { get; set; } = Guid.NewGuid();
    public Guid SedeId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = "AMBOS";
    public bool EsActivo { get; set; } = true;
    public Sede Sede { get; set; } = null!;
}

public sealed class UsuarioSede
{
    public Guid UsuarioId { get; set; }
    public Guid SedeId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public Sede Sede { get; set; } = null!;
}

public sealed class Proyecto
{
    public Guid ProyectoId { get; set; } = Guid.NewGuid();
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool EsActivo { get; set; } = true;
    public ICollection<Actividad> Actividades { get; set; } = new List<Actividad>();
}

public sealed class Actividad
{
    public Guid ActividadId { get; set; } = Guid.NewGuid();
    public Guid? ProyectoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool EsActivo { get; set; } = true;
    public Proyecto? Proyecto { get; set; }
}

public sealed class ClasificacionResiduo
{
    public int ClasificacionResiduoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? ColorHex { get; set; }
}

public sealed class TipoResiduo
{
    public Guid TipoResiduoId { get; set; } = Guid.NewGuid();
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public bool EsActivo { get; set; } = true;
}

public sealed class UnidadMedida
{
    public int UnidadMedidaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public sealed class ResiduoCatalogo
{
    public Guid ResiduoId { get; set; } = Guid.NewGuid();
    public Guid TipoResiduoId { get; set; }
    public int ClasificacionResiduoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int UnidadMedidaDefaultId { get; set; }
    public bool EsActivo { get; set; } = true;
    public TipoResiduo TipoResiduo { get; set; } = null!;
    public ClasificacionResiduo ClasificacionResiduo { get; set; } = null!;
    public UnidadMedida UnidadMedidaDefault { get; set; } = null!;
}

public sealed class EstadoRegistro
{
    public int EstadoRegistroId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public sealed class EstadoSincronizacion
{
    public int EstadoSincronizacionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
}

public sealed class Registro
{
    public Guid RegistroId { get; set; } = Guid.NewGuid();
    public string? CodigoLocal { get; set; }
    public DateTime FechaRegistro { get; set; }
    public Guid RegistradoPorUsuarioId { get; set; }
    public Guid ProyectoId { get; set; }
    public Guid ActividadId { get; set; }
    public Guid SedeId { get; set; }
    public Guid? PuntoGeneracionId { get; set; }
    public int EstadoRegistroId { get; set; }
    public int EstadoSincronizacionId { get; set; }
    public string? Observacion { get; set; }
    public string? OrigenDispositivo { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoUtc { get; set; }
    public bool Eliminado { get; set; }
    public Usuario RegistradoPorUsuario { get; set; } = null!;
    public Proyecto Proyecto { get; set; } = null!;
    public Actividad Actividad { get; set; } = null!;
    public Sede Sede { get; set; } = null!;
    public PuntoResiduo? PuntoGeneracion { get; set; }
    public EstadoRegistro EstadoRegistro { get; set; } = null!;
    public EstadoSincronizacion EstadoSincronizacion { get; set; } = null!;
    public ICollection<RegistroResiduo> Residuos { get; set; } = new List<RegistroResiduo>();
    public ICollection<Disposicion> Disposiciones { get; set; } = new List<Disposicion>();
}

public sealed class RegistroResiduo
{
    public Guid RegistroResiduoId { get; set; } = Guid.NewGuid();
    public Guid RegistroId { get; set; }
    public Guid TipoResiduoId { get; set; }
    public Guid ResiduoId { get; set; }
    public int UnidadMedidaId { get; set; }
    public decimal Cantidad { get; set; }
    public int EstadoSincronizacionId { get; set; }
    public string? Observacion { get; set; }
    public Guid CreadoPorUsuarioId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoUtc { get; set; }
    public bool Eliminado { get; set; }
    public Registro Registro { get; set; } = null!;
    public TipoResiduo TipoResiduo { get; set; } = null!;
    public ResiduoCatalogo Residuo { get; set; } = null!;
    public UnidadMedida UnidadMedida { get; set; } = null!;
    public EstadoSincronizacion EstadoSincronizacion { get; set; } = null!;
    public Usuario CreadoPorUsuario { get; set; } = null!;
    public ICollection<RegistroResiduoFoto> Fotos { get; set; } = new List<RegistroResiduoFoto>();
}

public sealed class RegistroResiduoFoto
{
    public Guid FotoId { get; set; } = Guid.NewGuid();
    public Guid RegistroResiduoId { get; set; }
    public string NombreArchivo { get; set; } = string.Empty;
    public string? RutaLocal { get; set; }
    public string? UrlNube { get; set; }
    public string? ContentType { get; set; }
    public string? HashArchivo { get; set; }
    public long? TamanoBytes { get; set; }
    public int EstadoSincronizacionId { get; set; }
    public Guid TomadaPorUsuarioId { get; set; }
    public DateTime TomadaUtc { get; set; } = DateTime.UtcNow;
    public bool Eliminado { get; set; }
    public RegistroResiduo RegistroResiduo { get; set; } = null!;
    public EstadoSincronizacion EstadoSincronizacion { get; set; } = null!;
    public Usuario TomadaPorUsuario { get; set; } = null!;
}

public sealed class EmpresaGestora
{
    public Guid EmpresaGestoraId { get; set; } = Guid.NewGuid();
    public string Ruc { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string? NombreComercial { get; set; }
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? NumeroAutorizacion { get; set; }
    public bool EsActivo { get; set; } = true;
}

public sealed class EstadoRetiro
{
    public int EstadoRetiroId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
}

public sealed class Retiro
{
    public Guid RetiroId { get; set; } = Guid.NewGuid();
    public string Codigo { get; set; } = string.Empty;
    public DateTime FechaRetiro { get; set; }
    public Guid SedeId { get; set; }
    public Guid EmpresaGestoraId { get; set; }
    public string? Vehiculo { get; set; }
    public string? Placa { get; set; }
    public string? Conductor { get; set; }
    public string? DocumentoTransporte { get; set; }
    public string? Observacion { get; set; }
    public int EstadoRetiroId { get; set; }
    public Guid CreadoPorUsuarioId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoUtc { get; set; }
    public bool Eliminado { get; set; }
    public Sede Sede { get; set; } = null!;
    public EmpresaGestora EmpresaGestora { get; set; } = null!;
    public EstadoRetiro EstadoRetiro { get; set; } = null!;
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
    public Retiro Retiro { get; set; } = null!;
    public RegistroResiduo RegistroResiduo { get; set; } = null!;
    public UnidadMedida UnidadMedida { get; set; } = null!;
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
    public Guid RegistradoPorUsuarioId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public RegistroResiduo RegistroResiduo { get; set; } = null!;
    public PuntoResiduo? PuntoOrigen { get; set; }
    public PuntoResiduo PuntoDestino { get; set; } = null!;
    public UnidadMedida UnidadMedida { get; set; } = null!;
    public Usuario RegistradoPorUsuario { get; set; } = null!;
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
    public bool EsActivo { get; set; } = true;
}

public sealed class DisposicionFinal
{
    public Guid DisposicionFinalId { get; set; } = Guid.NewGuid();
    public Guid RetiroId { get; set; }
    public Guid EmpresaGestoraId { get; set; }
    public DateTime? FechaDisposicion { get; set; }
    public int? TipoTratamientoId { get; set; }
    public string? CodigoDocumento { get; set; }
    public string? Observacion { get; set; }
    public int EstadoDisposicionId { get; set; }
    public Guid CreadoPorUsuarioId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoUtc { get; set; }
    public bool Eliminado { get; set; }
    public Retiro Retiro { get; set; } = null!;
    public EmpresaGestora EmpresaGestora { get; set; } = null!;
    public TipoTratamiento? TipoTratamiento { get; set; }
    public EstadoDisposicion EstadoDisposicion { get; set; } = null!;
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

// Modelo legado. Se conserva temporalmente mientras la API/UI migra al flujo Retiro -> DisposicionFinal.
public sealed class Disposicion
{
    public Guid DisposicionId { get; set; } = Guid.NewGuid();
    public Guid RegistroId { get; set; }
    public string? CodigoDocumento { get; set; }
    public DateTime? FechaDisposicion { get; set; }
    public string? EmpresaDisposicion { get; set; }
    public string? Observacion { get; set; }
    public int EstadoSincronizacionId { get; set; }
    public Guid CreadoPorUsuarioId { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ActualizadoUtc { get; set; }
    public bool Eliminado { get; set; }
    public Registro Registro { get; set; } = null!;
    public EstadoSincronizacion EstadoSincronizacion { get; set; } = null!;
    public Usuario CreadoPorUsuario { get; set; } = null!;
    public ICollection<DisposicionEvidencia> Evidencias { get; set; } = new List<DisposicionEvidencia>();
}

public sealed class DisposicionEvidencia
{
    public Guid EvidenciaId { get; set; } = Guid.NewGuid();
    public Guid DisposicionId { get; set; }
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
    public Disposicion Disposicion { get; set; } = null!;
    public EstadoSincronizacion EstadoSincronizacion { get; set; } = null!;
    public Usuario CreadoPorUsuario { get; set; } = null!;
}

public sealed class SyncOutbox
{
    public long SyncOutboxId { get; set; }
    public string Entidad { get; set; } = string.Empty;
    public Guid EntidadId { get; set; }
    public string Operacion { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
    public bool Procesado { get; set; }
    public DateTime CreadoUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcesadoUtc { get; set; }
}

public sealed class Auditoria
{
    public long AuditoriaId { get; set; }
    public Guid? UsuarioId { get; set; }
    public string Entidad { get; set; } = string.Empty;
    public Guid? EntidadId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string? DatosAntesJson { get; set; }
    public string? DatosDespuesJson { get; set; }
    public DateTime FechaUtc { get; set; } = DateTime.UtcNow;
    public string? IpOrigen { get; set; }
    public string? Dispositivo { get; set; }
    public Usuario? Usuario { get; set; }
}
