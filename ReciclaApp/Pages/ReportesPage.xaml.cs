using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class ReportesPage : ContentPage
{
    private CatalogosInicialDto? _catalogos;
    private IReadOnlyCollection<ControlGeneracionListItemDto> _controles = Array.Empty<ControlGeneracionListItemDto>();
    private bool _isLoading;
    private bool _inicializado;
    private ReporteResiduosDto? _ultimoReporte;

    public ReportesPage()
    {
        InitializeComponent();
        var hoy = DateTime.Today;
        DesdePicker.Date = new DateTime(hoy.Year, hoy.Month, 1);
        HastaPicker.Date = new DateTime(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
        {
            await AppNavigator.IrAlLoginAsync();
            return;
        }

        var usuario = session.CurrentUser;
        var autorizado = usuario is not null &&
                         (usuario.Roles.Contains("ADMINISTRADOR") ||
                          usuario.Roles.Contains("AMBIENTAL") ||
                          usuario.Roles.Contains("RESPONSABLE_OPERATIVO"));
        if (!autorizado)
        {
            await AppNavigator.IrAControlesGeneracionAsync();
            return;
        }

        if (!_inicializado)
        {
            await CargarFiltrosAsync();
            if (_inicializado)
                await ConsultarAsync();
        }
    }

    private async Task CargarFiltrosAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        SetBusy(true);
        ErrorBorder.IsVisible = false;

        try
        {
            var api = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var catalogosTask = api.ObtenerCatalogosAsync();
            var controlesTask = api.ListarControlesGeneracionAsync();
            await Task.WhenAll(catalogosTask, controlesTask);

            _catalogos = await catalogosTask;
            _controles = await controlesTask;

            SedePicker.ItemsSource = new[] { new OpcionGuid(null, "Todas las sedes") }
                .Concat(_catalogos.Sedes.OrderBy(x => x.Nombre).Select(x => new OpcionGuid(x.SedeId, x.Nombre)))
                .ToArray();

            EmpresaPicker.ItemsSource = new[] { new OpcionGuid(null, "Todas las empresas") }
                .Concat(_catalogos.Empresas
                    .Where(x => !x.EsGestoraResiduos)
                    .OrderBy(x => x.NombreComercial ?? x.RazonSocial)
                    .Select(x => new OpcionGuid(x.EmpresaId, x.NombreComercial ?? x.RazonSocial)))
                .ToArray();

            ClasificacionPicker.ItemsSource = new[] { new OpcionInt(null, "Todas") }
                .Concat(_catalogos.Clasificaciones
                    .OrderBy(x => x.Nombre)
                    .Select(x => new OpcionInt(x.ClasificacionResiduoId, x.Nombre)))
                .ToArray();

            SedePicker.SelectedIndex = 0;
            EmpresaPicker.SelectedIndex = 0;
            ClasificacionPicker.SelectedIndex = 0;
            ActualizarControles();
            ActualizarResiduos();
            _inicializado = true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<ReportesPage>>()?
                .LogError(ex, "Error cargando filtros de reportes.");
            MostrarError("No se pudieron cargar los filtros de reportería.");
        }
        finally
        {
            _isLoading = false;
            SetBusy(false);
        }
    }

    private void OnAlcanceChanged(object sender, EventArgs e)
    {
        if (_catalogos is not null)
            ActualizarControles();
    }

    private void OnClasificacionChanged(object sender, EventArgs e)
    {
        if (_catalogos is not null)
            ActualizarResiduos();
    }

    private void ActualizarControles()
    {
        var sedeId = (SedePicker.SelectedItem as OpcionGuid)?.Id;
        var empresaId = (EmpresaPicker.SelectedItem as OpcionGuid)?.Id;

        var controles = _controles.AsEnumerable();
        if (sedeId.HasValue)
            controles = controles.Where(x => x.SedeId == sedeId.Value);
        if (empresaId.HasValue)
            controles = controles.Where(x => x.EmpresaResponsableId == empresaId.Value);

        ControlPicker.ItemsSource = new[] { new OpcionGuid(null, "Todos los controles") }
            .Concat(controles
                .OrderByDescending(x => x.FechaInicio)
                .Select(x => new OpcionGuid(x.ControlGeneracionId, $"{x.Codigo} · {x.Sede}")))
            .ToArray();
        ControlPicker.SelectedIndex = 0;
    }

    private void ActualizarResiduos()
    {
        if (_catalogos is null)
            return;

        var clasificacionId = (ClasificacionPicker.SelectedItem as OpcionInt)?.Id;
        var residuos = _catalogos.Residuos.AsEnumerable();
        if (clasificacionId.HasValue)
            residuos = residuos.Where(x => x.ClasificacionResiduoId == clasificacionId.Value);

        ResiduoPicker.ItemsSource = new[] { new OpcionGuid(null, "Todos") }
            .Concat(residuos.OrderBy(x => x.Nombre).Select(x => new OpcionGuid(x.ResiduoId, x.Nombre)))
            .ToArray();
        ResiduoPicker.SelectedIndex = 0;
    }

    private async void OnConsultarClicked(object sender, EventArgs e) => await ConsultarAsync();

    private async Task ConsultarAsync()
    {
        if (_isLoading)
            return;

        if (HastaPicker.Date < DesdePicker.Date)
        {
            MostrarError("La fecha final no puede ser anterior a la fecha inicial.");
            return;
        }

        _isLoading = true;
        SetBusy(true);
        ErrorBorder.IsVisible = false;

        try
        {
            var reportesApi = AppServices.Services.GetRequiredService<IReporteApiClient>();
            _ultimoReporte = await reportesApi.ObtenerAsync(CrearFiltro());

            var resumen = _ultimoReporte.Resumen.Select(x => new ResumenVisual(
                x.Unidad,
                $"{x.Generado:0.###} {x.Unidad}",
                $"{x.Almacenado:0.###} {x.Unidad}",
                $"{x.Retirado:0.###} {x.Unidad}",
                $"{x.Saldo:0.###} {x.Unidad}",
                $"Pendiente de almacenar: {x.PendienteAlmacenamiento:0.###} {x.Unidad}"))
                .ToArray();

            var detalle = _ultimoReporte.Detalle.Select(MapDetalle).ToArray();
            BindingContext = new ReporteBinding(resumen, detalle);

            PeriodoLabel.Text = $"{_ultimoReporte.Desde:dd/MM/yyyy} al {_ultimoReporte.Hasta:dd/MM/yyyy}";
            CantidadLabel.Text = detalle.Length == 1 ? "1 residuo registrado" : $"{detalle.Length} residuos registrados";
            SinDatosLabel.IsVisible = detalle.Length == 0;
            ExportarButton.IsEnabled = detalle.Length > 0;
            ResultadosLayout.IsVisible = true;
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<ReportesPage>>()?
                .LogError(ex, "Error consultando reporte de residuos.");
            MostrarError("No se pudo generar el reporte con los filtros seleccionados.");
        }
        finally
        {
            _isLoading = false;
            SetBusy(false);
        }
    }

    private async void OnExportarClicked(object sender, EventArgs e)
    {
        if (_isLoading || _ultimoReporte is null || _ultimoReporte.Detalle.Count == 0)
            return;

        _isLoading = true;
        SetBusy(true);
        ErrorBorder.IsVisible = false;

        try
        {
            var reportesApi = AppServices.Services.GetRequiredService<IReporteApiClient>();
            var archivo = await reportesApi.ExportarExcelAsync(CrearFiltro());
            var nombreSeguro = Path.GetFileName(archivo.NombreArchivo);
            var ruta = Path.Combine(FileSystem.CacheDirectory, nombreSeguro);
            await File.WriteAllBytesAsync(ruta, archivo.Contenido);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Exportar reporte de residuos",
                File = new ShareFile(ruta, archivo.ContentType)
            });
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<ReportesPage>>()?
                .LogError(ex, "Error exportando reporte a Excel.");
            MostrarError("No se pudo generar o compartir el archivo Excel.");
        }
        finally
        {
            _isLoading = false;
            SetBusy(false);
        }
    }

    private async void OnLimpiarClicked(object sender, EventArgs e)
    {
        var hoy = DateTime.Today;
        DesdePicker.Date = new DateTime(hoy.Year, hoy.Month, 1);
        HastaPicker.Date = new DateTime(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month));
        SedePicker.SelectedIndex = 0;
        EmpresaPicker.SelectedIndex = 0;
        ClasificacionPicker.SelectedIndex = 0;
        ActualizarControles();
        ActualizarResiduos();
        await ConsultarAsync();
    }

    private ReporteResiduosFiltroRequest CrearFiltro() => new(
        DesdePicker.Date,
        HastaPicker.Date,
        (SedePicker.SelectedItem as OpcionGuid)?.Id,
        (EmpresaPicker.SelectedItem as OpcionGuid)?.Id,
        (ControlPicker.SelectedItem as OpcionGuid)?.Id,
        (ClasificacionPicker.SelectedItem as OpcionInt)?.Id,
        (ResiduoPicker.SelectedItem as OpcionGuid)?.Id);

    private static DetalleVisual MapDetalle(ReporteResiduoItemDto x)
    {
        var evidencia = x.Latitud.HasValue && x.Longitud.HasValue
            ? $"📍 {x.Latitud:0.000000}, {x.Longitud:0.000000}" +
              (x.PrecisionMetros.HasValue ? $" · ±{x.PrecisionMetros.Value:0.#} m" : string.Empty)
            : "📍 Sin coordenadas";
        evidencia += x.CantidadFotos == 1 ? " · 📷 1 foto" : $" · 📷 {x.CantidadFotos} fotos";

        return new DetalleVisual(
            x.Residuo,
            x.Clasificacion,
            $"{x.Generado:0.###} {x.Unidad}",
            $"{x.Sede} · {x.ControlCodigo}",
            $"{x.EmpresaResponsable} · {x.Proyecto} / {x.Actividad}",
            $"{x.FechaRegistro:dd/MM/yyyy HH:mm} · {x.RegistradoPor}",
            $"Almac. {x.Almacenado:0.###} {x.Unidad}",
            $"Ret. {x.Retirado:0.###} {x.Unidad}",
            $"Saldo {x.Saldo:0.###} {x.Unidad}",
            evidencia,
            string.IsNullOrWhiteSpace(x.Observacion) ? string.Empty : $"Obs.: {x.Observacion}",
            !string.IsNullOrWhiteSpace(x.Observacion));
    }

    private void SetBusy(bool busy)
    {
        ActivityIndicator.IsVisible = busy;
        ActivityIndicator.IsRunning = busy;
        ExportarButton.IsEnabled = !busy && _ultimoReporte?.Detalle.Count > 0;
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private void OnMenuTapped(object sender, TappedEventArgs e) => Shell.Current.FlyoutIsPresented = true;

    private sealed record OpcionGuid(Guid? Id, string Nombre);
    private sealed record OpcionInt(int? Id, string Nombre);

    private sealed record ResumenVisual(
        string Unidad,
        string Generado,
        string Almacenado,
        string Retirado,
        string Saldo,
        string Pendiente);

    private sealed record DetalleVisual(
        string Residuo,
        string Clasificacion,
        string Generado,
        string Contexto,
        string EmpresaProyecto,
        string RegistroTexto,
        string Almacenado,
        string Retirado,
        string Saldo,
        string Evidencia,
        string Observacion,
        bool TieneObservacion);

    private sealed record ReporteBinding(
        IReadOnlyCollection<ResumenVisual> Resumen,
        IReadOnlyCollection<DetalleVisual> Detalle);
}
