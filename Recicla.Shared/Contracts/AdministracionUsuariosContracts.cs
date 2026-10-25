namespace Recicla.Shared.Contracts;

public sealed record AdministracionRolDto(
    int RolId,
    string Codigo,
    string Nombre);

public sealed record AdministracionSedeDto(
    Guid SedeId,
    string Codigo,
    string Nombre);

public sealed record AdministracionUsuariosCatalogosDto(
    IReadOnlyCollection<AdministracionRolDto> Roles,
    IReadOnlyCollection<AdministracionSedeDto> Sedes);

public sealed record AdministracionUsuarioDto(
    Guid UsuarioId,
    string Usuario,
    string Nombres,
    string? Apellidos,
    string? Email,
    bool EsActivo,
    bool DebeCambiarClave,
    DateTime? UltimoAccesoUtc,
    IReadOnlyCollection<AdministracionRolDto> Roles,
    IReadOnlyCollection<AdministracionSedeDto> Sedes);

public sealed record GuardarAdministracionUsuarioRequest(
    string Usuario,
    string Nombres,
    string? Apellidos,
    string? Email,
    IReadOnlyCollection<int> RolIds,
    IReadOnlyCollection<Guid> SedeIds,
    string? Password);

public sealed record CambiarEstadoAdministracionUsuarioRequest(bool EsActivo);

public sealed record RestablecerClaveAdministracionUsuarioRequest(string Password);
