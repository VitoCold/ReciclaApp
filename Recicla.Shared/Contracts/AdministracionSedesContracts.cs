namespace Recicla.Shared.Contracts;

public sealed record AdministracionSedeDto(
    Guid SedeId,
    string Codigo,
    string Nombre,
    bool EsActivo,
    int PuntosActivos,
    int ControlesAbiertos);

public sealed record GuardarAdministracionSedeRequest(
    string Codigo,
    string Nombre);

public sealed record CambiarEstadoAdministracionSedeRequest(bool EsActivo);
