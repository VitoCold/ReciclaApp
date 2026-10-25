namespace Recicla.Shared.Contracts;

public sealed record AdministracionRolDto(
    int RolId,
    string Codigo,
    string Nombre);

public sealed record AdministracionUsuariosCatalogosDto(
    IReadOnlyCollection<AdministracionRolDto> Roles);

public sealed record AdministracionUsuarioDto(
    Guid UsuarioId,
    string Usuario,
    string Nombres,
    string? Apellidos,
    string? Email,
    bool EsActivo,
    bool DebeCambiarClave,
    DateTime? UltimoAccesoUtc,
    IReadOnlyCollection<AdministracionRolDto> Roles);

public sealed record GuardarAdministracionUsuarioRequest(
    string Usuario,
    string Nombres,
    string? Apellidos,
    string? Email,
    IReadOnlyCollection<int> RolIds,
    string? Password);

public sealed record CambiarEstadoAdministracionUsuarioRequest(bool EsActivo);

public sealed record RestablecerClaveAdministracionUsuarioRequest(string Password);
