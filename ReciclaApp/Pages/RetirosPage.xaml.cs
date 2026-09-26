using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class RetirosPage : ContentPage
{
    private readonly ObservableCollection<RetiroVisualItem> _retiros = new();
    private bool _isLoading;

    public RetirosPage()
    {
        InitializeComponent();
        RetirosCollectionView.ItemsSource = _retiros;
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
        if (usuario is null)
            return;

        UsuarioHeaderLabel.Text = string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        var puedeVer = usuario.Roles.Contains("AMBIENTAL") ||
                       usuario.Roles.Contains("RESPONSABLE_OPERATIVO") ||
                       usuario.Roles.Contains("ADMINISTRADOR");
        var puedeCrear = usuario.Roles.Contains("AMBIENTAL") ||
                         usuario.Roles.Contains("RESPONSABLE_OPERATIVO");

        NuevoRetiroButton.IsVisible = puedeCrear;
        if (!puedeVer)
        {
            MostrarError("Tu rol no tiene acceso a la gestión de retiros.");
            RetirosCollectionView.ItemsSource = Array.Empty<RetiroVisualItem>();
            return;
        }

        await CargarAsync(_retiros.Count == 0);
    }

    private async Task CargarAsync(bool showLoader)
    {
        if (_isLoading)
            return;

        _isLoading = true;
        ErrorBorder.IsVisible = false;
        if (showLoader)
        {
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;
        }

        try
        {
            var api = AppServices.Services.GetRequiredService<IRetiroApiClient>();
            var retiros = await api.ListarAsync();

            _retiros.Clear();
            foreach (var retiro in retiros)
                _retiros.Add(MapVisual(retiro));
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            MostrarError("Tu rol no tiene acceso a los retiros.");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<RetirosPage>>()?
                .LogError(ex, "Error cargando retiros.");
            MostrarError("No se pudieron cargar los retiros. Verifica la API e inténtalo nuevamente.");
        }
        finally
        {
            _isLoading = false;
            RetirosRefreshView.IsRefreshing = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private static RetiroVisualItem MapVisual(RetiroListItemDto retiro)
    {
        var totales = retiro.Totales.Count == 0
            ? "Sin cantidades"
            : string.Join(" · ", retiro.Totales.Select(x => $"{x.Cantidad:0.###} {x.Unidad}"));

        return new RetiroVisualItem(
            retiro.RetiroId,
            retiro.Codigo,
            retiro.Estado,
            retiro.FechaRetiro.ToString("dd/MM/yyyy HH:mm"),
            $"{retiro.Sede} · {retiro.PuntoAlmacenamiento}",
            retiro.EmpresaGestora,
            totales,
            retiro.CantidadDetalles == 1 ? "1 residuo" : $"{retiro.CantidadDetalles} residuos");
    }

    private async void OnNuevoRetiroClicked(object sender, EventArgs e) =>
        await AppNavigator.IrANuevoRetiroAsync();

    private async void OnRetiroTapped(object sender, TappedEventArgs e)
    {
        if (Guid.TryParse(e.Parameter?.ToString(), out var retiroId))
            await AppNavigator.IrADetalleRetiroAsync(retiroId);
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private void OnMenuTapped(object sender, TappedEventArgs e) => Shell.Current.FlyoutIsPresented = true;

    private async void OnPerfilTapped(object sender, TappedEventArgs e) => await AppNavigator.IrAPerfilAsync();

    private async void OnRefreshRequested(object sender, EventArgs e) => await CargarAsync(false);

    private sealed record RetiroVisualItem(
        Guid RetiroId,
        string Codigo,
        string Estado,
        string FechaTexto,
        string UbicacionTexto,
        string GestorTexto,
        string TotalesTexto,
        string DetallesTexto);
}
