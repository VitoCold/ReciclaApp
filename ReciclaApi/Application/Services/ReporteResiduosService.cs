using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Recicla.Shared.Contracts;
using ReciclaApi.Application.Common;
using ReciclaApi.Domain;
using ReciclaApi.Infrastructure.Persistence;

namespace ReciclaApi.Application.Services;

public interface IReporteResiduosService
{
    Task<ServiceResult<ReporteResiduosDto>> GenerarAsync(
        Guid usuarioId,
        ReporteResiduosFiltroRequest filtro,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<byte[]>> ExportarExcelAsync(
        Guid usuarioId,
        ReporteResiduosFiltroRequest filtro,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyCollection<ReporteArchivoHistoricoDto>>> ListarArchivosAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ReporteArchivoHistoricoContenido>> ObtenerArchivoAsync(
        Guid archivoId,
        Guid usuarioId,
        CancellationToken cancellationToken = default);
}

public sealed record ReporteArchivoHistoricoContenido(
    byte[] Contenido,
    string NombreArchivo,
    string ContentType);

public sealed class ReporteResiduosService(
    ReciclaDbContext context,
    IWebHostEnvironment environment,
    ILogger<ReporteResiduosService> logger) : IReporteResiduosService
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private string ArchivoRaiz => Path.Combine(environment.ContentRootPath, "App_Data", "reportes-residuos");

    public async Task<ServiceResult<ReporteResiduosDto>> GenerarAsync(
        Guid usuarioId,
        ReporteResiduosFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        var validacion = ValidarFiltro(filtro);
        if (validacion is not null)
            return ServiceResult<ReporteResiduosDto>.Fail(validacion, StatusCodes.Status400BadRequest);

        if (!await PuedeConsultarReportesAsync(usuarioId, cancellationToken))
            return ServiceResult<ReporteResiduosDto>.Fail("No tienes permiso para consultar reportes.", StatusCodes.Status403Forbidden);

        var reporte = await ConstruirAsync(usuarioId, filtro, cancellationToken);
        return ServiceResult<ReporteResiduosDto>.Ok(reporte);
    }

    public async Task<ServiceResult<byte[]>> ExportarExcelAsync(
        Guid usuarioId,
        ReporteResiduosFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        var validacion = ValidarFiltro(filtro);
        if (validacion is not null)
            return ServiceResult<byte[]>.Fail(validacion, StatusCodes.Status400BadRequest);

        if (!await PuedeConsultarReportesAsync(usuarioId, cancellationToken))
            return ServiceResult<byte[]>.Fail("No tienes permiso para exportar reportes.", StatusCodes.Status403Forbidden);

        var reporte = await ConstruirAsync(usuarioId, filtro, cancellationToken);
        var excel = CrearExcel(reporte);

        try
        {
            await ArchivarExcelAsync(usuarioId, filtro, reporte, excel, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "No se pudo archivar la exportación Excel de residuos para {UsuarioId}.", usuarioId);
            return ServiceResult<byte[]>.Fail(
                "El reporte fue generado, pero no pudo archivarse en el backend.",
                StatusCodes.Status500InternalServerError);
        }

        return ServiceResult<byte[]>.Ok(excel);
    }

    public async Task<ServiceResult<IReadOnlyCollection<ReporteArchivoHistoricoDto>>> ListarArchivosAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (!await PuedeConsultarReportesAsync(usuarioId, cancellationToken))
        {
            return ServiceResult<IReadOnlyCollection<ReporteArchivoHistoricoDto>>.Fail(
                "No tienes permiso para consultar el historial de reportes.",
                StatusCodes.Status403Forbidden);
        }

        var archivos = await LeerMetadatosAsync(cancellationToken);
        var esGlobal = await EsGlobalAsync(usuarioId, cancellationToken);
        var visibles = archivos
            .Where(x => esGlobal || x.GeneradoPorUsuarioId == usuarioId)
            .OrderByDescending(x => x.GeneradoUtc)
            .ToArray();

        return ServiceResult<IReadOnlyCollection<ReporteArchivoHistoricoDto>>.Ok(visibles);
    }

    public async Task<ServiceResult<ReporteArchivoHistoricoContenido>> ObtenerArchivoAsync(
        Guid archivoId,
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (!await PuedeConsultarReportesAsync(usuarioId, cancellationToken))
        {
            return ServiceResult<ReporteArchivoHistoricoContenido>.Fail(
                "No tienes permiso para consultar el historial de reportes.",
                StatusCodes.Status403Forbidden);
        }

        var encontrado = await BuscarMetadataAsync(archivoId, cancellationToken);
        if (encontrado is null)
        {
            return ServiceResult<ReporteArchivoHistoricoContenido>.Fail(
                "Archivo de reporte no encontrado.",
                StatusCodes.Status404NotFound);
        }

        var (metadata, metadataPath) = encontrado.Value;
        var esGlobal = await EsGlobalAsync(usuarioId, cancellationToken);
        if (!esGlobal && metadata.GeneradoPorUsuarioId != usuarioId)
        {
            return ServiceResult<ReporteArchivoHistoricoContenido>.Fail(
                "Archivo de reporte no encontrado o sin acceso.",
                StatusCodes.Status404NotFound);
        }

        var directory = Path.GetDirectoryName(metadataPath)!;
        var excelPath = Path.Combine(directory, metadata.NombreArchivo);
        if (!File.Exists(excelPath))
        {
            return ServiceResult<ReporteArchivoHistoricoContenido>.Fail(
                "El metadato existe, pero el archivo Excel ya no está disponible.",
                StatusCodes.Status404NotFound);
        }

        var contenido = await File.ReadAllBytesAsync(excelPath, cancellationToken);
        return ServiceResult<ReporteArchivoHistoricoContenido>.Ok(
            new ReporteArchivoHistoricoContenido(contenido, metadata.NombreArchivo, ExcelContentType));
    }

    private async Task<ReporteResiduosDto> ConstruirAsync(
        Guid usuarioId,
        ReporteResiduosFiltroRequest filtro,
        CancellationToken cancellationToken)
    {
        var desde = filtro.Desde.Date;
        var hasta = filtro.Hasta.Date;
        var hastaExclusiva = hasta.AddDays(1);
        var esGlobal = await EsGlobalAsync(usuarioId, cancellationToken);

        IQueryable<RegistroResiduo> query = context.RegistroResiduos
            .AsNoTracking()
            .Where(x =>
                !x.Eliminado &&
                !x.Registro.Eliminado &&
                x.Registro.ControlGeneracionId != null &&
                x.Registro.EstadoRegistro.Codigo == "REGISTRADO" &&
                x.Registro.FechaRegistro >= desde &&
                x.Registro.FechaRegistro < hastaExclusiva)
            .Include(x => x.Registro).ThenInclude(x => x.Sede)
            .Include(x => x.Registro).ThenInclude(x => x.ControlGeneracion)
            .Include(x => x.Registro).ThenInclude(x => x.EmpresaResponsable)
            .Include(x => x.Registro).ThenInclude(x => x.Proyecto)
            .Include(x => x.Registro).ThenInclude(x => x.Actividad)
            .Include(x => x.Registro).ThenInclude(x => x.RegistradoPorUsuario)
            .Include(x => x.Residuo).ThenInclude(x => x.ClasificacionResiduo)
            .Include(x => x.UnidadMedida)
            .Include(x => x.Fotos.Where(f => !f.Eliminado));

        if (!esGlobal)
        {
            query = query.Where(x => x.Registro.ControlGeneracion!.Usuarios.Any(u =>
                u.UsuarioId == usuarioId &&
                u.RolControl == "RESPONSABLE" &&
                u.EsActivo));
        }

        if (filtro.SedeId.HasValue)
            query = query.Where(x => x.Registro.SedeId == filtro.SedeId.Value);

        if (filtro.EmpresaResponsableId.HasValue)
            query = query.Where(x => x.Registro.EmpresaResponsableId == filtro.EmpresaResponsableId.Value);

        if (filtro.ControlGeneracionId.HasValue)
            query = query.Where(x => x.Registro.ControlGeneracionId == filtro.ControlGeneracionId.Value);

        if (filtro.ClasificacionResiduoId.HasValue)
            query = query.Where(x => x.Residuo.ClasificacionResiduoId == filtro.ClasificacionResiduoId.Value);

        if (filtro.ResiduoId.HasValue)
            query = query.Where(x => x.ResiduoId == filtro.ResiduoId.Value);

        var residuos = await query
            .OrderBy(x => x.Registro.FechaRegistro)
            .ThenBy(x => x.Registro.Sede.Nombre)
            .ThenBy(x => x.Residuo.Nombre)
            .ToListAsync(cancellationToken);

        if (residuos.Count == 0)
            return new ReporteResiduosDto(desde, hasta, Array.Empty<ReporteResumenUnidadDto>(), Array.Empty<ReporteResiduoItemDto>());

        var ids = residuos.Select(x => x.RegistroResiduoId).ToArray();

        var movimientos = await context.MovimientosResiduo
            .AsNoTracking()
            .Where(x => ids.Contains(x.RegistroResiduoId) && !x.Eliminado)
            .Select(x => new { x.RegistroResiduoId, x.Cantidad })
            .ToListAsync(cancellationToken);

        var retiros = await context.RetiroDetalles
            .AsNoTracking()
            .Where(x =>
                ids.Contains(x.RegistroResiduoId) &&
                !x.Retiro.Eliminado &&
                x.Retiro.EstadoRetiro.Codigo != "ANULADO")
            .Select(x => new { x.RegistroResiduoId, x.Cantidad })
            .ToListAsync(cancellationToken);

        var ubicaciones = await context.Set<RegistroResiduoUbicacion>()
            .AsNoTracking()
            .Where(x => ids.Contains(x.RegistroResiduoId))
            .ToDictionaryAsync(x => x.RegistroResiduoId, cancellationToken);

        var detalle = residuos.Select(x =>
        {
            var movido = movimientos.Where(m => m.RegistroResiduoId == x.RegistroResiduoId).Sum(m => m.Cantidad);
            var retirado = retiros.Where(r => r.RegistroResiduoId == x.RegistroResiduoId).Sum(r => r.Cantidad);
            var saldo = Math.Max(0m, x.Cantidad - retirado);
            var almacenado = Math.Max(0m, movido - retirado);
            var pendiente = Math.Max(0m, Math.Min(saldo, x.Cantidad - movido));
            ubicaciones.TryGetValue(x.RegistroResiduoId, out var ubicacion);

            var control = x.Registro.ControlGeneracion!;
            var usuario = x.Registro.RegistradoPorUsuario;
            var empresa = x.Registro.EmpresaResponsable;

            return new ReporteResiduoItemDto(
                x.RegistroResiduoId,
                x.RegistroId,
                x.Registro.FechaRegistro,
                x.Registro.SedeId,
                x.Registro.Sede.Nombre,
                control.ControlGeneracionId,
                control.Codigo,
                x.Registro.EmpresaResponsableId,
                empresa is null ? "Sin empresa" : NombreEmpresa(empresa),
                x.Registro.ProyectoId,
                x.Registro.Proyecto.Nombre,
                x.Registro.ActividadId,
                x.Registro.Actividad.Nombre,
                x.Residuo.ClasificacionResiduoId,
                x.Residuo.ClasificacionResiduo.Nombre,
                x.ResiduoId,
                x.Residuo.Nombre,
                x.UnidadMedida.Codigo,
                x.Cantidad,
                almacenado,
                retirado,
                saldo,
                pendiente,
                x.Registro.RegistradoPorUsuarioId,
                NombreUsuario(usuario),
                ubicacion?.Latitud,
                ubicacion?.Longitud,
                ubicacion?.PrecisionMetros,
                x.Fotos.Count,
                x.Observacion);
        }).ToArray();

        var resumen = detalle
            .GroupBy(x => x.Unidad)
            .Select(g => new ReporteResumenUnidadDto(
                g.Key,
                g.Sum(x => x.Generado),
                g.Sum(x => x.Almacenado),
                g.Sum(x => x.Retirado),
                g.Sum(x => x.Saldo),
                g.Sum(x => x.PendienteAlmacenamiento)))
            .OrderBy(x => x.Unidad)
            .ToArray();

        return new ReporteResiduosDto(desde, hasta, resumen, detalle);
    }

    private async Task ArchivarExcelAsync(
        Guid usuarioId,
        ReporteResiduosFiltroRequest filtro,
        ReporteResiduosDto reporte,
        byte[] contenido,
        CancellationToken cancellationToken)
    {
        var generadoUtc = DateTime.UtcNow;
        var archivoId = Guid.NewGuid();
        var directory = Path.Combine(
            ArchivoRaiz,
            generadoUtc.ToString("yyyy"),
            generadoUtc.ToString("MM"));
        Directory.CreateDirectory(directory);

        var nombreArchivo = $"reporte_residuos_{filtro.Desde:yyyyMMdd}_{filtro.Hasta:yyyyMMdd}_{generadoUtc:yyyyMMddHHmmss}_{archivoId.ToString("N")[..6]}.xlsx";
        var excelPath = Path.Combine(directory, nombreArchivo);
        var metadataPath = Path.Combine(directory, $"{archivoId:N}.json");

        await File.WriteAllBytesAsync(excelPath, contenido, cancellationToken);

        try
        {
            var generadoPor = await context.Usuarios
                .AsNoTracking()
                .Where(x => x.UsuarioId == usuarioId)
                .Select(x => (x.Nombres + " " + (x.Apellidos ?? string.Empty)).Trim())
                .FirstOrDefaultAsync(cancellationToken)
                ?? usuarioId.ToString();

            var metadata = new ReporteArchivoHistoricoDto(
                archivoId,
                nombreArchivo,
                generadoUtc,
                usuarioId,
                generadoPor,
                filtro with { Desde = filtro.Desde.Date, Hasta = filtro.Hasta.Date },
                reporte.Detalle.Count,
                contenido.LongLength,
                Convert.ToHexString(SHA256.HashData(contenido)));

            var json = JsonSerializer.Serialize(metadata, JsonOptions);
            await File.WriteAllTextAsync(metadataPath, json, cancellationToken);
        }
        catch
        {
            if (File.Exists(excelPath))
                File.Delete(excelPath);
            throw;
        }
    }

    private async Task<IReadOnlyCollection<ReporteArchivoHistoricoDto>> LeerMetadatosAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(ArchivoRaiz))
            return Array.Empty<ReporteArchivoHistoricoDto>();

        var result = new List<ReporteArchivoHistoricoDto>();
        foreach (var path in Directory.EnumerateFiles(ArchivoRaiz, "*.json", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                var metadata = JsonSerializer.Deserialize<ReporteArchivoHistoricoDto>(json, JsonOptions);
                if (metadata is not null)
                    result.Add(metadata);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                logger.LogWarning(ex, "No se pudo leer metadata de reporte archivado {Path}.", path);
            }
        }

        return result;
    }

    private async Task<(ReporteArchivoHistoricoDto Metadata, string MetadataPath)?> BuscarMetadataAsync(
        Guid archivoId,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(ArchivoRaiz))
            return null;

        var nombreMetadata = $"{archivoId:N}.json";
        var path = Directory
            .EnumerateFiles(ArchivoRaiz, nombreMetadata, SearchOption.AllDirectories)
            .FirstOrDefault();
        if (path is null)
            return null;

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var metadata = JsonSerializer.Deserialize<ReporteArchivoHistoricoDto>(json, JsonOptions);
        return metadata is null ? null : (metadata, path);
    }

    private static byte[] CrearExcel(ReporteResiduosDto reporte)
    {
        using var workbook = new XLWorkbook();
        var resumen = workbook.Worksheets.Add("Resumen");
        var detalle = workbook.Worksheets.Add("Detalle");

        resumen.Cell("A1").Value = "Reporte de gestión de residuos";
        resumen.Cell("A2").Value = $"Periodo: {reporte.Desde:dd/MM/yyyy} al {reporte.Hasta:dd/MM/yyyy}";
        resumen.Range("A1:F1").Merge();
        resumen.Range("A2:F2").Merge();
        resumen.Cell("A1").Style.Font.Bold = true;
        resumen.Cell("A1").Style.Font.FontSize = 16;

        var headersResumen = new[] { "Unidad", "Generado", "En almacenamiento", "Retirado", "Saldo", "Pendiente de almacenar" };
        for (var c = 0; c < headersResumen.Length; c++)
            resumen.Cell(4, c + 1).Value = headersResumen[c];

        var fila = 5;
        foreach (var item in reporte.Resumen)
        {
            resumen.Cell(fila, 1).Value = item.Unidad;
            resumen.Cell(fila, 2).Value = item.Generado;
            resumen.Cell(fila, 3).Value = item.Almacenado;
            resumen.Cell(fila, 4).Value = item.Retirado;
            resumen.Cell(fila, 5).Value = item.Saldo;
            resumen.Cell(fila, 6).Value = item.PendienteAlmacenamiento;
            fila++;
        }

        AplicarCabecera(resumen.Range(4, 1, 4, headersResumen.Length));
        resumen.SheetView.FreezeRows(4);
        resumen.Columns().AdjustToContents();

        var headersDetalle = new[]
        {
            "Fecha", "Sede", "Control", "Registro", "Empresa responsable", "Proyecto", "Actividad",
            "Clasificación", "Residuo", "Unidad", "Generado", "En almacenamiento", "Retirado", "Saldo",
            "Pendiente de almacenar", "Registrado por", "Latitud", "Longitud", "Precisión GPS (m)", "Fotos", "Observación"
        };

        for (var c = 0; c < headersDetalle.Length; c++)
            detalle.Cell(1, c + 1).Value = headersDetalle[c];

        fila = 2;
        foreach (var item in reporte.Detalle)
        {
            detalle.Cell(fila, 1).Value = item.FechaRegistro;
            detalle.Cell(fila, 1).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
            detalle.Cell(fila, 2).Value = item.Sede;
            detalle.Cell(fila, 3).Value = item.ControlCodigo;
            detalle.Cell(fila, 4).Value = item.RegistroId.ToString();
            detalle.Cell(fila, 5).Value = item.EmpresaResponsable;
            detalle.Cell(fila, 6).Value = item.Proyecto;
            detalle.Cell(fila, 7).Value = item.Actividad;
            detalle.Cell(fila, 8).Value = item.Clasificacion;
            detalle.Cell(fila, 9).Value = item.Residuo;
            detalle.Cell(fila, 10).Value = item.Unidad;
            detalle.Cell(fila, 11).Value = item.Generado;
            detalle.Cell(fila, 12).Value = item.Almacenado;
            detalle.Cell(fila, 13).Value = item.Retirado;
            detalle.Cell(fila, 14).Value = item.Saldo;
            detalle.Cell(fila, 15).Value = item.PendienteAlmacenamiento;
            detalle.Cell(fila, 16).Value = item.RegistradoPor;

            if (item.Latitud.HasValue)
                detalle.Cell(fila, 17).Value = item.Latitud.Value;
            if (item.Longitud.HasValue)
                detalle.Cell(fila, 18).Value = item.Longitud.Value;
            if (item.PrecisionMetros.HasValue)
                detalle.Cell(fila, 19).Value = item.PrecisionMetros.Value;

            detalle.Cell(fila, 20).Value = item.CantidadFotos;
            detalle.Cell(fila, 21).Value = item.Observacion ?? string.Empty;
            fila++;
        }

        AplicarCabecera(detalle.Range(1, 1, 1, headersDetalle.Length));
        detalle.SheetView.FreezeRows(1);
        detalle.RangeUsed()?.SetAutoFilter();
        detalle.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AplicarCabecera(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.White;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#0B5AA5");
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private async Task<bool> PuedeConsultarReportesAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        await context.UsuarioRoles
            .AsNoTracking()
            .AnyAsync(x =>
                x.UsuarioId == usuarioId &&
                x.Rol.EsActivo &&
                (x.Rol.Codigo == "ADMINISTRADOR" ||
                 x.Rol.Codigo == "AMBIENTAL" ||
                 x.Rol.Codigo == "RESPONSABLE_OPERATIVO"),
                cancellationToken);

    private async Task<bool> EsGlobalAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        await context.UsuarioRoles
            .AsNoTracking()
            .AnyAsync(x =>
                x.UsuarioId == usuarioId &&
                x.Rol.EsActivo &&
                (x.Rol.Codigo == "ADMINISTRADOR" || x.Rol.Codigo == "AMBIENTAL"),
                cancellationToken);

    private static string? ValidarFiltro(ReporteResiduosFiltroRequest filtro)
    {
        if (filtro.Hasta.Date < filtro.Desde.Date)
            return "La fecha final no puede ser anterior a la fecha inicial.";

        if ((filtro.Hasta.Date - filtro.Desde.Date).TotalDays > 366)
            return "El reporte permite consultar como máximo 366 días por vez.";

        return null;
    }

    private static string NombreUsuario(Usuario usuario) =>
        string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }.Where(x => !string.IsNullOrWhiteSpace(x)));

    private static string NombreEmpresa(Empresa empresa) =>
        string.IsNullOrWhiteSpace(empresa.NombreComercial) ? empresa.RazonSocial : empresa.NombreComercial;
}
