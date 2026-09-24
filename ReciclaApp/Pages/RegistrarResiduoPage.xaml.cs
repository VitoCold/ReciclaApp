using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(RegistroId), "registroId")]
public partial class RegistrarResiduoPage : ContentPage
{
    private CatalogosInicialDto? _catalogos;
    private UnidadMedidaDto? _unidadSeleccionada;
    private bool _isLoading;

    public string RegistroId { get; set; } = string.Empty;

    public RegistrarResiduoPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_catalogos is null)
            await CargarCatalogosAsync();
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

    private async void OnRegistrarClicked(object sender, EventArgs e)
    {
        ErrorBorder.IsVisible = false;

        if (!Guid.TryParse(RegistroId, out var registroId))
        {
            MostrarError("No se recibió un registro válido para asociar el residuo.");
            return;
        }

        if (TipoResiduoPicker.SelectedItem is not TipoResiduoDto tipo ||
            ResiduoPicker.SelectedItem is not ResiduoCatalogoDto residuo ||
            _unidadSeleccionada is null)
        {
            MostrarError("Selecciona el tipo de residuo y el residuo generado.");
            return;
        }

        if (!TryParseCantidad(CantidadEntry.Text, out var cantidad) || cantidad <= 0)
        {
            MostrarError("Ingresa una cantidad válida mayor a cero.");
            return;
        }

        if (_isLoading)
            return;

        _isLoading = true;
        SetBusy(true);

        try
        {
            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var request = new AgregarRegistroResiduoRequest(
                TipoResiduoId: tipo.TipoResiduoId,
                ResiduoId: residuo.ResiduoId,
                UnidadMedidaId: _unidadSeleccionada.UnidadMedidaId,
                Cantidad: cantidad,
                Observacion: string.IsNullOrWhiteSpace(ObservacionEditor.Text)
                    ? null
                    : ObservacionEditor.Text.Trim());

            await apiClient.AgregarResiduoAsync(registroId, request);
            await AppNavigator.IrAResiduosDelRegistroAsync(registroId);
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
                .LogError(ex, "Error registrando residuo en {RegistroId}.", registroId);
            MostrarError("No se pudo guardar el residuo. Revisa los datos e inténtalo nuevamente.");
        }
        finally
        {
            _isLoading = false;
            SetBusy(false);
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
        TipoResiduoPicker.IsEnabled = !busy;
        ResiduoPicker.IsEnabled = !busy;
        CantidadEntry.IsEnabled = !busy;
        ObservacionEditor.IsEnabled = !busy;
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
}
