using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Persistence;

namespace ReciclaApi.Application.Services;

public interface IRetiroService
{
    Task<IReadOnlyCollection<RetiroListItemDto>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<RetiroDetalleDto>> ObtenerAsync(Guid retiroId, Guid usuarioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<RetiroDetalleDto>> CrearAsync(Guid usuarioId, CrearRetiroRequest request, CancellationToken cancellationToken = default);
}

public sealed class RetiroService(ReciclaDbContext context) : IRetiroService
{
    public async Task<IReadOnlyCollection<RetiroListItemDto>> ListarAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var esGlobal = await EsGlobalAsync(usuarioId, cancellationToken);

        IQueryable<Retiro> query = context.Retiros
            .AsNoTracking()
            .Where(x => !x.Eliminado)
            .Include(x => x.Sede)
            .Include(x => x.PuntoAlmacenamiento)
            .Include(x => x.EmpresaGestora)
            .Include(x => x.EstadoRetiro)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.UnidadMedida);

        if (!esGlobal)
            query = query.Where(x => x.CreadoPorUsuarioId == usuarioId);

        var retiros = await query
            .OrderByDescending(x => x.FechaRetiro)
            .ThenByDescending(x => x.CreadoUtc)
            .ToListAsync(cancellationToken);

        return retiros.Select(MapListItem).ToArray();
    }

    public async Task<ServiceResult<RetiroDetalleDto>> ObtenerAsync(
        Guid retiroId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var retiro = await CargarDetalleAsync(retiroId, cancellationToken);
        if (retiro is null)
            return ServiceResult<RetiroDetalleDto>.Fail("Retiro no encontrado.", StatusCodes.Status404NotFound);

        if (!await EsGlobalAsync(usuarioId, cancellationToken) && retiro.CreadoPorUsuarioId != usuarioId)
            return ServiceResult<RetiroDetalleDto>.Fail("Retiro no encontrado o sin acceso.", StatusCodes.Status404NotFound);

        return ServiceResult<RetiroDetalleDto>.Ok(MapDetalle(retiro));
    }

    public async Task<ServiceResult<RetiroDetalleDto>> CrearAsync(
        Guid usuarioId,
        CrearRetiroRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Detalles is null || request.Detalles.Count == 0)
            return ServiceResult<RetiroDetalleDto>.Fail("Selecciona al menos un residuo para el retiro.", StatusCodes.Status400BadRequest);

        if (request.Detalles.Any(x => x.Cantidad <= 0))
            return ServiceResult<RetiroDetalleDto>.Fail("Todas las cantidades del retiro deben ser mayores a cero.", StatusCodes.Status400BadRequest);

        if (request.Detalles.Select(x => x.RegistroResiduoId).Distinct().Count() != request.Detalles.Count)
            return ServiceResult<RetiroDetalleDto>.Fail("Un residuo no puede aparecer más de una vez en el mismo retiro.", StatusCodes.Status400BadRequest);

        var esAmbiental = await TieneRolAsync(usuarioId, "AMBIENTAL", cancellationToken);
        var esResponsable = await TieneRolAsync(usuarioId, "RESPONSABLE_OPERATIVO", cancellationToken);
        if (!esAmbiental && !esResponsable)
            return ServiceResult<RetiroDetalleDto>.Fail("No tienes permiso para registrar retiros.", StatusCodes.Status403Forbidden);

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var punto = await context.PuntosResiduo
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.PuntoResiduoId == request.PuntoAlmacenamientoId &&
                x.SedeId == request.SedeId &&
                x.EsActivo &&
                (x.Tipo == "ALMACENAMIENTO" || x.Tipo == "AMBOS"),
                cancellationToken);

        if (punto is null)
            return await FailRollbackAsync("El punto de almacenamiento no es válido para la sede seleccionada.", StatusCodes.Status400BadRequest, transaction, cancellationToken);

        var gestor = await context.Empresas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmpresaId == request.EmpresaGestoraId && x.EsActivo && x.EsGestoraResiduos, cancellationToken);

        if (gestor is null)
            return await FailRollbackAsync("Selecciona una empresa gestora de residuos válida.", StatusCodes.Status400BadRequest, transaction, cancellationToken);

        var ids = request.Detalles.Select(x => x.RegistroResiduoId).ToArray();
        var residuos = await context.RegistroResiduos
            .Where(x => ids.Contains(x.RegistroResiduoId) && !x.Eliminado && !x.Registro.Eliminado)
            .Include(x => x.Registro).ThenInclude(x => x.EstadoRegistro)
            .Include(x => x.Registro).ThenInclude(x => x.ControlGeneracion)
            .Include(x => x.Residuo)
            .Include(x => x.UnidadMedida)
            .ToListAsync(cancellationToken);

        if (residuos.Count != ids.Length)
            return await FailRollbackAsync("Uno o más residuos seleccionados ya no existen.", StatusCodes.Status409Conflict, transaction, cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var detalle in request.Detalles)
        {
            var residuo = residuos.Single(x => x.RegistroResiduoId == detalle.RegistroResiduoId);

            if (residuo.Registro.EstadoRegistro.Codigo != "REGISTRADO" || residuo.Registro.ControlGeneracionId is null)
                return await FailRollbackAsync("Solo se pueden retirar residuos de registros finalizados dentro de un control de generación.", StatusCodes.Status409Conflict, transaction, cancellationToken);

            if (residuo.Registro.SedeId != request.SedeId)
                return await FailRollbackAsync("Todos los residuos del retiro deben pertenecer a la misma sede.", StatusCodes.Status400BadRequest, transaction, cancellationToken);

            if (!esAmbiental)
            {
                var autorizado = await context.Set<ControlGeneracionUsuario>()
                    .AsNoTracking()
                    .AnyAsync(x =>
                        x.ControlGeneracionId == residuo.Registro.ControlGeneracionId.Value &&
                        x.UsuarioId == usuarioId &&
                        x.RolControl == "RESPONSABLE" &&
                        x.EsActivo &&
                        x.FechaDesde <= now &&
                        (!x.FechaHasta.HasValue || x.FechaHasta.Value >= now),
                        cancellationToken);

                if (!autorizado)
                    return await FailRollbackAsync("El retiro incluye residuos de un control que no administras.", StatusCodes.Status403Forbidden, transaction, cancellationToken);
            }

            var movimientos = await context.MovimientosResiduo
                .AsNoTracking()
                .Where(x =>
                    x.RegistroResiduoId == detalle.RegistroResiduoId &&
                    x.PuntoDestinoId == request.PuntoAlmacenamientoId &&
                    !x.Eliminado)
                .Select(x => x.Cantidad)
                .ToListAsync(cancellationToken);
            var almacenado = movimientos.Sum();

            var retirosPrevios = await context.RetiroDetalles
                .AsNoTracking()
                .Where(x =>
                    x.RegistroResiduoId == detalle.RegistroResiduoId &&
                    x.Retiro.PuntoAlmacenamientoId == request.PuntoAlmacenamientoId &&
                    !x.Retiro.Eliminado &&
                    x.Retiro.EstadoRetiro.Codigo != "ANULADO")
                .Select(x => x.Cantidad)
                .ToListAsync(cancellationToken);
            var retirado = retirosPrevios.Sum();
            var saldo = almacenado - retirado;

            if (detalle.Cantidad > saldo)
            {
                return await FailRollbackAsync(
                    $"El saldo de {residuo.Residuo.Nombre} en {punto.Nombre} cambió. Disponible: {Math.Max(0m, saldo):0.###} {residuo.UnidadMedida.Codigo}.",
                    StatusCodes.Status409Conflict,
                    transaction,
                    cancellationToken);
            }
        }

        var estadoRetirado = await context.EstadosRetiro.FirstAsync(x => x.Codigo == "RETIRADO", cancellationToken);
        var estadoSync = await context.EstadosSincronizacion.FirstAsync(x => x.Codigo == "SINCRONIZADO", cancellationToken);
        var codigo = $"RET-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        var retiro = new Retiro
        {
            Codigo = codigo,
            FechaRetiro = request.FechaRetiro,
            SedeId = request.SedeId,
            PuntoAlmacenamientoId = request.PuntoAlmacenamientoId,
            EmpresaGestoraId = request.EmpresaGestoraId,
            EstadoRetiroId = estadoRetirado.EstadoRetiroId,
            DocumentoTransporte = Limpiar(request.DocumentoTransporte),
            Vehiculo = Limpiar(request.Vehiculo),
            Placa = Limpiar(request.Placa),
            Conductor = Limpiar(request.Conductor),
            Observacion = Limpiar(request.Observacion),
            EstadoSincronizacionId = estadoSync.EstadoSincronizacionId,
            CreadoPorUsuarioId = usuarioId
        };

        foreach (var detalle in request.Detalles)
        {
            var residuo = residuos.Single(x => x.RegistroResiduoId == detalle.RegistroResiduoId);
            retiro.Detalles.Add(new RetiroDetalle
            {
                RegistroResiduoId = detalle.RegistroResiduoId,
                Cantidad = detalle.Cantidad,
                UnidadMedidaId = residuo.UnidadMedidaId
            });
        }

        context.Retiros.Add(retiro);
        context.Auditoria.Add(new Auditoria
        {
            UsuarioId = usuarioId,
            Entidad = "Retiro",
            EntidadId = retiro.RetiroId,
            Accion = "CREAR",
            DatosDespuesJson = JsonSerializer.Serialize(new
            {
                retiro.Codigo,
                retiro.FechaRetiro,
                retiro.SedeId,
                retiro.PuntoAlmacenamientoId,
                retiro.EmpresaGestoraId,
                Detalles = request.Detalles
            })
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var creado = await CargarDetalleAsync(retiro.RetiroId, cancellationToken);
        return creado is null
            ? ServiceResult<RetiroDetalleDto>.Fail("El retiro fue registrado, pero no pudo recargarse.", StatusCodes.Status500InternalServerError)
            : ServiceResult<RetiroDetalleDto>.Ok(MapDetalle(creado), StatusCodes.Status201Created);
    }

    private async Task<Retiro?> CargarDetalleAsync(Guid retiroId, CancellationToken cancellationToken) =>
        await context.Retiros
            .AsNoTracking()
            .Where(x => x.RetiroId == retiroId && !x.Eliminado)
            .Include(x => x.Sede)
            .Include(x => x.PuntoAlmacenamiento)
            .Include(x => x.EmpresaGestora)
            .Include(x => x.EstadoRetiro)
            .Include(x => x.CreadoPorUsuario)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.UnidadMedida)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.RegistroResiduo)
                    .ThenInclude(x => x.Residuo)
                        .ThenInclude(x => x.ClasificacionResiduo)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.RegistroResiduo)
                    .ThenInclude(x => x.Registro)
                        .ThenInclude(x => x.ControlGeneracion)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<bool> EsGlobalAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        await context.UsuarioRoles
            .AsNoTracking()
            .AnyAsync(x =>
                x.UsuarioId == usuarioId &&
                x.Rol.EsActivo &&
                (x.Rol.Codigo == "AMBIENTAL" || x.Rol.Codigo == "ADMINISTRADOR"),
                cancellationToken);

    private async Task<bool> TieneRolAsync(Guid usuarioId, string codigo, CancellationToken cancellationToken) =>
        await context.UsuarioRoles
            .AsNoTracking()
            .AnyAsync(x => x.UsuarioId == usuarioId && x.Rol.EsActivo && x.Rol.Codigo == codigo, cancellationToken);

    private static RetiroListItemDto MapListItem(Retiro retiro) => new(
        retiro.RetiroId,
        retiro.Codigo,
        retiro.FechaRetiro,
        retiro.SedeId,
        retiro.Sede.Nombre,
        retiro.PuntoAlmacenamientoId!.Value,
        retiro.PuntoAlmacenamiento?.Nombre ?? "Sin punto",
        retiro.EmpresaGestoraId,
        NombreEmpresa(retiro.EmpresaGestora),
        retiro.EstadoRetiro.Nombre,
        retiro.Detalles.Count,
        Totales(retiro.Detalles),
        retiro.CreadoUtc);

    private static RetiroDetalleDto MapDetalle(Retiro retiro)
    {
        var creador = string.Join(" ", new[] { retiro.CreadoPorUsuario.Nombres, retiro.CreadoPorUsuario.Apellidos }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        var detalles = retiro.Detalles.Select(x => new RetiroDetalleItemDto(
            x.RetiroDetalleId,
            x.RegistroResiduoId,
            x.RegistroResiduo.RegistroId,
            x.RegistroResiduo.Registro.ControlGeneracionId!.Value,
            x.RegistroResiduo.Registro.ControlGeneracion?.Codigo ?? "Control",
            x.RegistroResiduo.Residuo.Nombre,
            x.RegistroResiduo.Residuo.ClasificacionResiduo.Nombre,
            x.Cantidad,
            x.UnidadMedidaId,
            x.UnidadMedida.Codigo)).ToArray();

        return new RetiroDetalleDto(
            retiro.RetiroId,
            retiro.Codigo,
            retiro.FechaRetiro,
            retiro.SedeId,
            retiro.Sede.Nombre,
            retiro.PuntoAlmacenamientoId!.Value,
            retiro.PuntoAlmacenamiento?.Nombre ?? "Sin punto",
            retiro.EmpresaGestoraId,
            NombreEmpresa(retiro.EmpresaGestora),
            retiro.EstadoRetiro.Nombre,
            retiro.DocumentoTransporte,
            retiro.Vehiculo,
            retiro.Placa,
            retiro.Conductor,
            retiro.Observacion,
            retiro.CreadoPorUsuarioId,
            creador,
            retiro.CreadoUtc,
            detalles,
            Totales(retiro.Detalles));
    }

    private static IReadOnlyCollection<RetiroResumenUnidadDto> Totales(IEnumerable<RetiroDetalle> detalles) =>
        detalles
            .GroupBy(x => x.UnidadMedida.Codigo)
            .Select(g => new RetiroResumenUnidadDto(g.Key, g.Sum(x => x.Cantidad)))
            .OrderBy(x => x.Unidad)
            .ToArray();

    private static string NombreEmpresa(Empresa empresa) =>
        string.IsNullOrWhiteSpace(empresa.NombreComercial) ? empresa.RazonSocial : empresa.NombreComercial;

    private static string? Limpiar(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<ServiceResult<RetiroDetalleDto>> FailRollbackAsync(
        string error,
        int statusCode,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return ServiceResult<RetiroDetalleDto>.Fail(error, statusCode);
    }
}
