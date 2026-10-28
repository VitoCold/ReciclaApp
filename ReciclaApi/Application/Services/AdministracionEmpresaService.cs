using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Persistence;

namespace ReciclaApi.Application.Services;

public interface IAdministracionEmpresaService
{
    Task<IReadOnlyCollection<AdministracionEmpresaDto>> ListarAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionEmpresaDto>> ObtenerAsync(Guid empresaId, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionEmpresaDto>> CrearAsync(Guid actorUsuarioId, GuardarAdministracionEmpresaRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionEmpresaDto>> ActualizarAsync(Guid empresaId, Guid actorUsuarioId, GuardarAdministracionEmpresaRequest request, CancellationToken cancellationToken = default);
    Task<ServiceResult<AdministracionEmpresaDto>> CambiarEstadoAsync(Guid empresaId, Guid actorUsuarioId, bool esActivo, CancellationToken cancellationToken = default);
}

public sealed class AdministracionEmpresaService(ReciclaDbContext context) : IAdministracionEmpresaService
{
    public async Task<IReadOnlyCollection<AdministracionEmpresaDto>> ListarAsync(
        CancellationToken cancellationToken = default)
    {
        var empresas = await context.Empresas
            .AsNoTracking()
            .OrderByDescending(x => x.EsActivo)
            .ThenByDescending(x => x.EsGestoraResiduos)
            .ThenBy(x => x.RazonSocial)
            .ToListAsync(cancellationToken);

        var resultado = new List<AdministracionEmpresaDto>(empresas.Count);
        foreach (var empresa in empresas)
            resultado.Add(await CrearDtoAsync(empresa, cancellationToken));

        return resultado;
    }

    public async Task<ServiceResult<AdministracionEmpresaDto>> ObtenerAsync(
        Guid empresaId,
        CancellationToken cancellationToken = default)
    {
        var empresa = await context.Empresas
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId, cancellationToken);

        return empresa is null
            ? ServiceResult<AdministracionEmpresaDto>.Fail("Empresa no encontrada.", StatusCodes.Status404NotFound)
            : ServiceResult<AdministracionEmpresaDto>.Ok(await CrearDtoAsync(empresa, cancellationToken));
    }

    public async Task<ServiceResult<AdministracionEmpresaDto>> CrearAsync(
        Guid actorUsuarioId,
        GuardarAdministracionEmpresaRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarAsync(request, null, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionEmpresaDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var empresa = new Empresa
        {
            Codigo = NormalizarCodigo(request.Codigo),
            Ruc = Limpiar(request.Ruc),
            RazonSocial = request.RazonSocial.Trim(),
            NombreComercial = Limpiar(request.NombreComercial),
            Direccion = Limpiar(request.Direccion),
            Telefono = Limpiar(request.Telefono),
            Email = Limpiar(request.Email),
            EsGestoraResiduos = request.EsGestoraResiduos,
            NumeroAutorizacion = request.EsGestoraResiduos ? Limpiar(request.NumeroAutorizacion) : null,
            EsActivo = true
        };

        context.Empresas.Add(empresa);
        Auditar(actorUsuarioId, empresa.EmpresaId, "CREAR", null, DatosAuditoria(empresa));

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionEmpresaDto>.Ok(
            await CrearDtoAsync(empresa, cancellationToken),
            StatusCodes.Status201Created);
    }

    public async Task<ServiceResult<AdministracionEmpresaDto>> ActualizarAsync(
        Guid empresaId,
        Guid actorUsuarioId,
        GuardarAdministracionEmpresaRequest request,
        CancellationToken cancellationToken = default)
    {
        var empresa = await context.Empresas
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId, cancellationToken);

        if (empresa is null)
            return ServiceResult<AdministracionEmpresaDto>.Fail("Empresa no encontrada.", StatusCodes.Status404NotFound);

        var validacion = await ValidarAsync(request, empresaId, cancellationToken);
        if (validacion is not null)
            return ServiceResult<AdministracionEmpresaDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        var tieneUso = await TieneUsoHistoricoAsync(empresaId, cancellationToken);
        if (tieneUso && empresa.EsGestoraResiduos != request.EsGestoraResiduos)
        {
            return ServiceResult<AdministracionEmpresaDto>.Fail(
                "El rol de la empresa no puede cambiarse después de su primer uso operativo. Puedes editar sus datos generales.",
                StatusCodes.Status409Conflict);
        }

        var antes = DatosAuditoria(empresa);

        empresa.Codigo = NormalizarCodigo(request.Codigo);
        empresa.Ruc = Limpiar(request.Ruc);
        empresa.RazonSocial = request.RazonSocial.Trim();
        empresa.NombreComercial = Limpiar(request.NombreComercial);
        empresa.Direccion = Limpiar(request.Direccion);
        empresa.Telefono = Limpiar(request.Telefono);
        empresa.Email = Limpiar(request.Email);
        empresa.EsGestoraResiduos = request.EsGestoraResiduos;
        empresa.NumeroAutorizacion = request.EsGestoraResiduos ? Limpiar(request.NumeroAutorizacion) : null;

        Auditar(actorUsuarioId, empresa.EmpresaId, "EDITAR", antes, DatosAuditoria(empresa));
        await context.SaveChangesAsync(cancellationToken);

        return ServiceResult<AdministracionEmpresaDto>.Ok(await CrearDtoAsync(empresa, cancellationToken));
    }

    public async Task<ServiceResult<AdministracionEmpresaDto>> CambiarEstadoAsync(
        Guid empresaId,
        Guid actorUsuarioId,
        bool esActivo,
        CancellationToken cancellationToken = default)
    {
        var empresa = await context.Empresas
            .FirstOrDefaultAsync(x => x.EmpresaId == empresaId, cancellationToken);

        if (empresa is null)
            return ServiceResult<AdministracionEmpresaDto>.Fail("Empresa no encontrada.", StatusCodes.Status404NotFound);

        if (empresa.EsActivo == esActivo)
            return ServiceResult<AdministracionEmpresaDto>.Ok(await CrearDtoAsync(empresa, cancellationToken));

        if (!esActivo && !empresa.EsGestoraResiduos)
        {
            var controlesAbiertos = await ContarControlesAbiertosAsync(empresaId, cancellationToken);
            if (controlesAbiertos > 0)
            {
                return ServiceResult<AdministracionEmpresaDto>.Fail(
                    "No puedes desactivar una empresa responsable usada por Controles de Generación abiertos.",
                    StatusCodes.Status409Conflict);
            }
        }

        var anterior = empresa.EsActivo;
        empresa.EsActivo = esActivo;

        Auditar(
            actorUsuarioId,
            empresa.EmpresaId,
            esActivo ? "ACTIVAR" : "DESACTIVAR",
            new { EsActivo = anterior },
            new { empresa.EsActivo });

        await context.SaveChangesAsync(cancellationToken);
        return ServiceResult<AdministracionEmpresaDto>.Ok(await CrearDtoAsync(empresa, cancellationToken));
    }

    private async Task<string?> ValidarAsync(
        GuardarAdministracionEmpresaRequest request,
        Guid? empresaId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo))
            return "El código de la empresa es obligatorio.";
        if (string.IsNullOrWhiteSpace(request.RazonSocial))
            return "La razón social es obligatoria.";

        var codigo = NormalizarCodigo(request.Codigo);
        if (codigo.Length > 40) return "El código no puede superar 40 caracteres.";
        if (Limpiar(request.Ruc)?.Length > 20) return "El RUC no puede superar 20 caracteres.";
        if (request.RazonSocial.Trim().Length > 180) return "La razón social no puede superar 180 caracteres.";
        if (Limpiar(request.NombreComercial)?.Length > 180) return "El nombre comercial no puede superar 180 caracteres.";
        if (Limpiar(request.Direccion)?.Length > 250) return "La dirección no puede superar 250 caracteres.";
        if (Limpiar(request.Telefono)?.Length > 50) return "El teléfono no puede superar 50 caracteres.";
        if (Limpiar(request.Email)?.Length > 160) return "El correo no puede superar 160 caracteres.";
        if (Limpiar(request.NumeroAutorizacion)?.Length > 120) return "El número de autorización no puede superar 120 caracteres.";

        var codigoDuplicado = await context.Empresas
            .AsNoTracking()
            .AnyAsync(x =>
                x.Codigo.ToUpper() == codigo &&
                (!empresaId.HasValue || x.EmpresaId != empresaId.Value),
                cancellationToken);
        if (codigoDuplicado)
            return "Ya existe una empresa con ese código.";

        var ruc = Limpiar(request.Ruc);
        if (ruc is not null)
        {
            var rucDuplicado = await context.Empresas
                .AsNoTracking()
                .AnyAsync(x =>
                    x.Ruc == ruc &&
                    (!empresaId.HasValue || x.EmpresaId != empresaId.Value),
                    cancellationToken);
            if (rucDuplicado)
                return "Ya existe una empresa con ese RUC.";
        }

        return null;
    }

    private async Task<AdministracionEmpresaDto> CrearDtoAsync(
        Empresa empresa,
        CancellationToken cancellationToken)
    {
        var controlesAbiertos = await ContarControlesAbiertosAsync(empresa.EmpresaId, cancellationToken);

        var retiros = await context.Retiros
            .AsNoTracking()
            .CountAsync(x => x.EmpresaGestoraId == empresa.EmpresaId && !x.Eliminado, cancellationToken);

        var disposiciones = await context.DisposicionesFinales
            .AsNoTracking()
            .CountAsync(x => x.EmpresaGestoraId == empresa.EmpresaId && !x.Eliminado, cancellationToken);

        return new AdministracionEmpresaDto(
            empresa.EmpresaId,
            empresa.Codigo,
            empresa.Ruc,
            empresa.RazonSocial,
            empresa.NombreComercial,
            empresa.Direccion,
            empresa.Telefono,
            empresa.Email,
            empresa.EsGestoraResiduos,
            empresa.NumeroAutorizacion,
            empresa.EsActivo,
            await TieneUsoHistoricoAsync(empresa.EmpresaId, cancellationToken),
            controlesAbiertos,
            retiros,
            disposiciones);
    }

    private Task<int> ContarControlesAbiertosAsync(Guid empresaId, CancellationToken cancellationToken) =>
        context.ControlesGeneracion
            .AsNoTracking()
            .CountAsync(x =>
                x.EmpresaResponsableId == empresaId &&
                !x.Eliminado &&
                x.EstadoControlGeneracion.Codigo != "FINALIZADO" &&
                x.EstadoControlGeneracion.Codigo != "ANULADO",
                cancellationToken);

    private async Task<bool> TieneUsoHistoricoAsync(Guid empresaId, CancellationToken cancellationToken)
    {
        if (await context.ControlesGeneracion.AsNoTracking()
            .AnyAsync(x => x.EmpresaResponsableId == empresaId, cancellationToken))
            return true;

        if (await context.Registros.AsNoTracking()
            .AnyAsync(x => x.EmpresaResponsableId == empresaId && !x.Eliminado, cancellationToken))
            return true;

        if (await context.Retiros.AsNoTracking()
            .AnyAsync(x => x.EmpresaGestoraId == empresaId && !x.Eliminado, cancellationToken))
            return true;

        return await context.DisposicionesFinales.AsNoTracking()
            .AnyAsync(x => x.EmpresaGestoraId == empresaId && !x.Eliminado, cancellationToken);
    }

    private void Auditar(Guid actorUsuarioId, Guid empresaId, string accion, object? antes, object? despues)
    {
        context.Auditoria.Add(new Auditoria
        {
            UsuarioId = actorUsuarioId,
            Entidad = "Empresa",
            EntidadId = empresaId,
            Accion = accion,
            DatosAntesJson = antes is null ? null : JsonSerializer.Serialize(antes),
            DatosDespuesJson = despues is null ? null : JsonSerializer.Serialize(despues)
        });
    }

    private static object DatosAuditoria(Empresa empresa) => new
    {
        empresa.Codigo,
        empresa.Ruc,
        empresa.RazonSocial,
        empresa.NombreComercial,
        empresa.Direccion,
        empresa.Telefono,
        empresa.Email,
        empresa.EsGestoraResiduos,
        empresa.NumeroAutorizacion,
        empresa.EsActivo
    };

    private static string NormalizarCodigo(string codigo) =>
        codigo.Trim().ToUpperInvariant().Replace(' ', '_');

    private static string? Limpiar(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
