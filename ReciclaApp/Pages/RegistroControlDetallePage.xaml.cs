using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(ControlId), "controlId")]
[QueryProperty(nameof(RegistroId), "registroId")]
public partial class RegistroControlDetallePage : ContentPage
{
    private Guid _controlId;
    private Guid _registroId;
    private bool _isLoading;
    private bool _puedeEditar;
    private RegistroControlDetalleDto? _registro;

    public string ControlId
    {
        set => Guid.TryParse(value, out _controlId);
    }

    public string RegistroId
    {
        set => Guid.TryParse(value, out _registroId);
    }

    public RegistroControlDetallePage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_controlId == Guid.Empty || _registroId == Guid.Empty)
        {
            MostrarError("No se recibió un registro válido.");
            return;
        }

        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
        {
            await AppNavigator.IrAlLoginAsync();
            return;
        }

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
            var api = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();

            var registroTask = api.ObtenerRegistroControlAsync(_controlId, _registroId);
            var controlTask = api.ObtenerControlGeneracionAsync(_controlId);
            var catalogosTask = api.ObtenerCatalogosAsync();
            await Task.WhenAll(registroTask, controlTask, catalogosTask);

            _registro = await registroTask;
            var control = await controlTask;
            var catalogos = await catalogosTask;

            var usuario = session.CurrentUser;
            var esAutor = usuario is not null && _registro.RegistradoPorUsuarioId == usuario.UsuarioId;
            var enProceso = EsEnProceso(_registro.Estado);
            var controlActivo = string.Equals(control.Estado, "Activo", StringComparison.OrdinalIgnoreCase);
            _puedeEditar = esAutor && enProceso && controlActivo;

            FechaLabel.Text = _registro.FechaRegistro.ToString("dd/MM/yyyy HH:mm");
            RegistradoPorLabel.Text = $"Registrado por {_registro.RegistradoPor}";
            EstadoLabel.Text = _registro.Estado;
            CantidadLabel.Text = _registro.Residuos.Count == 1
                ? "1 residuo registrado"
                : $"{_registro.Residuos.Count} residuos registrados";
            ObservacionLabel.Text = string.IsNullOrWhiteSpace(_registro.Observacion)
                ? "Sin observaciones generales"
                : _registro.Observacion;

            AplicarColorEstado(_registro.Estado);
            SoloLecturaLabel.IsVisible = !esAutor || !controlActivo;
            if (esAutor && !controlActivo)
                SoloLecturaLabel.Text = "El control no está activo. El registro queda disponible solo para consulta.";
            else if (!esAutor)
                SoloLecturaLabel.Text = "Puedes consultar este registro, pero solo quien lo creó puede modificarlo o finalizarlo.";

            AgregarResiduoButton.IsVisible = _puedeEditar;
            FinalizarLayout.IsVisible = _puedeEditar;
            FinalizarButton.IsEnabled = _puedeEditar && _registro.Residuos.Count > 0;
            SinResiduosLabel.IsVisible = _registro.Residuos.Count == 0;

            var residuos = _registro.Residuos
                .Select(x => MapResiduo(x, catalogos, _puedeEditar))
                .ToArray();
            BindingContext = new RegistroBinding(residuos);

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
            AppServices.Services.GetService<ILogger<RegistroControlDetallePage>>()?
                .LogError(ex, "Error cargando registro {RegistroId} del control {ControlId}.", _registroId, _controlId);
            MostrarError("No se pudo cargar el registro.");
        }
        finally
        {
            _isLoading = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private static ResiduoControlVisual MapResiduo(
        RegistroResiduoDto residuo,
        CatalogosInicialDto catalogos,
        bool puedeEditar)
    {
        var catalogo = catalogos.Residuos.FirstOrDefault(x => x.ResiduoId == residuo.ResiduoId);
        var clasificacion = catalogo is null
            ? null
            : catalogos.Clasificaciones.FirstOrDefault(x =>
                x.ClasificacionResiduoId == catalogo.ClasificacionResiduoId);
        var unidad = catalogos.UnidadesMedida.FirstOrDefault(x =>
            x.UnidadMedidaId == residuo.UnidadMedidaId);

        return new ResiduoControlVisual(
            residuo.RegistroResiduoId,
            catalogo?.Nombre ?? "Residuo",
            clasificacion?.Nombre ?? "Sin clasificación",
            $"{residuo.Cantidad:0.###} {unidad?.Codigo ?? string.Empty}".Trim(),
            string.IsNullOrWhiteSpace(residuo.Observacion) ? "Sin observación" : residuo.Observacion,
            puedeEditar);
    }

    private void AplicarColorEstado(string estado)
    {
        if (EsEnProceso(estado))
        {
            EstadoBorder.BackgroundColor = Color.FromArgb("#FFF4DC");
            EstadoLabel.TextColor = Color.FromArgb("#F59E0B");
            return;
        }

        if (string.Equals(estado, "Registrado", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(estado, "Completado", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(estado, "Validado", StringComparison.OrdinalIgnoreCase))
        {
            EstadoBorder.BackgroundColor = Color.FromArgb("#E8F7EE");
            EstadoLabel.TextColor = Color.FromArgb("#079542");
            return;
        }

        EstadoBorder.BackgroundColor = Color.FromArgb("#EAF2FF");
        EstadoLabel.TextColor = Color.FromArgb("#0753B7");
    }

    private static bool EsEnProceso(string estado) =>
        string.Equals(estado, "En proceso", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(estado, "Borrador", StringComparison.OrdinalIgnoreCase);

    private async void OnAgregarResiduoClicked(object sender, EventArgs e)
    {
        if (_puedeEditar)
            await AppNavigator.IrARegistrarResiduoDesdeControlAsync(_controlId, _registroId);
    }

    private async void OnEditarResiduoClicked(object sender, EventArgs e)
    {
        if (!_puedeEditar || sender is not Button button || !TryGetGuid(button.CommandParameter, out var residuoId))
            return;

        await AppNavigator.IrAEditarResiduoDesdeControlAsync(_controlId, _registroId, residuoId);
    }

    private async void OnEliminarResiduoClicked(object sender, EventArgs e)
    {
        if (!_puedeEditar || _isLoading || sender is not Button button ||
            !TryGetGuid(button.CommandParameter, out var residuoId))
            return;

        var confirmar = await DisplayAlert(
            "Eliminar residuo",
            "¿Deseas eliminar este residuo del registro?",
            "Eliminar",
            "Cancelar");
        if (!confirmar)
            return;

        try
        {
            _isLoading = true;
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;
            var api = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            await api.EliminarResiduoAsync(_registroId, residuoId);
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RegistroControlDetallePage>>()?
                .LogError(ex, "Error eliminando residuo {ResiduoId}.", residuoId);
            await DisplayAlert("No se pudo eliminar", "El residuo no pudo eliminarse. Inténtalo nuevamente.", "Aceptar");
        }
        finally
        {
            _isLoading = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }

        await CargarAsync();
    }

    private async void OnFinalizarClicked(object sender, EventArgs e)
    {
        if (!_puedeEditar || _registro is null || _registro.Residuos.Count == 0 || _isLoading)
            return;

        var confirmar = await DisplayAlert(
            "Finalizar registro",
            "Al finalizar ya no podrás agregar, editar ni eliminar residuos. El registro quedará disponible para el proceso de almacenamiento y retiro. ¿Deseas continuar?",
            "Finalizar",
            "Cancelar");
        if (!confirmar)
            return;

        _isLoading = true;
        FinalizarButton.IsEnabled = false;
        FinalizarButton.Text = "Finalizando...";

        try
        {
            var workflow = AppServices.Services.GetRequiredService<IControlGeneracionWorkflowClient>();
            await workflow.CompletarRegistroAsync(_controlId, _registroId);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            await DisplayAlert("No se puede finalizar", "El control o el registro ya no están disponibles para edición.", "Aceptar");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RegistroControlDetallePage>>()?
                .LogError(ex, "Error finalizando registro {RegistroId}.", _registroId);
            await DisplayAlert("No se pudo finalizar", "Verifica el registro y vuelve a intentarlo.", "Aceptar");
        }
        finally
        {
            _isLoading = false;
            FinalizarButton.Text = "Finalizar registro";
        }

        await CargarAsync();
    }

    private static bool TryGetGuid(object? value, out Guid guid) =>
        value is Guid direct
            ? (guid = direct) != Guid.Empty
            : Guid.TryParse(value?.ToString(), out guid);

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e) =>
        await AppNavigator.VolverAsync();

    private sealed record RegistroBinding(IReadOnlyCollection<ResiduoControlVisual> Residuos);

    private sealed record ResiduoControlVisual(
        Guid RegistroResiduoId,
        string Nombre,
        string Clasificacion,
        string CantidadTexto,
        string ObservacionTexto,
        bool PuedeEditar);
}
