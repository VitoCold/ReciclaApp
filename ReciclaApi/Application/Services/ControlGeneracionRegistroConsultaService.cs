using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Repositories;

namespace ReciclaApi.Application.Services;

public interface IControlGeneracionRegistroConsultaService
{
    Task<ServiceResult<RegistroControlDetalleDto>> ObtenerAsync(
        Guid controlId,
        Guid registroId,
        Guid usuarioId,
        CancellationToken cancellationToken = default);
}

public sealed class ControlGeneracionRegistroConsultaService(IUnitOfWork unitOfWork)
    : IControlGeneracionRegistroConsultaService
{
    public async Task<ServiceResult<RegistroControlDetalleDto>> ObtenerAsync(
        Guid controlId,
        Guid registroId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (!await PuedeVerControlAsync(controlId, usuarioId, cancellationToken))
            return ServiceResult<RegistroControlDetalleDto>.Fail("Control de generación no encontrado o sin acceso.", StatusCodes.Status404NotFound);

        var registro = await unitOfWork.Repository<Registro>().Query()
            .Where(x => x.ControlGeneracionId == controlId && x.RegistroId == registroId && !x.Eliminado)
            .Include(x => x.RegistradoPorUsuario)
            .Include(x => x.EstadoRegistro)
            .Include(x => x.Residuos.Where(r => !r.Eliminado))
                .ThenInclude(x => x.Fotos.Where(f => !f.Eliminado))
            .FirstOrDefaultAsync(cancellationToken);

        if (registro is null)
            return ServiceResult<RegistroControlDetalleDto>.Fail("Registro no encontrado.", StatusCodes.Status404NotFound);

        return ServiceResult<RegistroControlDetalleDto>.Ok(new RegistroControlDetalleDto(
            registro.RegistroId,
            controlId,
            registro.CodigoLocal,
            registro.FechaRegistro,
            registro.RegistradoPorUsuarioId,
            NombreUsuario(registro.RegistradoPorUsuario),
            registro.EstadoRegistro.Nombre,
            registro.Observacion,
            registro.Residuos.Select(MapResiduo).ToArray()));
    }

    private async Task<bool> PuedeVerControlAsync(
        Guid controlId,
        Guid usuarioId,
        CancellationToken cancellationToken)
    {
        var global = await unitOfWork.Repository<UsuarioRol>().Query()
            .AnyAsync(x =>
                x.UsuarioId == usuarioId &&
                x.Rol.EsActivo &&
                (x.Rol.Codigo == "AMBIENTAL" || x.Rol.Codigo == "ADMINISTRADOR"),
                cancellationToken);

        if (global)
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

    private static RegistroResiduoDto MapResiduo(RegistroResiduo x) => new(
        x.RegistroResiduoId,
        x.TipoResiduoId,
        x.ResiduoId,
        x.UnidadMedidaId,
        x.Cantidad,
        x.Observacion,
        x.Fotos.Select(f => new RegistroResiduoFotoDto(
            f.FotoId,
            f.NombreArchivo,
            f.UrlNube,
            f.ContentType,
            f.TamanoBytes,
            f.TomadaUtc)).ToArray());

    private static string NombreUsuario(Usuario usuario) =>
        string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
