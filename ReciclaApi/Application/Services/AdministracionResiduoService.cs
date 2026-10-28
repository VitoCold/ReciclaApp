using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Persistence;

namespace ReciclaApi.Application.Services;

public interface IAdministracionResiduoService
{
    Task<IReadOnlyCollection<AdministracionTipoResiduoDto>> ListarTiposAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionTipoResiduoDto>> ObtenerTipoAsync(Guid tipoResiduoId, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionTipoResiduoDto>> CrearTipoAsync(Guid actorUsuarioId, GuardarAdministracionTipoResiduoRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionTipoResiduoDto>> ActualizarTipoAsync(Guid tipoResiduoId, Guid actorUsuarioId, GuardarAdministracionTipoResiduoRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionTipoResiduoDto>> CambiarEstadoTipoAsync(Guid tipoResiduoId, Guid actorUsuarioId, bool esActivo, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<AdministracionResiduoCatalogoDto>> ListarResiduosAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionResiduoCatalogoDto>> ObtenerResiduoAsync(Guid residuoId, CancellationToken cancellationToken = default);
    Task<AdministracionResiduosCatalogosDto> ObtenerCatalogosResiduosAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionResiduoCatalogoDto>> CrearResiduoAsync(Guid actorUsuarioId, GuardarAdministracionResiduoCatalogoRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionResiduoCatalogoDto>> ActualizarResiduoAsync(Guid residuoId, Guid actorUsuarioId, GuardarAdministracionResiduoCatalogoRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionResiduoCatalogoDto>> CambiarEstadoResiduoAsync(Guid residuoId, Guid actorUsuarioId, bool esActivo, CancellationToken cancellationToken = default);
}

public sealed class AdministracionResiduoService(ReciclaDbContext context) : IAdministracionResiduoService
{
    public async Task<IReadOnlyCollection<AdministracionTipoResiduoDto>> ListarTiposAsync(
        CancellationToken cancellationToken = default)
    {
        var tipos = await context.TiposResiduo
            .AsNoTracking()
            .OrderByDescending(x => x.EsActivo)
            .ThenBy(x => x.Nombre)
            .ToListAsync(cancellationToken);

        var resultado = new List<AdministracionTipoResiduoDto>(tipos.Count);
        foreach (var tipo in tipos)
            resultado.Add(await CrearTipoDtoAsync(tipo, cancellationToken));

        return resultado;
    }

    public async Task<ServiceResult<AdministracionTipoResiduoDto>> ObtenerTipoAsync(
        Guid tipoResiduoId,
        CancellationToken cancellationToken = default)
    {
        var tipo = await context.TiposResiduo
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TipoResiduoId == tipoResiduoId, cancellationToken);

        return tipo is null
            ? ServiceResult<AdministracionTipoResiduoDto>.Fail("Tipo de residuo no encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<AdministracionTipoResiduoDto>.Ok(await CrearTipoDtoAsync(tipo, cancellationToken));
    }

    public async Task<ServiceResult<AdministracionTipoResiduoDto>> CrearTipoAsync(
        Guid actorUsuarioId,
        GuardarAdministracionTipoResiduoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarTipoAsync(request, null, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionTipoResiduoDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var tipo = new TipoResiduo
        {
            Codigo = NormalizarCodigo(request.Codigo),
            Nombre = request.Nombre.Trim(),
            EsActivo = true
        };

        context.TiposResiduo.Add(tipo);
        Auditar(actorUsuarioId, "TipoResiduo", tipo.TipoResiduoId, "CREAR", null, new
        {
            tipo.Codigo,
            tipo.Nombre,
            tipo.EsActivo
        });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionTipoResiduoDto>.Ok(
            await CrearTipoDtoAsync(tipo, cancellationToken),
            StatusCodes.Status201Created);
    }

    public async Task<ServiceResult<AdministracionTipoResiduoDto>> ActualizarTipoAsync(
        Guid tipoResiduoId,
        Guid actorUsuarioId,
        GuardarAdministracionTipoResiduoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarTipoAsync(request, tipoResiduoId, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionTipoResiduoDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var tipo = await context.TiposResiduo
            .FirstOrDefaultAsync(x => x.TipoResiduoId == tipoResiduoId, cancellationToken);

        if (tipo is null)
            return ServiceResult<AdministracionTipoResiduoDto>.Fail("Tipo de residuo no encontrado.", StatusCodes.Status404NotFound);

        var antes = new { tipo.Codigo, tipo.Nombre, tipo.EsActivo };
        tipo.Codigo = NormalizarCodigo(request.Codigo);
        tipo.Nombre = request.Nombre.Trim();

        Auditar(actorUsuarioId, "TipoResiduo", tipo.TipoResiduoId, "EDITAR", antes, new
        {
            tipo.Codigo,
            tipo.Nombre,
            tipo.EsActivo
        });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionTipoResiduoDto>.Ok(await CrearTipoDtoAsync(tipo, cancellationToken));
    }

    public async Task<ServiceResult<AdministracionTipoResiduoDto>> CambiarEstadoTipoAsync(
        Guid tipoResiduoId,
        Guid actorUsuarioId,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        var tipo = await context.TiposResiduo
            .FirstOrDefaultAsync(x => x.TipoResiduoId == tipoResiduoId, cancellationToken);

        if (tipo is null)
            return ServiceResult<AdministracionTipoResiduoDto>.Fail("Tipo de residuo no encontrado.", StatusCodes.Status404NotFound);

        if (tipo.EsActivo == esActivo)
            return ServiceResult<AdministracionTipoResiduoDto>.Ok(await CrearTipoDtoAsync(tipo, cancellationToken));

        if (!esActivo)
        {
            var tieneResiduosActivos = await context.ResiduosCatalogo
                .AsNoTracking()
                .AnyAsync(x => x.TipoResiduoId == tipoResiduoId && x.EsActivo, cancellationToken);

            if (tieneResiduosActivos)
            {
                return ServiceResult<AdministracionTipoResiduoDto>.Fail(
                    "Desactiva primero los residuos activos asociados a este tipo.",
                    StatusCodes.Status409Conflict);
            }
        }

        var anterior = tipo.EsActivo;
        tipo.EsActivo = esActivo;
        Auditar(actorUsuarioId, "TipoResiduo", tipo.TipoResiduoId, esActivo ? "ACTIVAR" : "DESACTIVAR",
            new { EsActivo = anterior },
            new { tipo.EsActivo });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionTipoResiduoDto>.Ok(await CrearTipoDtoAsync(tipo, cancellationToken));
    }

    public async Task<IReadOnlyCollection<AdministracionResiduoCatalogoDto>> ListarResiduosAsync(
        CancellationToken cancellationToken = default)
    {
        var residuos = await context.ResiduosCatalogo
            .AsNoTracking()
            .Include(x => x.TipoResiduo)
            .Include(x => x.ClasificacionResiduo)
            .Include(x => x.UnidadMedidaDefault)
            .ToListAsync(cancellationToken);

        residuos = residuos
            .OrderByDescending(x => x.EsActivo)
            .ThenBy(x => x.ClasificacionResiduo.Nombre)
            .ThenBy(x => x.Nombre)
            .ToList();

        var resultado = new List<AdministracionResiduoCatalogoDto>(residuos.Count);
        foreach (var residuo in residuos)
            resultado.Add(await CrearResiduoDtoAsync(residuo, cancellationToken));

        return resultado;
    }

    public async Task<ServiceResult<AdministracionResiduoCatalogoDto>> ObtenerResiduoAsync(
        Guid residuoId,
        CancellationToken cancellationToken = default)
    {
        var residuo = await QueryResiduo()
            .FirstOrDefaultAsync(x => x.ResiduoId == residuoId, cancellationToken);

        return residuo is null
            ? ServiceResult<AdministracionResiduoCatalogoDto>.Fail("Residuo no encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<AdministracionResiduoCatalogoDto>.Ok(await CrearResiduoDtoAsync(residuo, cancellationToken));
    }

    public async Task<AdministracionResiduosCatalogosDto> ObtenerCatalogosResiduosAsync(
        CancellationToken cancellationToken = default)
    {
        var tipos = await context.TiposResiduo
            .AsNoTracking()
            .Where(x => x.EsActivo)
            .OrderBy(x => x.Nombre)
            .Select(x => new TipoResiduoDto(x.TipoResiduoId, x.Codigo, x.Nombre))
            .ToListAsync(cancellationToken);

        var clasificaciones = await context.ClasificacionesResiduo
            .AsNoTracking()
            .OrderBy(x => x.Nombre)
            .Select(x => new ClasificacionResiduoDto(
                x.ClasificacionResiduoId,
                x.Codigo,
                x.Nombre,
                x.ColorHex))
            .ToListAsync(cancellationToken);

        var unidades = await context.UnidadesMedida
            .AsNoTracking()
            .OrderBy(x => x.Nombre)
            .Select(x => new UnidadMedidaDto(x.UnidadMedidaId, x.Codigo, x.Nombre))
            .ToListAsync(cancellationToken);

        return new AdministracionResiduosCatalogosDto(tipos, clasificaciones, unidades);
    }

    public async Task<ServiceResult<AdministracionResiduoCatalogoDto>> CrearResiduoAsync(
        Guid actorUsuarioId,
        GuardarAdministracionResiduoCatalogoRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarResiduoAsync(request, null, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionResiduoCatalogoDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var residuo = new ResiduoCatalogo
        {
            TipoResiduoId = request.TipoResiduoId,
            ClasificacionResiduoId = request.ClasificacionResiduoId,
            Codigo = NormalizarCodigo(request.Codigo),
            Nombre = request.Nombre.Trim(),
            Descripcion = Limpiar(request.Descripcion),
            UnidadMedidaDefaultId = request.UnidadMedidaDefaultId,
            EsActivo = true
        };

        context.ResiduosCatalogo.Add(residuo);
        Auditar(actorUsuarioId, "ResiduoCatalogo", residuo.ResiduoId, "CREAR", null, new
        {
            residuo.TipoResiduoId,
            residuo.ClasificacionResiduoId,
            residuo.Codigo,
            residuo.Nombre,
            residuo.Descripcion,
            residuo.UnidadMedidaDefaultId,
            residuo.EsActivo
        });

        await context.SaveChangesAsync(cancellationToken);
        residuo = await QueryResiduo().FirstAsync(x => x.ResiduoId == residuo.ResiduoId, cancellationToken);

        return ServiceResult<AdministracionResiduoCatalogoDto>.Ok(
            await CrearResiduoDtoAsync(residuo, cancellationToken),
            StatusCodes.Status201Created);
    }

    public async Task<ServiceResult<AdministracionResiduoCatalogoDto>> ActualizarResiduoAsync(
        Guid residuoId,
        Guid actorUsuarioId,
        GuardarAdministracionResiduoCatalogoRequest request,
        CancellationToken cancellationToken = default)
    {
        var residuo = await QueryResiduo(tracking: true)
            .FirstOrDefaultAsync(x => x.ResiduoId == residuoId, cancellationToken);

        if (residuo is null)
            return ServiceResult<AdministracionResiduoCatalogoDto>.Fail("Residuo no encontrado.", StatusCodes.Status404NotFound);

        var validacion = await ValidarResiduoAsync(request, residuoId, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionResiduoCatalogoDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var tieneUso = await TieneUsoResiduoAsync(residuoId, cancellationToken);
        if (tieneUso &&
            (residuo.TipoResiduoId != request.TipoResiduoId ||
             residuo.ClasificacionResiduoId != request.ClasificacionResiduoId))
        {
            return ServiceResult<AdministracionResiduoCatalogoDto>.Fail(
                "El tipo y la clasificación no pueden cambiarse después del primer uso del residuo. Puedes editar código, nombre, descripción y unidad predeterminada.",
                StatusCodes.Status409Conflict);
        }

        var antes = new
        {
            residuo.TipoResiduoId,
            residuo.ClasificacionResiduoId,
            residuo.Codigo,
            residuo.Nombre,
            residuo.Descripcion,
            residuo.UnidadMedidaDefaultId,
            residuo.EsActivo
        };

        residuo.TipoResiduoId = request.TipoResiduoId;
        residuo.ClasificacionResiduoId = request.ClasificacionResiduoId;
        residuo.Codigo = NormalizarCodigo(request.Codigo);
        residuo.Nombre = request.Nombre.Trim();
        residuo.Descripcion = Limpiar(request.Descripcion);
        residuo.UnidadMedidaDefaultId = request.UnidadMedidaDefaultId;

        Auditar(actorUsuarioId, "ResiduoCatalogo", residuo.ResiduoId, "EDITAR", antes, new
        {
            residuo.TipoResiduoId,
            residuo.ClasificacionResiduoId,
            residuo.Codigo,
            residuo.Nombre,
            residuo.Descripcion,
            residuo.UnidadMedidaDefaultId,
            residuo.EsActivo
        });

        await context.SaveChangesAsync(cancellationToken);
        residuo = await QueryResiduo().FirstAsync(x => x.ResiduoId == residuoId, cancellationToken);

        return ServiceResult<AdministracionResiduoCatalogoDto>.Ok(
            await CrearResiduoDtoAsync(residuo, cancellationToken));
    }

    public async Task<ServiceResult<AdministracionResiduoCatalogoDto>> CambiarEstadoResiduoAsync(
        Guid residuoId,
        Guid actorUsuarioId,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        var residuo = await QueryResiduo(tracking: true)
            .FirstOrDefaultAsync(x => x.ResiduoId == residuoId, cancellationToken);

        if (residuo is null)
            return ServiceResult<AdministracionResiduoCatalogoDto>.Fail("Residuo no encontrado.", StatusCodes.Status404NotFound);

        if (residuo.EsActivo == esActivo)
            return ServiceResult<AdministracionResiduoCatalogoDto>.Ok(await CrearResiduoDtoAsync(residuo, cancellationToken));

        if (esActivo && !residuo.TipoResiduo.EsActivo)
        {
            return ServiceResult<AdministracionResiduoCatalogoDto>.Fail(
                "No puedes activar un residuo cuyo tipo está inactivo.",
                StatusCodes.Status409Conflict);
        }

        var anterior = residuo.EsActivo;
        residuo.EsActivo = esActivo;
        Auditar(actorUsuarioId, "ResiduoCatalogo", residuo.ResiduoId, esActivo ? "ACTIVAR" : "DESACTIVAR",
            new { EsActivo = anterior },
            new { residuo.EsActivo });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionResiduoCatalogoDto>.Ok(await CrearResiduoDtoAsync(residuo, cancellationToken));
    }

    private async Task<string?> ValidarTipoAsync(
        GuardarAdministracionTipoResiduoRequest request,
        Guid? tipoResiduoId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
            return "El código del tipo es obligatorio.";
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return "El nombre del tipo es obligatorio.";

        var codigo = NormalizarCodigo(request.Codigo);
        if (codigo.Length > 40)
            return "El código del tipo no puede superar 40 caracteres.";
        if (request.Nombre.Trim().Length > 120)
            return "El nombre del tipo no puede superar 120 caracteres.";

        var duplicado = await context.TiposResiduo
            .AsNoTracking()
            .AnyAsync(x =>
                x.Codigo.ToUpper() == codigo &&
                (!tipoResiduoId.HasValue || x.TipoResiduoId != tipoResiduoId.Value),
                cancellationToken);

        return duplicado ? "Ya existe un tipo de residuo con ese código." : null;
    }

    private async Task<string?> ValidarResiduoAsync(
        GuardarAdministracionResiduoCatalogoRequest request,
        Guid? residuoId,
        CancellationToken cancellationToken)
    {
        if (request.TipoResiduoId == Guid.Empty)
            return "Selecciona un tipo de residuo.";
        if (request.ClasificacionResiduoId <= 0)
            return "Selecciona una clasificación.";
        if (request.UnidadMedidaDefaultId <= 0)
            return "Selecciona una unidad predeterminada.";
        if (string.IsNullOrWhiteSpace(request.Codigo))
            return "El código del residuo es obligatorio.";
        if (string.IsNullOrWhiteSpace(request.Nombre))
            return "El nombre del residuo es obligatorio.";

        var codigo = NormalizarCodigo(request.Codigo);
        if (codigo.Length > 40)
            return "El código del residuo no puede superar 40 caracteres.";
        if (request.Nombre.Trim().Length > 160)
            return "El nombre del residuo no puede superar 160 caracteres.";
        if (Limpiar(request.Descripcion)?.Length > 250)
            return "La descripción no puede superar 250 caracteres.";

        var tipo = await context.TiposResiduo
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TipoResiduoId == request.TipoResiduoId, cancellationToken);
        if (tipo is null)
            return "El tipo de residuo seleccionado no existe.";

        var mantieneTipoActual = residuoId.HasValue &&
            await context.ResiduosCatalogo.AsNoTracking()
                .AnyAsync(x => x.ResiduoId == residuoId.Value && x.TipoResiduoId == request.TipoResiduoId, cancellationToken);

        if (!tipo.EsActivo && !mantieneTipoActual)
            return "No puedes asignar un residuo a un tipo inactivo.";

        var clasificacionExiste = await context.ClasificacionesResiduo
            .AsNoTracking()
            .AnyAsync(x => x.ClasificacionResiduoId == request.ClasificacionResiduoId, cancellationToken);
        if (!clasificacionExiste)
            return "La clasificación seleccionada no existe.";

        var unidadExiste = await context.UnidadesMedida
            .AsNoTracking()
            .AnyAsync(x => x.UnidadMedidaId == request.UnidadMedidaDefaultId, cancellationToken);
        if (!unidadExiste)
            return "La unidad seleccionada no existe.";

        var duplicado = await context.ResiduosCatalogo
            .AsNoTracking()
            .AnyAsync(x =>
                x.Codigo.ToUpper() == codigo &&
                (!residuoId.HasValue || x.ResiduoId != residuoId.Value),
                cancellationToken);

        return duplicado ? "Ya existe un residuo con ese código." : null;
    }

    private async Task<AdministracionTipoResiduoDto> CrearTipoDtoAsync(
        TipoResiduo tipo,
        CancellationToken cancellationToken)
    {
        var residuosActivos = await context.ResiduosCatalogo
            .AsNoTracking()
            .CountAsync(x => x.TipoResiduoId == tipo.TipoResiduoId && x.EsActivo, cancellationToken);

        var tieneUso = await context.RegistroResiduos
            .AsNoTracking()
            .AnyAsync(x => x.TipoResiduoId == tipo.TipoResiduoId && !x.Eliminado, cancellationToken);

        return new AdministracionTipoResiduoDto(
            tipo.TipoResiduoId,
            tipo.Codigo,
            tipo.Nombre,
            tipo.EsActivo,
            residuosActivos,
            tieneUso);
    }

    private async Task<AdministracionResiduoCatalogoDto> CrearResiduoDtoAsync(
        ResiduoCatalogo residuo,
        CancellationToken cancellationToken)
    {
        var tieneUso = await TieneUsoResiduoAsync(residuo.ResiduoId, cancellationToken);

        return new AdministracionResiduoCatalogoDto(
            residuo.ResiduoId,
            residuo.TipoResiduoId,
            residuo.TipoResiduo.Codigo,
            residuo.TipoResiduo.Nombre,
            residuo.ClasificacionResiduoId,
            residuo.ClasificacionResiduo.Codigo,
            residuo.ClasificacionResiduo.Nombre,
            residuo.Codigo,
            residuo.Nombre,
            residuo.Descripcion,
            residuo.UnidadMedidaDefaultId,
            residuo.UnidadMedidaDefault.Codigo,
            residuo.UnidadMedidaDefault.Nombre,
            residuo.EsActivo,
            tieneUso);
    }

    private Task<bool> TieneUsoResiduoAsync(Guid residuoId, CancellationToken cancellationToken) =>
        context.RegistroResiduos
            .AsNoTracking()
            .AnyAsync(x => x.ResiduoId == residuoId && !x.Eliminado, cancellationToken);

    private IQueryable<ResiduoCatalogo> QueryResiduo(bool tracking = false)
    {
        var query = tracking
            ? context.ResiduosCatalogo.AsQueryable()
            : context.ResiduosCatalogo.AsNoTracking();

        return query
            .Include(x => x.TipoResiduo)
            .Include(x => x.ClasificacionResiduo)
            .Include(x => x.UnidadMedidaDefault);
    }

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

    private static string? Limpiar(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
