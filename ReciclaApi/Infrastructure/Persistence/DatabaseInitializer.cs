using Microsoft.EntityFrameworkCore;
using ReciclaApi.Application.Security;
using ReciclaApi.Domain;

namespace ReciclaApi.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    ReciclaDbContext context,
    IPasswordService passwordService,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (configuration.GetValue("Database:EnsureCreated", true))
            await context.Database.EnsureCreatedAsync(cancellationToken);

        await SeedCatalogosAsync(cancellationToken);
        await SeedAdminAsync(cancellationToken);
    }

    private async Task SeedCatalogosAsync(CancellationToken cancellationToken)
    {
        if (!await context.Roles.AnyAsync(cancellationToken))
        {
            context.Roles.AddRange(
                new Rol { Codigo = "ADMINISTRADOR", Nombre = "Administrador", Descripcion = "Gestiona la aplicación y sus catálogos." },
                new Rol { Codigo = "SUPERVISOR", Nombre = "Supervisor", Descripcion = "Supervisa los registros de las sedes asignadas." },
                new Rol { Codigo = "GENERAL", Nombre = "General", Descripcion = "Registra residuos y evidencias." });
        }

        if (!await context.EstadosRegistro.AnyAsync(cancellationToken))
        {
            context.EstadosRegistro.AddRange(
                new EstadoRegistro { Codigo = "BORRADOR", Nombre = "En proceso" },
                new EstadoRegistro { Codigo = "COMPLETADO", Nombre = "Completado" },
                new EstadoRegistro { Codigo = "OBSERVADO", Nombre = "Observado" },
                new EstadoRegistro { Codigo = "VALIDADO", Nombre = "Validado" },
                new EstadoRegistro { Codigo = "ANULADO", Nombre = "Anulado" });
        }
        else
        {
            var estadoEnProceso = await context.EstadosRegistro
                .FirstOrDefaultAsync(x => x.Codigo == "BORRADOR", cancellationToken);

            if (estadoEnProceso is not null && estadoEnProceso.Nombre != "En proceso")
                estadoEnProceso.Nombre = "En proceso";
        }

        if (!await context.EstadosSincronizacion.AnyAsync(cancellationToken))
        {
            context.EstadosSincronizacion.AddRange(
                new EstadoSincronizacion { Codigo = "LOCAL_PENDIENTE", Nombre = "Pendiente de sincronizar" },
                new EstadoSincronizacion { Codigo = "SINCRONIZANDO", Nombre = "Sincronizando" },
                new EstadoSincronizacion { Codigo = "SINCRONIZADO", Nombre = "Sincronizado" },
                new EstadoSincronizacion { Codigo = "ERROR", Nombre = "Error de sincronización" });
        }

        if (!await context.ClasificacionesResiduo.AnyAsync(cancellationToken))
        {
            context.ClasificacionesResiduo.AddRange(
                new ClasificacionResiduo { Codigo = "NO_PELIGROSO", Nombre = "No peligroso", ColorHex = "#079542" },
                new ClasificacionResiduo { Codigo = "PELIGROSO", Nombre = "Peligroso", ColorHex = "#E4312B" });
        }

        if (!await context.UnidadesMedida.AnyAsync(cancellationToken))
        {
            context.UnidadesMedida.AddRange(
                new UnidadMedida { Codigo = "KG", Nombre = "Kilogramos" },
                new UnidadMedida { Codigo = "TN", Nombre = "Toneladas" },
                new UnidadMedida { Codigo = "UND", Nombre = "Unidades" },
                new UnidadMedida { Codigo = "L", Nombre = "Litros" });
        }

        if (!await context.Sedes.AnyAsync(cancellationToken))
        {
            context.Sedes.AddRange(
                new Sede { Codigo = "PISCO", Nombre = "Pisco" },
                new Sede { Codigo = "ICA", Nombre = "Ica" },
                new Sede { Codigo = "CHINCHA", Nombre = "Chincha" },
                new Sede { Codigo = "NASCA", Nombre = "Nasca" },
                new Sede { Codigo = "MARCONA", Nombre = "Marcona" });
        }

        if (!await context.Proyectos.AnyAsync(cancellationToken))
        {
            context.Proyectos.AddRange(
                new Proyecto { Codigo = "DOT", Nombre = "DOT" },
                new Proyecto { Codigo = "OPERACIONES", Nombre = "Operaciones" },
                new Proyecto { Codigo = "MANTENIMIENTO", Nombre = "Mantenimiento" });
        }

        await context.SaveChangesAsync(cancellationToken);

        var dot = await context.Proyectos.FirstAsync(x => x.Codigo == "DOT", cancellationToken);
        if (!await context.Actividades.AnyAsync(cancellationToken))
        {
            context.Actividades.AddRange(
                new Actividad { ProyectoId = dot.ProyectoId, Codigo = "MANTENIMIENTO", Nombre = "Mantenimiento" },
                new Actividad { ProyectoId = dot.ProyectoId, Codigo = "LIMPIEZA", Nombre = "Limpieza general" },
                new Actividad { ProyectoId = dot.ProyectoId, Codigo = "ADMINISTRATIVA", Nombre = "Área administrativa" });
        }

        if (!await context.TiposResiduo.AnyAsync(cancellationToken))
            context.TiposResiduo.Add(new TipoResiduo { Codigo = "SOLIDO", Nombre = "Sólido" });

        await context.SaveChangesAsync(cancellationToken);

        if (!await context.ResiduosCatalogo.AnyAsync(cancellationToken))
        {
            var tipo = await context.TiposResiduo.FirstAsync(x => x.Codigo == "SOLIDO", cancellationToken);
            var kg = await context.UnidadesMedida.FirstAsync(x => x.Codigo == "KG", cancellationToken);
            var noPeligroso = await context.ClasificacionesResiduo.FirstAsync(x => x.Codigo == "NO_PELIGROSO", cancellationToken);
            var peligroso = await context.ClasificacionesResiduo.FirstAsync(x => x.Codigo == "PELIGROSO", cancellationToken);

            context.ResiduosCatalogo.AddRange(
                new ResiduoCatalogo { TipoResiduoId = tipo.TipoResiduoId, ClasificacionResiduoId = noPeligroso.ClasificacionResiduoId, Codigo = "PAPEL_CARTON", Nombre = "Papel y cartón", UnidadMedidaDefaultId = kg.UnidadMedidaId },
                new ResiduoCatalogo { TipoResiduoId = tipo.TipoResiduoId, ClasificacionResiduoId = peligroso.ClasificacionResiduoId, Codigo = "RECIPIENTES_CONTAMINADOS", Nombre = "Recipientes contaminados", UnidadMedidaDefaultId = kg.UnidadMedidaId },
                new ResiduoCatalogo { TipoResiduoId = tipo.TipoResiduoId, ClasificacionResiduoId = noPeligroso.ClasificacionResiduoId, Codigo = "VIDRIO", Nombre = "Vidrio", UnidadMedidaDefaultId = kg.UnidadMedidaId });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        var userName = configuration["Seed:AdminUser"] ?? "admin";
        if (await context.Usuarios.AnyAsync(x => x.UsuarioNombre == userName, cancellationToken))
            return;

        var password = configuration["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(password))
        {
            if (!environment.IsDevelopment())
            {
                logger.LogWarning("No se creó el administrador inicial porque Seed__AdminPassword no está configurado.");
                return;
            }

            password = "Recicla123!";
            logger.LogWarning("Usando la contraseña de desarrollo por defecto para el usuario {Usuario}. No usar en producción.", userName);
        }

        var (hash, salt) = passwordService.Hash(password);
        var adminRole = await context.Roles.FirstAsync(x => x.Codigo == "ADMINISTRADOR", cancellationToken);
        var sedes = await context.Sedes.ToListAsync(cancellationToken);

        var usuario = new Usuario
        {
            UsuarioNombre = userName,
            Nombres = "Administrador",
            Apellidos = "MVP",
            PasswordHash = hash,
            PasswordSalt = salt,
            DebeCambiarClave = true
        };

        usuario.UsuarioRoles.Add(new UsuarioRol { Usuario = usuario, Rol = adminRole });
        foreach (var sede in sedes)
            usuario.UsuarioSedes.Add(new UsuarioSede { Usuario = usuario, Sede = sede });

        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Usuario administrador inicial creado: {Usuario}", userName);
    }
}
