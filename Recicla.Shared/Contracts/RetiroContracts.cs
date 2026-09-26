namespace Recicla.Shared.Contracts;

public sealed record CrearRetiroDetalleRequest(
    Guid RegistroResiduoId,
    decimal Cantidad);

public sealed record CrearRetiroRequest(
    DateTime FechaRetiro,
    Guid SedeId,
    Guid PuntoAlmacenamientoId,
    Guid EmpresaGestoraId,
    string? DocumentoTransporte,
    string? Vehiculo,
    string? Placa,
    string? Conductor,
    string? Observacion,
    IReadOnlyCollection<CrearRetiroDetalleRequest> Detalles);

public sealed record RetiroResumenUnidadDto(
    string Unidad,
    decimal Cantidad);

public sealed record RetiroListItemDto(
    Guid RetiroId,
    string Codigo,
    DateTime FechaRetiro,
    Guid SedeId,
    string Sede,
    Guid PuntoAlmacenamientoId,
    string PuntoAlmacenamiento,
    Guid EmpresaGestoraId,
    string EmpresaGestora,
    string Estado,
    int CantidadDetalles,
    IReadOnlyCollection<RetiroResumenUnidadDto> Totales,
    DateTime CreadoUtc);

public sealed record RetiroDetalleItemDto(
    Guid RetiroDetalleId,
    Guid RegistroResiduoId,
    Guid RegistroId,
    Guid ControlGeneracionId,
    string ControlCodigo,
    string Residuo,
    string Clasificacion,
    decimal Cantidad,
    int UnidadMedidaId,
    string Unidad);

public sealed record RetiroDetalleDto(
    Guid RetiroId,
    string Codigo,
    DateTime FechaRetiro,
    Guid SedeId,
    string Sede,
    Guid PuntoAlmacenamientoId,
    string PuntoAlmacenamiento,
    Guid EmpresaGestoraId,
    string EmpresaGestora,
    string Estado,
    string? DocumentoTransporte,
    string? Vehiculo,
    string? Placa,
    string? Conductor,
    string? Observacion,
    Guid CreadoPorUsuarioId,
    string CreadoPor,
    DateTime CreadoUtc,
    IReadOnlyCollection<RetiroDetalleItemDto> Detalles,
    IReadOnlyCollection<RetiroResumenUnidadDto> Totales);
