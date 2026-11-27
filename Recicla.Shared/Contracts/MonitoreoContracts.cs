namespace Recicla.Shared.Contracts;

public sealed record MonitoreoResumenDto(
    int Total,
    int Peligrosos,
    int NoPeligrosos,
    int PendientesTraslado);

public sealed record MonitoreoSedeDto(
    Guid SedeId,
    string Codigo,
    string Nombre);

public sealed record MonitoreoClasificacionDto(
    int ClasificacionResiduoId,
    string Codigo,
    string Nombre,
    string? ColorHex);

public sealed record MonitoreoActividadItemDto(
    Guid RegistroResiduoId,
    Guid RegistroId,
    Guid ControlGeneracionId,
    string ControlCodigo,
    DateTime RegistradoUtc,
    Guid SedeId,
    string Sede,
    string Proyecto,
    string Actividad,
    string Residuo,
    string TipoResiduo,
    int ClasificacionResiduoId,
    string ClasificacionCodigo,
    string Clasificacion,
    string? ClasificacionColorHex,
    decimal Cantidad,
    string Unidad,
    Guid RegistradoPorUsuarioId,
    string RegistradoPor,
    string EstadoOperativoCodigo,
    string EstadoOperativo,
    bool TieneFoto,
    bool TieneGps);

public sealed record MonitoreoActividadDto(
    MonitoreoResumenDto Resumen,
    IReadOnlyCollection<MonitoreoActividadItemDto> Items,
    IReadOnlyCollection<MonitoreoSedeDto> Sedes,
    IReadOnlyCollection<MonitoreoClasificacionDto> Clasificaciones);

public sealed record MonitoreoActividadFiltroRequest(
    DateTime? DesdeUtc,
    DateTime? HastaUtc,
    Guid? SedeId,
    int? ClasificacionResiduoId,
    string? EstadoOperativo,
    string? Buscar,
    int Limite = 150);
