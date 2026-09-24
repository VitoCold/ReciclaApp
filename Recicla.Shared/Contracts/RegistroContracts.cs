namespace Recicla.Shared.Contracts;

public sealed record RegistroListItemDto(
    Guid RegistroId,
    DateTime FechaRegistro,
    string Proyecto,
    string Actividad,
    string Sede,
    string Estado,
    int CantidadResiduos,
    DateTime CreadoUtc);

public sealed record RegistroResiduoFotoDto(
    Guid FotoId,
    string NombreArchivo,
    string? UrlNube,
    string? ContentType,
    long? TamanoBytes,
    DateTime TomadaUtc);

public sealed record RegistroResiduoDto(
    Guid RegistroResiduoId,
    Guid TipoResiduoId,
    Guid ResiduoId,
    int UnidadMedidaId,
    decimal Cantidad,
    string? Observacion,
    IReadOnlyCollection<RegistroResiduoFotoDto> Fotos);

public sealed record DisposicionEvidenciaDto(
    Guid EvidenciaId,
    string TipoEvidencia,
    string NombreArchivo,
    string? UrlNube,
    string? ContentType,
    long? TamanoBytes,
    DateTime CreadoUtc);

public sealed record DisposicionDto(
    Guid DisposicionId,
    string? CodigoDocumento,
    DateTime? FechaDisposicion,
    string? EmpresaDisposicion,
    string? Observacion,
    IReadOnlyCollection<DisposicionEvidenciaDto> Evidencias);

public sealed record RegistroDetalleDto(
    Guid RegistroId,
    string? CodigoLocal,
    DateTime FechaRegistro,
    Guid ProyectoId,
    string Proyecto,
    Guid ActividadId,
    string Actividad,
    Guid SedeId,
    string Sede,
    string Estado,
    string? Observacion,
    IReadOnlyCollection<RegistroResiduoDto> Residuos,
    DisposicionDto? Disposicion);

public sealed record CrearRegistroRequest(
    string? CodigoLocal,
    DateTime FechaRegistro,
    Guid ProyectoId,
    Guid ActividadId,
    Guid SedeId,
    string? Observacion,
    string? OrigenDispositivo);

public sealed record ActualizarRegistroRequest(
    DateTime FechaRegistro,
    Guid ProyectoId,
    Guid ActividadId,
    Guid SedeId,
    string? Observacion);

public sealed record AgregarRegistroResiduoRequest(
    Guid TipoResiduoId,
    Guid ResiduoId,
    int UnidadMedidaId,
    decimal Cantidad,
    string? Observacion);

public sealed record ActualizarRegistroResiduoRequest(
    int UnidadMedidaId,
    decimal Cantidad,
    string? Observacion);

public sealed record CrearDisposicionRequest(
    string? CodigoDocumento,
    DateTime? FechaDisposicion,
    string? EmpresaDisposicion,
    string? Observacion);
