using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Persistence;

namespace ReciclaApi.Application.Services;

public interface IRegistroResiduoEvidenciaService
{
    Task<ServiceResult<IReadOnlyCollection<RegistroResiduoEvidenciaDto>>> ListarAsync(
        Guid registroId,
        Guid usuarioId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<RegistroResiduoEvidenciaDto>> GuardarUbicacionAsync(
        Guid registroId,
        Guid registroResiduoId,
        Guid usuarioId,
        GuardarUbicacionResiduoRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RegistroResiduoEvidenciaService(ReciclaDbContext context) : IRegistroResiduoEvidenciaService
{
    public async Task<ServiceResult<IReadOnlyCollection<RegistroResiduoEvidenciaDto>>> ListarAsync(
        Guid registroId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var registro = await context.Registros
            .AsNoTracking()
            .Where(x => x.RegistroId == registroId && !x.Eliminado)
            .Select(x => new
            {
                x.RegistroId,
                x.RegistradoPorUsuarioId,
                x.ControlGeneracionId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (registro is null || !await PuedeConsultarAsync(registro.RegistradoPorUsuarioId, registro.ControlGeneracionId, usuarioId, cancellationToken))
            return ServiceResult<IReadOnlyCollection<RegistroResiduoEvidenciaDto>>.Fail("Registro no encontrado o sin acceso.", StatusCodes.Status404NotFound);

        var residuos = await context.RegistroResiduos
            .AsNoTracking()
            .Where(x => x.RegistroId == registroId && !x.Eliminado)
            .Select(x => x.RegistroResiduoId)
            .ToListAsync(cancellationToken);

        if (residuos.Count == 0)
            return ServiceResult<IReadOnlyCollection<RegistroResiduoEvidenciaDto>>.Ok(Array.Empty<RegistroResiduoEvidenciaDto>());

        var ubicaciones = await context.Set<RegistroResiduoUbicacion>()
            .AsNoTracking()
            .Where(x => residuos.Contains(x.RegistroResiduoId))
            .ToDictionaryAsync(x => x.RegistroResiduoId, cancellationToken);

        var fotos = await context.RegistroResiduoFotos
            .AsNoTracking()
            .Where(x => residuos.Contains(x.RegistroResiduoId) && !x.Eliminado)
            .GroupBy(x => x.RegistroResiduoId)
            .Select(g => new { RegistroResiduoId = g.Key, Cantidad = g.Count() })
            .ToDictionaryAsync(x => x.RegistroResiduoId, x => x.Cantidad, cancellationToken);

        var result = residuos.Select(id =>
        {
            ubicaciones.TryGetValue(id, out var ubicacion);
            fotos.TryGetValue(id, out var cantidadFotos);
            return new RegistroResiduoEvidenciaDto(
                id,
                ubicacion?.Latitud,
                ubicacion?.Longitud,
                ubicacion?.PrecisionMetros,
                ubicacion?.CapturadaUtc,
                cantidadFotos);
        }).ToArray();

        return ServiceResult<IReadOnlyCollection<RegistroResiduoEvidenciaDto>>.Ok(result);
    }

    public async Task<ServiceResult<RegistroResiduoEvidenciaDto>> GuardarUbicacionAsync(
        Guid registroId,
        Guid registroResiduoId,
        Guid usuarioId,
        GuardarUbicacionResiduoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Latitud is < -90 or > 90 || request.Longitud is < -180 or > 180)
            return ServiceResult<RegistroResiduoEvidenciaDto>.Fail("Las coordenadas no son válidas.", StatusCodes.Status400BadRequest);

        if (request.PrecisionMetros.HasValue && request.PrecisionMetros.Value < 0)
            return ServiceResult<RegistroResiduoEvidenciaDto>.Fail("La precisión no puede ser negativa.", StatusCodes.Status400BadRequest);

        var residuo = await context.RegistroResiduos
            .Include(x => x.Registro)
                .ThenInclude(x => x.EstadoRegistro)
            .Include(x => x.Registro)
                .ThenInclude(x => x.ControlGeneracion!)
                    .ThenInclude(x => x.EstadoControlGeneracion)
            .FirstOrDefaultAsync(x =>
                x.RegistroResiduoId == registroResiduoId &&
                x.RegistroId == registroId &&
                !x.Eliminado &&
                !x.Registro.Eliminado,
                cancellationToken);

        if (residuo is null)
            return ServiceResult<RegistroResiduoEvidenciaDto>.Fail("Residuo no encontrado.", StatusCodes.Status404NotFound);

        if (residuo.Registro.RegistradoPorUsuarioId != usuarioId)
            return ServiceResult<RegistroResiduoEvidenciaDto>.Fail("Solo quien creó el registro puede actualizar la ubicación.", StatusCodes.Status403Forbidden);

        if (residuo.Registro.EstadoRegistro.Codigo is not ("BORRADOR" or "EN_PROCESO"))
            return ServiceResult<RegistroResiduoEvidenciaDto>.Fail("El registro ya no admite cambios.", StatusCodes.Status409Conflict);

        if (residuo.Registro.ControlGeneracion is not null &&
            residuo.Registro.ControlGeneracion.EstadoControlGeneracion.Codigo != "ACTIVO")
            return ServiceResult<RegistroResiduoEvidenciaDto>.Fail("El control debe estar activo para actualizar la ubicación.", StatusCodes.Status409Conflict);

        var ubicacion = await context.Set<RegistroResiduoUbicacion>()
            .FirstOrDefaultAsync(x => x.RegistroResiduoId == registroResiduoId, cancellationToken);

        if (ubicacion is null)
        {
            ubicacion = new RegistroResiduoUbicacion
            {
                RegistroResiduoId = registroResiduoId
            };
            context.Set<RegistroResiduoUbicacion>().Add(ubicacion);
        }

        ubicacion.Latitud = request.Latitud;
        ubicacion.Longitud = request.Longitud;
        ubicacion.PrecisionMetros = request.PrecisionMetros;
        ubicacion.CapturadaUtc = request.CapturadaUtc;
        ubicacion.ActualizadoUtc = DateTime.UtcNow;

        context.Auditoria.Add(new Auditoria
        {
            UsuarioId = usuarioId,
            Entidad = "RegistroResiduo",
            EntidadId = registroResiduoId,
            Accion = "CAPTURAR_UBICACION",
            DatosDespuesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                request.Latitud,
                request.Longitud,
                request.PrecisionMetros,
                request.CapturadaUtc
            })
        });

        await context.SaveChangesAsync(cancellationToken);

        var cantidadFotos = await context.RegistroResiduoFotos
            .AsNoTracking()
            .CountAsync(x => x.RegistroResiduoId == registroResiduoId && !x.Eliminado, cancellationToken);

        return ServiceResult<RegistroResiduoEvidenciaDto>.Ok(new RegistroResiduoEvidenciaDto(
            registroResiduoId,
            ubicacion.Latitud,
            ubicacion.Longitud,
            ubicacion.PrecisionMetros,
            ubicacion.CapturadaUtc,
            cantidadFotos));
    }

    private async Task<bool> PuedeConsultarAsync(
        Guid autorId,
        Guid? controlId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        if (autorId == usuarioId)
            return true;

        var global = await context.UsuarioRoles
            .AsNoTracking()
            .AnyAsync(x =>
                x.UsuarioId == usuarioId &&
                x.Rol.EsActivo &&
                (x.Rol.Codigo == "AMBIENTAL" || x.Rol.Codigo == "ADMINISTRADOR"),
                cancellationToken);
        if (global)
            return true;

        if (!controlId.HasValue)
            return false;

        var now = DateTime.UtcNow;
        return await context.Set<ControlGeneracionUsuario>()
            .AsNoTracking()
            .AnyAsync(x =>
                x.ControlGeneracionId == controlId.Value &&
                x.UsuarioId == usuarioId &&
                x.EsActivo &&
                x.FechaDesde <= now &&
                (!x.FechaHasta.HasValue || x.FechaHasta.Value >= now),
                cancellationToken);
    }
}
