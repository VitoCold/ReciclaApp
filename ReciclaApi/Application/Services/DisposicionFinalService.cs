using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Persistence;
using ReciclaApi.Infrastructure.Storage;

namespace ReciclaApi.Application.Services;

public interface IDisposicionFinalService
{
    Task<IReadOnlyCollection<TipoTratamientoDto>> ListarTiposTratamientoAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<DisposicionFinalDto>> ObtenerPorRetiroAsync(Guid retiroId, Guid usuarioId, CancellationToken cancellationToken = default);
    Task<ServiceResult<DisposicionFinalDto>> CrearAsync(Guid retiroId, Guid usuarioId, CrearDisposicionFinalRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<DisposicionFinalDto>> SubirEvidenciaAsync(
        Guid disposicionFinalId,
        Guid usuarioId,
        Stream content,
        string fileName,
        string? contentType,
        string tipoEvidencia,
        CancellationToken cancellationToken = default);
    Task<ServiceResult<DisposicionFinalDto>> ValidarAsync(Guid disposicionFinalId, Guid usuarioId, CancellationToken cancellationToken = default);
}

public sealed class DisposicionFinalService(
    ReciclaDbContext context,
    IFileStorageService fileStorage) : IDisposicionFinalService
{
    public async Task<IReadOnlyCollection<TipoTratamientoDto>> ListarTiposTratamientoAsync(CancellationToken cancellationToken = default) =>
        await context.TiposTratamiento
            .AsNoTracking()
            .Where(x => x.EsActivo)
            .OrderBy(x => x.Nombre)
            .Select(x => new TipoTratamientoDto(x.TipoTratamientoId, x.Codigo, x.Nombre, x.EsValorizacion))
            .ToListAsync(cancellationToken);

    public async Task<ServiceResult<DisposicionFinalDto>> ObtenerPorRetiroAsync(
        Guid retiroId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var retiro = await CargarRetiroAsync(retiroId, cancellationToken);
        if (retiro is null || !await PuedeConsultarAsync(retiro, usuarioId, cancellationToken))
            return ServiceResult<DisposicionFinalDto>.Fail("Retiro no encontrado o sin acceso.", StatusCodes.Status404NotFound);

        var disposicion = await CargarDisposicionPorRetiroAsync(retiroId, cancellationToken);
        return disposicion is null
            ? ServiceResult<DisposicionFinalDto>.Fail("El retiro todavía no tiene disposición final o valorización documentada.", StatusCodes.Status404NotFound)
            : ServiceResult<DisposicionFinalDto>.Ok(Map(disposicion));
    }

    public async Task<ServiceResult<DisposicionFinalDto>> CrearAsync(
        Guid retiroId,
        Guid usuarioId,
        CrearDisposicionFinalRequest request,
        CancellationToken cancellationToken = default)
    {
        var esAmbiental = await TieneRolAsync(usuarioId, "AMBIENTAL", cancellationToken);
        var esResponsable = await TieneRolAsync(usuarioId, "RESPONSABLE_OPERATIVO", cancellationToken);
        if (!esAmbiental && !esResponsable)
            return ServiceResult<DisposicionFinalDto>.Fail("No tienes permiso para documentar la disposición del retiro.", StatusCodes.Status403Forbidden);

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var retiro = await CargarRetiroAsync(retiroId, cancellationToken, tracking: true);
        if (retiro is null)
            return await FailRollbackAsync("Retiro no encontrado.", StatusCodes.Status404NotFound, transaction, cancellationToken);

        if (!esAmbiental && !await EsResponsableDeTodosLosControlesAsync(retiro, usuarioId, cancellationToken))
            return await FailRollbackAsync("No tienes permiso para documentar este retiro.", StatusCodes.Status403Forbidden, transaction, cancellationToken);

        if (retiro.EstadoRetiro.Codigo != "RETIRADO")
            return await FailRollbackAsync("Solo un retiro confirmado puede documentar disposición final o valorización.", StatusCodes.Status409Conflict, transaction, cancellationToken);

        if (request.FechaDisposicion < retiro.FechaRetiro)
            return await FailRollbackAsync("La fecha de disposición no puede ser anterior a la fecha del retiro.", StatusCodes.Status400BadRequest, transaction, cancellationToken);

        var yaExiste = await context.DisposicionesFinales
            .AnyAsync(x => x.RetiroId == retiroId && !x.Eliminado && x.EstadoDisposicion.Codigo != "ANULADA", cancellationToken);
        if (yaExiste)
            return await FailRollbackAsync("El retiro ya tiene una disposición final o valorización activa.", StatusCodes.Status409Conflict, transaction, cancellationToken);

        var gestor = await context.Empresas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmpresaId == request.EmpresaGestoraId && x.EsActivo && x.EsGestoraResiduos, cancellationToken);
        if (gestor is null)
            return await FailRollbackAsync("Selecciona una empresa gestora válida.", StatusCodes.Status400BadRequest, transaction, cancellationToken);

        var tratamiento = await context.TiposTratamiento
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TipoTratamientoId == request.TipoTratamientoId && x.EsActivo, cancellationToken);
        if (tratamiento is null)
            return await FailRollbackAsync("Selecciona un tipo de tratamiento válido.", StatusCodes.Status400BadRequest, transaction, cancellationToken);

        var estadoPendiente = await context.EstadosDisposicion.FirstAsync(x => x.Codigo == "PENDIENTE", cancellationToken);
        var estadoSync = await context.EstadosSincronizacion.FirstAsync(x => x.Codigo == "SINCRONIZADO", cancellationToken);

        var disposicion = new DisposicionFinal
        {
            RetiroId = retiroId,
            EmpresaGestoraId = request.EmpresaGestoraId,
            FechaDisposicion = request.FechaDisposicion,
            TipoTratamientoId = request.TipoTratamientoId,
            CodigoDocumento = Limpiar(request.CodigoDocumento),
            EstadoDisposicionId = estadoPendiente.EstadoDisposicionId,
            Observacion = Limpiar(request.Observacion),
            EstadoSincronizacionId = estadoSync.EstadoSincronizacionId,
            CreadoPorUsuarioId = usuarioId
        };

        context.DisposicionesFinales.Add(disposicion);
        context.Auditoria.Add(new Auditoria
        {
            UsuarioId = usuarioId,
            Entidad = "DisposicionFinal",
            EntidadId = disposicion.DisposicionFinalId,
            Accion = "CREAR",
            DatosDespuesJson = JsonSerializer.Serialize(new
            {
                disposicion.RetiroId,
                disposicion.EmpresaGestoraId,
                disposicion.FechaDisposicion,
                disposicion.TipoTratamientoId,
                disposicion.CodigoDocumento
            })
        });

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var creado = await CargarDisposicionAsync(disposicion.DisposicionFinalId, cancellationToken);
        return creado is null
            ? ServiceResult<DisposicionFinalDto>.Fail("La disposición fue creada, pero no pudo recargarse.", StatusCodes.Status500InternalServerError)
            : ServiceResult<DisposicionFinalDto>.Ok(Map(creado), StatusCodes.Status201Created);
    }

    public async Task<ServiceResult<DisposicionFinalDto>> SubirEvidenciaAsync(
        Guid disposicionFinalId,
        Guid usuarioId,
        Stream content,
        string fileName,
        string? contentType,
        string tipoEvidencia,
        CancellationToken cancellationToken = default)
    {
        var disposicion = await CargarDisposicionAsync(disposicionFinalId, cancellationToken, tracking: true);
        if (disposicion is null)
            return ServiceResult<DisposicionFinalDto>.Fail("Disposición no encontrada.", StatusCodes.Status404NotFound);

        if (disposicion.EstadoDisposicion.Codigo == "VALIDADA")
            return ServiceResult<DisposicionFinalDto>.Fail("La disposición ya fue validada y no admite nuevas evidencias.", StatusCodes.Status409Conflict);

        var esAmbiental = await TieneRolAsync(usuarioId, "AMBIENTAL", cancellationToken);
        var esResponsable = await TieneRolAsync(usuarioId, "RESPONSABLE_OPERATIVO", cancellationToken);
        if (!esAmbiental && !esResponsable)
            return ServiceResult<DisposicionFinalDto>.Fail("No tienes permiso para adjuntar evidencias.", StatusCodes.Status403Forbidden);

        var retiro = await CargarRetiroAsync(disposicion.RetiroId, cancellationToken);
        if (retiro is null || (!esAmbiental && !await EsResponsableDeTodosLosControlesAsync(retiro, usuarioId, cancellationToken)))
            return ServiceResult<DisposicionFinalDto>.Fail("No tienes permiso para adjuntar evidencia a este retiro.", StatusCodes.Status403Forbidden);

        var stored = await fileStorage.SaveAsync(
            content,
            fileName,
            $"uploads/disposiciones-finales/{disposicionFinalId:N}",
            cancellationToken);

        var estadoDocumentada = await context.EstadosDisposicion.FirstAsync(x => x.Codigo == "DOCUMENTADA", cancellationToken);
        var estadoSync = await context.EstadosSincronizacion.FirstAsync(x => x.Codigo == "SINCRONIZADO", cancellationToken);

        var evidencia = new DisposicionFinalEvidencia
        {
            DisposicionFinalId = disposicionFinalId,
            TipoEvidencia = string.IsNullOrWhiteSpace(tipoEvidencia) ? "DOCUMENTO" : tipoEvidencia.Trim().ToUpperInvariant(),
            NombreArchivo = Path.GetFileName(fileName),
            RutaLocal = stored.RutaLocal,
            UrlNube = stored.UrlPublica,
            ContentType = contentType,
            HashArchivo = stored.HashSha256,
            TamanoBytes = stored.TamanoBytes,
            EstadoSincronizacionId = estadoSync.EstadoSincronizacionId,
            CreadoPorUsuarioId = usuarioId
        };

        context.DisposicionFinalEvidencias.Add(evidencia);
        disposicion.EstadoDisposicionId = estadoDocumentada.EstadoDisposicionId;
        disposicion.ActualizadoUtc = DateTime.UtcNow;

        context.Auditoria.Add(new Auditoria
        {
            UsuarioId = usuarioId,
            Entidad = "DisposicionFinal",
            EntidadId = disposicionFinalId,
            Accion = "ADJUNTAR_EVIDENCIA",
            DatosDespuesJson = JsonSerializer.Serialize(new
            {
                evidencia.EvidenciaId,
                evidencia.TipoEvidencia,
                evidencia.NombreArchivo,
                evidencia.HashArchivo
            })
        });

        await context.SaveChangesAsync(cancellationToken);

        var actualizado = await CargarDisposicionAsync(disposicionFinalId, cancellationToken);
        return actualizado is null
            ? ServiceResult<DisposicionFinalDto>.Fail("La evidencia fue guardada, pero la disposición no pudo recargarse.", StatusCodes.Status500InternalServerError)
            : ServiceResult<DisposicionFinalDto>.Ok(Map(actualizado));
    }

    public async Task<ServiceResult<DisposicionFinalDto>> ValidarAsync(
        Guid disposicionFinalId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (!await TieneRolAsync(usuarioId, "AMBIENTAL", cancellationToken))
            return ServiceResult<DisposicionFinalDto>.Fail("Solo Ambiental puede validar la disposición final o valorización.", StatusCodes.Status403Forbidden);

        var disposicion = await CargarDisposicionAsync(disposicionFinalId, cancellationToken, tracking: true);
        if (disposicion is null)
            return ServiceResult<DisposicionFinalDto>.Fail("Disposición no encontrada.", StatusCodes.Status404NotFound);

        if (disposicion.EstadoDisposicion.Codigo == "VALIDADA")
            return ServiceResult<DisposicionFinalDto>.Ok(Map(disposicion));

        if (disposicion.EstadoDisposicion.Codigo != "DOCUMENTADA" || !disposicion.Evidencias.Any(x => !x.Eliminado))
            return ServiceResult<DisposicionFinalDto>.Fail("La disposición debe tener al menos una evidencia antes de validarse.", StatusCodes.Status409Conflict);

        var estadoValidada = await context.EstadosDisposicion.FirstAsync(x => x.Codigo == "VALIDADA", cancellationToken);
        disposicion.EstadoDisposicionId = estadoValidada.EstadoDisposicionId;
        disposicion.ActualizadoUtc = DateTime.UtcNow;

        context.Auditoria.Add(new Auditoria
        {
            UsuarioId = usuarioId,
            Entidad = "DisposicionFinal",
            EntidadId = disposicionFinalId,
            Accion = "VALIDAR",
            DatosDespuesJson = JsonSerializer.Serialize(new { Estado = "VALIDADA" })
        });

        await context.SaveChangesAsync(cancellationToken);

        var actualizado = await CargarDisposicionAsync(disposicionFinalId, cancellationToken);
        return actualizado is null
            ? ServiceResult<DisposicionFinalDto>.Fail("No se pudo recargar la disposición validada.", StatusCodes.Status500InternalServerError)
            : ServiceResult<DisposicionFinalDto>.Ok(Map(actualizado));
    }

    private async Task<Retiro?> CargarRetiroAsync(Guid retiroId, CancellationToken cancellationToken, bool tracking = false)
    {
        IQueryable<Retiro> query = tracking ? context.Retiros : context.Retiros.AsNoTracking();
        return await query
            .Where(x => x.RetiroId == retiroId && !x.Eliminado)
            .Include(x => x.EstadoRetiro)
            .Include(x => x.Detalles)
                .ThenInclude(x => x.RegistroResiduo)
                    .ThenInclude(x => x.Registro)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<DisposicionFinal?> CargarDisposicionPorRetiroAsync(Guid retiroId, CancellationToken cancellationToken) =>
        await QueryDisposicion(false)
            .FirstOrDefaultAsync(x => x.RetiroId == retiroId && !x.Eliminado && x.EstadoDisposicion.Codigo != "ANULADA", cancellationToken);

    private async Task<DisposicionFinal?> CargarDisposicionAsync(Guid disposicionFinalId, CancellationToken cancellationToken, bool tracking = false) =>
        await QueryDisposicion(tracking)
            .FirstOrDefaultAsync(x => x.DisposicionFinalId == disposicionFinalId && !x.Eliminado, cancellationToken);

    private IQueryable<DisposicionFinal> QueryDisposicion(bool tracking)
    {
        IQueryable<DisposicionFinal> query = tracking ? context.DisposicionesFinales : context.DisposicionesFinales.AsNoTracking();
        return query
            .Include(x => x.EmpresaGestora)
            .Include(x => x.TipoTratamiento)
            .Include(x => x.EstadoDisposicion)
            .Include(x => x.CreadoPorUsuario)
            .Include(x => x.Evidencias.Where(e => !e.Eliminado));
    }

    private async Task<bool> PuedeConsultarAsync(Retiro retiro, Guid usuarioId, CancellationToken cancellationToken)
    {
        if (await TieneRolAsync(usuarioId, "AMBIENTAL", cancellationToken) ||
            await TieneRolAsync(usuarioId, "ADMINISTRADOR", cancellationToken))
            return true;

        return await TieneRolAsync(usuarioId, "RESPONSABLE_OPERATIVO", cancellationToken) &&
               await EsResponsableDeTodosLosControlesAsync(retiro, usuarioId, cancellationToken);
    }

    private async Task<bool> EsResponsableDeTodosLosControlesAsync(Retiro retiro, Guid usuarioId, CancellationToken cancellationToken)
    {
        var controlIds = retiro.Detalles
            .Select(x => x.RegistroResiduo.Registro.ControlGeneracionId)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .Distinct()
            .ToArray();

        if (controlIds.Length == 0)
            return false;

        var now = DateTime.UtcNow;
        var autorizados = await context.Set<ControlGeneracionUsuario>()
            .AsNoTracking()
            .Where(x =>
                controlIds.Contains(x.ControlGeneracionId) &&
                x.UsuarioId == usuarioId &&
                x.RolControl == "RESPONSABLE" &&
                x.EsActivo &&
                x.FechaDesde <= now &&
                (!x.FechaHasta.HasValue || x.FechaHasta.Value >= now))
            .Select(x => x.ControlGeneracionId)
            .Distinct()
            .CountAsync(cancellationToken);

        return autorizados == controlIds.Length;
    }

    private Task<bool> TieneRolAsync(Guid usuarioId, string codigo, CancellationToken cancellationToken) =>
        context.UsuarioRoles
            .AsNoTracking()
            .AnyAsync(x => x.UsuarioId == usuarioId && x.Rol.EsActivo && x.Rol.Codigo == codigo, cancellationToken);

    private static DisposicionFinalDto Map(DisposicionFinal disposicion)
    {
        var creador = string.Join(" ", new[] { disposicion.CreadoPorUsuario.Nombres, disposicion.CreadoPorUsuario.Apellidos }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        var empresa = string.IsNullOrWhiteSpace(disposicion.EmpresaGestora.NombreComercial)
            ? disposicion.EmpresaGestora.RazonSocial
            : disposicion.EmpresaGestora.NombreComercial;

        var evidencias = disposicion.Evidencias
            .Where(x => !x.Eliminado)
            .OrderBy(x => x.CreadoUtc)
            .Select(x => new DisposicionFinalEvidenciaDto(
                x.EvidenciaId,
                x.TipoEvidencia,
                x.NombreArchivo,
                x.UrlNube,
                x.ContentType,
                x.TamanoBytes,
                x.CreadoUtc))
            .ToArray();

        return new DisposicionFinalDto(
            disposicion.DisposicionFinalId,
            disposicion.RetiroId,
            disposicion.EmpresaGestoraId,
            empresa,
            disposicion.FechaDisposicion,
            disposicion.TipoTratamientoId,
            disposicion.TipoTratamiento.Codigo,
            disposicion.TipoTratamiento.Nombre,
            disposicion.TipoTratamiento.EsValorizacion,
            disposicion.CodigoDocumento,
            disposicion.EstadoDisposicion.Nombre,
            disposicion.Observacion,
            disposicion.CreadoPorUsuarioId,
            creador,
            disposicion.CreadoUtc,
            evidencias);
    }

    private static string? Limpiar(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<ServiceResult<DisposicionFinalDto>> FailRollbackAsync(
        string error,
        int statusCode,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return ServiceResult<DisposicionFinalDto>.Fail(error, statusCode);
    }
}
