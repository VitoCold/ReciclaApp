using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class AdministracionSedesPage : ContentPage
{
    private IReadOnlyCollection<AdministracionSedeDto> _sedes = Array.Empty<AdministracionSedeDto>();
    private bool _isLoading;

    public AdministracionSedesPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync())
        {
            await AppNavigator.IrAlLoginAsync();
            return;
        }

        if (session.CurrentUser?.Roles.Contains("ADMINISTRADOR") != true)
        {
            await AppNavigator.IrAControlesGeneracionAsync();
            return;
        }

        await CargarAsync(_sedes.Count == 0);
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
            var api = AppServices.Services.GetRequiredService<IAdministracionSedeApiClient>();
            _sedes = await api.ListarAsync();
            AplicarFiltro();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<AdministracionSedesPage>>()?
                .LogError(ex, "Error cargando sedes administrativas.");
            MostrarError("No se pudieron cargar las sedes.");
        }
        finally
        {
            _isLoading = false;
            SedesRefreshView.IsRefreshing = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private void AplicarFiltro()
    {
        var filtro = BuscarEntry.Text?.Trim();
        var items = _sedes
            .Where(x =>
                string.IsNullOrWhiteSpace(filtro) ||
                x.Codigo.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                x.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase))
            .Select(x => new SedeVisual(
                x.SedeId,
                x.Codigo,
                x.Nombre,
                x.EsActivo ? "Activo" : "Inactivo",
                x.EsActivo ? "Desactivar" : "Activar",
                x.PuntosActivos,
                x.ControlesAbiertos))
            .ToArray();

        SedesCollectionView.ItemsSource = items;
    }

    private async void OnNuevaClicked(object sender, EventArgs e) =>
        await AppNavigator.IrANuevaAdministracionSedeAsync();

    private async void OnEditarClicked(object sender, EventArgs e)
    {
        if (sender is Button button && Guid.TryParse(button.CommandParameter?.ToString(), out var sedeId))
            await AppNavigator.IrAEditarAdministracionSedeAsync(sedeId);
    }

    private async void OnEstadoClicked(object sender, EventArgs e)
    {
        if (_isLoading || sender is not Button button ||
            !Guid.TryParse(button.CommandParameter?.ToString(), out var sedeId))
            return;

        var sede = _sedes.FirstOrDefault(x => x.SedeId == sedeId);
        if (sede is null)
            return;

        var activar = !sede.EsActivo;
        var confirmar = await DisplayAlert(
            activar ? "Activar sede" : "Desactivar sede",
            activar
                ? $"¿Activar nuevamente la sede {sede.Nombre}?"
                : $"¿Desactivar la sede {sede.Nombre}? Dejará de estar disponible para nuevas operaciones.",
            activar ? "Activar" : "Desactivar",
            "Cancelar");

        if (!confirmar)
            return;

        try
        {
            _isLoading = true;
            var api = AppServices.Services.GetRequiredService<IAdministracionSedeApiClient>();
            await api.CambiarEstadoAsync(sedeId, activar);
            _sedes = await api.ListarAsync();
            AplicarFiltro();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            await DisplayAlert("No se puede desactivar", ExtraerDetalle(ex.Message), "Aceptar");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<AdministracionSedesPage>>()?
                .LogError(ex, "Error cambiando estado de sede {SedeId}.", sedeId);
            await DisplayAlert("No se pudo actualizar", "No se pudo cambiar el estado de la sede.", "Aceptar");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static string ExtraerDetalle(string mensaje)
    {
        const string marcador = "\"detail\":\"";
        var inicio = mensaje.IndexOf(marcador, StringComparison.OrdinalIgnoreCase);
        if (inicio < 0)
            return "La sede todavía tiene dependencias operativas.";

        inicio += marcador.Length;
        var fin = mensaje.IndexOf('"', inicio);
        return fin > inicio ? mensaje[inicio..fin] : "La sede todavía tiene dependencias operativas.";
    }

    private void MostrarError(string mensaje)
    {
        ErrorLabel.Text = mensaje;
        ErrorBorder.IsVisible = true;
    }

    private void OnBuscarChanged(object sender, TextChangedEventArgs e) => AplicarFiltro();
    private async void OnRefreshRequested(object sender, EventArgs e) => await CargarAsync(false);
    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();

    private sealed record SedeVisual(
        Guid SedeId,
        string Codigo,
        string Nombre,
        string EstadoTexto,
        string AccionEstadoTexto,
        int PuntosActivos,
        int ControlesAbiertos)
    {
        public string CodigoTexto => $"Código: {Codigo}";
    }
}
