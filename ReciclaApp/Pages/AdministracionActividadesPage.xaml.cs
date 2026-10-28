using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class AdministracionActividadesPage : ContentPage
{
    private IReadOnlyCollection<AdministracionActividadDto> _items = Array.Empty<AdministracionActividadDto>();
    private bool _isLoading;

    public AdministracionActividadesPage() => InitializeComponent();

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

        await CargarAsync(_items.Count == 0);
    }

    private async Task CargarAsync(bool showLoader)
    {
        if (_isLoading) return;
        _isLoading = true;
        ErrorBorder.IsVisible = false;

        if (showLoader)
        {
            ActivityIndicator.IsVisible = true;
            ActivityIndicator.IsRunning = true;
        }

        try
        {
            var api = AppServices.Services.GetRequiredService<IAdministracionProyectoActividadApiClient>();
            _items = await api.ListarActividadesAsync();
            AplicarFiltro();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<AdministracionActividadesPage>>()?
                .LogError(ex, "Error cargando actividades administrativas.");
            ErrorLabel.Text = "No se pudieron cargar las actividades.";
            ErrorBorder.IsVisible = true;
        }
        finally
        {
            _isLoading = false;
            Refresh.IsRefreshing = false;
            ActivityIndicator.IsVisible = false;
            ActivityIndicator.IsRunning = false;
        }
    }

    private void AplicarFiltro()
    {
        var filtro = BuscarEntry.Text?.Trim();
        Collection.ItemsSource = _items
            .Where(x =>
                string.IsNullOrWhiteSpace(filtro) ||
                x.Codigo.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                x.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                (x.ProyectoNombre?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(x => new ActividadVisual(
                x.ActividadId,
                x.Nombre,
                x.ProyectoNombre is null ? "Sin proyecto" : $"{x.ProyectoNombre} · {x.ProyectoCodigo}",
                $"Código: {x.Codigo}",
                x.EsActivo ? "Activo" : "Inactivo",
                x.EsActivo ? "Desactivar" : "Activar",
                x.ControlesAbiertos > 0
                    ? $"{x.ControlesAbiertos} control(es) abierto(s)"
                    : x.TieneUsoHistorico ? "Con historial operativo" : "Sin uso operativo"))
            .ToArray();
    }

    private async void OnNuevaClicked(object sender, EventArgs e) => await AppNavigator.IrANuevaAdministracionActividadAsync();

    private async void OnEditarClicked(object sender, EventArgs e)
    {
        if (sender is Button button && Guid.TryParse(button.CommandParameter?.ToString(), out var id))
            await AppNavigator.IrAEditarAdministracionActividadAsync(id);
    }

    private async void OnEstadoClicked(object sender, EventArgs e)
    {
        if (_isLoading || sender is not Button button ||
            !Guid.TryParse(button.CommandParameter?.ToString(), out var id))
            return;

        var item = _items.FirstOrDefault(x => x.ActividadId == id);
        if (item is null) return;

        var activar = !item.EsActivo;
        var confirmar = await DisplayAlert(
            activar ? "Activar actividad" : "Desactivar actividad",
            activar ? $"¿Activar nuevamente {item.Nombre}?" :
                $"¿Desactivar {item.Nombre}? Dejará de aparecer para nuevos Controles.",
            activar ? "Activar" : "Desactivar",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            _isLoading = true;
            var api = AppServices.Services.GetRequiredService<IAdministracionProyectoActividadApiClient>();
            await api.CambiarEstadoActividadAsync(id, activar);
            _items = await api.ListarActividadesAsync();
            AplicarFiltro();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            await DisplayAlert("No se puede cambiar el estado", ExtraerDetalle(ex.Message), "Aceptar");
        }
        catch
        {
            await DisplayAlert("No se pudo actualizar", "No se pudo cambiar el estado de la actividad.", "Aceptar");
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
        if (inicio < 0) return "La actividad todavía tiene dependencias operativas.";
        inicio += marcador.Length;
        var fin = mensaje.IndexOf('"', inicio);
        return fin > inicio ? mensaje[inicio..fin] : "La actividad todavía tiene dependencias operativas.";
    }

    private void OnBuscarChanged(object sender, TextChangedEventArgs e) => AplicarFiltro();
    private async void OnRefreshRequested(object sender, EventArgs e) => await CargarAsync(false);
    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();

    private sealed record ActividadVisual(
        Guid ActividadId,
        string Nombre,
        string ProyectoTexto,
        string CodigoTexto,
        string EstadoTexto,
        string AccionEstadoTexto,
        string UsoTexto);
}
