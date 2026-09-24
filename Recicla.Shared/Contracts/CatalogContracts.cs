namespace Recicla.Shared.Contracts;

public sealed record SedeDto(Guid SedeId, string Codigo, string Nombre);

public sealed record ProyectoDto(Guid ProyectoId, string Codigo, string Nombre);

public sealed record ActividadDto(Guid ActividadId, Guid? ProyectoId, string Codigo, string Nombre);

public sealed record ClasificacionResiduoDto(int ClasificacionResiduoId, string Codigo, string Nombre, string? ColorHex);

public sealed record TipoResiduoDto(Guid TipoResiduoId, string Codigo, string Nombre);

public sealed record UnidadMedidaDto(int UnidadMedidaId, string Codigo, string Nombre);

public sealed record ResiduoCatalogoDto(
    Guid ResiduoId,
    Guid TipoResiduoId,
    int ClasificacionResiduoId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    int UnidadMedidaDefaultId);

public sealed record CatalogosInicialDto(
    IReadOnlyCollection<SedeDto> Sedes,
    IReadOnlyCollection<ProyectoDto> Proyectos,
    IReadOnlyCollection<ActividadDto> Actividades,
    IReadOnlyCollection<ClasificacionResiduoDto> Clasificaciones,
    IReadOnlyCollection<TipoResiduoDto> TiposResiduo,
    IReadOnlyCollection<UnidadMedidaDto> UnidadesMedida,
    IReadOnlyCollection<ResiduoCatalogoDto> Residuos);
