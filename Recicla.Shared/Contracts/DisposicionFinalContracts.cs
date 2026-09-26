namespace Recicla.Shared.Contracts;

public sealed record TipoTratamientoDto(
    int TipoTratamientoId,
    string Codigo,
    string Nombre,
    bool EsValorizacion);

public sealed record CrearDisposicionFinalRequest(
    Guid EmpresaGestoraId,
    DateTime FechaDisposicion,
    int TipoTratamientoId,
    string? CodigoDocumento,
    string? Observacion);

public sealed record DisposicionFinalEvidenciaDto(
    Guid EvidenciaId,
    string TipoEvidencia,
    string NombreArchivo,
    string? UrlNube,
    string? ContentType,
    long? TamanoBytes,
    DateTime CreadoUtc);

public sealed record DisposicionFinalDto(
    Guid DisposicionFinalId,
    Guid RetiroId,
    Guid EmpresaGestoraId,
    string EmpresaGestora,
    DateTime? FechaDisposicion,
    int TipoTratamientoId,
    string TipoTratamientoCodigo,
    string TipoTratamiento,
    bool EsValorizacion,
    string? CodigoDocumento,
    string Estado,
    string? Observacion,
    Guid CreadoPorUsuarioId,
    string CreadoPor,
    DateTime CreadoUtc,
    IReadOnlyCollection<DisposicionFinalEvidenciaDto> Evidencias);
