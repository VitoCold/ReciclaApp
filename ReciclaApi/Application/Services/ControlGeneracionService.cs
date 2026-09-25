using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Repositories;

namespace ReciclaApi.Application.Services;

public interface IControlGeneracionService
{
    Task<IReadOnlyCollection<ControlGeneracionListItemDto>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<ControlGeneracionDetalleDto>> ObtenerAsync(Guid controlId, Guid usuarioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<ControlGeneracionDetalleDto>> CrearAsync(Guid usuarioId, CrearControlGeneracionRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<ControlGeneracionDetalleDto>> ActualizarCabeceraAsync(Guid controlId, Guid usuarioId, ActualizarControlGeneracionRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<ControlGeneracionDetalleDto>> AprobarAsync(Guid controlId, Guid usuarioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<ControlGeneracionDetalleDto>> RechazarAsync(Guid controlId, Guid usuarioId, RechazarControlGeneracionRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<ControlGeneracionDetalleDto>> CambiarEstadoAsync(Guid controlId, Guid usuarioId, string estadoDestino, CambiarEstadoControlGeneracionRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<ControlGeneracionDetalleDto>> AsignarUsuarioAsync(Guid controlId, Guid usuarioId, AsignarControlGeneracionUsuarioRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> DesasignarUsuarioAsync(Guid controlId, Guid asignacionId, Guid usuarioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyCollection<RegistroControlListItemDto>>> ListarRegistrosAsync(Guid controlId, Guid usuarioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<RegistroControlCreadoDto>> CrearRegistroAsync(Guid controlId, Guid usuarioId, CrearRegistroEnControlRequest request, CancellationToken cancellationToken = default);
}

public sealed class ControlGeneracionService(IUnitOfWork unitOfWork) : IControlGeneracionService
{
    private static readonly string[] RolesControl = ["RESPONSABLE", "REGISTRADOR"];

    public async Task<IReadOnlyCollection<ControlGeneracionListItemDto>> ListarAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var global = await TieneRolAsync(usuarioId, "AMBIENTAL", cancellationToken)
            || await TieneRolAsync(usuarioId, "ADMINISTRADOR", cancellationToken);
        var now = DateTime.UtcNow;

        var query = QueryControles();
        if (!global)
        {
            query = query.Where(x => x.Usuarios.Any(u =>
                u.UsuarioId == usuarioId &&
                u.EsActivo &&
                u.FechaDesde <= now &&
                (!u.FechaHasta.HasValue || u.FechaHasta.Value >= now)));
        }

        var controles = await query
            .OrderByDescending(x => x.CreadoUtc)
            .ToListAsync(cancellationToken);

        return controles.Select(MapListItem).ToArray();
    }

    public async Task<ServiceResult<ControlGeneracionDetalleDto>> ObtenerAsync(
        Guid controlId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (!await PuedeVerControlAsync(controlId, usuarioId, cancellationToken))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Control de generación no encontrado o sin acceso.", StatusCodes.Status404NotFound);

        var control = await QueryControles()
            .FirstOrDefaultAsync(x => x.ControlGeneracionId == controlId && !x.Eliminado, cancellationToken);

        return control is null
            ? ServiceResult<ControlGeneracionDetalleDto>.Fail("Control de generación no encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<ControlGeneracionDetalleDto>.Ok(MapDetalle(control));
    }

    public async Task<ServiceResult<ControlGeneracionDetalleDto>> CrearAsync(
        Guid usuarioId,
        CrearControlGeneracionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await TieneRolAsync(usuarioId, "RESPONSABLE_OPERATIVO", cancellationToken))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Solo un responsable operativo puede crear un control de generación.", StatusCodes.Status403Forbidden);

        var error = await ValidarCabeceraAsync(request.SedeId, request.EmpresaResponsableId, request.ProyectoId, request.ActividadId, request.PuntoGeneracionId, request.FechaInicio, request.FechaFin, cancellationToken);
        if (error is not null)
            return ServiceResult<ControlGeneracionDetalleDto>.Fail(error, StatusCodes.Status400BadRequest);

        var estado = await GetEstadoAsync("PENDIENTE_APROBACION", cancellationToken);
        var control = new ControlGeneracion
        {
            Codigo = GenerarCodigo(),
            SedeId = request.SedeId,
            EmpresaResponsableId = request.EmpresaResponsableId,
            ProyectoId = request.ProyectoId,
            ActividadId = request.ActividadId,
            PuntoGeneracionId = request.PuntoGeneracionId,
            DescripcionTrabajo = request.DescripcionTrabajo,
            FechaInicio = request.FechaInicio,
            FechaFin = request.FechaFin,
            EstadoControlGeneracionId = estado.EstadoControlGeneracionId,
            Observacion = request.Observacion,
            CreadoPorUsuarioId = usuarioId
        };

        await unitOfWork.Repository<ControlGeneracion>().AddAsync(control, cancellationToken);
        await unitOfWork.Repository<ControlGeneracionUsuario>().AddAsync(new ControlGeneracionUsuario
        {
            ControlGeneracionId = control.ControlGeneracionId,
            UsuarioId = usuarioId,
            RolControl = "RESPONSABLE",
            EsPrincipal = true,
            FechaDesde = request.FechaInicio,
            AsignadoPorUsuarioId = usuarioId
        }, cancellationToken);

        await AuditarAsync(control.ControlGeneracionId, usuarioId, "CREAR", null, new
        {
            control.Codigo,
            control.SedeId,
            control.EmpresaResponsableId,
            control.ProyectoId,
            control.ActividadId,
            control.PuntoGeneracionId,
            control.FechaInicio,
            control.FechaFin,
            Estado = "PENDIENTE_APROBACION"
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var creado = await QueryControles().FirstAsync(x => x.ControlGeneracionId == control.ControlGeneracionId, cancellationToken);
        return ServiceResult<ControlGeneracionDetalleDto>.Ok(MapDetalle(creado), StatusCodes.Status201Created);
    }

    public async Task<ServiceResult<ControlGeneracionDetalleDto>> ActualizarCabeceraAsync(
        Guid controlId,
        Guid usuarioId,
        ActualizarControlGeneracionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await TieneRolAsync(usuarioId, "AMBIENTAL", cancellationToken))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Solo Ambiental puede modificar la información inicial del control.", StatusCodes.Status403Forbidden);

        if (string.IsNullOrWhiteSpace(request.MotivoModificacion))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Debe indicar el motivo de la modificación.", StatusCodes.Status400BadRequest);

        var control = await QueryControles(tracking: true)
            .FirstOrDefaultAsync(x => x.ControlGeneracionId == controlId && !x.Eliminado, cancellationToken);
        if (control is null)
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Control de generación no encontrado.", StatusCodes.Status404NotFound);

        if (control.EstadoControlGeneracion.Codigo == "ANULADO")
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("No se puede modificar un control anulado.", StatusCodes.Status409Conflict);

        var error = await ValidarCabeceraAsync(request.SedeId, request.EmpresaResponsableId, request.ProyectoId, request.ActividadId, request.PuntoGeneracionId, request.FechaInicio, request.FechaFin, cancellationToken);
        if (error is not null)
            return ServiceResult<ControlGeneracionDetalleDto>.Fail(error, StatusCodes.Status400BadRequest);

        var antes = new
        {
            control.SedeId,
            control.EmpresaResponsableId,
            control.ProyectoId,
            control.ActividadId,
            control.PuntoGeneracionId,
            control.DescripcionTrabajo,
            control.FechaInicio,
            control.FechaFin,
            control.Observacion
        };

        control.SedeId = request.SedeId;
        control.EmpresaResponsableId = request.EmpresaResponsableId;
        control.ProyectoId = request.ProyectoId;
        control.ActividadId = request.ActividadId;
        control.PuntoGeneracionId = request.PuntoGeneracionId;
        control.DescripcionTrabajo = request.DescripcionTrabajo;
        control.FechaInicio = request.FechaInicio;
        control.FechaFin = request.FechaFin;
        control.Observacion = request.Observacion;
        control.MotivoUltimoCambio = request.MotivoModificacion.Trim();
        control.ActualizadoUtc = DateTime.UtcNow;

        await AuditarAsync(controlId, usuarioId, "MODIFICAR_CABECERA", antes, new
        {
            control.SedeId,
            control.EmpresaResponsableId,
            control.ProyectoId,
            control.ActividadId,
            control.PuntoGeneracionId,
            control.DescripcionTrabajo,
            control.FechaInicio,
            control.FechaFin,
            control.Observacion,
            Motivo = request.MotivoModificacion
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult<ControlGeneracionDetalleDto>.Ok(MapDetalle(control));
    }

    public async Task<ServiceResult<ControlGeneracionDetalleDto>> AprobarAsync(
        Guid controlId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (!await TieneRolAsync(usuarioId, "AMBIENTAL", cancellationToken))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Solo Ambiental puede aprobar controles de generación.", StatusCodes.Status403Forbidden);

        var control = await QueryControles(tracking: true)
            .FirstOrDefaultAsync(x => x.ControlGeneracionId == controlId && !x.Eliminado, cancellationToken);
        if (control is null)
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Control de generación no encontrado.", StatusCodes.Status404NotFound);

        if (control.EstadoControlGeneracion.Codigo is not ("PENDIENTE_APROBACION" or "RECHAZADO"))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("El control no está pendiente de aprobación.", StatusCodes.Status409Conflict);

        var activo = await GetEstadoAsync("ACTIVO", cancellationToken);
        control.EstadoControlGeneracionId = activo.EstadoControlGeneracionId;
        control.EstadoControlGeneracion = activo;
        control.AprobadoPorUsuarioId = usuarioId;
        control.AprobadoUtc = DateTime.UtcNow;
        control.MotivoUltimoCambio = "Aprobado por Ambiental";
        control.ActualizadoUtc = DateTime.UtcNow;

        await AuditarAsync(controlId, usuarioId, "APROBAR", null, new { Estado = "ACTIVO" }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult<ControlGeneracionDetalleDto>.Ok(MapDetalle(control));
    }

    public async Task<ServiceResult<ControlGeneracionDetalleDto>> RechazarAsync(
        Guid controlId,
        Guid usuarioId,
        RechazarControlGeneracionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await TieneRolAsync(usuarioId, "AMBIENTAL", cancellationToken))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Solo Ambiental puede rechazar controles de generación.", StatusCodes.Status403Forbidden);

        if (string.IsNullOrWhiteSpace(request.Motivo))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Debe indicar el motivo del rechazo.", StatusCodes.Status400BadRequest);

        var control = await QueryControles(tracking: true)
            .FirstOrDefaultAsync(x => x.ControlGeneracionId == controlId && !x.Eliminado, cancellationToken);
        if (control is null)
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Control de generación no encontrado.", StatusCodes.Status404NotFound);

        if (control.EstadoControlGeneracion.Codigo != "PENDIENTE_APROBACION")
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Solo se puede rechazar un control pendiente de aprobación.", StatusCodes.Status409Conflict);

        var rechazado = await GetEstadoAsync("RECHAZADO", cancellationToken);
        control.EstadoControlGeneracionId = rechazado.EstadoControlGeneracionId;
        control.EstadoControlGeneracion = rechazado;
        control.MotivoUltimoCambio = request.Motivo.Trim();
        control.ActualizadoUtc = DateTime.UtcNow;

        await AuditarAsync(controlId, usuarioId, "RECHAZAR", null, new { Estado = "RECHAZADO", Motivo = request.Motivo }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult<ControlGeneracionDetalleDto>.Ok(MapDetalle(control));
    }

    public async Task<ServiceResult<ControlGeneracionDetalleDto>> CambiarEstadoAsync(
        Guid controlId,
        Guid usuarioId,
        string estadoDestino,
        CambiarEstadoControlGeneracionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await TieneRolAsync(usuarioId, "AMBIENTAL", cancellationToken))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Solo Ambiental puede cambiar el estado del control.", StatusCodes.Status403Forbidden);

        if (string.IsNullOrWhiteSpace(request.Motivo))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Debe indicar el motivo del cambio de estado.", StatusCodes.Status400BadRequest);

        estadoDestino = estadoDestino.ToUpperInvariant();
        var control = await QueryControles(tracking: true)
            .FirstOrDefaultAsync(x => x.ControlGeneracionId == controlId && !x.Eliminado, cancellationToken);
        if (control is null)
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Control de generación no encontrado.", StatusCodes.Status404NotFound);

        if (!TransicionPermitida(control.EstadoControlGeneracion.Codigo, estadoDestino))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail($"No se permite cambiar de {control.EstadoControlGeneracion.Codigo} a {estadoDestino}.", StatusCodes.Status409Conflict);

        var destino = await GetEstadoAsync(estadoDestino, cancellationToken);
        var origen = control.EstadoControlGeneracion.Codigo;
        control.EstadoControlGeneracionId = destino.EstadoControlGeneracionId;
        control.EstadoControlGeneracion = destino;
        control.MotivoUltimoCambio = request.Motivo.Trim();
        control.ActualizadoUtc = DateTime.UtcNow;

        await AuditarAsync(controlId, usuarioId, "CAMBIAR_ESTADO", new { Estado = origen }, new { Estado = estadoDestino, Motivo = request.Motivo }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult<ControlGeneracionDetalleDto>.Ok(MapDetalle(control));
    }

    public async Task<ServiceResult<ControlGeneracionDetalleDto>> AsignarUsuarioAsync(
        Guid controlId,
        Guid usuarioId,
        AsignarControlGeneracionUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await EsResponsableActivoAsync(controlId, usuarioId, cancellationToken))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Solo un responsable vigente del control puede gestionar su equipo.", StatusCodes.Status403Forbidden);

        var control = await QueryControles(tracking: true)
            .FirstOrDefaultAsync(x => x.ControlGeneracionId == controlId && !x.Eliminado, cancellationToken);
        if (control is null)
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Control de generación no encontrado.", StatusCodes.Status404NotFound);

        if (control.EstadoControlGeneracion.Codigo is "CERRADO" or "ANULADO")
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("No se pueden cambiar asignaciones en un control cerrado o anulado.", StatusCodes.Status409Conflict);

        var rolControl = request.RolControl.Trim().ToUpperInvariant();
        if (!RolesControl.Contains(rolControl))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Rol de control inválido.", StatusCodes.Status400BadRequest);

        if (request.EsPrincipal && rolControl != "RESPONSABLE")
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Solo un responsable puede marcarse como principal.", StatusCodes.Status400BadRequest);

        var desde = request.FechaDesde ?? DateTime.UtcNow;
        if (request.FechaHasta.HasValue && request.FechaHasta.Value < desde)
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("La fecha fin de asignación no puede ser anterior a la fecha de inicio.", StatusCodes.Status400BadRequest);

        var usuario = await unitOfWork.Repository<Usuario>().Query()
            .FirstOrDefaultAsync(x => x.UsuarioId == request.UsuarioId && x.EsActivo, cancellationToken);
        if (usuario is null)
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("Usuario no encontrado o inactivo.", StatusCodes.Status400BadRequest);

        var rolGlobalRequerido = rolControl == "RESPONSABLE" ? "RESPONSABLE_OPERATIVO" : "REGISTRADOR";
        if (!await TieneRolAsync(request.UsuarioId, rolGlobalRequerido, cancellationToken))
            return ServiceResult<ControlGeneracionDetalleDto>.Fail($"El usuario no tiene el rol global {rolGlobalRequerido}.", StatusCodes.Status400BadRequest);

        var duplicado = control.Usuarios.Any(x =>
            x.UsuarioId == request.UsuarioId &&
            x.RolControl == rolControl &&
            x.EsActivo &&
            (!x.FechaHasta.HasValue || x.FechaHasta.Value >= DateTime.UtcNow));
        if (duplicado)
            return ServiceResult<ControlGeneracionDetalleDto>.Fail("El usuario ya tiene una asignación vigente con ese rol.", StatusCodes.Status409Conflict);

        if (request.EsPrincipal)
        {
            foreach (var responsable in control.Usuarios.Where(x => x.RolControl == "RESPONSABLE" && x.EsPrincipal))
                responsable.EsPrincipal = false;
        }

        await unitOfWork.Repository<ControlGeneracionUsuario>().AddAsync(new ControlGeneracionUsuario
        {
            ControlGeneracionId = controlId,
            UsuarioId = request.UsuarioId,
            RolControl = rolControl,
            EsPrincipal = request.EsPrincipal,
            FechaDesde = desde,
            FechaHasta = request.FechaHasta,
            AsignadoPorUsuarioId = usuarioId
        }, cancellationToken);

        await AuditarAsync(controlId, usuarioId, "ASIGNAR_USUARIO", null, new
        {
            request.UsuarioId,
            RolControl = rolControl,
            request.EsPrincipal,
            FechaDesde = desde,
            request.FechaHasta
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        var actualizado = await QueryControles().FirstAsync(x => x.ControlGeneracionId == controlId, cancellationToken);
        return ServiceResult<ControlGeneracionDetalleDto>.Ok(MapDetalle(actualizado));
    }

    public async Task<ServiceResult> DesasignarUsuarioAsync(
        Guid controlId,
        Guid asignacionId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (!await EsResponsableActivoAsync(controlId, usuarioId, cancellationToken))
            return ServiceResult.Fail("Solo un responsable vigente del control puede gestionar su equipo.", StatusCodes.Status403Forbidden);

        var asignacion = await unitOfWork.Repository<ControlGeneracionUsuario>().Query(tracking: true)
            .Include(x => x.ControlGeneracion).ThenInclude(x => x.EstadoControlGeneracion)
            .FirstOrDefaultAsync(x => x.ControlGeneracionUsuarioId == asignacionId && x.ControlGeneracionId == controlId && x.EsActivo, cancellationToken);
        if (asignacion is null)
            return ServiceResult.Fail("Asignación no encontrada.", StatusCodes.Status404NotFound);

        if (asignacion.RolControl == "RESPONSABLE" && asignacion.ControlGeneracion.EstadoControlGeneracion.Codigo == "ACTIVO")
        {
            var now = DateTime.UtcNow;
            var otrosResponsables = await unitOfWork.Repository<ControlGeneracionUsuario>().Query()
                .CountAsync(x =>
                    x.ControlGeneracionId == controlId &&
                    x.ControlGeneracionUsuarioId != asignacionId &&
                    x.RolControl == "RESPONSABLE" &&
                    x.EsActivo &&
                    x.FechaDesde <= now &&
                    (!x.FechaHasta.HasValue || x.FechaHasta.Value >= now),
                    cancellationToken);

            if (otrosResponsables == 0)
                return ServiceResult.Fail("Un control activo debe conservar al menos un responsable vigente.", StatusCodes.Status409Conflict);
        }

        asignacion.EsActivo = false;
        asignacion.FechaHasta ??= DateTime.UtcNow;
        asignacion.ActualizadoUtc = DateTime.UtcNow;

        await AuditarAsync(controlId, usuarioId, "DESASIGNAR_USUARIO", new
        {
            asignacion.UsuarioId,
            asignacion.RolControl,
            asignacion.EsPrincipal
        }, null, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<IReadOnlyCollection<RegistroControlListItemDto>>> ListarRegistrosAsync(
        Guid controlId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (!await PuedeVerControlAsync(controlId, usuarioId, cancellationToken))
            return ServiceResult<IReadOnlyCollection<RegistroControlListItemDto>>.Fail("Control de generación no encontrado o sin acceso.", StatusCodes.Status404NotFound);

        var registros = await unitOfWork.Repository<Registro>().Query()
            .Where(x => x.ControlGeneracionId == controlId && !x.Eliminado)
            .Include(x => x.RegistradoPorUsuario)
            .Include(x => x.EstadoRegistro)
            .Include(x => x.Residuos.Where(r => !r.Eliminado))
            .OrderByDescending(x => x.FechaRegistro)
            .ToListAsync(cancellationToken);

        var result = registros.Select(x => new RegistroControlListItemDto(
            x.RegistroId,
            x.FechaRegistro,
            x.RegistradoPorUsuarioId,
            NombreUsuario(x.RegistradoPorUsuario),
            x.EstadoRegistro.Nombre,
            x.Residuos.Count,
            x.CreadoUtc)).ToArray();

        return ServiceResult<IReadOnlyCollection<RegistroControlListItemDto>>.Ok(result);
    }

    public async Task<ServiceResult<RegistroControlCreadoDto>> CrearRegistroAsync(
        Guid controlId,
        Guid usuarioId,
        CrearRegistroEnControlRequest request,
        CancellationToken cancellationToken = default)
    {
        var control = await QueryControles()
            .FirstOrDefaultAsync(x => x.ControlGeneracionId == controlId && !x.Eliminado, cancellationToken);
        if (control is null)
            return ServiceResult<RegistroControlCreadoDto>.Fail("Control de generación no encontrado.", StatusCodes.Status404NotFound);

        if (control.EstadoControlGeneracion.Codigo != "ACTIVO")
            return ServiceResult<RegistroControlCreadoDto>.Fail("Solo se pueden registrar residuos en un control activo.", StatusCodes.Status409Conflict);

        if (!await EsParticipanteOperativoActivoAsync(controlId, usuarioId, cancellationToken))
            return ServiceResult<RegistroControlCreadoDto>.Fail("No tiene una asignación vigente para registrar en este control.", StatusCodes.Status403Forbidden);

        if (request.FechaRegistro < control.FechaInicio || (control.FechaFin.HasValue && request.FechaRegistro > control.FechaFin.Value))
            return ServiceResult<RegistroControlCreadoDto>.Fail("La fecha del registro está fuera de la vigencia del control.", StatusCodes.Status400BadRequest);

        var estadoRegistro = await unitOfWork.Repository<EstadoRegistro>().Query(tracking: true)
            .FirstAsync(x => x.Codigo == "EN_PROCESO", cancellationToken);
        var estadoSync = await unitOfWork.Repository<EstadoSincronizacion>().Query(tracking: true)
            .FirstAsync(x => x.Codigo == "SINCRONIZADO", cancellationToken);

        var registro = new Registro
        {
            ControlGeneracionId = controlId,
            CodigoLocal = request.CodigoLocal,
            FechaRegistro = request.FechaRegistro,
            RegistradoPorUsuarioId = usuarioId,
            ProyectoId = control.ProyectoId,
            ActividadId = control.ActividadId,
            SedeId = control.SedeId,
            PuntoGeneracionId = control.PuntoGeneracionId,
            EmpresaResponsableId = control.EmpresaResponsableId,
            EstadoRegistroId = estadoRegistro.EstadoRegistroId,
            EstadoSincronizacionId = estadoSync.EstadoSincronizacionId,
            Observacion = request.Observacion,
            OrigenDispositivo = request.OrigenDispositivo
        };

        await unitOfWork.Repository<Registro>().AddAsync(registro, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<RegistroControlCreadoDto>.Ok(new RegistroControlCreadoDto(
            registro.RegistroId,
            controlId,
            registro.FechaRegistro,
            estadoRegistro.Nombre), StatusCodes.Status201Created);
    }

    private IQueryable<ControlGeneracion> QueryControles(bool tracking = false)
    {
        var query = unitOfWork.Repository<ControlGeneracion>().Query(tracking)
            .Include(x => x.Sede)
            .Include(x => x.EmpresaResponsable)
            .Include(x => x.Proyecto)
            .Include(x => x.Actividad)
            .Include(x => x.PuntoGeneracion)
            .Include(x => x.EstadoControlGeneracion)
            .Include(x => x.CreadoPorUsuario)
            .Include(x => x.AprobadoPorUsuario)
            .Include(x => x.Usuarios).ThenInclude(x => x.Usuario)
            .Include(x => x.Registros.Where(r => !r.Eliminado));

        return query.Where(x => !x.Eliminado);
    }

    private async Task<bool> PuedeVerControlAsync(Guid controlId, Guid usuarioId, CancellationToken cancellationToken)
    {
        if (await TieneRolAsync(usuarioId, "AMBIENTAL", cancellationToken) ||
            await TieneRolAsync(usuarioId, "ADMINISTRADOR", cancellationToken))
            return true;

        var now = DateTime.UtcNow;
        return await unitOfWork.Repository<ControlGeneracionUsuario>().Query()
            .AnyAsync(x =>
                x.ControlGeneracionId == controlId &&
                x.UsuarioId == usuarioId &&
                x.EsActivo &&
                x.FechaDesde <= now &&
                (!x.FechaHasta.HasValue || x.FechaHasta.Value >= now),
                cancellationToken);
    }

    private async Task<bool> EsResponsableActivoAsync(Guid controlId, Guid usuarioId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await unitOfWork.Repository<ControlGeneracionUsuario>().Query()
            .AnyAsync(x =>
                x.ControlGeneracionId == controlId &&
                x.UsuarioId == usuarioId &&
                x.RolControl == "RESPONSABLE" &&
                x.EsActivo &&
                x.FechaDesde <= now &&
                (!x.FechaHasta.HasValue || x.FechaHasta.Value >= now),
                cancellationToken);
    }

    private async Task<bool> EsParticipanteOperativoActivoAsync(Guid controlId, Guid usuarioId, CancellationToken cancellationToken)
    {
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

    private Task<bool> TieneRolAsync(Guid usuarioId, string rol, CancellationToken cancellationToken) =>
        unitOfWork.Repository<UsuarioRol>().Query()
            .AnyAsync(x => x.UsuarioId == usuarioId && x.Rol.Codigo == rol && x.Rol.EsActivo, cancellationToken);

    private Task<EstadoControlGeneracion> GetEstadoAsync(string codigo, CancellationToken cancellationToken) =>
        unitOfWork.Repository<EstadoControlGeneracion>().Query(tracking: true)
            .FirstAsync(x => x.Codigo == codigo, cancellationToken);

    private async Task<string?> ValidarCabeceraAsync(
        Guid sedeId,
        Guid empresaId,
        Guid proyectoId,
        Guid actividadId,
        Guid? puntoGeneracionId,
        DateTime fechaInicio,
        DateTime? fechaFin,
        CancellationToken cancellationToken)
    {
        if (fechaFin.HasValue && fechaFin.Value < fechaInicio)
            return "La fecha fin no puede ser anterior a la fecha de inicio.";

        if (!await unitOfWork.Repository<Sede>().Query().AnyAsync(x => x.SedeId == sedeId && x.EsActivo, cancellationToken))
            return "Sede inválida.";

        if (!await unitOfWork.Repository<Empresa>().Query().AnyAsync(x => x.EmpresaId == empresaId && x.EsActivo, cancellationToken))
            return "Empresa responsable inválida.";

        if (!await unitOfWork.Repository<Proyecto>().Query().AnyAsync(x => x.ProyectoId == proyectoId && x.EsActivo, cancellationToken))
            return "Proyecto inválido.";

        if (!await unitOfWork.Repository<Actividad>().Query().AnyAsync(x => x.ActividadId == actividadId && x.EsActivo && x.ProyectoId == proyectoId, cancellationToken))
            return "Actividad inválida para el proyecto seleccionado.";

        if (puntoGeneracionId.HasValue)
        {
            var puntoValido = await unitOfWork.Repository<PuntoResiduo>().Query()
                .AnyAsync(x =>
                    x.PuntoResiduoId == puntoGeneracionId.Value &&
                    x.SedeId == sedeId &&
                    x.EsActivo &&
                    (x.Tipo == "GENERACION" || x.Tipo == "AMBOS"),
                    cancellationToken);
            if (!puntoValido)
                return "Punto de generación inválido para la sede seleccionada.";
        }

        return null;
    }

    private async Task AuditarAsync(
        Guid controlId,
        Guid usuarioId,
        string accion,
        object? antes,
        object? despues,
        CancellationToken cancellationToken)
    {
        await unitOfWork.Repository<Auditoria>().AddAsync(new Auditoria
        {
            UsuarioId = usuarioId,
            Entidad = "ControlGeneracion",
            EntidadId = controlId,
            Accion = accion,
            DatosAntesJson = antes is null ? null : JsonSerializer.Serialize(antes),
            DatosDespuesJson = despues is null ? null : JsonSerializer.Serialize(despues)
        }, cancellationToken);
    }

    private static bool TransicionPermitida(string origen, string destino) => (origen, destino) switch
    {
        ("ACTIVO", "SUSPENDIDO") => true,
        ("ACTIVO", "CERRADO") => true,
        ("ACTIVO", "ANULADO") => true,
        ("SUSPENDIDO", "ACTIVO") => true,
        ("SUSPENDIDO", "CERRADO") => true,
        ("SUSPENDIDO", "ANULADO") => true,
        ("PENDIENTE_APROBACION", "ANULADO") => true,
        ("RECHAZADO", "ANULADO") => true,
        _ => false
    };

    private static string GenerarCodigo() =>
        $"CG-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

    private static string NombreUsuario(Usuario usuario) =>
        string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static ControlGeneracionListItemDto MapListItem(ControlGeneracion x) => new(
        x.ControlGeneracionId,
        x.Codigo,
        x.SedeId,
        x.Sede.Nombre,
        x.EmpresaResponsableId,
        x.EmpresaResponsable.NombreComercial ?? x.EmpresaResponsable.RazonSocial,
        x.ProyectoId,
        x.Proyecto.Nombre,
        x.ActividadId,
        x.Actividad.Nombre,
        x.FechaInicio,
        x.FechaFin,
        x.EstadoControlGeneracion.Nombre,
        x.Usuarios.Count(u => u.RolControl == "REGISTRADOR" && u.EsActivo),
        x.Registros.Count);

    private static ControlGeneracionDetalleDto MapDetalle(ControlGeneracion x) => new(
        x.ControlGeneracionId,
        x.Codigo,
        x.SedeId,
        x.Sede.Nombre,
        x.EmpresaResponsableId,
        x.EmpresaResponsable.NombreComercial ?? x.EmpresaResponsable.RazonSocial,
        x.ProyectoId,
        x.Proyecto.Nombre,
        x.ActividadId,
        x.Actividad.Nombre,
        x.PuntoGeneracionId,
        x.PuntoGeneracion?.Nombre,
        x.DescripcionTrabajo,
        x.FechaInicio,
        x.FechaFin,
        x.EstadoControlGeneracion.Nombre,
        x.Observacion,
        x.MotivoUltimoCambio,
        x.CreadoPorUsuarioId,
        NombreUsuario(x.CreadoPorUsuario),
        x.AprobadoPorUsuarioId,
        x.AprobadoPorUsuario is null ? null : NombreUsuario(x.AprobadoPorUsuario),
        x.AprobadoUtc,
        x.Usuarios
            .OrderByDescending(u => u.EsPrincipal)
            .ThenBy(u => u.RolControl)
            .ThenBy(u => u.Usuario.Nombres)
            .Select(u => new ControlGeneracionUsuarioDto(
                u.ControlGeneracionUsuarioId,
                u.UsuarioId,
                NombreUsuario(u.Usuario),
                u.RolControl,
                u.EsPrincipal,
                u.FechaDesde,
                u.FechaHasta,
                u.EsActivo))
            .ToArray());
}
