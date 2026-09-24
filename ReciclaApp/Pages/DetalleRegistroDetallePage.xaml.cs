using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics;
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

    public string RegistroId { get; set; } = string.Empty;

    public DetalleRegistroDetallePage()
    {
        InitializeComponent();
        BindableLayout.SetItemsSource(ResiduosStack, _residuos);
        BindableLayout.SetItemsSource(EvidenciasStack, _evidencias);
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
        _evidencias.Clear();

        var tieneDisposicion = disposicion is not null;
        SinDisposicionBorder.IsVisible = !tieneDisposicion;
        DisposicionBorder.IsVisible = tieneDisposicion;

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
                evidencia.TipoEvidencia,
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
            "Al finalizar ya no podrás agregar, editar ni eliminar residuos. ¿Deseas continuar?",
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
