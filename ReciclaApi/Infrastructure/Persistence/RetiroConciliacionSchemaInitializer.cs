using System.Data;
using Microsoft.EntityFrameworkCore;

namespace ReciclaApi.Infrastructure.Persistence;

public sealed class RetiroConciliacionSchemaInitializer(
    ReciclaDbContext context,
    IConfiguration configuration,
    ILogger<RetiroConciliacionSchemaInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";

        if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('dbo.RetiroDetalles', 'TipoRetiro') IS NULL
                    ALTER TABLE dbo.RetiroDetalles ADD TipoRetiro NVARCHAR(10) NULL;

                IF COL_LENGTH('dbo.RetiroDetalles', 'SaldoEstimadoAntes') IS NULL
                    ALTER TABLE dbo.RetiroDetalles ADD SaldoEstimadoAntes DECIMAL(18,3) NULL;

                IF COL_LENGTH('dbo.RetiroDetalles', 'DiferenciaConciliacion') IS NULL
                    ALTER TABLE dbo.RetiroDetalles ADD DiferenciaConciliacion DECIMAL(18,3) NULL;

                UPDATE dbo.RetiroDetalles
                SET TipoRetiro = ISNULL(TipoRetiro, 'PARCIAL'),
                    SaldoEstimadoAntes = ISNULL(SaldoEstimadoAntes, Cantidad);

                ALTER TABLE dbo.RetiroDetalles ALTER COLUMN TipoRetiro NVARCHAR(10) NOT NULL;
                ALTER TABLE dbo.RetiroDetalles ALTER COLUMN SaldoEstimadoAntes DECIMAL(18,3) NOT NULL;

                IF NOT EXISTS (
                    SELECT 1 FROM sys.check_constraints
                    WHERE name = 'CK_RetiroDetalles_TipoRetiro')
                BEGIN
                    ALTER TABLE dbo.RetiroDetalles
                    ADD CONSTRAINT CK_RetiroDetalles_TipoRetiro
                    CHECK (TipoRetiro IN ('PARCIAL', 'TOTAL'));
                END;
                """,
                cancellationToken);
        }
        else
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync(cancellationToken);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """PRAGMA table_info("RetiroDetalles");""";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    columns.Add(reader.GetString(1));
            }

            if (!columns.Contains("TipoRetiro"))
                await context.Database.ExecuteSqlRawAsync(
                    """ALTER TABLE "RetiroDetalles" ADD COLUMN "TipoRetiro" TEXT NOT NULL DEFAULT 'PARCIAL';""",
                    cancellationToken);

            if (!columns.Contains("SaldoEstimadoAntes"))
                await context.Database.ExecuteSqlRawAsync(
                    """ALTER TABLE "RetiroDetalles" ADD COLUMN "SaldoEstimadoAntes" NUMERIC NOT NULL DEFAULT 0;""",
                    cancellationToken);

            if (!columns.Contains("DiferenciaConciliacion"))
                await context.Database.ExecuteSqlRawAsync(
                    """ALTER TABLE "RetiroDetalles" ADD COLUMN "DiferenciaConciliacion" NUMERIC NULL;""",
                    cancellationToken);

            await context.Database.ExecuteSqlRawAsync(
                """
                UPDATE "RetiroDetalles"
                SET "TipoRetiro" = COALESCE("TipoRetiro", 'PARCIAL'),
                    "SaldoEstimadoAntes" =
                        CASE WHEN "SaldoEstimadoAntes" = 0 THEN "Cantidad" ELSE "SaldoEstimadoAntes" END;
                """,
                cancellationToken);
        }

        logger.LogInformation("Esquema de conciliación de retiros verificado.");
    }
}
