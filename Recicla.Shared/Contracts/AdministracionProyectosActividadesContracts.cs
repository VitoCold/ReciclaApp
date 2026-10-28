namespace Recicla.Shared.Contracts;

public sealed record AdministracionProyectoDto(
    Guid ProyectoId,
    string Codigo,
    string Nombre,
    bool EsActivo,
    int ActividadesActivas,
    int ControlesAbiertos,
    bool TieneUsoHistorico);

public sealed record GuardarAdministracionProyectoRequest(
    string Codigo,
    string Nombre);

public sealed record CambiarEstadoAdministracionProyectoRequest(bool EsActivo);

public sealed record AdministracionActividadDto(
    Guid ActividadId,
    Guid? ProyectoId,
    string? ProyectoCodigo,
    string? ProyectoNombre,
    string Codigo,
    string Nombre,
    bool EsActivo,
    int ControlesAbiertos,
    bool TieneUsoHistorico);

public sealed record AdministracionActividadesCatalogosDto(
    IReadOnlyCollection<ProyectoDto> Proyectos);

public sealed record GuardarAdministracionActividadRequest(
    Guid ProyectoId,
    string Codigo,
    string Nombre);

public sealed record CambiarEstadoAdministracionActividadRequest(bool EsActivo);
