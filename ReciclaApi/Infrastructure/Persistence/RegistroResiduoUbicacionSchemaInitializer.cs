using Microsoft.EntityFrameworkCore;

namespace ReciclaApi.Infrastructure.Persistence;

public sealed class RegistroResiduoUbicacionSchemaInitializer(
    ReciclaDbContext context,
    IConfiguration configuration,
    ILogger<RegistroResiduoUbicacionSchemaInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'[dbo].[RegistroResiduoUbicaciones]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[RegistroResiduoUbicaciones]
                    (
                        [RegistroResiduoId] uniqueidentifier NOT NULL,
                        [Latitud] float NOT NULL,
                        [Longitud] float NOT NULL,
                        [PrecisionMetros] float NULL,
                        [CapturadaUtc] datetime2 NOT NULL,
                        [ActualizadoUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_RegistroResiduoUbicaciones] PRIMARY KEY ([RegistroResiduoId]),
                        CONSTRAINT [FK_RegistroResiduoUbicaciones_RegistroResiduos_RegistroResiduoId]
                            FOREIGN KEY ([RegistroResiduoId]) REFERENCES [dbo].[RegistroResiduos] ([RegistroResiduoId])
                    );
                END
                """,
                cancellationToken);
        }
        else
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS "RegistroResiduoUbicaciones"
                (
                    "RegistroResiduoId" TEXT NOT NULL CONSTRAINT "PK_RegistroResiduoUbicaciones" PRIMARY KEY,
                    "Latitud" REAL NOT NULL,
                    "Longitud" REAL NOT NULL,
                    "PrecisionMetros" REAL NULL,
                    "CapturadaUtc" TEXT NOT NULL,
                    "ActualizadoUtc" TEXT NOT NULL,
                    CONSTRAINT "FK_RegistroResiduoUbicaciones_RegistroResiduos_RegistroResiduoId"
                        FOREIGN KEY ("RegistroResiduoId") REFERENCES "RegistroResiduos" ("RegistroResiduoId") ON DELETE RESTRICT
                );
                """,
                cancellationToken);
        }

        logger.LogInformation("Esquema de geolocalización por residuo verificado.");
    }
}
