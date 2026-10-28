using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Persistence;

namespace ReciclaApi.Application.Services;

public interface IAdministracionProyectoActividadService
{
    Task<IReadOnlyCollection<AdministracionProyectoDto>> ListarProyectosAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionProyectoDto>> ObtenerProyectoAsync(Guid proyectoId, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionProyectoDto>> CrearProyectoAsync(Guid actorUsuarioId, GuardarAdministracionProyectoRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionProyectoDto>> ActualizarProyectoAsync(Guid proyectoId, Guid actorUsuarioId, GuardarAdministracionProyectoRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionProyectoDto>> CambiarEstadoProyectoAsync(Guid proyectoId, Guid actorUsuarioId, bool esActivo, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AdministracionActividadDto>> ListarActividadesAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionActividadDto>> ObtenerActividadAsync(Guid actividadId, CancellationToken cancellationToken = default);
    Task<AdministracionActividadesCatalogosDto> ObtenerCatalogosActividadesAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionActividadDto>> CrearActividadAsync(Guid actorUsuarioId, GuardarAdministracionActividadRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionActividadDto>> ActualizarActividadAsync(Guid actividadId, Guid actorUsuarioId, GuardarAdministracionActividadRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionActividadDto>> CambiarEstadoActividadAsync(Guid actividadId, Guid actorUsuarioId, bool esActivo, CancellationToken cancellationToken = default);
}

public sealed class AdministracionProyectoActividadService(ReciclaDbContext context) : IAdministracionProyectoActividadService
{
    public async Task<IReadOnlyCollection<AdministracionProyectoDto>> ListarProyectosAsync(
        CancellationToken cancellationToken = default)
    {
        var proyectos = await context.Proyectos
            .AsNoTracking()
            .OrderByDescending(x => x.EsActivo)
            .ThenBy(x => x.Nombre)
            .ToListAsync(cancellationToken);

        var resultado = new List<AdministracionProyectoDto>(proyectos.Count);
        foreach (var proyecto in proyectos)
            resultado.Add(await CrearProyectoDtoAsync(proyecto, cancellationToken));

        return resultado;
    }

    public async Task<ServiceResult<AdministracionProyectoDto>> ObtenerProyectoAsync(
        Guid proyectoId,
        CancellationToken cancellationToken = default)
    {
        var proyecto = await context.Proyectos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProyectoId == proyectoId, cancellationToken);

        return proyecto is null
            ? ServiceResult<AdministracionProyectoDto>.Fail("Proyecto no encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<AdministracionProyectoDto>.Ok(await CrearProyectoDtoAsync(proyecto, cancellationToken));
    }

    public async Task<ServiceResult<AdministracionProyectoDto>> CrearProyectoAsync(
        Guid actorUsuarioId,
        GuardarAdministracionProyectoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarProyectoAsync(request, null, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionProyectoDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var proyecto = new Proyecto
        {
            Codigo = NormalizarCodigo(request.Codigo),
            Nombre = request.Nombre.Trim(),
            EsActivo = true
        };

        context.Proyectos.Add(proyecto);
        Auditar(actorUsuarioId, "Proyecto", proyecto.ProyectoId, "CREAR", null, new
        {
            proyecto.Codigo,
            proyecto.Nombre,
            proyecto.EsActivo
        });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionProyectoDto>.Ok(
            await CrearProyectoDtoAsync(proyecto, cancellationToken),
            StatusCodes.Status201Created);
    }

    public async Task<ServiceResult<AdministracionProyectoDto>> ActualizarProyectoAsync(
        Guid proyectoId,
        Guid actorUsuarioId,
        GuardarAdministracionProyectoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarProyectoAsync(request, proyectoId, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionProyectoDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var proyecto = await context.Proyectos
            .FirstOrDefaultAsync(x => x.ProyectoId == proyectoId, cancellationToken);

        if (proyecto is null)
            return ServiceResult<AdministracionProyectoDto>.Fail("Proyecto no encontrado.", StatusCodes.Status404NotFound);

        var antes = new { proyecto.Codigo, proyecto.Nombre, proyecto.EsActivo };
        proyecto.Codigo = NormalizarCodigo(request.Codigo);
        proyecto.Nombre = request.Nombre.Trim();

        Auditar(actorUsuarioId, "Proyecto", proyecto.ProyectoId, "EDITAR", antes, new
        {
            proyecto.Codigo,
            proyecto.Nombre,
            proyecto.EsActivo
        });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionProyectoDto>.Ok(
            await CrearProyectoDtoAsync(proyecto, cancellationToken));
    }

    public async Task<ServiceResult<AdministracionProyectoDto>> CambiarEstadoProyectoAsync(
        Guid proyectoId,
        Guid actorUsuarioId,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        var proyecto = await context.Proyectos
            .FirstOrDefaultAsync(x => x.ProyectoId == proyectoId, cancellationToken);

        if (proyecto is null)
            return ServiceResult<AdministracionProyectoDto>.Fail("Proyecto no encontrado.", StatusCodes.Status404NotFound);

        if (proyecto.EsActivo == esActivo)
            return ServiceResult<AdministracionProyectoDto>.Ok(await CrearProyectoDtoAsync(proyecto, cancellationToken));

        if (!esActivo)
        {
            var actividadesActivas = await context.Actividades
                .AsNoTracking()
                .AnyAsync(x => x.ProyectoId == proyectoId && x.EsActivo, cancellationToken);
            if (actividadesActivas)
            {
                return ServiceResult<AdministracionProyectoDto>.Fail(
                    "Desactiva primero las actividades activas del proyecto.",
                    StatusCodes.Status409Conflict);
            }

            if (await ContarControlesAbiertosProyectoAsync(proyectoId, cancellationToken) > 0)
            {
                return ServiceResult<AdministracionProyectoDto>.Fail(
                    "No puedes desactivar un proyecto usado por Controles de Generación abiertos.",
                    StatusCodes.Status409Conflict);
            }
        }

        var anterior = proyecto.EsActivo;
        proyecto.EsActivo = esActivo;

        Auditar(actorUsuarioId, "Proyecto", proyecto.ProyectoId, esActivo ? "ACTIVAR" : "DESACTIVAR",
            new { EsActivo = anterior },
            new { proyecto.EsActivo });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionProyectoDto>.Ok(await CrearProyectoDtoAsync(proyecto, cancellationToken));
    }

    public async Task<IReadOnlyCollection<AdministracionActividadDto>> ListarActividadesAsync(
        CancellationToken cancellationToken = default)
    {
        var actividades = await context.Actividades
            .AsNoTracking()
            .Include(x => x.Proyecto)
            .ToListAsync(cancellationToken);

        actividades = actividades
            .OrderByDescending(x => x.EsActivo)
            .ThenBy(x => x.Proyecto?.Nombre ?? string.Empty)
            .ThenBy(x => x.Nombre)
            .ToList();

        var resultado = new List<AdministracionActividadDto>(actividades.Count);
        foreach (var actividad in actividades)
            resultado.Add(await CrearActividadDtoAsync(actividad, cancellationToken));

        return resultado;
    }

    public async Task<ServiceResult<AdministracionActividadDto>> ObtenerActividadAsync(
        Guid actividadId,
        CancellationToken cancellationToken = default)
    {
        var actividad = await context.Actividades
            .AsNoTracking()
            .Include(x => x.Proyecto)
            .FirstOrDefaultAsync(x => x.ActividadId == actividadId, cancellationToken);

        return actividad is null
            ? ServiceResult<AdministracionActividadDto>.Fail("Actividad no encontrada.", StatusCodes.Status404NotFound)
            : ServiceResult<AdministracionActividadDto>.Ok(await CrearActividadDtoAsync(actividad, cancellationToken));
    }

    public async Task<AdministracionActividadesCatalogosDto> ObtenerCatalogosActividadesAsync(
        CancellationToken cancellationToken = default)
    {
        var proyectos = await context.Proyectos
            .AsNoTracking()
            .Where(x => x.EsActivo)
            .OrderBy(x => x.Nombre)
            .Select(x => new ProyectoDto(x.ProyectoId, x.Codigo, x.Nombre))
            .ToListAsync(cancellationToken);

        return new AdministracionActividadesCatalogosDto(proyectos);
    }

    public async Task<ServiceResult<AdministracionActividadDto>> CrearActividadAsync(
        Guid actorUsuarioId,
        GuardarAdministracionActividadRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarActividadAsync(request, null, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionActividadDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var actividad = new Actividad
        {
            ProyectoId = request.ProyectoId,
            Codigo = NormalizarCodigo(request.Codigo),
            Nombre = request.Nombre.Trim(),
            EsActivo = true
        };

        context.Actividades.Add(actividad);
        Auditar(actorUsuarioId, "Actividad", actividad.ActividadId, "CREAR", null, new
        {
            actividad.ProyectoId,
            actividad.Codigo,
            actividad.Nombre,
            actividad.EsActivo
        });

        await context.SaveChangesAsync(cancellationToken);
        actividad.Proyecto = await context.Proyectos
            .AsNoTracking()
            .FirstAsync(x => x.ProyectoId == request.ProyectoId, cancellationToken);

        return ServiceResult<AdministracionActividadDto>.Ok(
            await CrearActividadDtoAsync(actividad, cancellationToken),
            StatusCodes.Status201Created);
    }

    public async Task<ServiceResult<AdministracionActividadDto>> ActualizarActividadAsync(
        Guid actividadId,
        Guid actorUsuarioId,
        GuardarAdministracionActividadRequest request,
        CancellationToken cancellationToken = default)
    {
        var actividad = await context.Actividades
            .Include(x => x.Proyecto)
            .FirstOrDefaultAsync(x => x.ActividadId == actividadId, cancellationToken);

        if (actividad is null)
            return ServiceResult<AdministracionActividadDto>.Fail("Actividad no encontrada.", StatusCodes.Status404NotFound);

        var validacion = await ValidarActividadAsync(request, actividadId, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionActividadDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var tieneUso = await TieneUsoActividadAsync(actividadId, cancellationToken);
        if (tieneUso && actividad.ProyectoId != request.ProyectoId)
        {
            return ServiceResult<AdministracionActividadDto>.Fail(
                "El proyecto de una actividad no puede cambiarse después de su primer uso operativo.",
                StatusCodes.Status409Conflict);
        }

        var antes = new
        {
            actividad.ProyectoId,
            actividad.Codigo,
            actividad.Nombre,
            actividad.EsActivo
        };

        actividad.ProyectoId = request.ProyectoId;
        actividad.Codigo = NormalizarCodigo(request.Codigo);
        actividad.Nombre = request.Nombre.Trim();

        Auditar(actorUsuarioId, "Actividad", actividad.ActividadId, "EDITAR", antes, new
        {
            actividad.ProyectoId,
            actividad.Codigo,
            actividad.Nombre,
            actividad.EsActivo
        });

        await context.SaveChangesAsync(cancellationToken);
        actividad.Proyecto = await context.Proyectos
            .AsNoTracking()
            .FirstAsync(x => x.ProyectoId == request.ProyectoId, cancellationToken);

        return ServiceResult<AdministracionActividadDto>.Ok(
            await CrearActividadDtoAsync(actividad, cancellationToken));
    }

    public async Task<ServiceResult<AdministracionActividadDto>> CambiarEstadoActividadAsync(
        Guid actividadId,
        Guid actorUsuarioId,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        var actividad = await context.Actividades
            .Include(x => x.Proyecto)
            .FirstOrDefaultAsync(x => x.ActividadId == actividadId, cancellationToken);

        if (actividad is null)
            return ServiceResult<AdministracionActividadDto>.Fail("Actividad no encontrada.", StatusCodes.Status404NotFound);

        if (actividad.EsActivo == esActivo)
            return ServiceResult<AdministracionActividadDto>.Ok(await CrearActividadDtoAsync(actividad, cancellationToken));

        if (esActivo && (actividad.Proyecto is null || !actividad.Proyecto.EsActivo))
        {
            return ServiceResult<AdministracionActividadDto>.Fail(
                "No puedes activar una actividad si su proyecto está inactivo o no está asignado.",
                StatusCodes.Status409Conflict);
        }

        if (!esActivo && await ContarControlesAbiertosActividadAsync(actividadId, cancellationToken) > 0)
        {
            return ServiceResult<AdministracionActividadDto>.Fail(
                "No puedes desactivar una actividad usada por Controles de Generación abiertos.",
                StatusCodes.Status409Conflict);
        }

        var anterior = actividad.EsActivo;
        actividad.EsActivo = esActivo;

        Auditar(actorUsuarioId, "Actividad", actividad.ActividadId, esActivo ? "ACTIVAR" : "DESACTIVAR",
            new { EsActivo = anterior },
            new { actividad.EsActivo });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionActividadDto>.Ok(await CrearActividadDtoAsync(actividad, cancellationToken));
    }

    private async Task<string?> ValidarProyectoAsync(
        GuardarAdministracionProyectoRequest request,
        Guid? proyectoId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
            return "El código del proyecto es obligatorio.";
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return "El nombre del proyecto es obligatorio.";

        var codigo = NormalizarCodigo(request.Codigo);
        if (codigo.Length > 40)
            return "El código del proyecto no puede superar 40 caracteres.";
        if (request.Nombre.Trim().Length > 160)
            return "El nombre del proyecto no puede superar 160 caracteres.";

        var duplicado = await context.Proyectos
            .AsNoTracking()
            .AnyAsync(x =>
                x.Codigo.ToUpper() == codigo &&
                (!proyectoId.HasValue || x.ProyectoId != proyectoId.Value),
                cancellationToken);

        return duplicado ? "Ya existe un proyecto con ese código." : null;
    }

    private async Task<string?> ValidarActividadAsync(
        GuardarAdministracionActividadRequest request,
        Guid? actividadId,
        CancellationToken cancellationToken)
    {
        if (request.ProyectoId == Guid.Empty)
            return "Selecciona un proyecto.";
        if (string.IsNullOrWhiteSpace(request.Codigo))
            return "El código de la actividad es obligatorio.";
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return "El nombre de la actividad es obligatorio.";

        var proyecto = await context.Proyectos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ProyectoId == request.ProyectoId, cancellationToken);
        if (proyecto is null)
            return "El proyecto seleccionado no existe.";

        var mantieneProyectoActual = actividadId.HasValue &&
            await context.Actividades
                .AsNoTracking()
                .AnyAsync(x => x.ActividadId == actividadId.Value && x.ProyectoId == request.ProyectoId, cancellationToken);

        if (!proyecto.EsActivo && !mantieneProyectoActual)
            return "No puedes asignar la actividad a un proyecto inactivo.";

        var codigo = NormalizarCodigo(request.Codigo);
        if (codigo.Length > 40)
            return "El código de la actividad no puede superar 40 caracteres.";
        if (request.Nombre.Trim().Length > 160)
            return "El nombre de la actividad no puede superar 160 caracteres.";

        var duplicado = await context.Actividades
            .AsNoTracking()
            .AnyAsync(x =>
                x.ProyectoId == request.ProyectoId &&
                x.Codigo.ToUpper() == codigo &&
                (!actividadId.HasValue || x.ActividadId != actividadId.Value),
                cancellationToken);

        return duplicado ? "Ya existe una actividad con ese código dentro del proyecto." : null;
    }

    private async Task<AdministracionProyectoDto> CrearProyectoDtoAsync(
        Proyecto proyecto,
        CancellationToken cancellationToken)
    {
        var actividadesActivas = await context.Actividades
            .AsNoTracking()
            .CountAsync(x => x.ProyectoId == proyecto.ProyectoId && x.EsActivo, cancellationToken);

        var controlesAbiertos = await ContarControlesAbiertosProyectoAsync(proyecto.ProyectoId, cancellationToken);

        var tieneUso = await context.ControlesGeneracion.AsNoTracking()
            .AnyAsync(x => x.ProyectoId == proyecto.ProyectoId, cancellationToken)
            || await context.Registros.AsNoTracking()
                .AnyAsync(x => x.ProyectoId == proyecto.ProyectoId && !x.Eliminado, cancellationToken);

        return new AdministracionProyectoDto(
            proyecto.ProyectoId,
            proyecto.Codigo,
            proyecto.Nombre,
            proyecto.EsActivo,
            actividadesActivas,
            controlesAbiertos,
            tieneUso);
    }

    private async Task<AdministracionActividadDto> CrearActividadDtoAsync(
        Actividad actividad,
        CancellationToken cancellationToken)
    {
        var controlesAbiertos = await ContarControlesAbiertosActividadAsync(actividad.ActividadId, cancellationToken);
        var tieneUso = await TieneUsoActividadAsync(actividad.ActividadId, cancellationToken);

        return new AdministracionActividadDto(
            actividad.ActividadId,
            actividad.ProyectoId,
            actividad.Proyecto?.Codigo,
            actividad.Proyecto?.Nombre,
            actividad.Codigo,
            actividad.Nombre,
            actividad.EsActivo,
            controlesAbiertos,
            tieneUso);
    }

    private Task<int> ContarControlesAbiertosProyectoAsync(Guid proyectoId, CancellationToken cancellationToken) =>
        context.ControlesGeneracion
            .AsNoTracking()
            .CountAsync(x =>
                x.ProyectoId == proyectoId &&
                !x.Eliminado &&
                x.EstadoControlGeneracion.Codigo != "FINALIZADO" &&
                x.EstadoControlGeneracion.Codigo != "ANULADO",
                cancellationToken);

    private Task<int> ContarControlesAbiertosActividadAsync(Guid actividadId, CancellationToken cancellationToken) =>
        context.ControlesGeneracion
            .AsNoTracking()
            .CountAsync(x =>
                x.ActividadId == actividadId &&
                !x.Eliminado &&
                x.EstadoControlGeneracion.Codigo != "FINALIZADO" &&
                x.EstadoControlGeneracion.Codigo != "ANULADO",
                cancellationToken);

    private async Task<bool> TieneUsoActividadAsync(Guid actividadId, CancellationToken cancellationToken) =>
        await context.ControlesGeneracion
            .AsNoTracking()
            .AnyAsync(x => x.ActividadId == actividadId, cancellationToken)
        || await context.Registros
            .AsNoTracking()
            .AnyAsync(x => x.ActividadId == actividadId && !x.Eliminado, cancellationToken);

    private void Auditar(
        Guid actorUsuarioId,
        string entidad,
        Guid entidadId,
        string accion,
        object? antes,
        object? despues)
    {
        context.Auditoria.Add(new Auditoria
        {
            UsuarioId = actorUsuarioId,
            Entidad = entidad,
            EntidadId = entidadId,
            Accion = accion,
            DatosAntesJson = antes is null ? null : JsonSerializer.Serialize(antes),
            DatosDespuesJson = despues is null ? null : JsonSerializer.Serialize(despues)
        });
    }

    private static string NormalizarCodigo(string codigo) =>
        codigo.Trim().ToUpperInvariant().Replace(' ', '_');
}
