using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Application.Security;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Repositories;

namespace ReciclaApi.Application.Services;

public interface IAuthService
{
    Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<UsuarioDto>> MeAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public sealed class AuthService(
    IUnitOfWork unitOfWork,
    IPasswordService passwordService,
    IJwtTokenService jwtTokenService) : IAuthService
{
    public async Task<ServiceResult<LoginResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Usuario) || string.IsNullOrWhiteSpace(request.Password))
            return ServiceResult<LoginResponse>.Fail("Usuario y contraseña son obligatorios.", StatusCodes.Status400BadRequest);

        var usuario = await unitOfWork.Repository<Usuario>()
            .Query(tracking: true)
            .Include(x => x.UsuarioRoles)
                .ThenInclude(x => x.Rol)
            .Include(x => x.UsuarioSedes)
            .FirstOrDefaultAsync(
                x => x.UsuarioNombre == request.Usuario && x.EsActivo,
                cancellationToken);

        if (usuario?.PasswordHash is null || usuario.PasswordSalt is null ||
            !passwordService.Verify(request.Password, usuario.PasswordHash, usuario.PasswordSalt))
            return ServiceResult<LoginResponse>.Fail("Credenciales inválidas.", StatusCodes.Status401Unauthorized);

        usuario.UltimoAccesoUtc = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var token = jwtTokenService.Create(usuario);
        return ServiceResult<LoginResponse>.Ok(new LoginResponse(
            token.Token,
            token.ExpiraUtc,
            MapUsuario(usuario)));
    }

    public async Task<ServiceResult<UsuarioDto>> MeAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var usuario = await unitOfWork.Repository<Usuario>()
            .Query()
            .Include(x => x.UsuarioRoles)
                .ThenInclude(x => x.Rol)
            .Include(x => x.UsuarioSedes)
            .FirstOrDefaultAsync(x => x.UsuarioId == usuarioId && x.EsActivo, cancellationToken);

        return usuario is null
            ? ServiceResult<UsuarioDto>.Fail("Usuario no encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<UsuarioDto>.Ok(MapUsuario(usuario));
    }

    private static UsuarioDto MapUsuario(Usuario usuario) => new(
        usuario.UsuarioId,
        usuario.UsuarioNombre,
        usuario.Nombres,
        usuario.Apellidos,
        usuario.Email,
        usuario.UsuarioRoles.Where(x => x.Rol.EsActivo).Select(x => x.Rol.Codigo).ToArray(),
        usuario.UsuarioSedes.Select(x => x.SedeId).ToArray());
}

public interface ICatalogoService
{
    Task<CatalogosInicialDto> ObtenerInicialAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public sealed class CatalogoService(IUnitOfWork unitOfWork) : ICatalogoService
{
    public async Task<CatalogosInicialDto> ObtenerInicialAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var rolesUsuario = await unitOfWork.Repository<UsuarioRol>().Query()
            .Where(x => x.UsuarioId == usuarioId && x.Rol.EsActivo)
            .Select(x => x.Rol.Codigo)
            .ToListAsync(cancellationToken);

        var accesoGlobal = rolesUsuario.Contains("AMBIENTAL") || rolesUsuario.Contains("ADMINISTRADOR");

        var sedeIds = accesoGlobal
            ? await unitOfWork.Repository<Sede>().Query()
                .Where(x => x.EsActivo)
                .Select(x => x.SedeId)
                .ToListAsync(cancellationToken)
            : await unitOfWork.Repository<UsuarioSede>().Query()
                .Where(x => x.UsuarioId == usuarioId)
                .Select(x => x.SedeId)
                .ToListAsync(cancellationToken);

        var sedes = await unitOfWork.Repository<Sede>().Query()
            .Where(x => x.EsActivo && sedeIds.Contains(x.SedeId))
            .OrderBy(x => x.Nombre)
            .Select(x => new SedeDto(x.SedeId, x.Codigo, x.Nombre))
            .ToListAsync(cancellationToken);

        var proyectos = await unitOfWork.Repository<Proyecto>().Query()
            .Where(x => x.EsActivo)
            .OrderBy(x => x.Nombre)
            .Select(x => new ProyectoDto(x.ProyectoId, x.Codigo, x.Nombre))
            .ToListAsync(cancellationToken);

        var actividades = await unitOfWork.Repository<Actividad>().Query()
            .Where(x => x.EsActivo)
            .OrderBy(x => x.Nombre)
            .Select(x => new ActividadDto(x.ActividadId, x.ProyectoId, x.Codigo, x.Nombre))
            .ToListAsync(cancellationToken);

        var clasificaciones = await unitOfWork.Repository<ClasificacionResiduo>().Query()
            .OrderBy(x => x.Nombre)
            .Select(x => new ClasificacionResiduoDto(x.ClasificacionResiduoId, x.Codigo, x.Nombre, x.ColorHex))
            .ToListAsync(cancellationToken);

        var tipos = await unitOfWork.Repository<TipoResiduo>().Query()
            .Where(x => x.EsActivo)
            .OrderBy(x => x.Nombre)
            .Select(x => new TipoResiduoDto(x.TipoResiduoId, x.Codigo, x.Nombre))
            .ToListAsync(cancellationToken);

        var unidades = await unitOfWork.Repository<UnidadMedida>().Query()
            .OrderBy(x => x.Nombre)
            .Select(x => new UnidadMedidaDto(x.UnidadMedidaId, x.Codigo, x.Nombre))
            .ToListAsync(cancellationToken);

        var residuos = await unitOfWork.Repository<ResiduoCatalogo>().Query()
            .Where(x => x.EsActivo)
            .OrderBy(x => x.Nombre)
            .Select(x => new ResiduoCatalogoDto(
                x.ResiduoId,
                x.TipoResiduoId,
                x.ClasificacionResiduoId,
                x.Codigo,
                x.Nombre,
                x.Descripcion,
                x.UnidadMedidaDefaultId))
            .ToListAsync(cancellationToken);

        var empresas = await unitOfWork.Repository<Empresa>().Query()
            .Where(x => x.EsActivo)
            .OrderBy(x => x.RazonSocial)
            .Select(x => new EmpresaDto(
                x.EmpresaId,
                x.Codigo,
                x.RazonSocial,
                x.NombreComercial,
                x.EsGestoraResiduos))
            .ToListAsync(cancellationToken);

        var puntosResiduo = await unitOfWork.Repository<PuntoResiduo>().Query()
            .Where(x => x.EsActivo && sedeIds.Contains(x.SedeId))
            .OrderBy(x => x.Nombre)
            .Select(x => new PuntoResiduoDto(
                x.PuntoResiduoId,
                x.SedeId,
                x.Codigo,
                x.Nombre,
                x.Tipo))
            .ToListAsync(cancellationToken);

        var usuarios = await unitOfWork.Repository<Usuario>().Query()
            .Include(x => x.UsuarioRoles)
                .ThenInclude(x => x.Rol)
            .Where(x => x.EsActivo && x.UsuarioRoles.Any(r =>
                r.Rol.EsActivo && (r.Rol.Codigo == "RESPONSABLE_OPERATIVO" || r.Rol.Codigo == "REGISTRADOR")))
            .OrderBy(x => x.Nombres)
            .ThenBy(x => x.Apellidos)
            .ToListAsync(cancellationToken);

        var usuariosAsignables = usuarios
            .Select(x => new UsuarioAsignableDto(
                x.UsuarioId,
                string.Join(" ", new[] { x.Nombres, x.Apellidos }.Where(n => !string.IsNullOrWhiteSpace(n))),
                x.UsuarioRoles.Where(r => r.Rol.EsActivo).Select(r => r.Rol.Codigo).ToArray()))
            .ToArray();

        return new CatalogosInicialDto(
            sedes,
            proyectos,
            actividades,
            clasificaciones,
            tipos,
            unidades,
            residuos,
            empresas,
            puntosResiduo,
            usuariosAsignables);
    }
}
