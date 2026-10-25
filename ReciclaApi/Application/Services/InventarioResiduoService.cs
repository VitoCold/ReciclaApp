using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Repositories;

namespace ReciclaApi.Application.Services;

public interface IInventarioResiduoService
{
    Task<InventarioResiduosDto> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<InventarioResiduoItemDto>> AlmacenarAsync(
        Guid registroResiduoId,
        Guid usuarioId,
        AlmacenarResiduoRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class InventarioResiduoService(IUnitOfWork unitOfWork) : IInventarioResiduoService
{
    public async Task<InventarioResiduosDto> ListarAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var items = await ConstruirItemsAsync(usuarioId, null, cancellationToken);
        var visibles = items
            .Where(x => x.CantidadDisponible > 0)
            .OrderBy(x => x.Sede)
            .ThenBy(x => x.Residuo)
            .ThenBy(x => x.FechaRegistro)
            .ToArray();

        var resumen = visibles
            .GroupBy(x => x.Unidad)
            .Select(g => new InventarioResumenUnidadDto(
                g.Key,
                g.Sum(x => x.CantidadDisponible),
                g.Sum(x => x.Almacenamientos.Sum(a => a.Cantidad)),
                g.Sum(x => x.CantidadPendienteAlmacenamiento)))
            .OrderBy(x => x.Unidad)
            .ToArray();

        return new InventarioResiduosDto(resumen, visibles);
    }

    public async Task<ServiceResult<InventarioResiduoItemDto>> AlmacenarAsync(
        Guid registroResiduoId,
        Guid usuarioId,
        AlmacenarResiduoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Cantidad <= 0)
            return ServiceResult<InventarioResiduoItemDto>.Fail("La cantidad a almacenar debe ser mayor a cero.", StatusCodes.Status400BadRequest);

        var residuo = await unitOfWork.Repository<RegistroResiduo>().Query(tracking: true)
            .Include(x => x.Registro).ThenInclude(x => x.EstadoRegistro)
            .Include(x => x.Registro).ThenInclude(x => x.ControlGeneracion)
            .Include(x => x.UnidadMedida)
            .FirstOrDefaultAsync(x => x.RegistroResiduoId == registroResiduoId && !x.Eliminado && !x.Registro.Eliminado, cancellationToken);

        if (residuo is null || residuo.Registro.ControlGeneracionId is null)
            return ServiceResult<InventarioResiduoItemDto>.Fail("Residuo no encontrado en un control de generación.", StatusCodes.Status404NotFound);

        if (residuo.Registro.EstadoRegistro.Codigo != "REGISTRADO")
            return ServiceResult<InventarioResiduoItemDto>.Fail("Solo los residuos de registros finalizados pueden enviarse a almacenamiento.", StatusCodes.Status409Conflict);

        if (!await PuedeGestionarAsync(residuo.Registro.ControlGeneracionId.Value, usuarioId, cancellationToken))
            return ServiceResult<InventarioResiduoItemDto>.Fail("No tienes permiso para registrar el almacenamiento de este residuo.", StatusCodes.Status403Forbidden);

        if (request.FechaMovimiento < residuo.Registro.FechaRegistro)
            return ServiceResult<InventarioResiduoItemDto>.Fail("La fecha de almacenamiento no puede ser anterior a la generación del residuo.", StatusCodes.Status400BadRequest);

        var punto = await unitOfWork.Repository<PuntoResiduo>().Query()
            .FirstOrDefaultAsync(x =>
                x.PuntoResiduoId == request.PuntoAlmacenamientoId &&
                x.SedeId == residuo.Registro.SedeId &&
                x.EsActivo &&
                (x.Tipo == "ALMACENAMIENTO" || x.Tipo == "AMBOS"),
                cancellationToken);

        if (punto is null)
            return ServiceResult<InventarioResiduoItemDto>.Fail("El punto de almacenamiento no es válido para la sede del residuo.", StatusCodes.Status400BadRequest);

        // SQLite no traduce SUM(decimal) de forma portable. Leemos solo las cantidades
        // y hacemos el agregado en memoria para mantener el mismo comportamiento en
        // SQLite (desarrollo) y SQL Server (producción).
        var cantidadesMovidas = await unitOfWork.Repository<MovimientoResiduo>().Query()
            .Where(x => x.RegistroResiduoId == registroResiduoId && !x.Eliminado)
            .Select(x => x.Cantidad)
            .ToListAsync(cancellationToken);
        var yaMovido = cantidadesMovidas.Sum();

        var pendiente = residuo.Cantidad - yaMovido;
        if (pendiente <= 0)
            return ServiceResult<InventarioResiduoItemDto>.Fail("Todo el residuo ya fue asignado a almacenamiento.", StatusCodes.Status409Conflict);

        if (request.Cantidad > pendiente)
            return ServiceResult<InventarioResiduoItemDto>.Fail($"La cantidad supera el saldo pendiente de almacenamiento ({pendiente:0.###} {residuo.UnidadMedida.Codigo}).", StatusCodes.Status409Conflict);

        var estadoSync = await unitOfWork.Repository<EstadoSincronizacion>().Query(tracking: true)
            .FirstAsync(x => x.Codigo == "SINCRONIZADO", cancellationToken);

        var movimiento = new MovimientoResiduo
        {
            RegistroResiduoId = registroResiduoId,
            FechaMovimiento = request.FechaMovimiento,
            PuntoOrigenId = residuo.Registro.PuntoGeneracionId,
            PuntoDestinoId = request.PuntoAlmacenamientoId,
            Cantidad = request.Cantidad,
            UnidadMedidaId = residuo.UnidadMedidaId,
            Observacion = request.Observacion,
            EstadoSincronizacionId = estadoSync.EstadoSincronizacionId,
            RegistradoPorUsuarioId = usuarioId
        };

        await unitOfWork.Repository<MovimientoResiduo>().AddAsync(movimiento, cancellationToken);
        await unitOfWork.Repository<Auditoria>().AddAsync(new Auditoria
        {
            UsuarioId = usuarioId,
            Entidad = "RegistroResiduo",
            EntidadId = registroResiduoId,
            Accion = "ALMACENAR",
            DatosDespuesJson = JsonSerializer.Serialize(new
            {
                movimiento.MovimientoResiduoId,
                movimiento.PuntoOrigenId,
                movimiento.PuntoDestinoId,
                movimiento.Cantidad,
                movimiento.UnidadMedidaId,
                movimiento.FechaMovimiento
            })
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var actualizado = (await ConstruirItemsAsync(usuarioId, registroResiduoId, cancellationToken)).SingleOrDefault();
        return actualizado is null
            ? ServiceResult<InventarioResiduoItemDto>.Fail("No se pudo reconstruir el saldo actualizado.", StatusCodes.Status500InternalServerError)
            : ServiceResult<InventarioResiduoItemDto>.Ok(actualizado);
    }

    private async Task<IReadOnlyCollection<InventarioResiduoItemDto>> ConstruirItemsAsync(
        Guid usuarioId,
        Guid? registroResiduoId,
        CancellationToken cancellationToken)
    {
        var esGlobal = await TieneRolAsync(usuarioId, "AMBIENTAL", cancellationToken) ||
                       await TieneRolAsync(usuarioId, "ADMINISTRADOR", cancellationToken);
        var now = DateTime.UtcNow;

        IQueryable<RegistroResiduo> query = unitOfWork.Repository<RegistroResiduo>().Query()
            .Where(x =>
                !x.Eliminado &&
                !x.Registro.Eliminado &&
                x.Registro.ControlGeneracionId != null &&
                x.Registro.EstadoRegistro.Codigo == "REGISTRADO")
            .Include(x => x.Registro).ThenInclude(x => x.Sede)
            .Include(x => x.Registro).ThenInclude(x => x.ControlGeneracion)
            .Include(x => x.Registro).ThenInclude(x => x.RegistradoPorUsuario)
            .Include(x => x.Residuo).ThenInclude(x => x.ClasificacionResiduo)
            .Include(x => x.UnidadMedida);

        if (registroResiduoId.HasValue)
            query = query.Where(x => x.RegistroResiduoId == registroResiduoId.Value);

        if (!esGlobal)
        {
            query = query.Where(x => x.Registro.ControlGeneracion!.Usuarios.Any(u =>
                u.UsuarioId == usuarioId &&
                u.EsActivo &&
                u.FechaDesde <= now &&
                (!u.FechaHasta.HasValue || u.FechaHasta.Value >= now)));
        }

        var residuos = await query.ToListAsync(cancellationToken);
        if (residuos.Count == 0)
            return Array.Empty<InventarioResiduoItemDto>();

        var ids = residuos.Select(x => x.RegistroResiduoId).ToArray();

        var movimientos = await unitOfWork.Repository<MovimientoResiduo>().Query()
            .Where(x => ids.Contains(x.RegistroResiduoId) && !x.Eliminado)
            .Include(x => x.PuntoDestino)
            .ToListAsync(cancellationToken);

        var retiros = await unitOfWork.Repository<RetiroDetalle>().Query()
            .Where(x =>
                ids.Contains(x.RegistroResiduoId) &&
                !x.Retiro.Eliminado &&
                x.Retiro.EstadoRetiro.Codigo != "ANULADO")
            .Include(x => x.Retiro)
            .ToListAsync(cancellationToken);

        return residuos.Select(residuo =>
        {
            var movimientosResiduo = movimientos.Where(x => x.RegistroResiduoId == residuo.RegistroResiduoId).ToArray();
            var retirosResiduo = retiros.Where(x => x.RegistroResiduoId == residuo.RegistroResiduoId).ToArray();

            // Cantidad retirada conserva el peso real reportado por la EO-RS.
            // El saldo operativo se calcula por almacén y respeta los cierres TOTAL,
            // que llevan a cero el stock físico aunque exista diferencia de pesaje.
            var retirado = retirosResiduo.Sum(x => x.Cantidad);
            var movido = movimientosResiduo.Sum(x => x.Cantidad);
            var pendiente = Math.Max(0m, residuo.Cantidad - movido);

            var almacenamientos = movimientosResiduo
                .GroupBy(x => new
                {
                    x.PuntoDestinoId,
                    x.PuntoDestino.Codigo,
                    x.PuntoDestino.Nombre
                })
                .Select(g =>
                {
                    var retirosPunto = retirosResiduo
                        .Where(r => r.Retiro.PuntoAlmacenamientoId == g.Key.PuntoDestinoId)
                        .ToArray();

                    var ultimoTotal = retirosPunto
                        .Where(r => r.TipoRetiro == "TOTAL")
                        .OrderByDescending(r => r.Retiro.FechaRetiro)
                        .ThenByDescending(r => r.Retiro.CreadoUtc)
                        .FirstOrDefault();

                    decimal saldo;
                    if (ultimoTotal is null)
                    {
                        saldo = g.Sum(m => m.Cantidad) - retirosPunto.Sum(r => r.Cantidad);
                    }
                    else
                    {
                        var movidoDespues = g
                            .Where(m => m.FechaMovimiento > ultimoTotal.Retiro.FechaRetiro)
                            .Sum(m => m.Cantidad);
                        var retiradoDespues = retirosPunto
                            .Where(r => r.Retiro.FechaRetiro > ultimoTotal.Retiro.FechaRetiro)
                            .Sum(r => r.Cantidad);
                        saldo = movidoDespues - retiradoDespues;
                    }

                    return new InventarioUbicacionDto(
                        g.Key.PuntoDestinoId,
                        g.Key.Codigo,
                        g.Key.Nombre,
                        Math.Max(0m, saldo));
                })
                .Where(x => x.Cantidad > 0)
                .OrderBy(x => x.Punto)
                .ToArray();

            var disponible = pendiente + almacenamientos.Sum(x => x.Cantidad);

            var control = residuo.Registro.ControlGeneracion!;
            var usuario = residuo.Registro.RegistradoPorUsuario;
            var nombreUsuario = string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            return new InventarioResiduoItemDto(
                residuo.RegistroResiduoId,
                residuo.RegistroId,
                control.ControlGeneracionId,
                control.Codigo,
                residuo.Registro.FechaRegistro,
                residuo.Registro.SedeId,
                residuo.Registro.Sede.Nombre,
                residuo.ResiduoId,
                residuo.Residuo.Nombre,
                residuo.Residuo.ClasificacionResiduo.Nombre,
                residuo.UnidadMedidaId,
                residuo.UnidadMedida.Codigo,
                residuo.Cantidad,
                retirado,
                disponible,
                pendiente,
                EstadoOperativoCodigo(movido, pendiente, disponible, retirosResiduo.Length > 0),
                EstadoOperativoNombre(movido, pendiente, disponible, retirosResiduo.Length > 0),
                almacenamientos,
                residuo.Registro.RegistradoPorUsuarioId,
                nombreUsuario);
        }).ToArray();
    }

    private async Task<bool> PuedeGestionarAsync(
        Guid controlId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var tieneRolOperativo =
            await TieneRolAsync(usuarioId, "RESPONSABLE_OPERATIVO", cancellationToken) ||
            await TieneRolAsync(usuarioId, "REGISTRADOR", cancellationToken);

        if (!tieneRolOperativo)
            return false;

        var now = DateTime.UtcNow;
        return await unitOfWork.Repository<ControlGeneracionUsuario>().Query()
            .AnyAsync(x =>
                x.ControlGeneracionId == controlId &&
                x.UsuarioId == usuarioId &&
                (x.RolControl == "RESPONSABLE" || x.RolControl == "REGISTRADOR") &&
                x.EsActivo &&
                x.FechaDesde <= now &&
                (!x.FechaHasta.HasValue || x.FechaHasta.Value >= now),
                cancellationToken);
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

    private static string EstadoOperativoNombre(
        decimal cantidadMovida,
        decimal cantidadPendiente,
        decimal cantidadDisponible,
        bool tieneRetiros) =>
        cantidadDisponible <= 0 && tieneRetiros
            ? "Retirado"
            : cantidadMovida <= 0
                ? "Pendiente de traslado"
                : cantidadPendiente > 0
                    ? "Traslado parcial"
                    : "En almacén temporal";

    private Task<bool> TieneRolAsync(Guid usuarioId, string rol, CancellationToken cancellationToken) =>
        unitOfWork.Repository<UsuarioRol>().Query()
            .AnyAsync(x => x.UsuarioId == usuarioId && x.Rol.Codigo == rol && x.Rol.EsActivo, cancellationToken);
}
