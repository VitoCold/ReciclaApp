using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Persistence;

namespace ReciclaApi.Application.Services;

public interface IAdministracionSedeService
{
    Task<IReadOnlyCollection<AdministracionSedeDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionSedeDto>> ObtenerAsync(Guid sedeId, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionSedeDto>> CrearAsync(
        Guid actorUsuarioId,
        GuardarAdministracionSedeRequest request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionSedeDto>> ActualizarAsync(
        Guid sedeId,
        Guid actorUsuarioId,
        GuardarAdministracionSedeRequest request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionSedeDto>> CambiarEstadoAsync(
        Guid sedeId,
        Guid actorUsuarioId,
        bool esActivo,
        CancellationToken cancellationToken = default);
}

public sealed class AdministracionSedeService(ReciclaDbContext context) : IAdministracionSedeService
{
    public async Task<IReadOnlyCollection<AdministracionSedeDto>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        var sedes = await context.Sedes
            .AsNoTracking()
            .OrderByDescending(x => x.EsActivo)
            .ThenBy(x => x.Nombre)
            .ToListAsync(cancellationToken);

        if (sedes.Count == 0)
            return Array.Empty<AdministracionSedeDto>();

        var sedeIds = sedes.Select(x => x.SedeId).ToArray();

        var puntosActivos = await context.PuntosResiduo
            .AsNoTracking()
            .Where(x => x.EsActivo && sedeIds.Contains(x.SedeId))
            .GroupBy(x => x.SedeId)
            .Select(g => new { SedeId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.SedeId, x => x.Cantidad, cancellationToken);

        var controlesAbiertos = await context.ControlesGeneracion
            .AsNoTracking()
            .Where(x =>
                sedeIds.Contains(x.SedeId) &&
                !x.Eliminado &&
                x.EstadoControlGeneracion.Codigo != "FINALIZADO" &&
                x.EstadoControlGeneracion.Codigo != "ANULADO")
            .GroupBy(x => x.SedeId)
            .Select(g => new { SedeId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.SedeId, x => x.Cantidad, cancellationToken);

        return sedes
            .Select(sede => new AdministracionSedeDto(
                sede.SedeId,
                sede.Codigo,
                sede.Nombre,
                sede.EsActivo,
                puntosActivos.GetValueOrDefault(sede.SedeId),
                controlesAbiertos.GetValueOrDefault(sede.SedeId)))
            .ToArray();
    }

    public async Task<ServiceResult<AdministracionSedeDto>> ObtenerAsync(
        Guid sedeId,
        CancellationToken cancellationToken = default)
    {
        var sede = await context.Sedes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SedeId == sedeId, cancellationToken);

        if (sede is null)
            return ServiceResult<AdministracionSedeDto>.Fail("Sede no encontrada.", StatusCodes.Status404NotFound);

        return ServiceResult<AdministracionSedeDto>.Ok(
            await CrearDtoAsync(sede, cancellationToken));
    }

    public async Task<ServiceResult<AdministracionSedeDto>> CrearAsync(
        Guid actorUsuarioId,
        GuardarAdministracionSedeRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarAsync(request, null, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionSedeDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var sede = new Sede
        {
            Codigo = NormalizarCodigo(request.Codigo),
            Nombre = request.Nombre.Trim(),
            EsActivo = true
        };

        context.Sedes.Add(sede);
        Auditar(actorUsuarioId, sede.SedeId, "CREAR", null, new
        {
            sede.Codigo,
            sede.Nombre,
            sede.EsActivo
        });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionSedeDto>.Ok(
            new AdministracionSedeDto(sede.SedeId, sede.Codigo, sede.Nombre, true, 0, 0),
            StatusCodes.Status201Created);
    }

    public async Task<ServiceResult<AdministracionSedeDto>> ActualizarAsync(
        Guid sedeId,
        Guid actorUsuarioId,
        GuardarAdministracionSedeRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarAsync(request, sedeId, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionSedeDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var sede = await context.Sedes
            .FirstOrDefaultAsync(x => x.SedeId == sedeId, cancellationToken);

        if (sede is null)
            return ServiceResult<AdministracionSedeDto>.Fail("Sede no encontrada.", StatusCodes.Status404NotFound);

        var antes = new { sede.Codigo, sede.Nombre, sede.EsActivo };
        sede.Codigo = NormalizarCodigo(request.Codigo);
        sede.Nombre = request.Nombre.Trim();

        Auditar(actorUsuarioId, sede.SedeId, "EDITAR", antes, new
        {
            sede.Codigo,
            sede.Nombre,
            sede.EsActivo
        });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionSedeDto>.Ok(
            await CrearDtoAsync(sede, cancellationToken));
    }

    public async Task<ServiceResult<AdministracionSedeDto>> CambiarEstadoAsync(
        Guid sedeId,
        Guid actorUsuarioId,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        var sede = await context.Sedes
            .FirstOrDefaultAsync(x => x.SedeId == sedeId, cancellationToken);

        if (sede is null)
            return ServiceResult<AdministracionSedeDto>.Fail("Sede no encontrada.", StatusCodes.Status404NotFound);

        if (sede.EsActivo == esActivo)
        {
            return ServiceResult<AdministracionSedeDto>.Ok(
                await CrearDtoAsync(sede, cancellationToken));
        }

        if (!esActivo)
        {
            var controlesAbiertos = await context.ControlesGeneracion
                .AsNoTracking()
                .AnyAsync(x =>
                    x.SedeId == sedeId &&
                    !x.Eliminado &&
                    x.EstadoControlGeneracion.Codigo != "FINALIZADO" &&
                    x.EstadoControlGeneracion.Codigo != "ANULADO",
                    cancellationToken);

            if (controlesAbiertos)
            {
                return ServiceResult<AdministracionSedeDto>.Fail(
                    "No puedes desactivar una sede con Controles de Generación abiertos.",
                    StatusCodes.Status409Conflict);
            }

            var puntosActivos = await context.PuntosResiduo
                .AsNoTracking()
                .AnyAsync(x => x.SedeId == sedeId && x.EsActivo, cancellationToken);

            if (puntosActivos)
            {
                return ServiceResult<AdministracionSedeDto>.Fail(
                    "Desactiva primero los puntos de generación/almacenamiento activos de la sede.",
                    StatusCodes.Status409Conflict);
            }
        }

        var estadoAnterior = sede.EsActivo;
        sede.EsActivo = esActivo;

        Auditar(actorUsuarioId, sede.SedeId, esActivo ? "ACTIVAR" : "DESACTIVAR",
            new { EsActivo = estadoAnterior },
            new { sede.EsActivo });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionSedeDto>.Ok(
            await CrearDtoAsync(sede, cancellationToken));
    }

    private async Task<string?> ValidarAsync(
        GuardarAdministracionSedeRequest request,
        Guid? sedeId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
            return "El código de la sede es obligatorio.";
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return "El nombre de la sede es obligatorio.";

        var codigo = NormalizarCodigo(request.Codigo);
        if (codigo.Length > 30)
            return "El código de la sede no puede superar 30 caracteres.";
        if (request.Nombre.Trim().Length > 100)
            return "El nombre de la sede no puede superar 100 caracteres.";

        var codigoDuplicado = await context.Sedes
            .AsNoTracking()
            .AnyAsync(x =>
                x.Codigo.ToUpper() == codigo &&
                (!sedeId.HasValue || x.SedeId != sedeId.Value),
                cancellationToken);

        if (codigoDuplicado)
            return "Ya existe una sede con ese código.";

        return null;
    }

    private async Task<AdministracionSedeDto> CrearDtoAsync(
        Sede sede,
        CancellationToken cancellationToken)
    {
        var puntosActivos = await context.PuntosResiduo
            .AsNoTracking()
            .CountAsync(x => x.SedeId == sede.SedeId && x.EsActivo, cancellationToken);

        var controlesAbiertos = await context.ControlesGeneracion
            .AsNoTracking()
            .CountAsync(x =>
                x.SedeId == sede.SedeId &&
                !x.Eliminado &&
                x.EstadoControlGeneracion.Codigo != "FINALIZADO" &&
                x.EstadoControlGeneracion.Codigo != "ANULADO",
                cancellationToken);

        return new AdministracionSedeDto(
            sede.SedeId,
            sede.Codigo,
            sede.Nombre,
            sede.EsActivo,
            puntosActivos,
            controlesAbiertos);
    }

    private void Auditar(Guid actorUsuarioId, Guid sedeId, string accion, object? antes, object? despues)
    {
        context.Auditoria.Add(new Auditoria
        {
            UsuarioId = actorUsuarioId,
            Entidad = "Sede",
            EntidadId = sedeId,
            Accion = accion,
            DatosAntesJson = antes is null ? null : JsonSerializer.Serialize(antes),
            DatosDespuesJson = despues is null ? null : JsonSerializer.Serialize(despues)
        });
    }

    private static string NormalizarCodigo(string codigo) =>
        codigo.Trim().ToUpperInvariant().Replace(' ', '_');

}
