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

    Task<ServiceResult<RegistroControlDetalleDto>> CompletarAsync(
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

        var registro = await QueryRegistro(controlId, registroId, tracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        return registro is null
            ? ServiceResult<RegistroControlDetalleDto>.Fail("Registro no encontrado.", StatusCodes.Status404NotFound)
            : ServiceResult<RegistroControlDetalleDto>.Ok(MapDetalle(registro, controlId));
    }

    public async Task<ServiceResult<RegistroControlDetalleDto>> CompletarAsync(
        Guid controlId,
        Guid registroId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (!await PuedeVerControlAsync(controlId, usuarioId, cancellationToken))
            return ServiceResult<RegistroControlDetalleDto>.Fail("Control de generación no encontrado o sin acceso.", StatusCodes.Status404NotFound);

        var registro = await QueryRegistro(controlId, registroId, tracking: true)
            .FirstOrDefaultAsync(cancellationToken);
        if (registro is null)
            return ServiceResult<RegistroControlDetalleDto>.Fail("Registro no encontrado.", StatusCodes.Status404NotFound);

        if (registro.RegistradoPorUsuarioId != usuarioId)
            return ServiceResult<RegistroControlDetalleDto>.Fail("Solo quien creó el registro puede finalizarlo.", StatusCodes.Status403Forbidden);

        if (registro.ControlGeneracion?.EstadoControlGeneracion.Codigo != "ACTIVO")
            return ServiceResult<RegistroControlDetalleDto>.Fail("El control debe estar activo para finalizar registros.", StatusCodes.Status409Conflict);

        if (registro.EstadoRegistro.Codigo is not ("BORRADOR" or "EN_PROCESO"))
            return ServiceResult<RegistroControlDetalleDto>.Fail("El registro ya no se encuentra en proceso.", StatusCodes.Status409Conflict);

        if (!registro.Residuos.Any(x => !x.Eliminado))
            return ServiceResult<RegistroControlDetalleDto>.Fail("Debe registrar al menos un residuo antes de finalizar.", StatusCodes.Status400BadRequest);

        var registrado = await unitOfWork.Repository<EstadoRegistro>().Query(tracking: true)
            .FirstAsync(x => x.Codigo == "REGISTRADO", cancellationToken);

        registro.EstadoRegistroId = registrado.EstadoRegistroId;
        registro.EstadoRegistro = registrado;
        registro.ActualizadoUtc = DateTime.UtcNow;

        await unitOfWork.Repository<Auditoria>().AddAsync(new Auditoria
        {
            UsuarioId = usuarioId,
            Entidad = "Registro",
            EntidadId = registroId,
            Accion = "FINALIZAR_REGISTRO_CONTROL",
            DatosAntesJson = "{\"Estado\":\"EN_PROCESO\"}",
            DatosDespuesJson = "{\"Estado\":\"REGISTRADO\"}"
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ServiceResult<RegistroControlDetalleDto>.Ok(MapDetalle(registro, controlId));
    }

    private IQueryable<Registro> QueryRegistro(Guid controlId, Guid registroId, bool tracking)
    {
        return unitOfWork.Repository<Registro>().Query(tracking)
            .Where(x => x.ControlGeneracionId == controlId && x.RegistroId == registroId && !x.Eliminado)
            .Include(x => x.ControlGeneracion!)
                .ThenInclude(x => x.EstadoControlGeneracion)
            .Include(x => x.RegistradoPorUsuario)
            .Include(x => x.EstadoRegistro)
            .Include(x => x.Residuos.Where(r => !r.Eliminado))
                .ThenInclude(x => x.Fotos.Where(f => !f.Eliminado));
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

    private static RegistroControlDetalleDto MapDetalle(Registro registro, Guid controlId) => new(
        registro.RegistroId,
        controlId,
        registro.CodigoLocal,
        registro.FechaRegistro,
        registro.RegistradoPorUsuarioId,
        NombreUsuario(registro.RegistradoPorUsuario),
        registro.EstadoRegistro.Nombre,
        registro.Observacion,
        registro.Residuos.Where(x => !x.Eliminado).Select(MapResiduo).ToArray());

    private static RegistroResiduoDto MapResiduo(RegistroResiduo x) => new(
        x.RegistroResiduoId,
        x.TipoResiduoId,
        x.ResiduoId,
        x.UnidadMedidaId,
        x.Cantidad,
        x.Observacion,
        x.Fotos.Where(f => !f.Eliminado).Select(f => new RegistroResiduoFotoDto(
            f.FotoId,
            f.NombreArchivo,
            f.UrlNube,
            f.ContentType,
            f.TamanoBytes,
            f.TomadaUtc)).ToArray());

    private static string NombreUsuario(Usuario usuario) =>
        string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
