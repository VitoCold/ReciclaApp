using Microsoft.EntityFrameworkCore;
using ReciclaApi.Application.Security;
using ReciclaApi.Domain;

namespace ReciclaApi.Infrastructure.Persistence;

public sealed class DevelopmentDataSeeder(
    ReciclaDbContext context,
    IPasswordService passwordService,
    IHostEnvironment environment,
    ILogger<DevelopmentDataSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
            return;

        const string password = "Recicla123!";

        await EnsureUserAsync(
            "ambiental",
            "Ambiental",
            "Pruebas",
            ["AMBIENTAL"],
            password,
            assignAllSites: true,
            cancellationToken);

        await EnsureUserAsync(
            "responsable",
            "Responsable",
            "Operativo",
            ["RESPONSABLE_OPERATIVO"],
            password,
            assignAllSites: true,
            cancellationToken);

        await EnsureUserAsync(
            "registrador",
            "Usuario",
            "Registrador",
            ["REGISTRADOR"],
            password,
            assignAllSites: true,
            cancellationToken);

        await EnsureWasteManagerAsync(cancellationToken);
        await EnsureStoragePointsAsync(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Datos de desarrollo listos: usuarios de prueba, gestor y puntos de almacenamiento temporal.");
    }

    private async Task EnsureWasteManagerAsync(CancellationToken cancellationToken)
    {
        if (await context.Empresas.AnyAsync(x => x.Codigo == "GESTOR_PRUEBA", cancellationToken))
            return;

        context.Empresas.Add(new Empresa
        {
            Codigo = "GESTOR_PRUEBA",
            RazonSocial = "Gestor Ambiental de Prueba S.A.C.",
            NombreComercial = "Gestor de prueba",
            EsGestoraResiduos = true,
            NumeroAutorizacion = "DEV",
            EsActivo = true
        });
    }

    private async Task EnsureStoragePointsAsync(CancellationToken cancellationToken)
    {
        var sedes = await context.Sedes
            .Where(x => x.EsActivo)
            .ToListAsync(cancellationToken);

        foreach (var sede in sedes)
        {
            var tieneAlmacen = await context.PuntosResiduo.AnyAsync(x =>
                x.SedeId == sede.SedeId &&
                x.EsActivo &&
                (x.Tipo == "ALMACENAMIENTO" || x.Tipo == "AMBOS"),
                cancellationToken);

            if (tieneAlmacen)
                continue;

            context.PuntosResiduo.Add(new PuntoResiduo
            {
                SedeId = sede.SedeId,
                Codigo = "ALMACEN_TEMPORAL",
                Nombre = "Almacén temporal",
                Tipo = "ALMACENAMIENTO",
                EsActivo = true
            });
        }
    }

    private async Task EnsureUserAsync(
        string userName,
        string nombres,
        string apellidos,
        IReadOnlyCollection<string> roleCodes,
        string password,
        bool assignAllSites,
        CancellationToken cancellationToken)
    {
        if (await context.Usuarios.AnyAsync(x => x.UsuarioNombre == userName, cancellationToken))
            return;

        var roles = await context.Roles
            .Where(x => roleCodes.Contains(x.Codigo))
            .ToListAsync(cancellationToken);

        var (hash, salt) = passwordService.Hash(password);
        var usuario = new Usuario
        {
            UsuarioNombre = userName,
            Nombres = nombres,
            Apellidos = apellidos,
            PasswordHash = hash,
            PasswordSalt = salt,
            DebeCambiarClave = false,
            EsActivo = true
        };

        foreach (var rol in roles)
            usuario.UsuarioRoles.Add(new UsuarioRol { Usuario = usuario, Rol = rol });

        if (assignAllSites)
        {
            var sedes = await context.Sedes.Where(x => x.EsActivo).ToListAsync(cancellationToken);
            foreach (var sede in sedes)
                usuario.UsuarioSedes.Add(new UsuarioSede { Usuario = usuario, Sede = sede });
        }

        context.Usuarios.Add(usuario);
    }
}
