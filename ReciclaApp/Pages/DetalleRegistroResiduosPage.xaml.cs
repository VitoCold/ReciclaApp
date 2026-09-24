using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

[QueryProperty(nameof(RegistroId), "registroId")]
public partial class DetalleRegistroResiduosPage : ContentPage
{
    private readonly ObservableCollection<ResiduoVisualItem> _residuos = new();
    private bool _isLoading;

    public string RegistroId { get; set; } = string.Empty;

    public DetalleRegistroResiduosPage()
    {
        InitializeComponent();
        ResiduosCollectionView.ItemsSource = _residuos;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarAsync(showLoader: _residuos.Count == 0);
    }

    private async Task CargarAsync(bool showLoader)
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

        if (showLoader)
        {
            ResiduosActivityIndicator.IsVisible = true;
            ResiduosActivityIndicator.IsRunning = true;
        }

        try
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
            {
                await AppNavigator.IrAlLoginAsync();
                return;
            }

            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var detalle = await apiClient.ObtenerRegistroAsync(registroId);
            var catalogos = await apiClient.ObtenerCatalogosAsync();

            _residuos.Clear();
            foreach (var residuo in detalle.Residuos)
            {
                var catalogo = catalogos.Residuos.FirstOrDefault(x => x.ResiduoId == residuo.ResiduoId);
                var clasificacion = catalogo is null
                    ? null
                    : catalogos.Clasificaciones.FirstOrDefault(x => x.ClasificacionResiduoId == catalogo.ClasificacionResiduoId);
                var unidad = catalogos.UnidadesMedida.FirstOrDefault(x => x.UnidadMedidaId == residuo.UnidadMedidaId);

                _residuos.Add(new ResiduoVisualItem(
                    residuo.RegistroResiduoId,
                    catalogo?.Nombre ?? "Residuo",
                    clasificacion?.Nombre ?? "Sin clasificación",
                    clasificacion?.Codigo == "PELIGROSO"
                        ? Color.FromArgb("#FFE9E7")
                        : Color.FromArgb("#E8F7EE"),
                    TryColor(clasificacion?.ColorHex) ?? Color.FromArgb("#079542"),
                    $"{residuo.Cantidad:0.###} {unidad?.Codigo ?? string.Empty}".Trim(),
                    residuo.Observacion,
                    residuo.Fotos.Count == 1 ? "1 foto" : $"{residuo.Fotos.Count} fotos"));
            }

            CantidadHeaderLabel.Text = _residuos.Count == 1
                ? "1 residuo"
                : $"{_residuos.Count} residuos";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            MostrarError("El registro no existe o ya no tienes acceso a él.");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<DetalleRegistroResiduosPage>>()?
                .LogError(ex, "Error cargando residuos del registro {RegistroId}.", RegistroId);
            MostrarError("No se pudieron cargar los residuos del registro.");
        }
        finally
        {
            _isLoading = false;
            ResiduosRefreshView.IsRefreshing = false;
            ResiduosActivityIndicator.IsRunning = false;
            ResiduosActivityIndicator.IsVisible = false;
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
        if (Guid.TryParse(RegistroId, out var registroId))
            await AppNavigator.IrARegistrarResiduoAsync(registroId);
    }

    private async void OnDetalleTapped(object sender, TappedEventArgs e)
    {
        if (Guid.TryParse(RegistroId, out var registroId))
            await AppNavigator.IrADetalleRegistroAsync(registroId);
    }

    private async void OnDisposicionTapped(object sender, TappedEventArgs e)
    {
        if (Guid.TryParse(RegistroId, out var registroId))
            await AppNavigator.IrADisposicionDelRegistroAsync(registroId);
    }

    private async void OnRefreshRequested(object sender, EventArgs e)
    {
        await CargarAsync(showLoader: false);
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
        string FotosTexto)
    {
        public bool TieneObservacion => !string.IsNullOrWhiteSpace(Observacion);
    }
}
