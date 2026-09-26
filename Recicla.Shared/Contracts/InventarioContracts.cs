namespace Recicla.Shared.Contracts;

public sealed record InventarioUbicacionDto(
    Guid PuntoResiduoId,
    string Codigo,
    string Punto,
    decimal Cantidad);

public sealed record InventarioResiduoItemDto(
    Guid RegistroResiduoId,
    Guid RegistroId,
    Guid ControlGeneracionId,
    string ControlCodigo,
    DateTime FechaRegistro,
    Guid SedeId,
    string Sede,
    Guid ResiduoId,
    string Residuo,
    string Clasificacion,
    int UnidadMedidaId,
    string Unidad,
    decimal CantidadGenerada,
    decimal CantidadRetirada,
    decimal CantidadDisponible,
    decimal CantidadPendienteAlmacenamiento,
    IReadOnlyCollection<InventarioUbicacionDto> Almacenamientos,
    Guid RegistradoPorUsuarioId,
    string RegistradoPor);

public sealed record InventarioResumenUnidadDto(
    string Unidad,
    decimal Disponible,
    decimal Almacenado,
    decimal PendienteAlmacenamiento);

public sealed record InventarioResiduosDto(
    IReadOnlyCollection<InventarioResumenUnidadDto> Resumen,
    IReadOnlyCollection<InventarioResiduoItemDto> Items);

public sealed record AlmacenarResiduoRequest(
    Guid PuntoAlmacenamientoId,
    decimal Cantidad,
    DateTime FechaMovimiento,
    string? Observacion);
