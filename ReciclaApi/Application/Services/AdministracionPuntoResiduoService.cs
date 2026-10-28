using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Persistence;

namespace ReciclaApi.Application.Services;

public interface IAdministracionPuntoResiduoService
{
    Task<IReadOnlyCollection<AdministracionPuntoResiduoDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionPuntoResiduoDto>> ObtenerAsync(Guid puntoResiduoId, CancellationToken cancellationToken = default);
    Task<AdministracionPuntosCatalogosDto> ObtenerCatalogosAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionPuntoResiduoDto>> CrearAsync(
        Guid actorUsuarioId,
        GuardarAdministracionPuntoResiduoRequest request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionPuntoResiduoDto>> ActualizarAsync(
        Guid puntoResiduoId,
        Guid actorUsuarioId,
        GuardarAdministracionPuntoResiduoRequest request,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionPuntoResiduoDto>> CambiarEstadoAsync(
        Guid puntoResiduoId,
        Guid actorUsuarioId,
        bool esActivo,
        CancellationToken cancellationToken = default);
}

public sealed class AdministracionPuntoResiduoService(ReciclaDbContext context) : IAdministracionPuntoResiduoService
{
    private static readonly string[] TiposPermitidos = ["GENERACION", "ALMACENAMIENTO", "AMBOS"];

    public async Task<IReadOnlyCollection<AdministracionPuntoResiduoDto>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        var puntos = await context.PuntosResiduo
            .AsNoTracking()
            .Include(x => x.Sede)
            .OrderByDescending(x => x.EsActivo)
            .ThenBy(x => x.Sede.Nombre)
            .ThenBy(x => x.Nombre)
            .ToListAsync(cancellationToken);

        var resultado = new List<AdministracionPuntoResiduoDto>(puntos.Count);
        foreach (var punto in puntos)
            resultado.Add(await CrearDtoAsync(punto, cancellationToken));

        return resultado;
    }

    public async Task<ServiceResult<AdministracionPuntoResiduoDto>> ObtenerAsync(
        Guid puntoResiduoId,
        CancellationToken cancellationToken = default)
    {
        var punto = await context.PuntosResiduo
            .AsNoTracking()
            .Include(x => x.Sede)
            .FirstOrDefaultAsync(x => x.PuntoResiduoId == puntoResiduoId, cancellationToken);

        return punto is null
            ? ServiceResult<AdministracionPuntoResiduoDto>.Fail("Punto de residuo no encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<AdministracionPuntoResiduoDto>.Ok(await CrearDtoAsync(punto, cancellationToken));
    }

    public async Task<AdministracionPuntosCatalogosDto> ObtenerCatalogosAsync(
        CancellationToken cancellationToken = default)
    {
        var sedes = await context.Sedes
            .AsNoTracking()
            .Where(x => x.EsActivo)
            .OrderBy(x => x.Nombre)
            .Select(x => new SedeDto(x.SedeId, x.Codigo, x.Nombre))
            .ToListAsync(cancellationToken);

        return new AdministracionPuntosCatalogosDto(sedes, TiposPermitidos);
    }

    public async Task<ServiceResult<AdministracionPuntoResiduoDto>> CrearAsync(
        Guid actorUsuarioId,
        GuardarAdministracionPuntoResiduoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarAsync(request, null, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionPuntoResiduoDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var punto = new PuntoResiduo
        {
            SedeId = request.SedeId,
            Codigo = NormalizarCodigo(request.Codigo),
            Nombre = request.Nombre.Trim(),
            Tipo = NormalizarTipo(request.Tipo),
            EsActivo = true
        };

        context.PuntosResiduo.Add(punto);
        Auditar(actorUsuarioId, punto.PuntoResiduoId, "CREAR", null, new
        {
            punto.SedeId,
            punto.Codigo,
            punto.Nombre,
            punto.Tipo,
            punto.EsActivo
        });

        await context.SaveChangesAsync(cancellationToken);

        punto.Sede = await context.Sedes
            .AsNoTracking()
            .FirstAsync(x => x.SedeId == punto.SedeId, cancellationToken);

        return ServiceResult<AdministracionPuntoResiduoDto>.Ok(
            await CrearDtoAsync(punto, cancellationToken),
            StatusCodes.Status201Created);
    }

    public async Task<ServiceResult<AdministracionPuntoResiduoDto>> ActualizarAsync(
        Guid puntoResiduoId,
        Guid actorUsuarioId,
        GuardarAdministracionPuntoResiduoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarAsync(request, puntoResiduoId, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionPuntoResiduoDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var punto = await context.PuntosResiduo
            .Include(x => x.Sede)
            .FirstOrDefaultAsync(x => x.PuntoResiduoId == puntoResiduoId, cancellationToken);

        if (punto is null)
            return ServiceResult<AdministracionPuntoResiduoDto>.Fail("Punto de residuo no encontrado.", StatusCodes.Status404NotFound);

        var nuevoTipo = NormalizarTipo(request.Tipo);
        var tieneUso = await TieneUsoHistoricoAsync(puntoResiduoId, cancellationToken);
        if (tieneUso && (punto.SedeId != request.SedeId || punto.Tipo != nuevoTipo))
        {
            return ServiceResult<AdministracionPuntoResiduoDto>.Fail(
                "La sede y el tipo no pueden cambiarse después de que el punto tiene uso operativo. Puedes editar su código o nombre.",
                StatusCodes.Status409Conflict);
        }

        var antes = new
        {
            punto.SedeId,
            punto.Codigo,
            punto.Nombre,
            punto.Tipo,
            punto.EsActivo
        };

        punto.SedeId = request.SedeId;
        punto.Codigo = NormalizarCodigo(request.Codigo);
        punto.Nombre = request.Nombre.Trim();
        punto.Tipo = nuevoTipo;

        Auditar(actorUsuarioId, punto.PuntoResiduoId, "EDITAR", antes, new
        {
            punto.SedeId,
            punto.Codigo,
            punto.Nombre,
            punto.Tipo,
            punto.EsActivo
        });

        await context.SaveChangesAsync(cancellationToken);

        punto.Sede = await context.Sedes
            .AsNoTracking()
            .FirstAsync(x => x.SedeId == punto.SedeId, cancellationToken);

        return ServiceResult<AdministracionPuntoResiduoDto>.Ok(
            await CrearDtoAsync(punto, cancellationToken));
    }

    public async Task<ServiceResult<AdministracionPuntoResiduoDto>> CambiarEstadoAsync(
        Guid puntoResiduoId,
        Guid actorUsuarioId,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        var punto = await context.PuntosResiduo
            .Include(x => x.Sede)
            .FirstOrDefaultAsync(x => x.PuntoResiduoId == puntoResiduoId, cancellationToken);

        if (punto is null)
            return ServiceResult<AdministracionPuntoResiduoDto>.Fail("Punto de residuo no encontrado.", StatusCodes.Status404NotFound);

        if (punto.EsActivo == esActivo)
            return ServiceResult<AdministracionPuntoResiduoDto>.Ok(await CrearDtoAsync(punto, cancellationToken));

        if (esActivo && !punto.Sede.EsActivo)
        {
            return ServiceResult<AdministracionPuntoResiduoDto>.Fail(
                "No puedes activar un punto perteneciente a una sede inactiva.",
                StatusCodes.Status409Conflict);
        }

        if (!esActivo)
        {
            var controlesAbiertos = await ContarControlesAbiertosAsync(puntoResiduoId, cancellationToken);
            if (controlesAbiertos > 0)
            {
                return ServiceResult<AdministracionPuntoResiduoDto>.Fail(
                    "No puedes desactivar un punto usado por Controles de Generación abiertos.",
                    StatusCodes.Status409Conflict);
            }

            if (await TieneSaldoAlmacenadoAsync(puntoResiduoId, cancellationToken))
            {
                return ServiceResult<AdministracionPuntoResiduoDto>.Fail(
                    "No puedes desactivar un punto de almacenamiento que todavía tiene saldo operativo.",
                    StatusCodes.Status409Conflict);
            }
        }

        var anterior = punto.EsActivo;
        punto.EsActivo = esActivo;

        Auditar(
            actorUsuarioId,
            punto.PuntoResiduoId,
            esActivo ? "ACTIVAR" : "DESACTIVAR",
            new { EsActivo = anterior },
            new { punto.EsActivo });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionPuntoResiduoDto>.Ok(await CrearDtoAsync(punto, cancellationToken));
    }

    private async Task<string?> ValidarAsync(
        GuardarAdministracionPuntoResiduoRequest request,
        Guid? puntoResiduoId,
        CancellationToken cancellationToken)
    {
        if (request.SedeId == Guid.Empty)
            return "Selecciona una sede.";
        if (string.IsNullOrWhiteSpace(request.Codigo))
            return "El código del punto es obligatorio.";
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return "El nombre del punto es obligatorio.";

        var tipo = NormalizarTipo(request.Tipo);
        if (!TiposPermitidos.Contains(tipo))
            return "Selecciona un tipo de punto válido.";

        var codigo = NormalizarCodigo(request.Codigo);
        if (codigo.Length > 40)
            return "El código del punto no puede superar 40 caracteres.";
        if (request.Nombre.Trim().Length > 160)
            return "El nombre del punto no puede superar 160 caracteres.";

        var sede = await context.Sedes
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SedeId == request.SedeId, cancellationToken);
        if (sede is null)
            return "La sede seleccionada no existe.";

        var mantieneSedeActual = puntoResiduoId.HasValue &&
            await context.PuntosResiduo
                .AsNoTracking()
                .AnyAsync(x =>
                    x.PuntoResiduoId == puntoResiduoId.Value &&
                    x.SedeId == request.SedeId,
                    cancellationToken);

        if (!sede.EsActivo && !mantieneSedeActual)
            return "No puedes asignar el punto a una sede inactiva.";

        var duplicado = await context.PuntosResiduo
            .AsNoTracking()
            .AnyAsync(x =>
                x.SedeId == request.SedeId &&
                x.Codigo.ToUpper() == codigo &&
                (!puntoResiduoId.HasValue || x.PuntoResiduoId != puntoResiduoId.Value),
                cancellationToken);

        if (duplicado)
            return "Ya existe un punto con ese código dentro de la sede.";

        return null;
    }

    private async Task<AdministracionPuntoResiduoDto> CrearDtoAsync(
        PuntoResiduo punto,
        CancellationToken cancellationToken)
    {
        var tieneUso = await TieneUsoHistoricoAsync(punto.PuntoResiduoId, cancellationToken);
        var controlesAbiertos = await ContarControlesAbiertosAsync(punto.PuntoResiduoId, cancellationToken);
        var tieneSaldo = await TieneSaldoAlmacenadoAsync(punto.PuntoResiduoId, cancellationToken);

        return new AdministracionPuntoResiduoDto(
            punto.PuntoResiduoId,
            punto.SedeId,
            punto.Sede.Codigo,
            punto.Sede.Nombre,
            punto.Codigo,
            punto.Nombre,
            punto.Tipo,
            punto.EsActivo,
            tieneUso,
            controlesAbiertos,
            tieneSaldo);
    }

    private async Task<bool> TieneUsoHistoricoAsync(
        Guid puntoResiduoId,
        CancellationToken cancellationToken)
    {
        if (await context.ControlesGeneracion.AsNoTracking()
            .AnyAsync(x => x.PuntoGeneracionId == puntoResiduoId, cancellationToken))
            return true;

        if (await context.Registros.AsNoTracking()
            .AnyAsync(x => x.PuntoGeneracionId == puntoResiduoId && !x.Eliminado, cancellationToken))
            return true;

        if (await context.MovimientosResiduo.AsNoTracking()
            .AnyAsync(x =>
                !x.Eliminado &&
                (x.PuntoOrigenId == puntoResiduoId || x.PuntoDestinoId == puntoResiduoId),
                cancellationToken))
            return true;

        return await context.Retiros.AsNoTracking()
            .AnyAsync(x =>
                !x.Eliminado &&
                x.PuntoAlmacenamientoId == puntoResiduoId,
                cancellationToken);
    }

    private Task<int> ContarControlesAbiertosAsync(
        Guid puntoResiduoId,
        CancellationToken cancellationToken) =>
        context.ControlesGeneracion
            .AsNoTracking()
            .CountAsync(x =>
                x.PuntoGeneracionId == puntoResiduoId &&
                !x.Eliminado &&
                x.EstadoControlGeneracion.Codigo != "FINALIZADO" &&
                x.EstadoControlGeneracion.Codigo != "ANULADO",
                cancellationToken);

    private async Task<bool> TieneSaldoAlmacenadoAsync(
        Guid puntoResiduoId,
        CancellationToken cancellationToken)
    {
        var movimientos = await context.MovimientosResiduo
            .AsNoTracking()
            .Where(x => x.PuntoDestinoId == puntoResiduoId && !x.Eliminado)
            .Select(x => new
            {
                x.RegistroResiduoId,
                x.FechaMovimiento,
                x.Cantidad
            })
            .ToListAsync(cancellationToken);

        if (movimientos.Count == 0)
            return false;

        var ids = movimientos.Select(x => x.RegistroResiduoId).Distinct().ToArray();
        var retiros = await context.RetiroDetalles
            .AsNoTracking()
            .Where(x =>
                ids.Contains(x.RegistroResiduoId) &&
                x.Retiro.PuntoAlmacenamientoId == puntoResiduoId &&
                !x.Retiro.Eliminado &&
                x.Retiro.EstadoRetiro.Codigo != "ANULADO")
            .Select(x => new
            {
                x.RegistroResiduoId,
                x.Cantidad,
                x.TipoRetiro,
                x.Retiro.FechaRetiro,
                x.Retiro.CreadoUtc
            })
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            var movs = movimientos.Where(x => x.RegistroResiduoId == id).ToArray();
            var rets = retiros.Where(x => x.RegistroResiduoId == id).ToArray();

            var ultimoTotal = rets
                .Where(x => x.TipoRetiro == "TOTAL")
                .OrderByDescending(x => x.FechaRetiro)
                .ThenByDescending(x => x.CreadoUtc)
                .FirstOrDefault();

            decimal saldo;
            if (ultimoTotal is null)
            {
                saldo = movs.Sum(x => x.Cantidad) - rets.Sum(x => x.Cantidad);
            }
            else
            {
                var movidoDespues = movs
                    .Where(x => x.FechaMovimiento > ultimoTotal.FechaRetiro)
                    .Sum(x => x.Cantidad);
                var retiradoDespues = rets
                    .Where(x => x.FechaRetiro > ultimoTotal.FechaRetiro)
                    .Sum(x => x.Cantidad);
                saldo = movidoDespues - retiradoDespues;
            }

            if (saldo > 0)
                return true;
        }

        return false;
    }

    private void Auditar(Guid actorUsuarioId, Guid puntoResiduoId, string accion, object? antes, object? despues)
    {
        context.Auditoria.Add(new Auditoria
        {
            UsuarioId = actorUsuarioId,
            Entidad = "PuntoResiduo",
            EntidadId = puntoResiduoId,
            Accion = accion,
            DatosAntesJson = antes is null ? null : JsonSerializer.Serialize(antes),
            DatosDespuesJson = despues is null ? null : JsonSerializer.Serialize(despues)
        });
    }

    private static string NormalizarCodigo(string codigo) =>
        codigo.Trim().ToUpperInvariant().Replace(' ', '_');

    private static string NormalizarTipo(string? tipo) =>
        string.IsNullOrWhiteSpace(tipo) ? string.Empty : tipo.Trim().ToUpperInvariant();
}
