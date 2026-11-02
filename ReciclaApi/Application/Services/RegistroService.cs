using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Repositories;
using ReciclaApi.Infrastructure.Storage;

namespace ReciclaApi.Application.Services;

public interface IRegistroService
{
    Task<IReadOnlyCollection<RegistroListItemDto>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<RegistroDetalleDto>> ObtenerAsync(Guid registroId, Guid usuarioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<RegistroDetalleDto>> CrearAsync(Guid usuarioId, CrearRegistroRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<RegistroDetalleDto>> ActualizarAsync(Guid registroId, Guid usuarioId, ActualizarRegistroRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<RegistroResiduoDto>> AgregarResiduoAsync(Guid registroId, Guid usuarioId, AgregarRegistroResiduoRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<RegistroResiduoDto>> ActualizarResiduoAsync(Guid registroId, Guid registroResiduoId, Guid usuarioId, ActualizarRegistroResiduoRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult> EliminarResiduoAsync(Guid registroId, Guid registroResiduoId, Guid usuarioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<RegistroDetalleDto>> CompletarAsync(Guid registroId, Guid usuarioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<DisposicionDto>> GuardarDisposicionAsync(Guid registroId, Guid usuarioId, CrearDisposicionRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<RegistroResiduoFotoDto>> SubirFotoAsync(Guid registroId, Guid registroResiduoId, Guid usuarioId, Stream content, string fileName, string? contentType, CancellationToken cancellationToken = default);
    Task<ServiceResult<DisposicionEvidenciaDto>> SubirEvidenciaAsync(Guid disposicionId, Guid usuarioId, Stream content, string fileName, string? contentType, string tipoEvidencia, CancellationToken cancellationToken = default);
}

public sealed class RegistroService(
    IUnitOfWork unitOfWork,
    IFileStorageService fileStorage,
    IControlAccessService controlAccess) : IRegistroService
{
    public async Task<IReadOnlyCollection<RegistroListItemDto>> ListarAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var registros = await unitOfWork.Registros.ListarPorUsuarioAsync(usuarioId, cancellationToken);
        return registros.Select(MapListItem).ToArray();
    }

    public async Task<ServiceResult<RegistroDetalleDto>> ObtenerAsync(
        Guid registroId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var registro = await unitOfWork.Registros.ObtenerDetalleAsync(registroId, usuarioId, false, cancellationToken);
        return registro is null
            ? ServiceResult<RegistroDetalleDto>.Fail("Registro no encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<RegistroDetalleDto>.Ok(MapDetalle(registro));
    }

    public Task<ServiceResult<RegistroDetalleDto>> CrearAsync(
        Guid usuarioId,
        CrearRegistroRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ServiceResult<RegistroDetalleDto>.Fail(
            "El registro independiente fue retirado. Inicia el registro desde un Control de Generación activo.",
            StatusCodes.Status410Gone));

    public Task<ServiceResult<RegistroDetalleDto>> ActualizarAsync(
        Guid registroId,
        Guid usuarioId,
        ActualizarRegistroRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ServiceResult<RegistroDetalleDto>.Fail(
            "La edición independiente de la cabecera fue retirada. La cabecera operativa se administra desde el Control de Generación.",
            StatusCodes.Status410Gone));

    public async Task<ServiceResult<RegistroResiduoDto>> AgregarResiduoAsync(
        Guid registroId,
        Guid usuarioId,
        AgregarRegistroResiduoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Cantidad <= 0)
            return ServiceResult<RegistroResiduoDto>.Fail("La cantidad debe ser mayor a cero.", StatusCodes.Status400BadRequest);

        var registro = await unitOfWork.Registros.ObtenerDetalleAsync(registroId, usuarioId, true, cancellationToken);
        if (registro is null)
            return ServiceResult<RegistroResiduoDto>.Fail("Registro no encontrado.", StatusCodes.Status404NotFound);

        var acceso = await ValidarOperacionControlAsync(registro, usuarioId, cancellationToken);
        if (acceso is not null)
            return ServiceResult<RegistroResiduoDto>.Fail(acceso, StatusCodes.Status403Forbidden);

        if (!EsBorrador(registro))
            return ServiceResult<RegistroResiduoDto>.Fail("Solo se pueden agregar residuos a un registro en borrador.", StatusCodes.Status409Conflict);

        var catalogo = await unitOfWork.Repository<ResiduoCatalogo>().Query()
            .FirstOrDefaultAsync(x => x.ResiduoId == request.ResiduoId && x.EsActivo, cancellationToken);

        if (catalogo is null || catalogo.TipoResiduoId != request.TipoResiduoId)
            return ServiceResult<RegistroResiduoDto>.Fail("El residuo seleccionado no es válido para el tipo indicado.", StatusCodes.Status400BadRequest);

        var unidadExiste = await unitOfWork.Repository<UnidadMedida>().Query()
            .AnyAsync(x => x.UnidadMedidaId == request.UnidadMedidaId, cancellationToken);
        if (!unidadExiste)
            return ServiceResult<RegistroResiduoDto>.Fail("Unidad de medida inválida.", StatusCodes.Status400BadRequest);

        var estadoSync = await GetEstadoSyncAsync(cancellationToken);
        var residuo = new RegistroResiduo
        {
            RegistroId = registroId,
            TipoResiduoId = request.TipoResiduoId,
            ResiduoId = request.ResiduoId,
            UnidadMedidaId = request.UnidadMedidaId,
            Cantidad = request.Cantidad,
            Observacion = request.Observacion,
            EstadoSincronizacionId = estadoSync.EstadoSincronizacionId,
            CreadoPorUsuarioId = usuarioId
        };

        await unitOfWork.Repository<RegistroResiduo>().AddAsync(residuo, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<RegistroResiduoDto>.Ok(MapResiduo(residuo), StatusCodes.Status201Created);
    }

    public async Task<ServiceResult<RegistroResiduoDto>> ActualizarResiduoAsync(
        Guid registroId,
        Guid registroResiduoId,
        Guid usuarioId,
        ActualizarRegistroResiduoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Cantidad <= 0)
            return ServiceResult<RegistroResiduoDto>.Fail("La cantidad debe ser mayor a cero.", StatusCodes.Status400BadRequest);

        var registro = await unitOfWork.Registros.ObtenerDetalleAsync(registroId, usuarioId, true, cancellationToken);
        if (registro is null)
            return ServiceResult<RegistroResiduoDto>.Fail("Registro no encontrado.", StatusCodes.Status404NotFound);

        var acceso = await ValidarOperacionControlAsync(registro, usuarioId, cancellationToken);
        if (acceso is not null)
            return ServiceResult<RegistroResiduoDto>.Fail(acceso, StatusCodes.Status403Forbidden);

        if (!EsBorrador(registro))
            return ServiceResult<RegistroResiduoDto>.Fail("Solo se pueden editar residuos de un registro en borrador.", StatusCodes.Status409Conflict);

        var residuo = registro.Residuos.FirstOrDefault(x => x.RegistroResiduoId == registroResiduoId && !x.Eliminado);
        if (residuo is null)
            return ServiceResult<RegistroResiduoDto>.Fail("Residuo no encontrado.", StatusCodes.Status404NotFound);

        var unidadExiste = await unitOfWork.Repository<UnidadMedida>().Query()
            .AnyAsync(x => x.UnidadMedidaId == request.UnidadMedidaId, cancellationToken);
        if (!unidadExiste)
            return ServiceResult<RegistroResiduoDto>.Fail("Unidad de medida inválida.", StatusCodes.Status400BadRequest);

        residuo.UnidadMedidaId = request.UnidadMedidaId;
        residuo.Cantidad = request.Cantidad;
        residuo.Observacion = request.Observacion;
        residuo.ActualizadoUtc = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult<RegistroResiduoDto>.Ok(MapResiduo(residuo));
    }

    public async Task<ServiceResult> EliminarResiduoAsync(
        Guid registroId,
        Guid registroResiduoId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var registro = await unitOfWork.Registros.ObtenerDetalleAsync(registroId, usuarioId, true, cancellationToken);
        if (registro is null)
            return ServiceResult.Fail("Registro no encontrado.", StatusCodes.Status404NotFound);

        var acceso = await ValidarOperacionControlAsync(registro, usuarioId, cancellationToken);
        if (acceso is not null)
            return ServiceResult.Fail(acceso, StatusCodes.Status403Forbidden);

        if (!EsBorrador(registro))
            return ServiceResult.Fail("Solo se pueden eliminar residuos de un registro en borrador.", StatusCodes.Status409Conflict);

        var residuo = registro.Residuos.FirstOrDefault(x => x.RegistroResiduoId == registroResiduoId && !x.Eliminado);
        if (residuo is null)
            return ServiceResult.Fail("Residuo no encontrado.", StatusCodes.Status404NotFound);

        residuo.Eliminado = true;
        residuo.ActualizadoUtc = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult.Ok();
    }

    public async Task<ServiceResult<RegistroDetalleDto>> CompletarAsync(
        Guid registroId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var registro = await unitOfWork.Registros.ObtenerDetalleAsync(registroId, usuarioId, true, cancellationToken);
        if (registro is null)
            return ServiceResult<RegistroDetalleDto>.Fail("Registro no encontrado.", StatusCodes.Status404NotFound);

        var acceso = await ValidarOperacionControlAsync(registro, usuarioId, cancellationToken);
        if (acceso is not null)
            return ServiceResult<RegistroDetalleDto>.Fail(acceso, StatusCodes.Status403Forbidden);

        if (!EsBorrador(registro))
            return ServiceResult<RegistroDetalleDto>.Fail("El registro ya no se encuentra en borrador.", StatusCodes.Status409Conflict);

        if (!registro.Residuos.Any(x => !x.Eliminado))
            return ServiceResult<RegistroDetalleDto>.Fail("Debe registrar al menos un residuo antes de completar.", StatusCodes.Status400BadRequest);

        var completado = await GetEstadoRegistroAsync("COMPLETADO", cancellationToken);
        registro.EstadoRegistroId = completado.EstadoRegistroId;
        registro.EstadoRegistro = completado;
        registro.ActualizadoUtc = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var detalle = await unitOfWork.Registros.ObtenerDetalleAsync(registroId, usuarioId, false, cancellationToken);
        return ServiceResult<RegistroDetalleDto>.Ok(MapDetalle(detalle!));
    }

    public Task<ServiceResult<DisposicionDto>> GuardarDisposicionAsync(
        Guid registroId,
        Guid usuarioId,
        CrearDisposicionRequest request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ServiceResult<DisposicionDto>.Fail(
            "La disposición legacy fue retirada. El destino final se documenta desde un Retiro EO-RS.",
            StatusCodes.Status410Gone));

    public async Task<ServiceResult<RegistroResiduoFotoDto>> SubirFotoAsync(
        Guid registroId,
        Guid registroResiduoId,
        Guid usuarioId,
        Stream content,
        string fileName,
        string? contentType,
        CancellationToken cancellationToken = default)
    {
        var registro = await unitOfWork.Registros.ObtenerDetalleAsync(registroId, usuarioId, true, cancellationToken);
        if (registro is null)
            return ServiceResult<RegistroResiduoFotoDto>.Fail("Registro no encontrado.", StatusCodes.Status404NotFound);

        var acceso = await ValidarOperacionControlAsync(registro, usuarioId, cancellationToken);
        if (acceso is not null)
            return ServiceResult<RegistroResiduoFotoDto>.Fail(acceso, StatusCodes.Status403Forbidden);

        var residuo = registro.Residuos.FirstOrDefault(x => x.RegistroResiduoId == registroResiduoId && !x.Eliminado);
        if (residuo is null)
            return ServiceResult<RegistroResiduoFotoDto>.Fail("Residuo no encontrado.", StatusCodes.Status404NotFound);

        var stored = await fileStorage.SaveAsync(content, fileName, $"uploads/residuos/{registroId:N}", cancellationToken);
        var estadoSync = await GetEstadoSyncAsync(cancellationToken);

        var foto = new RegistroResiduoFoto
        {
            RegistroResiduoId = registroResiduoId,
            NombreArchivo = stored.NombreArchivo,
            RutaLocal = stored.RutaLocal,
            UrlNube = stored.UrlPublica,
            ContentType = contentType,
            HashArchivo = stored.HashSha256,
            TamanoBytes = stored.TamanoBytes,
            EstadoSincronizacionId = estadoSync.EstadoSincronizacionId,
            TomadaPorUsuarioId = usuarioId
        };

        await unitOfWork.Repository<RegistroResiduoFoto>().AddAsync(foto, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ServiceResult<RegistroResiduoFotoDto>.Ok(MapFoto(foto), StatusCodes.Status201Created);
    }

    public Task<ServiceResult<DisposicionEvidenciaDto>> SubirEvidenciaAsync(
        Guid disposicionId,
        Guid usuarioId,
        Stream content,
        string fileName,
        string? contentType,
        string tipoEvidencia,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ServiceResult<DisposicionEvidenciaDto>.Fail(
            "La evidencia de disposición legacy fue retirada. Usa el cierre documental del Retiro EO-RS.",
            StatusCodes.Status410Gone));

    private async Task<string?> ValidarOperacionControlAsync(
        Registro registro,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        if (!registro.ControlGeneracionId.HasValue)
            return "El registro no pertenece a un Control de Generación operativo.";

        var control = await unitOfWork.Repository<ControlGeneracion>().Query()
            .Include(x => x.EstadoControlGeneracion)
            .FirstOrDefaultAsync(
                x => x.ControlGeneracionId == registro.ControlGeneracionId.Value && !x.Eliminado,
                cancellationToken);

        if (control is null)
            return "El Control de Generación asociado no existe.";

        if (control.EstadoControlGeneracion.Codigo != "ACTIVO")
            return "Solo se pueden modificar registros mientras el Control de Generación está ACTIVO.";

        if (!await controlAccess.EsParticipanteOperativoVigenteAsync(
                control.ControlGeneracionId,
                usuarioId,
                cancellationToken))
            return "No tienes una asignación vigente para operar en este Control.";

        return null;
    }

    private async Task<string?> ValidarCabeceraAsync(
        Guid proyectoId,
        Guid actividadId,
        Guid sedeId,
        CancellationToken cancellationToken)
    {
        var proyectoExiste = await unitOfWork.Repository<Proyecto>().Query()
            .AnyAsync(x => x.ProyectoId == proyectoId && x.EsActivo, cancellationToken);
        if (!proyectoExiste)
            return "Proyecto inválido.";

        var actividadExiste = await unitOfWork.Repository<Actividad>().Query()
            .AnyAsync(x => x.ActividadId == actividadId && x.EsActivo && (x.ProyectoId == null || x.ProyectoId == proyectoId), cancellationToken);
        if (!actividadExiste)
            return "Actividad inválida para el proyecto seleccionado.";

        var sedeExiste = await unitOfWork.Repository<Sede>().Query()
            .AnyAsync(x => x.SedeId == sedeId && x.EsActivo, cancellationToken);
        return sedeExiste ? null : "Sede inválida.";
    }

    private Task<EstadoRegistro> GetEstadoRegistroAsync(string codigo, CancellationToken cancellationToken) =>
        unitOfWork.Repository<EstadoRegistro>().Query(tracking: true)
            .FirstAsync(x => x.Codigo == codigo, cancellationToken);

    private Task<EstadoSincronizacion> GetEstadoSyncAsync(CancellationToken cancellationToken) =>
        unitOfWork.Repository<EstadoSincronizacion>().Query(tracking: true)
            .FirstAsync(x => x.Codigo == "SINCRONIZADO", cancellationToken);

    private static bool EsBorrador(Registro registro) =>
        registro.EstadoRegistro.Codigo is "BORRADOR" or "EN_PROCESO";

    private static RegistroListItemDto MapListItem(Registro x) => new(
        x.RegistroId,
        x.FechaRegistro,
        x.Proyecto.Nombre,
        x.Actividad.Nombre,
        x.Sede.Nombre,
        x.EstadoRegistro.Nombre,
        x.Residuos.Count(r => !r.Eliminado),
        x.CreadoUtc);

    private static RegistroDetalleDto MapDetalle(Registro x)
    {
        var disposicion = x.Disposiciones.FirstOrDefault(d => !d.Eliminado);
        return new RegistroDetalleDto(
            x.RegistroId,
            x.CodigoLocal,
            x.FechaRegistro,
            x.ProyectoId,
            x.Proyecto.Nombre,
            x.ActividadId,
            x.Actividad.Nombre,
            x.SedeId,
            x.Sede.Nombre,
            x.EstadoRegistro.Nombre,
            x.Observacion,
            x.Residuos.Where(r => !r.Eliminado).Select(MapResiduo).ToArray(),
            disposicion is null ? null : MapDisposicion(disposicion));
    }

    private static RegistroResiduoDto MapResiduo(RegistroResiduo x) => new(
        x.RegistroResiduoId,
        x.TipoResiduoId,
        x.ResiduoId,
        x.UnidadMedidaId,
        x.Cantidad,
        x.Observacion,
        x.Fotos.Where(f => !f.Eliminado).Select(MapFoto).ToArray());

    private static RegistroResiduoFotoDto MapFoto(RegistroResiduoFoto x) => new(
        x.FotoId,
        x.NombreArchivo,
        x.UrlNube,
        x.ContentType,
        x.TamanoBytes,
        x.TomadaUtc);

    private static DisposicionDto MapDisposicion(Disposicion x) => new(
        x.DisposicionId,
        x.CodigoDocumento,
        x.FechaDisposicion,
        x.EmpresaDisposicion,
        x.Observacion,
        x.Evidencias.Where(e => !e.Eliminado).Select(MapEvidencia).ToArray());

    private static DisposicionEvidenciaDto MapEvidencia(DisposicionEvidencia x) => new(
        x.EvidenciaId,
        x.TipoEvidencia,
        x.NombreArchivo,
        x.UrlNube,
        x.ContentType,
        x.TamanoBytes,
        x.CreadoUtc);
}
