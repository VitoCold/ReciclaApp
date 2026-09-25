namespace Recicla.Shared.Contracts;

public sealed record CrearControlGeneracionRequest(
    Guid SedeId,
    Guid EmpresaResponsableId,
    Guid ProyectoId,
    Guid ActividadId,
    Guid? PuntoGeneracionId,
    string? DescripcionTrabajo,
    DateTime FechaInicio,
    DateTime? FechaFin,
    string? Observacion);

public sealed record ActualizarControlGeneracionRequest(
    Guid SedeId,
    Guid EmpresaResponsableId,
    Guid ProyectoId,
    Guid ActividadId,
    Guid? PuntoGeneracionId,
    string? DescripcionTrabajo,
    DateTime FechaInicio,
    DateTime? FechaFin,
    string? Observacion,
    string MotivoModificacion);

public sealed record RechazarControlGeneracionRequest(string Motivo);

public sealed record CambiarEstadoControlGeneracionRequest(string Motivo);

public sealed record AsignarControlGeneracionUsuarioRequest(
    Guid UsuarioId,
    string RolControl,
    bool EsPrincipal,
    DateTime? FechaDesde,
    DateTime? FechaHasta);

public sealed record CrearRegistroEnControlRequest(
    string? CodigoLocal,
    DateTime FechaRegistro,
    string? Observacion,
    string? OrigenDispositivo);

public sealed record ControlGeneracionUsuarioDto(
    Guid ControlGeneracionUsuarioId,
    Guid UsuarioId,
    string NombreUsuario,
    string RolControl,
    bool EsPrincipal,
    DateTime FechaDesde,
    DateTime? FechaHasta,
    bool EsActivo);

public sealed record ControlGeneracionListItemDto(
    Guid ControlGeneracionId,
    string Codigo,
    Guid SedeId,
    string Sede,
    Guid EmpresaResponsableId,
    string EmpresaResponsable,
    Guid ProyectoId,
    string Proyecto,
    Guid ActividadId,
    string Actividad,
    DateTime FechaInicio,
    DateTime? FechaFin,
    string Estado,
    int CantidadRegistradores,
    int CantidadRegistros);

public sealed record ControlGeneracionDetalleDto(
    Guid ControlGeneracionId,
    string Codigo,
    Guid SedeId,
    string Sede,
    Guid EmpresaResponsableId,
    string EmpresaResponsable,
    Guid ProyectoId,
    string Proyecto,
    Guid ActividadId,
    string Actividad,
    Guid? PuntoGeneracionId,
    string? PuntoGeneracion,
    string? DescripcionTrabajo,
    DateTime FechaInicio,
    DateTime? FechaFin,
    string Estado,
    string? Observacion,
    string? MotivoUltimoCambio,
    Guid CreadoPorUsuarioId,
    string CreadoPor,
    Guid? AprobadoPorUsuarioId,
    string? AprobadoPor,
    DateTime? AprobadoUtc,
    IReadOnlyCollection<ControlGeneracionUsuarioDto> Usuarios);

public sealed record RegistroControlListItemDto(
    Guid RegistroId,
    DateTime FechaRegistro,
    Guid RegistradoPorUsuarioId,
    string RegistradoPor,
    string Estado,
    int CantidadResiduos,
    DateTime CreadoUtc);

public sealed record RegistroControlDetalleDto(
    Guid RegistroId,
    Guid ControlGeneracionId,
    string? CodigoLocal,
    DateTime FechaRegistro,
    Guid RegistradoPorUsuarioId,
    string RegistradoPor,
    string Estado,
    string? Observacion,
    IReadOnlyCollection<RegistroResiduoDto> Residuos);

public sealed record RegistroControlCreadoDto(
    Guid RegistroId,
    Guid ControlGeneracionId,
    DateTime FechaRegistro,
    string Estado);
