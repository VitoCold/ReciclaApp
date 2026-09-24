using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(RegistroId), "registroId")]
public partial class DetalleRegistroDetallePage : ContentPage
{
    private readonly ObservableCollection<ResiduoVisualItem> _residuos = new();
    private readonly ObservableCollection<EvidenciaVisualItem> _evidencias = new();
    private bool _isLoading;
    private bool _enProceso;
    private bool _puedeGestionarDisposicion;
    private DisposicionDto? _disposicionActual;

    public string RegistroId { get; set; } = string.Empty;

    public DetalleRegistroDetallePage()
    {
        InitializeComponent();
        BindableLayout.SetItemsSource(ResiduosStack, _residuos);
        BindableLayout.SetItemsSource(EvidenciasStack, _evidencias);

        FechaDisposicionPicker.MaximumDate = DateTime.Today;
        FechaDisposicionPicker.Date = DateTime.Today;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarDetalleAsync();
    }

    private async Task CargarDetalleAsync()
    {
        if (_isLoading)
            return;

        if (!Guid.TryParse(RegistroId, out var registroId))
        {
            MostrarError("No se recibió un identificador de registro válido.");
            return;
        }

        _isLoading = true;
        ErrorBorder.IsVisible = false;
        DetalleActivityIndicator.IsVisible = true;
        DetalleActivityIndicator.IsRunning = true;

        try
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
            {
                await AppNavigator.IrAlLoginAsync();
                return;
            }

            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var detalleTask = apiClient.ObtenerRegistroAsync(registroId);
            var catalogosTask = apiClient.ObtenerCatalogosAsync();

            await Task.WhenAll(detalleTask, catalogosTask);

            var registro = await detalleTask;
            var catalogos = await catalogosTask;

            CargarInformacionGeneral(registro, session.CurrentUser);
            AplicarEstado(registro.Estado, registro.Residuos.Count);
            CargarResiduos(registro, catalogos, _enProceso);
            CargarDisposicion(registro.Disposicion);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            MostrarError("El registro ya no existe o no tienes acceso a él.");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<DetalleRegistroDetallePage>>()?
                .LogError(ex, "Error cargando el detalle del registro {RegistroId}.", RegistroId);
            MostrarError("No se pudo cargar el detalle del registro.");
        }
        finally
        {
            _isLoading = false;
            DetalleActivityIndicator.IsRunning = false;
            DetalleActivityIndicator.IsVisible = false;
        }
    }

    private void CargarInformacionGeneral(RegistroDetalleDto registro, UsuarioDto? usuario)
    {
        FechaLabel.Text = registro.FechaRegistro.ToString("dd/MM/yyyy HH:mm");
        ProyectoLabel.Text = registro.Proyecto;
        ActividadLabel.Text = registro.Actividad;
        SedeLabel.Text = registro.Sede;
        CantidadResiduosLabel.Text = registro.Residuos.Count == 1
            ? "1 residuo"
            : $"{registro.Residuos.Count} residuos";
        ObservacionLabel.Text = string.IsNullOrWhiteSpace(registro.Observacion)
            ? "Sin observaciones"
            : registro.Observacion;

        RegistradoPorLabel.Text = usuario is null
            ? "Usuario"
            : string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
    }

    private void CargarResiduos(
        RegistroDetalleDto registro,
        CatalogosInicialDto catalogos,
        bool esEditable)
    {
        _residuos.Clear();

        foreach (var residuo in registro.Residuos)
        {
            var catalogo = catalogos.Residuos.FirstOrDefault(x => x.ResiduoId == residuo.ResiduoId);
            var clasificacion = catalogo is null
                ? null
                : catalogos.Clasificaciones.FirstOrDefault(x =>
                    x.ClasificacionResiduoId == catalogo.ClasificacionResiduoId);
            var unidad = catalogos.UnidadesMedida.FirstOrDefault(x =>
                x.UnidadMedidaId == residuo.UnidadMedidaId);

            var esPeligroso = string.Equals(
                clasificacion?.Codigo,
                "PELIGROSO",
                StringComparison.OrdinalIgnoreCase);

            _residuos.Add(new ResiduoVisualItem(
                residuo.RegistroResiduoId,
                catalogo?.Nombre ?? "Residuo",
                clasificacion?.Nombre ?? "Sin clasificación",
                esPeligroso ? Color.FromArgb("#FFE9E7") : Color.FromArgb("#E8F7EE"),
                TryColor(clasificacion?.ColorHex) ?? (esPeligroso
                    ? Color.FromArgb("#E4312B")
                    : Color.FromArgb("#079542")),
                $"{residuo.Cantidad:0.###} {unidad?.Codigo ?? string.Empty}".Trim(),
                residuo.Observacion,
                residuo.Fotos.Count == 1 ? "1 foto" : $"{residuo.Fotos.Count} fotos",
                esEditable));
        }

        SinResiduosBorder.IsVisible = _residuos.Count == 0;
        ResumenResiduosLabel.Text = _residuos.Count == 1
            ? "1 residuo registrado"
            : $"{_residuos.Count} residuos registrados";
    }

    private void CargarDisposicion(DisposicionDto? disposicion)
    {
        _disposicionActual = disposicion;
        _evidencias.Clear();
        DisposicionFormularioBorder.IsVisible = false;
        DisposicionErrorBorder.IsVisible = false;

        var tieneDisposicion = disposicion is not null;
        SinDisposicionBorder.IsVisible = !tieneDisposicion;
        DisposicionBorder.IsVisible = tieneDisposicion;

        RegistrarDisposicionButton.IsVisible = !tieneDisposicion && _puedeGestionarDisposicion;
        EditarDisposicionButton.IsVisible = tieneDisposicion && _puedeGestionarDisposicion;
        AdjuntarEvidenciaButton.IsVisible = tieneDisposicion && _puedeGestionarDisposicion;

        SinDisposicionMensajeLabel.Text = _enProceso
            ? "Finaliza primero el registro para habilitar la disposición final."
            : _puedeGestionarDisposicion
                ? "Registra el destino final del residuo y luego adjunta sus evidencias."
                : "La disposición final no está disponible para el estado actual del registro.";

        if (disposicion is null)
            return;

        DocumentoDisposicionLabel.Text = string.IsNullOrWhiteSpace(disposicion.CodigoDocumento)
            ? "Sin documento"
            : disposicion.CodigoDocumento;
        FechaDisposicionLabel.Text = disposicion.FechaDisposicion?.ToString("dd/MM/yyyy") ?? "Sin fecha";
        EmpresaDisposicionLabel.Text = string.IsNullOrWhiteSpace(disposicion.EmpresaDisposicion)
            ? "Sin empresa"
            : disposicion.EmpresaDisposicion;
        CantidadEvidenciasLabel.Text = disposicion.Evidencias.Count.ToString();

        ObservacionDisposicionLabel.Text = disposicion.Observacion;
        ObservacionDisposicionLabel.IsVisible = !string.IsNullOrWhiteSpace(disposicion.Observacion);

        foreach (var evidencia in disposicion.Evidencias.OrderByDescending(x => x.CreadoUtc))
        {
            _evidencias.Add(new EvidenciaVisualItem(
                evidencia.NombreArchivo,
                FormatearTipoEvidencia(evidencia.TipoEvidencia),
                evidencia.CreadoUtc.ToLocalTime().ToString("dd/MM/yyyy")));
        }
    }

    private void AplicarEstado(string estado, int cantidadResiduos)
    {
        var estadoVisible = string.Equals(estado, "Borrador", StringComparison.OrdinalIgnoreCase)
            ? "En proceso"
            : estado;

        EstadoLabel.Text = estadoVisible;
        _enProceso = string.Equals(estadoVisible, "En proceso", StringComparison.OrdinalIgnoreCase);
        _puedeGestionarDisposicion =
            string.Equals(estadoVisible, "Completado", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(estadoVisible, "Validado", StringComparison.OrdinalIgnoreCase);

        AgregarResiduoButton.IsVisible = _enProceso;
        FinalizarButton.IsVisible = _enProceso;
        FinalizarButton.IsEnabled = _enProceso && cantidadResiduos > 0;
        FinalizarAyudaLabel.IsVisible = _enProceso && cantidadResiduos == 0;

        switch (estadoVisible)
        {
            case "En proceso":
                EstadoBorder.BackgroundColor = Color.FromArgb("#FFF4DC");
                EstadoLabel.TextColor = Color.FromArgb("#F59E0B");
                break;
            case "Completado":
            case "Validado":
                EstadoBorder.BackgroundColor = Color.FromArgb("#E8F7EE");
                EstadoLabel.TextColor = Color.FromArgb("#079542");
                break;
            case "Observado":
            case "Anulado":
                EstadoBorder.BackgroundColor = Color.FromArgb("#FFE9E7");
                EstadoLabel.TextColor = Color.FromArgb("#E4312B");
                break;
            default:
                EstadoBorder.BackgroundColor = Color.FromArgb("#EAF2FF");
                EstadoLabel.TextColor = Color.FromArgb("#0753B7");
                break;
        }
    }

    private static Color? TryColor(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
            return null;

        try
        {
            return Color.FromArgb(colorHex);
        }
        catch
        {
            return null;
        }
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private async void OnAgregarResiduoClicked(object sender, EventArgs e)
    {
        if (_enProceso && Guid.TryParse(RegistroId, out var registroId))
            await AppNavigator.IrARegistrarResiduoAsync(registroId);
    }

    private async void OnEditarResiduoClicked(object sender, EventArgs e)
    {
        if (!_enProceso || !Guid.TryParse(RegistroId, out var registroId) ||
            sender is not Button button || !TryGetGuid(button.CommandParameter, out var registroResiduoId))
            return;

        await AppNavigator.IrAEditarResiduoAsync(registroId, registroResiduoId);
    }

    private async void OnEliminarResiduoClicked(object sender, EventArgs e)
    {
        if (!_enProceso || _isLoading || !Guid.TryParse(RegistroId, out var registroId) ||
            sender is not Button button || !TryGetGuid(button.CommandParameter, out var registroResiduoId))
            return;

        var item = _residuos.FirstOrDefault(x => x.RegistroResiduoId == registroResiduoId);
        var confirmar = await DisplayAlert(
            "Eliminar residuo",
            $"¿Deseas eliminar {item?.Nombre ?? "este residuo"} del registro?",
            "Eliminar",
            "Cancelar");

        if (!confirmar)
            return;

        _isLoading = true;
        DetalleActivityIndicator.IsVisible = true;
        DetalleActivityIndicator.IsRunning = true;

        try
        {
            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            await apiClient.EliminarResiduoAsync(registroId, registroResiduoId);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
            return;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            await DisplayAlert("Registro cerrado", "Este registro ya no se encuentra en proceso y no puede modificarse.", "Aceptar");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<DetalleRegistroDetallePage>>()?
                .LogError(ex, "Error eliminando residuo {RegistroResiduoId} del registro {RegistroId}.", registroResiduoId, registroId);
            await DisplayAlert("No se pudo eliminar", "No se pudo eliminar el residuo. Inténtalo nuevamente.", "Aceptar");
        }
        finally
        {
            _isLoading = false;
            DetalleActivityIndicator.IsRunning = false;
            DetalleActivityIndicator.IsVisible = false;
        }

        await CargarDetalleAsync();
    }

    private async void OnFinalizarClicked(object sender, EventArgs e)
    {
        if (!_enProceso || _residuos.Count == 0 || _isLoading ||
            !Guid.TryParse(RegistroId, out var registroId))
            return;

        var confirmar = await DisplayAlert(
            "Finalizar registro",
            "Al finalizar ya no podrás agregar, editar ni eliminar residuos. Luego podrás registrar la disposición final. ¿Deseas continuar?",
            "Finalizar",
            "Cancelar");

        if (!confirmar)
            return;

        _isLoading = true;
        FinalizarButton.IsEnabled = false;
        FinalizarButton.Text = "Finalizando...";

        try
        {
            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            await apiClient.CompletarRegistroAsync(registroId);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
            return;
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<DetalleRegistroDetallePage>>()?
                .LogError(ex, "Error finalizando el registro {RegistroId}.", registroId);
            await DisplayAlert("No se pudo finalizar", "Verifica que el registro tenga residuos y vuelve a intentarlo.", "Aceptar");
        }
        finally
        {
            _isLoading = false;
            FinalizarButton.Text = "Finalizar registro";
        }

        await CargarDetalleAsync();
    }

    private void OnRegistrarDisposicionClicked(object sender, EventArgs e)
    {
        if (!_puedeGestionarDisposicion)
            return;

        MostrarFormularioDisposicion(null);
    }

    private void OnEditarDisposicionClicked(object sender, EventArgs e)
    {
        if (!_puedeGestionarDisposicion || _disposicionActual is null)
            return;

        MostrarFormularioDisposicion(_disposicionActual);
    }

    private void MostrarFormularioDisposicion(DisposicionDto? disposicion)
    {
        DisposicionErrorBorder.IsVisible = false;
        SinDisposicionBorder.IsVisible = false;
        DisposicionBorder.IsVisible = false;
        DisposicionFormularioBorder.IsVisible = true;

        var esEdicion = disposicion is not null;
        TituloDisposicionFormularioLabel.Text = esEdicion
            ? "Editar disposición final"
            : "Registrar disposición final";
        GuardarDisposicionButton.Text = esEdicion
            ? "Guardar cambios"
            : "Guardar disposición final";

        FechaDisposicionPicker.Date = disposicion?.FechaDisposicion?.Date ?? DateTime.Today;
        EmpresaDisposicionEntry.Text = disposicion?.EmpresaDisposicion ?? string.Empty;
        DocumentoDisposicionEntry.Text = disposicion?.CodigoDocumento ?? string.Empty;
        ObservacionDisposicionEditor.Text = disposicion?.Observacion ?? string.Empty;
    }

    private void OnCancelarDisposicionClicked(object sender, EventArgs e)
    {
        CargarDisposicion(_disposicionActual);
    }

    private async void OnGuardarDisposicionClicked(object sender, EventArgs e)
    {
        if (!_puedeGestionarDisposicion || _isLoading ||
            !Guid.TryParse(RegistroId, out var registroId))
            return;

        DisposicionErrorBorder.IsVisible = false;

        var empresa = EmpresaDisposicionEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(empresa))
        {
            MostrarErrorDisposicion("Indica la empresa operadora o destino final de los residuos.");
            return;
        }

        if (FechaDisposicionPicker.Date.Date > DateTime.Today)
        {
            MostrarErrorDisposicion("La fecha de disposición no puede ser futura.");
            return;
        }

        _isLoading = true;
        SetDisposicionBusy(true);
        var guardado = false;

        try
        {
            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var request = new CrearDisposicionRequest(
                CodigoDocumento: TextoOpcional(DocumentoDisposicionEntry.Text),
                FechaDisposicion: FechaDisposicionPicker.Date.Date,
                EmpresaDisposicion: empresa,
                Observacion: TextoOpcional(ObservacionDisposicionEditor.Text));

            await apiClient.GuardarDisposicionAsync(registroId, request);
            guardado = true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
            return;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            MostrarErrorDisposicion("Finaliza el registro antes de registrar su disposición final.");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<DetalleRegistroDetallePage>>()?
                .LogError(ex, "Error guardando la disposición final del registro {RegistroId}.", registroId);
            MostrarErrorDisposicion("No se pudo guardar la disposición final. Inténtalo nuevamente.");
        }
        finally
        {
            _isLoading = false;
            SetDisposicionBusy(false);
        }

        if (guardado)
            await CargarDetalleAsync();
    }

    private async void OnAdjuntarEvidenciaClicked(object sender, EventArgs e)
    {
        if (!_puedeGestionarDisposicion || _isLoading || _disposicionActual is null)
            return;

        var tipoSeleccionado = await DisplayActionSheet(
            "Tipo de evidencia",
            "Cancelar",
            null,
            "Documento",
            "Certificado",
            "Foto");

        if (string.IsNullOrWhiteSpace(tipoSeleccionado) || tipoSeleccionado == "Cancelar")
            return;

        FileResult? archivo;
        try
        {
            archivo = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Selecciona la evidencia de disposición final"
            });
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<DetalleRegistroDetallePage>>()?
                .LogWarning(ex, "No se pudo abrir el selector de evidencias.");
            await DisplayAlert("Evidencia", "No se pudo abrir el selector de archivos.", "Aceptar");
            return;
        }

        if (archivo is null)
            return;

        _isLoading = true;
        AdjuntarEvidenciaButton.IsEnabled = false;
        DetalleActivityIndicator.IsVisible = true;
        DetalleActivityIndicator.IsRunning = true;
        var subida = false;

        try
        {
            await using var stream = await archivo.OpenReadAsync();
            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var contentType = string.IsNullOrWhiteSpace(archivo.ContentType)
                ? "application/octet-stream"
                : archivo.ContentType;

            await apiClient.SubirEvidenciaAsync(
                _disposicionActual.DisposicionId,
                stream,
                archivo.FileName,
                contentType,
                NormalizarTipoEvidencia(tipoSeleccionado));

            subida = true;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
            return;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            await DisplayAlert("Archivo demasiado grande", "La evidencia no puede superar los 15 MB.", "Aceptar");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<DetalleRegistroDetallePage>>()?
                .LogError(ex, "Error subiendo evidencia para disposición {DisposicionId}.", _disposicionActual.DisposicionId);
            await DisplayAlert("No se pudo adjuntar", "No se pudo subir la evidencia. Inténtalo nuevamente.", "Aceptar");
        }
        finally
        {
            _isLoading = false;
            AdjuntarEvidenciaButton.IsEnabled = true;
            DetalleActivityIndicator.IsRunning = false;
            DetalleActivityIndicator.IsVisible = false;
        }

        if (subida)
            await CargarDetalleAsync();
    }

    private void MostrarErrorDisposicion(string mensaje)
    {
        DisposicionErrorLabel.Text = mensaje;
        DisposicionErrorBorder.IsVisible = true;
    }

    private void SetDisposicionBusy(bool busy)
    {
        GuardarDisposicionButton.IsEnabled = !busy;
        GuardarDisposicionButton.Text = busy
            ? "Guardando..."
            : _disposicionActual is null
                ? "Guardar disposición final"
                : "Guardar cambios";
        FechaDisposicionPicker.IsEnabled = !busy;
        EmpresaDisposicionEntry.IsEnabled = !busy;
        DocumentoDisposicionEntry.IsEnabled = !busy;
        ObservacionDisposicionEditor.IsEnabled = !busy;
    }

    private static string? TextoOpcional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizarTipoEvidencia(string value) => value switch
    {
        "Certificado" => "CERTIFICADO",
        "Foto" => "FOTO",
        _ => "DOCUMENTO"
    };

    private static string FormatearTipoEvidencia(string value) => value.ToUpperInvariant() switch
    {
        "CERTIFICADO" => "Certificado",
        "FOTO" => "Foto",
        "DOCUMENTO" => "Documento",
        _ => value
    };

    private static bool TryGetGuid(object? value, out Guid id)
    {
        if (value is Guid guid)
        {
            id = guid;
            return true;
        }

        return Guid.TryParse(value?.ToString(), out id);
    }

    private async void OnVolverTapped(object sender, TappedEventArgs e)
    {
        await AppNavigator.VolverAsync();
    }

    private sealed record ResiduoVisualItem(
        Guid RegistroResiduoId,
        string Nombre,
        string Clasificacion,
        Color ClasificacionFondo,
        Color ClasificacionColor,
        string CantidadTexto,
        string? Observacion,
        string FotosTexto,
        bool EsEditable)
    {
        public bool TieneObservacion => !string.IsNullOrWhiteSpace(Observacion);
    }

    private sealed record EvidenciaVisualItem(
        string NombreArchivo,
        string TipoEvidencia,
        string FechaTexto);
}
