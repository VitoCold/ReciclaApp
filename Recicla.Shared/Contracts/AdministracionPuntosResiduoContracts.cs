namespace Recicla.Shared.Contracts;

public sealed record AdministracionPuntoResiduoDto(
    Guid PuntoResiduoId,
    Guid SedeId,
    string SedeCodigo,
    string SedeNombre,
    string Codigo,
    string Nombre,
    string Tipo,
    bool EsActivo,
    bool TieneUsoHistorico,
    int ControlesAbiertos,
    bool TieneSaldoAlmacenado);

public sealed record AdministracionPuntosCatalogosDto(
    IReadOnlyCollection<SedeDto> Sedes,
    IReadOnlyCollection<string> Tipos);

public sealed record GuardarAdministracionPuntoResiduoRequest(
    Guid SedeId,
    string Codigo,
    string Nombre,
    string Tipo);

public sealed record CambiarEstadoAdministracionPuntoResiduoRequest(bool EsActivo);
