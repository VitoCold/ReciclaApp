using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(ControlId), "controlId")]
[QueryProperty(nameof(RegistroId), "registroId")]
[QueryProperty(nameof(RegistroResiduoId), "registroResiduoId")]
public partial class RegistrarResiduoPage : ContentPage
{
    private readonly List<Microsoft.Maui.Storage.FileResult> _fotosPendientes = new();
    private readonly ObservableCollection<FotoPreviewItem> _fotosPreview = new();
    private CatalogosInicialDto? _catalogos;
    private UnidadMedidaDto? _unidadSeleccionada;
    private bool _isLoading;
    private bool _edicionCargada;
    private double? _latitud;
    private double? _longitud;
    private double? _precisionMetros;
    private DateTime? _ubicacionCapturadaUtc;
    private int _fotosExistentes;
    private Task<bool>? _capturaUbicacionEnCurso;

    public string ControlId { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;
    public string RegistroResiduoId { get; set; } = string.Empty;

    private bool EsEdicion => Guid.TryParse(RegistroResiduoId, out _);
    private bool VieneDeControl => Guid.TryParse(ControlId, out _);
    private bool TieneUbicacion => _latitud.HasValue && _longitud.HasValue && _ubicacionCapturadaUtc.HasValue;
    private bool TieneFoto => _fotosExistentes + _fotosPendientes.Count > 0;

    public RegistrarResiduoPage()
    {
        InitializeComponent();
        FotosPreviewLayout.BindingContext = _fotosPreview;
        ActualizarEstadoEvidencia();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_catalogos is null)
            await CargarCatalogosAsync();

        if (EsEdicion && !_edicionCargada && _catalogos is not null)
            await CargarResiduoEdicionAsync();

        if (!TieneUbicacion && !_isLoading)
            await CapturarUbicacionAutomaticaAsync();
    }

    private async Task CargarCatalogosAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        SetBusy(true);
        ErrorBorder.IsVisible = false;

        try
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
            {
                await AppNavigator.IrAlLoginAsync();
                return;
            }

            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            _catalogos = await apiClient.ObtenerCatalogosAsync();

            TipoResiduoPicker.ItemsSource = _catalogos.TiposResiduo.ToList();
            ResiduoPicker.ItemsSource = Array.Empty<ResiduoCatalogoDto>();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RegistrarResiduoPage>>()?
                .LogError(ex, "Error cargando catálogos para registrar residuos.");
            MostrarError("No se pudieron cargar los tipos y residuos disponibles.");
        }
        finally
        {
            _isLoading = false;
            SetBusy(false);
        }
    }

    private async Task CargarResiduoEdicionAsync()
    {
        if (_catalogos is null || _isLoading ||
            !Guid.TryParse(RegistroId, out var registroId) ||
            !Guid.TryParse(RegistroResiduoId, out var registroResiduoId))
            return;

        _isLoading = true;
        SetBusy(true);
        ErrorBorder.IsVisible = false;

        try
        {
            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            RegistroResiduoDto? residuo;
            string estado;

            if (VieneDeControl && Guid.TryParse(ControlId, out var controlId))
            {
                var registroControl = await apiClient.ObtenerRegistroControlAsync(controlId, registroId);
                residuo = registroControl.Residuos.FirstOrDefault(x => x.RegistroResiduoId == registroResiduoId);
                estado = registroControl.Estado;
            }
            else
            {
                var registro = await apiClient.ObtenerRegistroAsync(registroId);
                residuo = registro.Residuos.FirstOrDefault(x => x.RegistroResiduoId == registroResiduoId);
                estado = registro.Estado;
            }

            if (residuo is null)
            {
                MostrarError("El residuo ya no existe en este registro.");
                return;
            }

            var estadoEditable = string.Equals(estado, "En proceso", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(estado, "Borrador", StringComparison.OrdinalIgnoreCase);
            if (!estadoEditable)
            {
                MostrarError("Este registro ya no se encuentra en proceso y no puede modificarse.");
                RegistrarButton.IsEnabled = false;
                return;
            }

            var tipo = _catalogos.TiposResiduo.FirstOrDefault(x => x.TipoResiduoId == residuo.TipoResiduoId);
            TipoResiduoPicker.SelectedItem = tipo;

            var residuosDisponibles = _catalogos.Residuos
                .Where(x => x.TipoResiduoId == residuo.TipoResiduoId)
                .OrderBy(x => x.Nombre)
                .ToList();
            ResiduoPicker.ItemsSource = residuosDisponibles;
            ResiduoPicker.SelectedItem = residuosDisponibles.FirstOrDefault(x => x.ResiduoId == residuo.ResiduoId);

            _unidadSeleccionada = _catalogos.UnidadesMedida
                .FirstOrDefault(x => x.UnidadMedidaId == residuo.UnidadMedidaId);
            UnidadLabel.Text = _unidadSeleccionada?.Codigo ?? "-";

            CantidadEntry.Text = residuo.Cantidad.ToString("0.###", CultureInfo.CurrentCulture);
            ObservacionEditor.Text = residuo.Observacion ?? string.Empty;

            var evidenciaApi = AppServices.Services.GetRequiredService<IRegistroResiduoEvidenciaApiClient>();
            var evidencias = await evidenciaApi.ListarAsync(registroId);
            var evidencia = evidencias.FirstOrDefault(x => x.RegistroResiduoId == registroResiduoId);

            _latitud = evidencia?.Latitud;
            _longitud = evidencia?.Longitud;
            _precisionMetros = evidencia?.PrecisionMetros;
            _ubicacionCapturadaUtc = evidencia?.UbicacionCapturadaUtc;
            _fotosExistentes = evidencia?.CantidadFotos ?? residuo.Fotos.Count;
            CargarPreviewsExistentes(residuo.Fotos);

            TituloPageLabel.Text = "Editar residuo";
            TituloFormularioLabel.Text = "Editar información";
            SubtituloFormularioLabel.Text = "Actualiza los datos y verifica la evidencia del residuo";
            RegistrarButton.Text = "Guardar cambios";

            _edicionCargada = true;
            ActualizarEstadoEvidencia();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RegistrarResiduoPage>>()?
                .LogError(ex, "Error cargando residuo {RegistroResiduoId} para edición.", RegistroResiduoId);
            MostrarError("No se pudo cargar el residuo para editarlo.");
        }
        finally
        {
            _isLoading = false;
            SetBusy(false);
        }
    }

    private void OnTipoResiduoChanged(object sender, EventArgs e)
    {
        _unidadSeleccionada = null;
        UnidadLabel.Text = "-";
        ResiduoPicker.SelectedItem = null;

        if (_catalogos is null || TipoResiduoPicker.SelectedItem is not TipoResiduoDto tipo)
        {
            ResiduoPicker.ItemsSource = Array.Empty<ResiduoCatalogoDto>();
            return;
        }

        ResiduoPicker.ItemsSource = _catalogos.Residuos
            .Where(x => x.TipoResiduoId == tipo.TipoResiduoId)
            .OrderBy(x => x.Nombre)
            .ToList();
    }

    private void OnResiduoChanged(object sender, EventArgs e)
    {
        _unidadSeleccionada = null;
        UnidadLabel.Text = "-";

        if (_catalogos is null || ResiduoPicker.SelectedItem is not ResiduoCatalogoDto residuo)
            return;

        _unidadSeleccionada = _catalogos.UnidadesMedida
            .FirstOrDefault(x => x.UnidadMedidaId == residuo.UnidadMedidaDefaultId);

        UnidadLabel.Text = _unidadSeleccionada?.Codigo ?? "-";
    }

    private Task<bool> CapturarUbicacionAutomaticaAsync()
    {
        if (TieneUbicacion)
            return Task.FromResult(true);

        if (_capturaUbicacionEnCurso is { IsCompleted: false })
            return _capturaUbicacionEnCurso;

        _capturaUbicacionEnCurso = CapturarUbicacionInternaAsync();
        return _capturaUbicacionEnCurso;
    }

    private async Task<bool> CapturarUbicacionInternaAsync()
    {
        try
        {
            UbicacionEstadoLabel.Text = "Obteniendo ubicación automáticamente...";

            var permiso = await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<Microsoft.Maui.ApplicationModel.Permissions.LocationWhenInUse>();
            if (permiso != Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
                permiso = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<Microsoft.Maui.ApplicationModel.Permissions.LocationWhenInUse>();

            if (permiso != Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
            {
                UbicacionEstadoLabel.Text = "Permiso de ubicación requerido";
                return false;
            }

            var request = new Microsoft.Maui.Devices.Sensors.GeolocationRequest(
                Microsoft.Maui.Devices.Sensors.GeolocationAccuracy.High,
                TimeSpan.FromSeconds(15));
            var location = await Microsoft.Maui.Devices.Sensors.Geolocation.Default.GetLocationAsync(request);

            if (location is null)
            {
                UbicacionEstadoLabel.Text = "No se pudo obtener la ubicación automáticamente";
                return false;
            }

            _latitud = location.Latitude;
            _longitud = location.Longitude;
            _precisionMetros = location.Accuracy;
            _ubicacionCapturadaUtc = DateTime.UtcNow;
            ActualizarEstadoEvidencia();
            return true;
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RegistrarResiduoPage>>()?
                .LogWarning(ex, "No se pudo capturar automáticamente la ubicación del residuo.");
            UbicacionEstadoLabel.Text = "No se pudo obtener la ubicación automáticamente";
            return false;
        }
    }

    private async void OnTomarFotoClicked(object sender, EventArgs e)
    {
        if (_isLoading)
            return;

        ErrorBorder.IsVisible = false;
        try
        {
            var permiso = await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<Microsoft.Maui.ApplicationModel.Permissions.Camera>();
            if (permiso != Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
                permiso = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<Microsoft.Maui.ApplicationModel.Permissions.Camera>();

            if (permiso != Microsoft.Maui.ApplicationModel.PermissionStatus.Granted)
            {
                MostrarError("Debes permitir el acceso a la cámara para registrar evidencia fotográfica.");
                return;
            }

            if (!Microsoft.Maui.Media.MediaPicker.Default.IsCaptureSupported)
            {
                MostrarError("La captura de fotos no está disponible en este dispositivo.");
                return;
            }

            var foto = await Microsoft.Maui.Media.MediaPicker.Default.CapturePhotoAsync();
            if (foto is null)
                return;

            var preview = await CrearPreviewPendienteAsync(foto);
            _fotosPendientes.Add(foto);
            _fotosPreview.Add(preview);
            ActualizarEstadoEvidencia();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RegistrarResiduoPage>>()?
                .LogWarning(ex, "No se pudo capturar la fotografía del residuo.");
            MostrarError("No se pudo tomar o previsualizar la foto. Verifica el permiso de cámara e inténtalo nuevamente.");
        }
    }

    private void OnEliminarFotoPendienteClicked(object sender, EventArgs e)
    {
        if (_isLoading || sender is not Button button || button.CommandParameter is not FotoPreviewItem preview || !preview.PuedeEliminar)
            return;

        if (preview.Archivo is not null)
            _fotosPendientes.Remove(preview.Archivo);

        _fotosPreview.Remove(preview);
        EliminarCachePreview(preview.RutaCache);
        ActualizarEstadoEvidencia();
    }

    private async void OnRegistrarClicked(object sender, EventArgs e)
    {
        ErrorBorder.IsVisible = false;

        if (!Guid.TryParse(RegistroId, out var registroId))
        {
            MostrarError("No se recibió un registro válido para asociar el residuo.");
            return;
        }

        if (_unidadSeleccionada is null)
        {
            MostrarError("No se pudo determinar la unidad de medida del residuo.");
            return;
        }

        if (!TryParseCantidad(CantidadEntry.Text, out var cantidad) || cantidad <= 0)
        {
            MostrarError("Ingresa una cantidad válida mayor a cero.");
            return;
        }

        if (!TieneUbicacion && !await CapturarUbicacionAutomaticaAsync())
        {
            MostrarError("No se pudo obtener la ubicación GPS automáticamente. Verifica el permiso de ubicación y que el GPS esté activo, luego vuelve a guardar.");
            return;
        }

        if (!TieneFoto)
        {
            MostrarError("Toma al menos una fotografía del residuo antes de guardarlo.");
            return;
        }

        if (_isLoading)
            return;

        _isLoading = true;
        SetBusy(true);

        try
        {
            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var evidenciaApi = AppServices.Services.GetRequiredService<IRegistroResiduoEvidenciaApiClient>();
            var observacion = string.IsNullOrWhiteSpace(ObservacionEditor.Text)
                ? null
                : ObservacionEditor.Text.Trim();

            Guid registroResiduoId;
            if (EsEdicion && Guid.TryParse(RegistroResiduoId, out var existenteId))
            {
                var request = new ActualizarRegistroResiduoRequest(
                    UnidadMedidaId: _unidadSeleccionada.UnidadMedidaId,
                    Cantidad: cantidad,
                    Observacion: observacion);

                await apiClient.ActualizarResiduoAsync(registroId, existenteId, request);
                registroResiduoId = existenteId;
            }
            else
            {
                if (TipoResiduoPicker.SelectedItem is not TipoResiduoDto tipo ||
                    ResiduoPicker.SelectedItem is not ResiduoCatalogoDto residuo)
                {
                    MostrarError("Selecciona el tipo de residuo y el residuo generado.");
                    return;
                }

                var request = new AgregarRegistroResiduoRequest(
                    TipoResiduoId: tipo.TipoResiduoId,
                    ResiduoId: residuo.ResiduoId,
                    UnidadMedidaId: _unidadSeleccionada.UnidadMedidaId,
                    Cantidad: cantidad,
                    Observacion: observacion);

                var creado = await apiClient.AgregarResiduoAsync(registroId, request);
                registroResiduoId = creado.RegistroResiduoId;
                RegistroResiduoId = registroResiduoId.ToString();
                _edicionCargada = true;
            }

            await evidenciaApi.GuardarUbicacionAsync(
                registroId,
                registroResiduoId,
                new GuardarUbicacionResiduoRequest(
                    _latitud!.Value,
                    _longitud!.Value,
                    _precisionMetros,
                    _ubicacionCapturadaUtc!.Value));

            foreach (var foto in _fotosPendientes.ToArray())
            {
                await using var stream = await foto.OpenReadAsync();
                var contentType = string.IsNullOrWhiteSpace(foto.ContentType) ? "image/jpeg" : foto.ContentType;
                await apiClient.SubirFotoAsync(
                    registroId,
                    registroResiduoId,
                    stream,
                    foto.FileName,
                    contentType);

                _fotosPendientes.Remove(foto);
                _fotosExistentes++;

                var preview = _fotosPreview.FirstOrDefault(x => ReferenceEquals(x.Archivo, foto));
                if (preview is not null)
                {
                    _fotosPreview.Remove(preview);
                    EliminarCachePreview(preview.RutaCache);
                }
            }

            ActualizarEstadoEvidencia();

            if (VieneDeControl)
                await AppNavigator.VolverAsync();
            else
                await AppNavigator.IrAResiduosDelRegistroAsync(registroId);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            MostrarError("Este registro ya no se encuentra en proceso y no puede modificarse.");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            MostrarError("La fotografía supera el tamaño máximo permitido de 15 MB.");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RegistrarResiduoPage>>()?
                .LogError(ex, "Error guardando residuo y evidencia en {RegistroId}.", registroId);
            MostrarError("No se pudo completar el guardado. La pantalla conservará la evidencia pendiente para que puedas reintentar.");
        }
        finally
        {
            _isLoading = false;
            SetBusy(false);
        }
    }

    private async Task<FotoPreviewItem> CrearPreviewPendienteAsync(Microsoft.Maui.Storage.FileResult foto)
    {
        var extension = Path.GetExtension(foto.FileName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".jpg";

        var rutaCache = Path.Combine(
            Microsoft.Maui.Storage.FileSystem.CacheDirectory,
            $"residuo_preview_{Guid.NewGuid():N}{extension}");

        await using (var origen = await foto.OpenReadAsync())
        await using (var destino = File.Create(rutaCache))
            await origen.CopyToAsync(destino);

        return new FotoPreviewItem(
            Guid.NewGuid(),
            ImageSource.FromStream(() => File.OpenRead(rutaCache)),
            "Nueva · pendiente de guardar",
            true,
            foto,
            rutaCache);
    }

    private void CargarPreviewsExistentes(IEnumerable<RegistroResiduoFotoDto> fotos)
    {
        foreach (var existente in _fotosPreview.Where(x => !x.PuedeEliminar).ToArray())
            _fotosPreview.Remove(existente);

        var httpClient = AppServices.Services.GetService<HttpClient>();
        foreach (var foto in fotos)
        {
            var fuente = CrearFuenteRemota(foto.UrlNube, httpClient?.BaseAddress);
            if (fuente is null)
                continue;

            _fotosPreview.Add(new FotoPreviewItem(
                foto.FotoId,
                fuente,
                "Guardada",
                false,
                null,
                null));
        }
    }

    private static ImageSource? CrearFuenteRemota(string? url, Uri? baseAddress)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluta) &&
            (absoluta.Scheme == Uri.UriSchemeHttp || absoluta.Scheme == Uri.UriSchemeHttps))
            return ImageSource.FromUri(absoluta);

        if (baseAddress is null)
            return null;

        return ImageSource.FromUri(new Uri(baseAddress, url));
    }

    private static void EliminarCachePreview(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta))
            return;

        try
        {
            if (File.Exists(ruta))
                File.Delete(ruta);
        }
        catch
        {
            // El caché es temporal; si el sistema mantiene el archivo abierto se limpiará después.
        }
    }

    private void ActualizarEstadoEvidencia()
    {
        if (TieneUbicacion)
        {
            var precision = _precisionMetros.HasValue
                ? $" · precisión ±{_precisionMetros.Value:0.#} m"
                : string.Empty;
            UbicacionEstadoLabel.Text = $"{_latitud:0.000000}, {_longitud:0.000000}{precision}";
        }
        else if (_capturaUbicacionEnCurso is { IsCompleted: false })
        {
            UbicacionEstadoLabel.Text = "Obteniendo ubicación automáticamente...";
        }
        else
        {
            UbicacionEstadoLabel.Text = "Ubicación pendiente de captura automática";
        }

        var totalFotos = _fotosExistentes + _fotosPendientes.Count;
        FotosEstadoLabel.Text = totalFotos switch
        {
            0 => "Ninguna foto registrada",
            1 => "1 foto registrada/lista",
            _ => $"{totalFotos} fotos registradas/listas"
        };
        TomarFotoButton.Text = totalFotos == 0 ? "Tomar foto" : "Tomar otra foto";
        FotosPreviewScroll.IsVisible = _fotosPreview.Count > 0;

        if (_fotosPendientes.Count > 0)
        {
            FotosDetalleLabel.Text = _fotosPendientes.Count == 1
                ? "1 foto nueva pendiente de guardar"
                : $"{_fotosPendientes.Count} fotos nuevas pendientes de guardar";
            FotosDetalleLabel.IsVisible = true;
        }
        else
        {
            FotosDetalleLabel.Text = string.Empty;
            FotosDetalleLabel.IsVisible = false;
        }
    }

    private static bool TryParseCantidad(string? value, out decimal cantidad)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out cantidad) ||
               decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out cantidad);
    }

    private void SetBusy(bool busy)
    {
        RegistrarButton.IsEnabled = !busy;
        TipoResiduoPicker.IsEnabled = !busy && !EsEdicion;
        ResiduoPicker.IsEnabled = !busy && !EsEdicion;
        CantidadEntry.IsEnabled = !busy;
        ObservacionEditor.IsEnabled = !busy;
        TomarFotoButton.IsEnabled = !busy;
        ResiduoActivityIndicator.IsVisible = busy;
        ResiduoActivityIndicator.IsRunning = busy;
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private async void OnVolverTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.VolverAsync();
    }

    private sealed record FotoPreviewItem(
        Guid Id,
        ImageSource Fuente,
        string EstadoTexto,
        bool PuedeEliminar,
        Microsoft.Maui.Storage.FileResult? Archivo,
        string? RutaCache);
}
