using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Application.Security;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Persistence;

namespace ReciclaApi.Application.Services;

public interface IAdministracionUsuarioService
{
    Task<IReadOnlyCollection<AdministracionUsuarioDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionUsuarioDto>> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task<AdministracionUsuariosCatalogosDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionUsuarioDto>> CrearAsync(
        Guid actorUsuarioId,
        GuardarAdministracionUsuarioRequest request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionUsuarioDto>> ActualizarAsync(
        Guid usuarioId,
        Guid actorUsuarioId,
        GuardarAdministracionUsuarioRequest request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionUsuarioDto>> CambiarEstadoAsync(
        Guid usuarioId,
        Guid actorUsuarioId,
        bool esActivo,
        CancellationToken cancellationToken = default);
    Task<ServiceResult> RestablecerClaveAsync(
        Guid usuarioId,
        Guid actorUsuarioId,
        string password,
        CancellationToken cancellationToken = default);
}

public sealed class AdministracionUsuarioService(
    ReciclaDbContext context,
    IPasswordService passwordService) : IAdministracionUsuarioService
{
    private static readonly string[] RolesAdministrables =
    [
        "ADMINISTRADOR",
        "AMBIENTAL",
        "RESPONSABLE_OPERATIVO",
        "REGISTRADOR"
    ];

    public async Task<IReadOnlyCollection<AdministracionUsuarioDto>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        var usuarios = await QueryUsuarios()
            .OrderByDescending(x => x.EsActivo)
            .ThenBy(x => x.Nombres)
            .ThenBy(x => x.Apellidos)
            .ToListAsync(cancellationToken);

        return usuarios.Select(Map).ToArray();
    }

    public async Task<ServiceResult<AdministracionUsuarioDto>> ObtenerAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var usuario = await QueryUsuarios()
            .FirstOrDefaultAsync(x => x.UsuarioId == usuarioId, cancellationToken);

        return usuario is null
            ? ServiceResult<AdministracionUsuarioDto>.Fail("Usuario no encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<AdministracionUsuarioDto>.Ok(Map(usuario));
    }

    public async Task<AdministracionUsuariosCatalogosDto> ObtenerCatalogosAsync(
        CancellationToken cancellationToken = default)
    {
        var roles = await context.Roles
            .AsNoTracking()
            .Where(x => x.EsActivo && RolesAdministrables.Contains(x.Codigo))
            .OrderBy(x => x.Nombre)
            .Select(x => new AdministracionRolDto(x.RolId, x.Codigo, x.Nombre))
            .ToListAsync(cancellationToken);

        return new AdministracionUsuariosCatalogosDto(roles);
    }

    public async Task<ServiceResult<AdministracionUsuarioDto>> CrearAsync(
        Guid actorUsuarioId,
        GuardarAdministracionUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarRequestAsync(request, null, true, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionUsuarioDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var roles = await context.Roles
            .Where(x => request.RolIds.Distinct().Contains(x.RolId) && x.EsActivo && RolesAdministrables.Contains(x.Codigo))
            .ToListAsync(cancellationToken);

        var (hash, salt) = passwordService.Hash(request.Password!.Trim());

        var usuario = new Usuario
        {
            UsuarioNombre = request.Usuario.Trim(),
            Nombres = request.Nombres.Trim(),
            Apellidos = Limpiar(request.Apellidos),
            Email = Limpiar(request.Email),
            PasswordHash = hash,
            PasswordSalt = salt,
            DebeCambiarClave = true,
            EsActivo = true
        };

        foreach (var rol in roles)
            usuario.UsuarioRoles.Add(new UsuarioRol { Usuario = usuario, Rol = rol });

        context.Usuarios.Add(usuario);
        context.Auditoria.Add(new Auditoria
        {
            UsuarioId = actorUsuarioId,
            Entidad = "Usuario",
            EntidadId = usuario.UsuarioId,
            Accion = "CREAR",
            DatosDespuesJson = JsonSerializer.Serialize(new
            {
                usuario.UsuarioNombre,
                usuario.Nombres,
                usuario.Apellidos,
                usuario.Email,
                Roles = roles.Select(x => x.Codigo)
            })
        });

        await context.SaveChangesAsync(cancellationToken);
        var recargado = await QueryUsuarios().FirstAsync(x => x.UsuarioId == usuario.UsuarioId, cancellationToken);
        return ServiceResult<AdministracionUsuarioDto>.Ok(Map(recargado), StatusCodes.Status201Created);
    }

    public async Task<ServiceResult<AdministracionUsuarioDto>> ActualizarAsync(
        Guid usuarioId,
        Guid actorUsuarioId,
        GuardarAdministracionUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarRequestAsync(request, usuarioId, false, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionUsuarioDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var usuario = await context.Usuarios
            .Include(x => x.UsuarioRoles)
            .FirstOrDefaultAsync(x => x.UsuarioId == usuarioId, cancellationToken);

        if (usuario is null)
            return ServiceResult<AdministracionUsuarioDto>.Fail("Usuario no encontrado.", StatusCodes.Status404NotFound);

        var roles = await context.Roles
            .Where(x => request.RolIds.Distinct().Contains(x.RolId) && x.EsActivo && RolesAdministrables.Contains(x.Codigo))
            .ToListAsync(cancellationToken);

        var eraAdministrador = await context.UsuarioRoles
            .AsNoTracking()
            .AnyAsync(x =>
                x.UsuarioId == usuarioId &&
                x.Rol.EsActivo &&
                x.Rol.Codigo == "ADMINISTRADOR",
                cancellationToken);
        var seguiraAdministrador = roles.Any(x => x.Codigo == "ADMINISTRADOR");

        if (usuario.EsActivo && eraAdministrador && !seguiraAdministrador &&
            !await ExisteOtroAdministradorActivoAsync(usuarioId, cancellationToken))
        {
            return ServiceResult<AdministracionUsuarioDto>.Fail(
                "No puedes quitar el rol ADMINISTRADOR al último administrador activo.",
                StatusCodes.Status409Conflict);
        }

        var antes = new
        {
            usuario.UsuarioNombre,
            usuario.Nombres,
            usuario.Apellidos,
            usuario.Email,
            Roles = usuario.UsuarioRoles.Select(x => x.RolId).ToArray()
        };

        usuario.UsuarioNombre = request.Usuario.Trim();
        usuario.Nombres = request.Nombres.Trim();
        usuario.Apellidos = Limpiar(request.Apellidos);
        usuario.Email = Limpiar(request.Email);
        usuario.ActualizadoUtc = DateTime.UtcNow;

        var rolIdsDeseados = roles.Select(x => x.RolId).ToHashSet();
        var rolesAEliminar = usuario.UsuarioRoles
            .Where(x => !rolIdsDeseados.Contains(x.RolId))
            .ToArray();
        context.UsuarioRoles.RemoveRange(rolesAEliminar);

        var rolIdsActuales = usuario.UsuarioRoles.Select(x => x.RolId).ToHashSet();
        foreach (var rol in roles.Where(x => !rolIdsActuales.Contains(x.RolId)))
            usuario.UsuarioRoles.Add(new UsuarioRol { UsuarioId = usuario.UsuarioId, RolId = rol.RolId });

        context.Auditoria.Add(new Auditoria
        {
            UsuarioId = actorUsuarioId,
            Entidad = "Usuario",
            EntidadId = usuario.UsuarioId,
            Accion = "EDITAR",
            DatosAntesJson = JsonSerializer.Serialize(antes),
            DatosDespuesJson = JsonSerializer.Serialize(new
            {
                usuario.UsuarioNombre,
                usuario.Nombres,
                usuario.Apellidos,
                usuario.Email,
                Roles = roles.Select(x => x.RolId).ToArray()
            })
        });

        await context.SaveChangesAsync(cancellationToken);
        var recargado = await QueryUsuarios().FirstAsync(x => x.UsuarioId == usuario.UsuarioId, cancellationToken);
        return ServiceResult<AdministracionUsuarioDto>.Ok(Map(recargado));
    }

    public async Task<ServiceResult<AdministracionUsuarioDto>> CambiarEstadoAsync(
        Guid usuarioId,
        Guid actorUsuarioId,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        if (usuarioId == actorUsuarioId && !esActivo)
            return ServiceResult<AdministracionUsuarioDto>.Fail(
                "No puedes desactivar tu propio usuario mientras tienes la sesión iniciada.",
                StatusCodes.Status409Conflict);

        var usuario = await context.Usuarios
            .FirstOrDefaultAsync(x => x.UsuarioId == usuarioId, cancellationToken);

        if (usuario is null)
            return ServiceResult<AdministracionUsuarioDto>.Fail("Usuario no encontrado.", StatusCodes.Status404NotFound);

        if (!esActivo)
        {
            var esAdministrador = await context.UsuarioRoles
                .AsNoTracking()
                .AnyAsync(x =>
                    x.UsuarioId == usuarioId &&
                    x.Rol.EsActivo &&
                    x.Rol.Codigo == "ADMINISTRADOR",
                    cancellationToken);

            if (esAdministrador && !await ExisteOtroAdministradorActivoAsync(usuarioId, cancellationToken))
            {
                return ServiceResult<AdministracionUsuarioDto>.Fail(
                    "No puedes desactivar al último administrador activo.",
                    StatusCodes.Status409Conflict);
            }
        }

        usuario.EsActivo = esActivo;
        usuario.ActualizadoUtc = DateTime.UtcNow;

        context.Auditoria.Add(new Auditoria
        {
            UsuarioId = actorUsuarioId,
            Entidad = "Usuario",
            EntidadId = usuario.UsuarioId,
            Accion = esActivo ? "ACTIVAR" : "DESACTIVAR"
        });

        await context.SaveChangesAsync(cancellationToken);
        var recargado = await QueryUsuarios().FirstAsync(x => x.UsuarioId == usuario.UsuarioId, cancellationToken);
        return ServiceResult<AdministracionUsuarioDto>.Ok(Map(recargado));
    }

    public async Task<ServiceResult> RestablecerClaveAsync(
        Guid usuarioId,
        Guid actorUsuarioId,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Trim().Length < 8)
            return ServiceResult.Fail("La contraseña temporal debe tener al menos 8 caracteres.", StatusCodes.Status400BadRequest);

        var usuario = await context.Usuarios
            .FirstOrDefaultAsync(x => x.UsuarioId == usuarioId, cancellationToken);

        if (usuario is null)
            return ServiceResult.Fail("Usuario no encontrado.", StatusCodes.Status404NotFound);

        var (hash, salt) = passwordService.Hash(password.Trim());
        usuario.PasswordHash = hash;
        usuario.PasswordSalt = salt;
        usuario.DebeCambiarClave = true;
        usuario.ActualizadoUtc = DateTime.UtcNow;

        context.Auditoria.Add(new Auditoria
        {
            UsuarioId = actorUsuarioId,
            Entidad = "Usuario",
            EntidadId = usuario.UsuarioId,
            Accion = "RESTABLECER_CLAVE"
        });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    private async Task<string?> ValidarRequestAsync(
        GuardarAdministracionUsuarioRequest request,
        Guid? usuarioId,
        bool requierePassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Usuario))
            return "El nombre de usuario es obligatorio.";
        if (string.IsNullOrWhiteSpace(request.Nombres))
            return "Los nombres son obligatorios.";
        if (requierePassword && (string.IsNullOrWhiteSpace(request.Password) || request.Password.Trim().Length < 8))
            return "La contraseña temporal debe tener al menos 8 caracteres.";
        if (request.RolIds is null || request.RolIds.Count == 0)
            return "Selecciona al menos un rol.";

        var usuarioNormalizado = request.Usuario.Trim().ToLower();
        var duplicado = await context.Usuarios
            .AsNoTracking()
            .AnyAsync(x =>
                x.UsuarioNombre.ToLower() == usuarioNormalizado &&
                (!usuarioId.HasValue || x.UsuarioId != usuarioId.Value),
                cancellationToken);
        if (duplicado)
            return "El nombre de usuario ya existe.";

        var roles = await context.Roles
            .AsNoTracking()
            .Where(x => request.RolIds.Distinct().Contains(x.RolId) && x.EsActivo && RolesAdministrables.Contains(x.Codigo))
            .ToListAsync(cancellationToken);

        if (roles.Count != request.RolIds.Distinct().Count())
            return "Uno o más roles seleccionados no son válidos.";

        return null;
    }

    private Task<bool> ExisteOtroAdministradorActivoAsync(
        Guid excluirUsuarioId,
        CancellationToken cancellationToken) =>
        context.UsuarioRoles
            .AsNoTracking()
            .AnyAsync(x =>
                x.UsuarioId != excluirUsuarioId &&
                x.Usuario.EsActivo &&
                x.Rol.EsActivo &&
                x.Rol.Codigo == "ADMINISTRADOR",
                cancellationToken);

    private IQueryable<Usuario> QueryUsuarios() =>
        context.Usuarios
            .AsNoTracking()
            .Include(x => x.UsuarioRoles).ThenInclude(x => x.Rol);

    private static AdministracionUsuarioDto Map(Usuario usuario) => new(
        usuario.UsuarioId,
        usuario.UsuarioNombre,
        usuario.Nombres,
        usuario.Apellidos,
        usuario.Email,
        usuario.EsActivo,
        usuario.DebeCambiarClave,
        usuario.UltimoAccesoUtc,
        usuario.UsuarioRoles
            .Where(x => x.Rol.EsActivo && RolesAdministrables.Contains(x.Rol.Codigo))
            .OrderBy(x => x.Rol.Nombre)
            .Select(x => new AdministracionRolDto(x.RolId, x.Rol.Codigo, x.Rol.Nombre))
            .ToArray());

    private static string? Limpiar(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
