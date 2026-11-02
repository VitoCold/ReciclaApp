using Microsoft.EntityFrameworkCore;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Persistence;

namespace ReciclaApi.Application.Services;

public interface IControlAccessService
{
    Task<bool> TieneRolAsync(Guid usuarioId, string rol, CancellationToken cancellationToken = default);
    Task<bool> EsGlobalAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task<bool> TieneAsignacionVigenteAsync(
        Guid controlId,
        Guid usuarioId,
        IReadOnlyCollection<string>? rolesControl = null,
        CancellationToken cancellationToken = default);
    Task<bool> EsResponsableVigenteAsync(Guid controlId, Guid usuarioId, CancellationToken cancellationToken = default);
    Task<bool> EsParticipanteOperativoVigenteAsync(Guid controlId, Guid usuarioId, CancellationToken cancellationToken = default);
    Task<bool> EsResponsableDeTodosAsync(IEnumerable<Guid> controlIds, Guid usuarioId, CancellationToken cancellationToken = default);
}

public sealed class ControlAccessService(ReciclaDbContext context) : IControlAccessService
{
    private static readonly string[] RolesOperativos = ["RESPONSABLE", "REGISTRADOR"];

    public Task<bool> TieneRolAsync(Guid usuarioId, string rol, CancellationToken cancellationToken = default) =>
        context.UsuarioRoles
            .AsNoTracking()
            .AnyAsync(x =>
                x.UsuarioId == usuarioId &&
                x.Rol.EsActivo &&
                x.Rol.Codigo == rol,
                cancellationToken);

    public Task<bool> EsGlobalAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        context.UsuarioRoles
            .AsNoTracking()
            .AnyAsync(x =>
                x.UsuarioId == usuarioId &&
                x.Rol.EsActivo &&
                (x.Rol.Codigo == "ADMINISTRADOR" || x.Rol.Codigo == "AMBIENTAL"),
                cancellationToken);

    public Task<bool> TieneAsignacionVigenteAsync(
        Guid controlId,
        Guid usuarioId,
        IReadOnlyCollection<string>? rolesControl = null,
        CancellationToken cancellationToken = default)
    {
        var hoy = DateTime.UtcNow.Date;
        var query = context.Set<ControlGeneracionUsuario>()
            .AsNoTracking()
            .Where(x =>
                x.ControlGeneracionId == controlId &&
                x.UsuarioId == usuarioId &&
                x.EsActivo &&
                x.FechaDesde <= hoy &&
                (!x.FechaHasta.HasValue || x.FechaHasta.Value >= hoy));

        if (rolesControl is not null && rolesControl.Count > 0)
            query = query.Where(x => rolesControl.Contains(x.RolControl));

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> EsResponsableVigenteAsync(
        Guid controlId,
        Guid usuarioId,
        CancellationToken cancellationToken = default) =>
        TieneAsignacionVigenteAsync(controlId, usuarioId, ["RESPONSABLE"], cancellationToken);

    public Task<bool> EsParticipanteOperativoVigenteAsync(
        Guid controlId,
        Guid usuarioId,
        CancellationToken cancellationToken = default) =>
        TieneAsignacionVigenteAsync(controlId, usuarioId, RolesOperativos, cancellationToken);

    public async Task<bool> EsResponsableDeTodosAsync(
        IEnumerable<Guid> controlIds,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var ids = controlIds.Distinct().ToArray();
        if (ids.Length == 0)
            return false;

        var hoy = DateTime.UtcNow.Date;
        var autorizados = await context.Set<ControlGeneracionUsuario>()
            .AsNoTracking()
            .Where(x =>
                ids.Contains(x.ControlGeneracionId) &&
                x.UsuarioId == usuarioId &&
                x.RolControl == "RESPONSABLE" &&
                x.EsActivo &&
                x.FechaDesde <= hoy &&
                (!x.FechaHasta.HasValue || x.FechaHasta.Value >= hoy))
            .Select(x => x.ControlGeneracionId)
            .Distinct()
            .CountAsync(cancellationToken);

        return autorizados == ids.Length;
    }
}
