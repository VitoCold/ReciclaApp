using Microsoft.EntityFrameworkCore;
using ReciclaApi.Domain;

namespace ReciclaApi.Infrastructure.Persistence;

public static class TraceabilitySeeder
{
    public static async Task SeedAsync(ReciclaDbContext context, CancellationToken cancellationToken = default)
    {
        await EnsureRegistroStatesAsync(context, cancellationToken);
        await EnsureTraceabilityStatesAsync(context, cancellationToken);
        await EnsureMeasurementUnitsAsync(context, cancellationToken);
        await EnsureWasteCatalogAsync(context, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureRegistroStatesAsync(ReciclaDbContext context, CancellationToken cancellationToken)
    {
        var existingCodes = await context.EstadosRegistro
            .Select(x => x.Codigo)
            .ToListAsync(cancellationToken);

        if (!existingCodes.Contains("EN_PROCESO"))
            context.EstadosRegistro.Add(new EstadoRegistro { Codigo = "EN_PROCESO", Nombre = "En proceso" });

        if (!existingCodes.Contains("REGISTRADO"))
            context.EstadosRegistro.Add(new EstadoRegistro { Codigo = "REGISTRADO", Nombre = "Registrado" });
    }

    private static async Task EnsureTraceabilityStatesAsync(ReciclaDbContext context, CancellationToken cancellationToken)
    {
        var retiroCodes = await context.EstadosRetiro
            .Select(x => x.Codigo)
            .ToListAsync(cancellationToken);

        if (!retiroCodes.Contains("PROGRAMADO"))
            context.EstadosRetiro.Add(new EstadoRetiro { Codigo = "PROGRAMADO", Nombre = "Programado" });
        if (!retiroCodes.Contains("EN_RETIRO"))
            context.EstadosRetiro.Add(new EstadoRetiro { Codigo = "EN_RETIRO", Nombre = "En retiro" });
        if (!retiroCodes.Contains("RETIRADO"))
            context.EstadosRetiro.Add(new EstadoRetiro { Codigo = "RETIRADO", Nombre = "Retirado" });
        if (!retiroCodes.Contains("ANULADO"))
            context.EstadosRetiro.Add(new EstadoRetiro { Codigo = "ANULADO", Nombre = "Anulado" });

        var disposicionCodes = await context.EstadosDisposicion
            .Select(x => x.Codigo)
            .ToListAsync(cancellationToken);

        if (!disposicionCodes.Contains("PENDIENTE"))
            context.EstadosDisposicion.Add(new EstadoDisposicion { Codigo = "PENDIENTE", Nombre = "Pendiente" });
        if (!disposicionCodes.Contains("DOCUMENTADA"))
            context.EstadosDisposicion.Add(new EstadoDisposicion { Codigo = "DOCUMENTADA", Nombre = "Documentada" });
        if (!disposicionCodes.Contains("VALIDADA"))
            context.EstadosDisposicion.Add(new EstadoDisposicion { Codigo = "VALIDADA", Nombre = "Validada" });
        if (!disposicionCodes.Contains("OBSERVADA"))
            context.EstadosDisposicion.Add(new EstadoDisposicion { Codigo = "OBSERVADA", Nombre = "Observada" });
        if (!disposicionCodes.Contains("ANULADA"))
            context.EstadosDisposicion.Add(new EstadoDisposicion { Codigo = "ANULADA", Nombre = "Anulada" });

        var treatmentCodes = await context.TiposTratamiento
            .Select(x => x.Codigo)
            .ToListAsync(cancellationToken);

        if (!treatmentCodes.Contains("DISPOSICION_FINAL"))
            context.TiposTratamiento.Add(new TipoTratamiento { Codigo = "DISPOSICION_FINAL", Nombre = "Disposición final", EsValorizacion = false });
        if (!treatmentCodes.Contains("RECICLAJE"))
            context.TiposTratamiento.Add(new TipoTratamiento { Codigo = "RECICLAJE", Nombre = "Reciclaje", EsValorizacion = true });
        if (!treatmentCodes.Contains("REUTILIZACION"))
            context.TiposTratamiento.Add(new TipoTratamiento { Codigo = "REUTILIZACION", Nombre = "Reutilización", EsValorizacion = true });
        if (!treatmentCodes.Contains("VALORIZACION"))
            context.TiposTratamiento.Add(new TipoTratamiento { Codigo = "VALORIZACION", Nombre = "Valorización", EsValorizacion = true });
        if (!treatmentCodes.Contains("TRATAMIENTO"))
            context.TiposTratamiento.Add(new TipoTratamiento { Codigo = "TRATAMIENTO", Nombre = "Tratamiento", EsValorizacion = false });
    }

    private static async Task EnsureMeasurementUnitsAsync(ReciclaDbContext context, CancellationToken cancellationToken)
    {
        var unitCodes = await context.UnidadesMedida
            .Select(x => x.Codigo)
            .ToListAsync(cancellationToken);

        if (!unitCodes.Contains("M3"))
            context.UnidadesMedida.Add(new UnidadMedida { Codigo = "M3", Nombre = "Metros cúbicos" });

        if (!unitCodes.Contains("KG"))
            context.UnidadesMedida.Add(new UnidadMedida { Codigo = "KG", Nombre = "Kilogramos" });

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureWasteCatalogAsync(ReciclaDbContext context, CancellationToken cancellationToken)
    {
        var solidType = await context.TiposResiduo.FirstOrDefaultAsync(x => x.Codigo == "SOLIDO", cancellationToken);
        if (solidType is null)
        {
            solidType = new TipoResiduo { Codigo = "SOLIDO", Nombre = "Sólido" };
            context.TiposResiduo.Add(solidType);
            await context.SaveChangesAsync(cancellationToken);
        }

        var kg = await context.UnidadesMedida.FirstAsync(x => x.Codigo == "KG", cancellationToken);
        var nonHazardous = await context.ClasificacionesResiduo.FirstAsync(x => x.Codigo == "NO_PELIGROSO", cancellationToken);
        var hazardous = await context.ClasificacionesResiduo.FirstAsync(x => x.Codigo == "PELIGROSO", cancellationToken);

        var existingCodes = await context.ResiduosCatalogo
            .Select(x => x.Codigo)
            .ToListAsync(cancellationToken);

        AddWasteIfMissing(context, existingCodes, solidType.TipoResiduoId, nonHazardous.ClasificacionResiduoId, kg.UnidadMedidaId, "PLASTICO", "Plástico");
        AddWasteIfMissing(context, existingCodes, solidType.TipoResiduoId, nonHazardous.ClasificacionResiduoId, kg.UnidadMedidaId, "PAPEL_CARTON", "Papel y cartón");
        AddWasteIfMissing(context, existingCodes, solidType.TipoResiduoId, nonHazardous.ClasificacionResiduoId, kg.UnidadMedidaId, "METALES", "Metales");
        AddWasteIfMissing(context, existingCodes, solidType.TipoResiduoId, nonHazardous.ClasificacionResiduoId, kg.UnidadMedidaId, "VIDRIO", "Vidrio");
        AddWasteIfMissing(context, existingCodes, solidType.TipoResiduoId, nonHazardous.ClasificacionResiduoId, kg.UnidadMedidaId, "ORGANICOS", "Orgánicos");
        AddWasteIfMissing(context, existingCodes, solidType.TipoResiduoId, nonHazardous.ClasificacionResiduoId, kg.UnidadMedidaId, "GENERALES", "Generales");
        AddWasteIfMissing(context, existingCodes, solidType.TipoResiduoId, nonHazardous.ClasificacionResiduoId, kg.UnidadMedidaId, "OTROS_NO_PELIGROSO", "Otros no peligrosos", "Especificar el residuo en la observación.");

        AddWasteIfMissing(context, existingCodes, solidType.TipoResiduoId, hazardous.ClasificacionResiduoId, kg.UnidadMedidaId, "RECIPIENTES_CONTAMINADOS", "Recipientes contaminados");
        AddWasteIfMissing(context, existingCodes, solidType.TipoResiduoId, hazardous.ClasificacionResiduoId, kg.UnidadMedidaId, "TRAPOS_CONTAMINADOS", "Trapos contaminados");
        AddWasteIfMissing(context, existingCodes, solidType.TipoResiduoId, hazardous.ClasificacionResiduoId, kg.UnidadMedidaId, "TUBERIA_PE_GAS_NATURAL", "Tubería de polietileno con gas natural");
        AddWasteIfMissing(context, existingCodes, solidType.TipoResiduoId, hazardous.ClasificacionResiduoId, kg.UnidadMedidaId, "OTROS_PELIGROSO", "Otros peligrosos", "Especificar el residuo en la observación.");
    }

    private static void AddWasteIfMissing(
        ReciclaDbContext context,
        ICollection<string> existingCodes,
        Guid tipoResiduoId,
        int clasificacionResiduoId,
        int unidadMedidaId,
        string codigo,
        string nombre,
        string? descripcion = null)
    {
        if (existingCodes.Contains(codigo))
            return;

        context.ResiduosCatalogo.Add(new ResiduoCatalogo
        {
            TipoResiduoId = tipoResiduoId,
            ClasificacionResiduoId = clasificacionResiduoId,
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = descripcion,
            UnidadMedidaDefaultId = unidadMedidaId
        });
        existingCodes.Add(codigo);
    }
}
