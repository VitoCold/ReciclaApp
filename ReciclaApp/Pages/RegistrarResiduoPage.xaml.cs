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
    private CatalogosInicialDto? _catalogos;
    private UnidadMedidaDto? _unidadSeleccionada;
    private bool _isLoading;
    private bool _edicionCargada;

    public string ControlId { get; set; } = string.Empty;
    public string RegistroId { get; set; } = string.Empty;
    public string RegistroResiduoId { get; set; } = string.Empty;

    private bool EsEdicion => Guid.TryParse(RegistroResiduoId, out _);
    private bool VieneDeControl => Guid.TryParse(ControlId, out _);

    public RegistrarResiduoPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_catalogos is null)
            await CargarCatalogosAsync();

        if (EsEdicion && !_edicionCargada && _catalogos is not null)
            await CargarResiduoEdicionAsync();
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
            var registro = await apiClient.ObtenerRegistroAsync(registroId);
            var residuo = registro.Residuos.FirstOrDefault(x => x.RegistroResiduoId == registroResiduoId);

            if (residuo is null)
            {
                MostrarError("El residuo ya no existe en este registro.");
                return;
            }

            var estadoEditable = string.Equals(registro.Estado, "En proceso", StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(registro.Estado, "Borrador", StringComparison.OrdinalIgnoreCase);
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

            TituloPageLabel.Text = "Editar residuo";
            TituloFormularioLabel.Text = "Editar información";
            SubtituloFormularioLabel.Text = "Actualiza la cantidad u observación del residuo";
            RegistrarButton.Text = "Guardar cambios";

            _edicionCargada = true;
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

        if (_isLoading)
            return;

        _isLoading = true;
        SetBusy(true);

        try
        {
            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var observacion = string.IsNullOrWhiteSpace(ObservacionEditor.Text)
                ? null
                : ObservacionEditor.Text.Trim();

            if (EsEdicion && Guid.TryParse(RegistroResiduoId, out var registroResiduoId))
            {
                var request = new ActualizarRegistroResiduoRequest(
                    UnidadMedidaId: _unidadSeleccionada.UnidadMedidaId,
                    Cantidad: cantidad,
                    Observacion: observacion);

                await apiClient.ActualizarResiduoAsync(registroId, registroResiduoId, request);
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

                await apiClient.AgregarResiduoAsync(registroId, request);
            }

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
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RegistrarResiduoPage>>()?
                .LogError(ex, "Error guardando residuo en {RegistroId}.", registroId);
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
        TipoResiduoPicker.IsEnabled = !busy && !EsEdicion;
        ResiduoPicker.IsEnabled = !busy && !EsEdicion;
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
