namespace Recicla.Shared.Contracts;

public sealed record AdministracionTipoResiduoDto(
    Guid TipoResiduoId,
    string Codigo,
    string Nombre,
    bool EsActivo,
    int ResiduosActivos,
    bool TieneUsoHistorico);

public sealed record GuardarAdministracionTipoResiduoRequest(
    string Codigo,
    string Nombre);

public sealed record CambiarEstadoAdministracionTipoResiduoRequest(bool EsActivo);

public sealed record AdministracionResiduoCatalogoDto(
    Guid ResiduoId,
    Guid TipoResiduoId,
    string TipoCodigo,
    string TipoNombre,
    int ClasificacionResiduoId,
    string ClasificacionCodigo,
    string ClasificacionNombre,
    string Codigo,
    string Nombre,
    string? Descripcion,
    int UnidadMedidaDefaultId,
    string UnidadCodigo,
    string UnidadNombre,
    bool EsActivo,
    bool TieneUsoHistorico);

public sealed record AdministracionResiduosCatalogosDto(
    IReadOnlyCollection<TipoResiduoDto> TiposResiduo,
    IReadOnlyCollection<ClasificacionResiduoDto> Clasificaciones,
    IReadOnlyCollection<UnidadMedidaDto> UnidadesMedida);

public sealed record GuardarAdministracionResiduoCatalogoRequest(
    Guid TipoResiduoId,
    int ClasificacionResiduoId,
    string Codigo,
    string Nombre,
    string? Descripcion,
    int UnidadMedidaDefaultId);

public sealed record CambiarEstadoAdministracionResiduoCatalogoRequest(bool EsActivo);
