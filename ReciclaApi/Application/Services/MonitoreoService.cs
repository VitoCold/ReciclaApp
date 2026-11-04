using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Infrastructure.Persistence;

namespace ReciclaApi.Application.Services;

public interface IMonitoreoService
{
    Task<MonitoreoActividadDto> ObtenerActividadAsync(
        MonitoreoActividadFiltroRequest filtro,
        CancellationToken cancellationToken = default);
}

public sealed class MonitoreoService(ReciclaDbContext context) : IMonitoreoService
{
    private const int MaxCandidatos = 600;

    public async Task<MonitoreoActividadDto> ObtenerActividadAsync(
        MonitoreoActividadFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        var limite = Math.Clamp(filtro.Limite, 1, 200);

        var query = context.RegistroResiduos
            .AsNoTracking()
            .Where(x =>
                !x.Eliminado &&
                !x.Registro.Eliminado &&
                x.Registro.ControlGeneracionId != null &&
                x.Registro.EstadoRegistro.Codigo == "REGISTRADO")
            .Include(x => x.Registro).ThenInclude(x => x.ControlGeneracion)
            .Include(x => x.Registro).ThenInclude(x => x.Sede)
            .Include(x => x.Registro).ThenInclude(x => x.Proyecto)
            .Include(x => x.Registro).ThenInclude(x => x.Actividad)
            .Include(x => x.Registro).ThenInclude(x => x.RegistradoPorUsuario)
            .Include(x => x.Residuo).ThenInclude(x => x.ClasificacionResiduo)
            .Include(x => x.TipoResiduo)
            .Include(x => x.UnidadMedida)
            .AsQueryable();

        if (filtro.DesdeUtc.HasValue)
            query = query.Where(x => x.CreadoUtc >= filtro.DesdeUtc.Value);

        if (filtro.HastaUtc.HasValue)
            query = query.Where(x => x.CreadoUtc <= filtro.HastaUtc.Value);

        if (filtro.SedeId.HasValue)
            query = query.Where(x => x.Registro.SedeId == filtro.SedeId.Value);

        if (filtro.ClasificacionResiduoId.HasValue)
            query = query.Where(x => x.Residuo.ClasificacionResiduoId == filtro.ClasificacionResiduoId.Value);

        var residuos = await query
            .OrderByDescending(x => x.CreadoUtc)
            .Take(MaxCandidatos)
            .ToListAsync(cancellationToken);

        var ids = residuos.Select(x => x.RegistroResiduoId).ToArray();

        var movimientos = ids.Length == 0
            ? []
            : await context.MovimientosResiduo
                .AsNoTracking()
                .Where(x => ids.Contains(x.RegistroResiduoId) && !x.Eliminado)
                .ToListAsync(cancellationToken);

        var retiros = ids.Length == 0
            ? []
            : await context.RetiroDetalles
                .AsNoTracking()
                .Where(x =>
                    ids.Contains(x.RegistroResiduoId) &&
                    !x.Retiro.Eliminado &&
                    x.Retiro.EstadoRetiro.Codigo != "ANULADO")
                .Include(x => x.Retiro)
                .ToListAsync(cancellationToken);

        var conFoto = ids.Length == 0
            ? new HashSet<Guid>()
            : (await context.RegistroResiduoFotos
                .AsNoTracking()
                .Where(x => ids.Contains(x.RegistroResiduoId) && !x.Eliminado)
                .Select(x => x.RegistroResiduoId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var conGps = ids.Length == 0
            ? new HashSet<Guid>()
            : (await context.Set<Domain.RegistroResiduoUbicacion>()
                .AsNoTracking()
                .Where(x => ids.Contains(x.RegistroResiduoId))
                .Select(x => x.RegistroResiduoId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

        var items = residuos.Select(residuo =>
        {
            var movs = movimientos
                .Where(x => x.RegistroResiduoId == residuo.RegistroResiduoId)
                .ToArray();

            var rets = retiros
                .Where(x => x.RegistroResiduoId == residuo.RegistroResiduoId)
                .ToArray();

            var movido = movs.Sum(x => x.Cantidad);
            var pendiente = Math.Max(0m, residuo.Cantidad - movido);
            var saldoAlmacen = CalcularSaldoAlmacen(movs, rets);
            var disponible = pendiente + saldoAlmacen;
            var estadoCodigo = EstadoOperativoCodigo(movido, pendiente, disponible, rets.Length > 0);
            var estado = EstadoOperativoNombre(estadoCodigo);

            return new MonitoreoActividadItemDto(
                residuo.RegistroResiduoId,
                residuo.RegistroId,
                residuo.Registro.ControlGeneracionId!.Value,
                residuo.Registro.ControlGeneracion!.Codigo,
                residuo.CreadoUtc,
                residuo.Registro.SedeId,
                residuo.Registro.Sede.Nombre,
                residuo.Registro.Proyecto.Nombre,
                residuo.Registro.Actividad.Nombre,
                residuo.Residuo.Nombre,
                residuo.TipoResiduo.Nombre,
                residuo.Residuo.ClasificacionResiduoId,
                residuo.Residuo.ClasificacionResiduo.Codigo,
                residuo.Residuo.ClasificacionResiduo.Nombre,
                residuo.Residuo.ClasificacionResiduo.ColorHex,
                residuo.Cantidad,
                residuo.UnidadMedida.Codigo,
                residuo.Registro.RegistradoPorUsuarioId,
                NombreUsuario(residuo.Registro.RegistradoPorUsuario),
                estadoCodigo,
                estado,
                conFoto.Contains(residuo.RegistroResiduoId),
                conGps.Contains(residuo.RegistroResiduoId));
        }).ToArray();

        if (!string.IsNullOrWhiteSpace(filtro.EstadoOperativo))
        {
            items = items
                .Where(x => string.Equals(
                    x.EstadoOperativoCodigo,
                    filtro.EstadoOperativo.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var buscar = filtro.Buscar.Trim();
            items = items.Where(x =>
                    x.Residuo.Contains(buscar, StringComparison.OrdinalIgnoreCase) ||
                    x.ControlCodigo.Contains(buscar, StringComparison.OrdinalIgnoreCase) ||
                    x.Sede.Contains(buscar, StringComparison.OrdinalIgnoreCase) ||
                    x.Proyecto.Contains(buscar, StringComparison.OrdinalIgnoreCase) ||
                    x.Actividad.Contains(buscar, StringComparison.OrdinalIgnoreCase) ||
                    x.RegistradoPor.Contains(buscar, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        var resumen = new MonitoreoResumenDto(
            items.Length,
            items.Count(x => x.ClasificacionCodigo == "PELIGROSO"),
            items.Count(x => x.ClasificacionCodigo == "NO_PELIGROSO"),
            items.Count(x => x.EstadoOperativoCodigo == "PENDIENTE_TRASLADO"));

        var sedes = await context.Sedes
            .AsNoTracking()
            .Where(x => x.EsActivo)
            .OrderBy(x => x.Nombre)
            .Select(x => new MonitoreoSedeDto(x.SedeId, x.Codigo, x.Nombre))
            .ToListAsync(cancellationToken);

        var clasificaciones = await context.ClasificacionesResiduo
            .AsNoTracking()
            .OrderBy(x => x.Nombre)
            .Select(x => new MonitoreoClasificacionDto(
                x.ClasificacionResiduoId,
                x.Codigo,
                x.Nombre,
                x.ColorHex))
            .ToListAsync(cancellationToken);

        return new MonitoreoActividadDto(
            resumen,
            items.Take(limite).ToArray(),
            sedes,
            clasificaciones);
    }

    private static decimal CalcularSaldoAlmacen(
        IReadOnlyCollection<Domain.MovimientoResiduo> movimientos,
        IReadOnlyCollection<Domain.RetiroDetalle> retiros)
    {
        decimal total = 0m;

        foreach (var grupo in movimientos.GroupBy(x => x.PuntoDestinoId))
        {
            var retirosPunto = retiros
                .Where(x => x.Retiro.PuntoAlmacenamientoId == grupo.Key)
                .ToArray();

            var ultimoTotal = retirosPunto
                .Where(x => x.TipoRetiro == "TOTAL")
                .OrderByDescending(x => x.Retiro.FechaRetiro)
                .ThenByDescending(x => x.Retiro.CreadoUtc)
                .FirstOrDefault();

            decimal saldo;
            if (ultimoTotal is null)
            {
                saldo = grupo.Sum(x => x.Cantidad) - retirosPunto.Sum(x => x.Cantidad);
            }
            else
            {
                var movidoDespues = grupo
                    .Where(x => x.FechaMovimiento > ultimoTotal.Retiro.FechaRetiro)
                    .Sum(x => x.Cantidad);

                var retiradoDespues = retirosPunto
                    .Where(x => x.Retiro.FechaRetiro > ultimoTotal.Retiro.FechaRetiro)
                    .Sum(x => x.Cantidad);

                saldo = movidoDespues - retiradoDespues;
            }

            total += Math.Max(0m, saldo);
        }

        return total;
    }

    private static string EstadoOperativoCodigo(
        decimal cantidadMovida,
        decimal cantidadPendiente,
        decimal cantidadDisponible,
        bool tieneRetiros) =>
        cantidadDisponible <= 0 && tieneRetiros
            ? "RETIRADO"
            : cantidadMovida <= 0
                ? "PENDIENTE_TRASLADO"
                : cantidadPendiente > 0
                    ? "TRASLADO_PARCIAL"
                    : "EN_ALMACEN_TEMPORAL";

    private static string EstadoOperativoNombre(string codigo) => codigo switch
    {
        "RETIRADO" => "Retirado",
        "PENDIENTE_TRASLADO" => "Pendiente de traslado",
        "TRASLADO_PARCIAL" => "Traslado parcial",
        "EN_ALMACEN_TEMPORAL" => "En almacén temporal",
        _ => "Registrado"
    };

    private static string NombreUsuario(Domain.Usuario usuario) =>
        string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
}
