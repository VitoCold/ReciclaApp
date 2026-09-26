using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(RetiroId), "retiroId")]
public partial class RetiroDetallePage : ContentPage
{
    private Guid _retiroId;
    private bool _isLoading;
    private DisposicionFinalDto? _disposicion;
    private UsuarioDto? _usuario;

    public string RetiroId
    {
        set => Guid.TryParse(value, out _retiroId);
    }

    public RetiroDetallePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_retiroId == Guid.Empty)
        {
            MostrarError("No se recibió un retiro válido.");
            return;
        }

        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
        {
            await AppNavigator.IrAlLoginAsync();
            return;
        }

        _usuario = session.CurrentUser;
        await CargarAsync();
    }

    private async Task CargarAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        ErrorBorder.IsVisible = false;
        ContenidoLayout.IsVisible = false;
        ActivityIndicator.IsVisible = true;
        ActivityIndicator.IsRunning = true;

        try
        {
            var retiroApi = AppServices.Services.GetRequiredService<IRetiroApiClient>();
            var disposicionApi = AppServices.Services.GetRequiredService<IDisposicionFinalApiClient>();
            var retiro = await retiroApi.ObtenerAsync(_retiroId);

            try
            {
                _disposicion = await disposicionApi.ObtenerPorRetiroAsync(_retiroId);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                _disposicion = null;
            }

            CodigoLabel.Text = retiro.Codigo;
            FechaLabel.Text = retiro.FechaRetiro.ToString("dd/MM/yyyy HH:mm");
            EstadoLabel.Text = retiro.Estado;
            UbicacionLabel.Text = $"{retiro.Sede} · {retiro.PuntoAlmacenamiento}";
            GestorLabel.Text = $"Gestor de retiro: {retiro.EmpresaGestora}";
            TotalesLabel.Text = retiro.Totales.Count == 0
                ? "Sin cantidades"
                : string.Join(" · ", retiro.Totales.Select(x => $"{x.Cantidad:0.###} {x.Unidad}"));
            CreadoPorLabel.Text = $"Registrado por {retiro.CreadoPor} · {retiro.CreadoUtc.ToLocalTime():dd/MM/yyyy HH:mm}";

            DocumentoLabel.Text = string.IsNullOrWhiteSpace(retiro.DocumentoTransporte)
                ? "Documento: no registrado"
                : $"Documento: {retiro.DocumentoTransporte}";
            VehiculoLabel.Text = string.Join(" · ", new[]
            {
                string.IsNullOrWhiteSpace(retiro.Vehiculo) ? null : retiro.Vehiculo,
                string.IsNullOrWhiteSpace(retiro.Placa) ? null : retiro.Placa
            }.Where(x => !string.IsNullOrWhiteSpace(x)).DefaultIfEmpty("Vehículo: no registrado"));
            ConductorLabel.Text = string.IsNullOrWhiteSpace(retiro.Conductor)
                ? "Conductor: no registrado"
                : $"Conductor: {retiro.Conductor}";
            ObservacionLabel.Text = string.IsNullOrWhiteSpace(retiro.Observacion)
                ? "Sin observaciones"
                : retiro.Observacion;

            var detalles = retiro.Detalles
                .Select(x => new DetalleVisual(
                    x.Residuo,
                    $"{x.ControlCodigo} · {x.Clasificacion}",
                    $"{x.Cantidad:0.###} {x.Unidad}"))
                .ToArray();
            CantidadLabel.Text = detalles.Length == 1 ? "1 residuo" : $"{detalles.Length} residuos";

            var evidencias = PintarDestinoFinal();
            BindingContext = new DetailBinding(detalles, evidencias);
            ContenidoLayout.IsVisible = true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RetiroDetallePage>>()?
                .LogError(ex, "Error cargando retiro {RetiroId}.", _retiroId);
            MostrarError("No se pudo cargar el retiro.");
        }
        finally
        {
            _isLoading = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private EvidenciaVisual[] PintarDestinoFinal()
    {
        var esAmbiental = _usuario?.Roles.Contains("AMBIENTAL") == true;
        var esResponsable = _usuario?.Roles.Contains("RESPONSABLE_OPERATIVO") == true;
        var puedeDocumentar = esAmbiental || esResponsable;

        if (_disposicion is null)
        {
            DestinoEstadoLabel.Text = "Pendiente";
            DestinoDetalleLayout.IsVisible = false;
            DestinoPendienteLabel.IsVisible = true;
            DocumentarDestinoButton.IsVisible = puedeDocumentar;
            AdjuntarEvidenciaButton.IsVisible = false;
            ValidarDestinoButton.IsVisible = false;
            return Array.Empty<EvidenciaVisual>();
        }

        DestinoEstadoLabel.Text = _disposicion.Estado;
        DestinoDetalleLayout.IsVisible = true;
        DestinoPendienteLabel.IsVisible = false;
        DocumentarDestinoButton.IsVisible = false;

        var prefijo = _disposicion.EsValorizacion ? "Valorización" : "Tratamiento / disposición";
        DestinoTipoLabel.Text = $"{prefijo}: {_disposicion.TipoTratamiento}";
        DestinoGestorLabel.Text = $"Gestor: {_disposicion.EmpresaGestora}";
        DestinoFechaLabel.Text = _disposicion.FechaDisposicion.HasValue
            ? $"Fecha: {_disposicion.FechaDisposicion.Value:dd/MM/yyyy}"
            : "Fecha no registrada";
        DestinoDocumentoLabel.Text = string.IsNullOrWhiteSpace(_disposicion.CodigoDocumento)
            ? "Documento: sin código registrado"
            : $"Documento: {_disposicion.CodigoDocumento}";
        DestinoObservacionLabel.Text = string.IsNullOrWhiteSpace(_disposicion.Observacion)
            ? "Sin observaciones"
            : _disposicion.Observacion;

        var evidencias = _disposicion.Evidencias
            .Select(x => new EvidenciaVisual(
                x.NombreArchivo,
                $"{x.TipoEvidencia} · {FormatearTamano(x.TamanoBytes)}"))
            .ToArray();

        CantidadEvidenciasLabel.Text = evidencias.Length == 1 ? "1 archivo" : $"{evidencias.Length} archivos";
        SinEvidenciasLabel.IsVisible = evidencias.Length == 0;

        var validada = string.Equals(_disposicion.Estado, "Validada", StringComparison.OrdinalIgnoreCase);
        var documentada = string.Equals(_disposicion.Estado, "Documentada", StringComparison.OrdinalIgnoreCase);
        AdjuntarEvidenciaButton.IsVisible = puedeDocumentar && !validada;
        ValidarDestinoButton.IsVisible = esAmbiental && documentada && evidencias.Length > 0;
        return evidencias;
    }

    private async void OnDocumentarDestinoClicked(object sender, EventArgs e) =>
        await AppNavigator.IrANuevaDisposicionFinalAsync(_retiroId);

    private async void OnAdjuntarEvidenciaClicked(object sender, EventArgs e)
    {
        if (_isLoading || _disposicion is null)
            return;

        try
        {
            var archivo = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona certificado, manifiesto o constancia"
            });
            if (archivo is null)
                return;

            var tipo = await DisplayActionSheet(
                "Tipo de evidencia",
                "Cancelar",
                null,
                "CERTIFICADO",
                "MANIFIESTO",
                "CONSTANCIA",
                "OTRO");
            if (string.IsNullOrWhiteSpace(tipo) || tipo == "Cancelar")
                return;

            _isLoading = true;
            AdjuntarEvidenciaButton.IsEnabled = false;
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;

            await using var stream = await archivo.OpenReadAsync();
            var api = AppServices.Services.GetRequiredService<IDisposicionFinalApiClient>();
            _disposicion = await api.SubirEvidenciaAsync(
                _disposicion.DisposicionFinalId,
                stream,
                archivo.FileName,
                string.IsNullOrWhiteSpace(archivo.ContentType) ? "application/octet-stream" : archivo.ContentType,
                tipo);

            await CargarAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            await DisplayAlert("Archivo demasiado grande", "La evidencia no puede superar 15 MB.", "Aceptar");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RetiroDetallePage>>()?
                .LogError(ex, "Error adjuntando evidencia a disposición {DisposicionId}.", _disposicion?.DisposicionFinalId);
            await DisplayAlert("No se pudo adjuntar", "No se pudo guardar la evidencia. Inténtalo nuevamente.", "Aceptar");
        }
        finally
        {
            _isLoading = false;
            AdjuntarEvidenciaButton.IsEnabled = true;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private async void OnValidarDestinoClicked(object sender, EventArgs e)
    {
        if (_isLoading || _disposicion is null)
            return;

        var confirmar = await DisplayAlert(
            "Validar documentación",
            "¿Confirmas que la documentación respalda correctamente el destino final de este retiro?",
            "Validar",
            "Cancelar");
        if (!confirmar)
            return;

        try
        {
            _isLoading = true;
            ValidarDestinoButton.IsEnabled = false;
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;

            var api = AppServices.Services.GetRequiredService<IDisposicionFinalApiClient>();
            _disposicion = await api.ValidarAsync(_disposicion.DisposicionFinalId);
            await CargarAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RetiroDetallePage>>()?
                .LogError(ex, "Error validando disposición {DisposicionId}.", _disposicion?.DisposicionFinalId);
            await DisplayAlert("No se pudo validar", "Revisa que exista al menos una evidencia e inténtalo nuevamente.", "Aceptar");
        }
        finally
        {
            _isLoading = false;
            ValidarDestinoButton.IsEnabled = true;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private static string FormatearTamano(long? bytes)
    {
        if (!bytes.HasValue)
            return "tamaño desconocido";
        if (bytes.Value < 1024)
            return $"{bytes.Value} B";
        if (bytes.Value < 1024 * 1024)
            return $"{bytes.Value / 1024d:0.#} KB";
        return $"{bytes.Value / 1024d / 1024d:0.#} MB";
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();

    private sealed record DetailBinding(
        IReadOnlyCollection<DetalleVisual> Detalles,
        IReadOnlyCollection<EvidenciaVisual> Evidencias);
    private sealed record DetalleVisual(string Residuo, string OrigenTexto, string CantidadTexto);
    private sealed record EvidenciaVisual(string NombreArchivo, string TipoTexto);
}
