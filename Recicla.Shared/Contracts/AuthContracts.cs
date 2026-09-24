namespace Recicla.Shared.Contracts;

public sealed record LoginRequest(string Usuario, string Password);

public sealed record UsuarioDto(
    Guid UsuarioId,
    string Usuario,
    string Nombres,
    string? Apellidos,
    string? Email,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<Guid> SedeIds);

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiraUtc,
    UsuarioDto Usuario);
