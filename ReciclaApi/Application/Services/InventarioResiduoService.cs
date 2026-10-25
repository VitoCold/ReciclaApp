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
    Task<ServiceResult<TrazabilidadResiduoDto>> ObtenerTrazabilidadAsync(
        Guid registroResiduoId,
        Guid usuarioId,
        CancellationToken cancellationToken = default);
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

    public async Task<ServiceResult<TrazabilidadResiduoDto>> ObtenerTrazabilidadAsync(
        Guid registroResiduoId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var residuo = await unitOfWork.Repository<RegistroResiduo>().Query()
            .Where(x =>
                x.RegistroResiduoId == registroResiduoId &&
                !x.Eliminado &&
                !x.Registro.Eliminado &&
                x.Registro.ControlGeneracionId != null)
            .Include(x => x.Registro).ThenInclude(x => x.ControlGeneracion)
            .Include(x => x.Registro).ThenInclude(x => x.Sede)
            .Include(x => x.Registro).ThenInclude(x => x.PuntoGeneracion)
            .Include(x => x.Registro).ThenInclude(x => x.RegistradoPorUsuario)
            .Include(x => x.Residuo).ThenInclude(x => x.ClasificacionResiduo)
            .Include(x => x.UnidadMedida)
            .FirstOrDefaultAsync(cancellationToken);

        if (residuo is null)
            return ServiceResult<TrazabilidadResiduoDto>.Fail("Residuo no encontrado.", StatusCodes.Status404NotFound);

        var esGlobal = await TieneRolAsync(usuarioId, "AMBIENTAL", cancellationToken) ||
                       await TieneRolAsync(usuarioId, "ADMINISTRADOR", cancellationToken);

        if (!esGlobal)
        {
            var now = DateTime.UtcNow;
            var tieneAcceso = await unitOfWork.Repository<ControlGeneracionUsuario>().Query()
                .AnyAsync(x =>
                    x.ControlGeneracionId == residuo.Registro.ControlGeneracionId!.Value &&
                    x.UsuarioId == usuarioId &&
                    x.EsActivo &&
                    x.FechaDesde <= now &&
                    (!x.FechaHasta.HasValue || x.FechaHasta.Value >= now),
                    cancellationToken);

            if (!tieneAcceso)
                return ServiceResult<TrazabilidadResiduoDto>.Fail("Residuo no encontrado o sin acceso.", StatusCodes.Status404NotFound);
        }

        var movimientos = await unitOfWork.Repository<MovimientoResiduo>().Query()
            .Where(x => x.RegistroResiduoId == registroResiduoId && !x.Eliminado)
            .Include(x => x.PuntoDestino)
            .Include(x => x.RegistradoPorUsuario)
            .OrderBy(x => x.FechaMovimiento)
            .ToListAsync(cancellationToken);

        var retiros = await unitOfWork.Repository<RetiroDetalle>().Query()
            .Where(x =>
                x.RegistroResiduoId == registroResiduoId &&
                !x.Retiro.Eliminado &&
                x.Retiro.EstadoRetiro.Codigo != "ANULADO")
            .Include(x => x.Retiro).ThenInclude(x => x.EmpresaGestora)
            .Include(x => x.Retiro).ThenInclude(x => x.PuntoAlmacenamiento)
            .Include(x => x.Retiro).ThenInclude(x => x.EstadoRetiro)
            .OrderBy(x => x.Retiro.FechaRetiro)
            .ToListAsync(cancellationToken);

        var retiroIds = retiros.Select(x => x.RetiroId).Distinct().ToArray();
        var disposiciones = retiroIds.Length == 0
            ? new List<DisposicionFinal>()
            : await unitOfWork.Repository<DisposicionFinal>().Query()
                .Where(x =>
                    retiroIds.Contains(x.RetiroId) &&
                    !x.Eliminado &&
                    x.EstadoDisposicion.Codigo != "ANULADA")
                .Include(x => x.TipoTratamiento)
                .Include(x => x.EstadoDisposicion)
                .Include(x => x.EmpresaGestora)
                .Include(x => x.Evidencias.Where(e => !e.Eliminado))
                .OrderBy(x => x.FechaDisposicion)
                .ToListAsync(cancellationToken);

        var eventos = new List<TrazabilidadResiduoEventoDto>
        {
            new(
                residuo.Registro.FechaRegistro,
                "GENERACION",
                "Generación y registro",
                "REGISTRADO",
                residuo.Cantidad,
                residuo.UnidadMedida.Codigo,
                $"Registrado por {NombreUsuario(residuo.Registro.RegistradoPorUsuario)}" +
                (residuo.Registro.PuntoGeneracion is null
                    ? string.Empty
                    : $" · Punto: {residuo.Registro.PuntoGeneracion.Nombre}"))
        };

        eventos.AddRange(movimientos.Select(x => new TrazabilidadResiduoEventoDto(
            x.FechaMovimiento,
            "ALMACENAMIENTO",
            "Traslado a almacén temporal",
            "ALMACENADO",
            x.Cantidad,
            residuo.UnidadMedida.Codigo,
            $"{x.PuntoDestino.Nombre} · Registrado por {NombreUsuario(x.RegistradoPorUsuario)}")));

        foreach (var detalle in retiros)
        {
            var textoConciliacion = detalle.TipoRetiro == "TOTAL"
                ? $"Saldo estimado antes: {detalle.SaldoEstimadoAntes:0.###} {residuo.UnidadMedida.Codigo} · Diferencia de conciliación: {(detalle.DiferenciaConciliacion ?? 0):+0.###;-0.###;0} {residuo.UnidadMedida.Codigo} · Saldo operativo: 0"
                : $"Saldo estimado antes: {detalle.SaldoEstimadoAntes:0.###} {residuo.UnidadMedida.Codigo} · Saldo estimado después: {Math.Max(0m, detalle.SaldoEstimadoAntes - detalle.Cantidad):0.###} {residuo.UnidadMedida.Codigo}";

            eventos.Add(new TrazabilidadResiduoEventoDto(
                detalle.Retiro.FechaRetiro,
                "RETIRO",
                $"Retiro EO-RS · {detalle.TipoRetiro}",
                detalle.Retiro.EstadoRetiro.Codigo,
                detalle.Cantidad,
                residuo.UnidadMedida.Codigo,
                $"{detalle.Retiro.Codigo} · EO-RS: {NombreEmpresa(detalle.Retiro.EmpresaGestora)} · {textoConciliacion}"));

            foreach (var disposicion in disposiciones.Where(x => x.RetiroId == detalle.RetiroId))
            {
                var fechaEvento = disposicion.FechaDisposicion ?? disposicion.CreadoUtc;
                eventos.Add(new TrazabilidadResiduoEventoDto(
                    fechaEvento,
                    "DESTINO_FINAL",
                    disposicion.TipoTratamiento.EsValorizacion ? "Valorización" : "Destino final / tratamiento",
                    disposicion.EstadoDisposicion.Codigo,
                    null,
                    null,
                    $"{disposicion.TipoTratamiento.Nombre} · {NombreEmpresa(disposicion.EmpresaGestora)} · {disposicion.Evidencias.Count} evidencia(s)" +
                    (string.IsNullOrWhiteSpace(disposicion.CodigoDocumento)
                        ? string.Empty
                        : $" · Documento: {disposicion.CodigoDocumento}")));
            }
        }

        var ultimoTotal = retiros
            .Where(x => x.TipoRetiro == "TOTAL")
            .OrderByDescending(x => x.Retiro.FechaRetiro)
            .ThenByDescending(x => x.CreadoUtc)
            .FirstOrDefault();

        var cierreCompleto = ultimoTotal is not null &&
            disposiciones.Any(x =>
                x.RetiroId == ultimoTotal.RetiroId &&
                x.EstadoDisposicion.Codigo == "VALIDADA");

        var estadoActual = cierreCompleto
            ? "Cierre documental completo"
            : ultimoTotal is not null
                ? "Retirado · documentación pendiente"
                : retiros.Count > 0
                    ? "Retiro parcial"
                    : movimientos.Count > 0
                        ? "En almacén temporal"
                        : "Generado";

        var dto = new TrazabilidadResiduoDto(
            residuo.RegistroResiduoId,
            residuo.Residuo.Nombre,
            residuo.Residuo.ClasificacionResiduo.Nombre,
            residuo.Registro.ControlGeneracion?.Codigo ?? "Control",
            residuo.Registro.Sede.Nombre,
            residuo.Cantidad,
            residuo.UnidadMedida.Codigo,
            estadoActual,
            cierreCompleto,
            eventos
                .OrderBy(x => x.Fecha)
                .ThenBy(x => OrdenEvento(x.TipoCodigo))
                .ToArray());

        return ServiceResult<TrazabilidadResiduoDto>.Ok(dto);
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

    private static int OrdenEvento(string tipo) => tipo switch
    {
        "GENERACION" => 1,
        "ALMACENAMIENTO" => 2,
        "RETIRO" => 3,
        "DESTINO_FINAL" => 4,
        _ => 99
    };

    private static string NombreUsuario(Usuario usuario) =>
        string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string NombreEmpresa(Empresa empresa) =>
        string.IsNullOrWhiteSpace(empresa.NombreComercial)
            ? empresa.RazonSocial
            : empresa.NombreComercial;

    private Task<bool> TieneRolAsync(Guid usuarioId, string rol, CancellationToken cancellationToken) =>
        unitOfWork.Repository<UsuarioRol>().Query()
            .AnyAsync(x => x.UsuarioId == usuarioId && x.Rol.Codigo == rol && x.Rol.EsActivo, cancellationToken);
}
