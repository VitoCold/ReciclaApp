using Microsoft.EntityFrameworkCore;
using ReciclaApi.Domain;

namespace ReciclaApi.Infrastructure.Persistence;

public static class ControlGeneracionSeeder
{
    public static async Task SeedAsync(ReciclaDbContext context, CancellationToken cancellationToken = default)
    {
        await EnsureRolesAsync(context, cancellationToken);
        await NormalizeLegacyStatesAsync(context, cancellationToken);
        await EnsureStatesAsync(context, cancellationToken);
        await EnsureBaseCompanyAsync(context, cancellationToken);
        await MigrateLegacyRoleAssignmentsAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureRolesAsync(ReciclaDbContext context, CancellationToken cancellationToken)
    {
        var roles = await context.Roles.Select(x => x.Codigo).ToListAsync(cancellationToken);

        if (!roles.Contains("AMBIENTAL"))
            context.Roles.Add(new Rol { Codigo = "AMBIENTAL", Nombre = "Ambiental", Descripcion = "Aprueba y supervisa todos los controles de generación." });

        if (!roles.Contains("RESPONSABLE_OPERATIVO"))
            context.Roles.Add(new Rol { Codigo = "RESPONSABLE_OPERATIVO", Nombre = "Responsable operativo", Descripcion = "Crea controles de generación y administra el personal asignado." });

        if (!roles.Contains("REGISTRADOR"))
            context.Roles.Add(new Rol { Codigo = "REGISTRADOR", Nombre = "Registrador", Descripcion = "Registra residuos en controles de generación donde está asignado." });

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task NormalizeLegacyStatesAsync(ReciclaDbContext context, CancellationToken cancellationToken)
    {
        var mappings = new (string Legacy, string Current, string Nombre)[]
        {
            ("PENDIENTE_APROBACION", "PENDIENTE_REVISION", "Pendiente de revisión"),
            ("RECHAZADO", "OBSERVADO", "Observado"),
            ("CERRADO", "FINALIZADO", "Finalizado")
        };

        foreach (var (legacyCode, currentCode, currentName) in mappings)
        {
            var legacy = await context.Set<EstadoControlGeneracion>()
                .FirstOrDefaultAsync(x => x.Codigo == legacyCode, cancellationToken);
            if (legacy is null)
                continue;

            var current = await context.Set<EstadoControlGeneracion>()
                .FirstOrDefaultAsync(x => x.Codigo == currentCode, cancellationToken);

            if (current is null)
            {
                legacy.Codigo = currentCode;
                legacy.Nombre = currentName;
                continue;
            }

            var controles = await context.Set<ControlGeneracion>()
                .Where(x => x.EstadoControlGeneracionId == legacy.EstadoControlGeneracionId)
                .ToListAsync(cancellationToken);

            foreach (var control in controles)
                control.EstadoControlGeneracionId = current.EstadoControlGeneracionId;

            context.Set<EstadoControlGeneracion>().Remove(legacy);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureStatesAsync(ReciclaDbContext context, CancellationToken cancellationToken)
    {
        var states = await context.Set<EstadoControlGeneracion>()
            .Select(x => x.Codigo)
            .ToListAsync(cancellationToken);

        AddStateIfMissing(context, states, "BORRADOR", "Borrador");
        AddStateIfMissing(context, states, "PENDIENTE_REVISION", "Pendiente de revisión");
        AddStateIfMissing(context, states, "OBSERVADO", "Observado");
        AddStateIfMissing(context, states, "APROBADO", "Aprobado");
        AddStateIfMissing(context, states, "ACTIVO", "Activo");
        AddStateIfMissing(context, states, "FINALIZADO", "Finalizado");

        // Estados excepcionales fuera del flujo normal.
        AddStateIfMissing(context, states, "SUSPENDIDO", "Suspendido");
        AddStateIfMissing(context, states, "ANULADO", "Anulado");
    }

    private static async Task EnsureBaseCompanyAsync(ReciclaDbContext context, CancellationToken cancellationToken)
    {
        if (await context.Empresas.AnyAsync(x => x.Codigo == "CONTUGAS", cancellationToken))
            return;

        context.Empresas.Add(new Empresa
        {
            Codigo = "CONTUGAS",
            RazonSocial = "Contugas S.A.C.",
            NombreComercial = "Contugas",
            EsGestoraResiduos = false,
            EsActivo = true
        });
    }

    private static void AddStateIfMissing(
        ReciclaDbContext context,
        ICollection<string> existingCodes,
        string codigo,
        string nombre)
    {
        if (existingCodes.Contains(codigo))
            return;

        context.Set<EstadoControlGeneracion>().Add(new EstadoControlGeneracion
        {
            Codigo = codigo,
            Nombre = nombre
        });
        existingCodes.Add(codigo);
    }

    private static async Task MigrateLegacyRoleAssignmentsAsync(
        ReciclaDbContext context,
        CancellationToken cancellationToken)
    {
        var responsableRole = await context.Roles.FirstAsync(x => x.Codigo == "RESPONSABLE_OPERATIVO", cancellationToken);
        var registradorRole = await context.Roles.FirstAsync(x => x.Codigo == "REGISTRADOR", cancellationToken);

        var legacySupervisores = await context.UsuarioRoles
            .Where(x => x.Rol.Codigo == "SUPERVISOR")
            .Select(x => x.UsuarioId)
            .ToListAsync(cancellationToken);

        foreach (var usuarioId in legacySupervisores)
        {
            var exists = await context.UsuarioRoles.AnyAsync(
                x => x.UsuarioId == usuarioId && x.RolId == responsableRole.RolId,
                cancellationToken);
            if (!exists)
                context.UsuarioRoles.Add(new UsuarioRol { UsuarioId = usuarioId, RolId = responsableRole.RolId });
        }

        var legacyGenerales = await context.UsuarioRoles
            .Where(x => x.Rol.Codigo == "GENERAL")
            .Select(x => x.UsuarioId)
            .ToListAsync(cancellationToken);

        foreach (var usuarioId in legacyGenerales)
        {
            var exists = await context.UsuarioRoles.AnyAsync(
                x => x.UsuarioId == usuarioId && x.RolId == registradorRole.RolId,
                cancellationToken);
            if (!exists)
                context.UsuarioRoles.Add(new UsuarioRol { UsuarioId = usuarioId, RolId = registradorRole.RolId });
        }
    }
}
