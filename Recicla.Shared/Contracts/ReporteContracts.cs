namespace Recicla.Shared.Contracts;

public sealed record ReporteResiduosFiltroRequest(
    DateTime Desde,
    DateTime Hasta,
    Guid? SedeId = null,
    Guid? EmpresaResponsableId = null,
    Guid? ControlGeneracionId = null,
    int? ClasificacionResiduoId = null,
    Guid? ResiduoId = null);

public sealed record ReporteResumenUnidadDto(
    string Unidad,
    decimal Generado,
    decimal Almacenado,
    decimal Retirado,
    decimal Saldo,
    decimal PendienteAlmacenamiento);

public sealed record ReporteResiduoItemDto(
    Guid RegistroResiduoId,
    Guid RegistroId,
    DateTime FechaRegistro,
    Guid SedeId,
    string Sede,
    Guid ControlGeneracionId,
    string ControlCodigo,
    Guid? EmpresaResponsableId,
    string EmpresaResponsable,
    Guid ProyectoId,
    string Proyecto,
    Guid ActividadId,
    string Actividad,
    int ClasificacionResiduoId,
    string Clasificacion,
    Guid ResiduoId,
    string Residuo,
    string Unidad,
    decimal Generado,
    decimal Almacenado,
    decimal Retirado,
    decimal Saldo,
    decimal PendienteAlmacenamiento,
    Guid RegistradoPorUsuarioId,
    string RegistradoPor,
    double? Latitud,
    double? Longitud,
    double? PrecisionMetros,
    int CantidadFotos,
    string? Observacion);

public sealed record ReporteResiduosDto(
    DateTime Desde,
    DateTime Hasta,
    IReadOnlyCollection<ReporteResumenUnidadDto> Resumen,
    IReadOnlyCollection<ReporteResiduoItemDto> Detalle);
