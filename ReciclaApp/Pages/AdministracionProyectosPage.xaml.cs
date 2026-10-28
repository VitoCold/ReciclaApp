using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class AdministracionProyectosPage : ContentPage
{
    private IReadOnlyCollection<AdministracionProyectoDto> _items = Array.Empty<AdministracionProyectoDto>();
    private bool _isLoading;

    public AdministracionProyectosPage() => InitializeComponent();

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
            _items = await api.ListarProyectosAsync();
            AplicarFiltro();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<AdministracionProyectosPage>>()?
                .LogError(ex, "Error cargando proyectos administrativos.");
            ErrorLabel.Text = "No se pudieron cargar los proyectos.";
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
                x.Nombre.Contains(filtro, StringComparison.OrdinalIgnoreCase))
            .Select(x => new ProyectoVisual(
                x.ProyectoId,
                x.Nombre,
                $"Código: {x.Codigo}",
                x.EsActivo ? "Activo" : "Inactivo",
                x.EsActivo ? "Desactivar" : "Activar",
                ConstruirUso(x)))
            .ToArray();
    }

    private static string ConstruirUso(AdministracionProyectoDto x)
    {
        var partes = new List<string>();
        if (x.ActividadesActivas > 0) partes.Add($"{x.ActividadesActivas} actividad(es) activa(s)");
        if (x.ControlesAbiertos > 0) partes.Add($"{x.ControlesAbiertos} control(es) abierto(s)");
        if (x.TieneUsoHistorico && partes.Count == 0) partes.Add("con historial operativo");
        return partes.Count == 0 ? "Sin uso operativo" : string.Join(" · ", partes);
    }

    private async void OnNuevoClicked(object sender, EventArgs e) => await AppNavigator.IrANuevoAdministracionProyectoAsync();

    private async void OnEditarClicked(object sender, EventArgs e)
    {
        if (sender is Button button && Guid.TryParse(button.CommandParameter?.ToString(), out var id))
            await AppNavigator.IrAEditarAdministracionProyectoAsync(id);
    }

    private async void OnEstadoClicked(object sender, EventArgs e)
    {
        if (_isLoading || sender is not Button button ||
            !Guid.TryParse(button.CommandParameter?.ToString(), out var id))
            return;

        var item = _items.FirstOrDefault(x => x.ProyectoId == id);
        if (item is null) return;

        var activar = !item.EsActivo;
        var confirmar = await DisplayAlert(
            activar ? "Activar proyecto" : "Desactivar proyecto",
            activar ? $"¿Activar nuevamente {item.Nombre}?" :
                $"¿Desactivar {item.Nombre}? Dejará de estar disponible para nuevos Controles.",
            activar ? "Activar" : "Desactivar",
            "Cancelar");

        if (!confirmar) return;

        try
        {
            _isLoading = true;
            var api = AppServices.Services.GetRequiredService<IAdministracionProyectoActividadApiClient>();
            await api.CambiarEstadoProyectoAsync(id, activar);
            _items = await api.ListarProyectosAsync();
            AplicarFiltro();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            await DisplayAlert("No se puede cambiar el estado", ExtraerDetalle(ex.Message), "Aceptar");
        }
        catch
        {
            await DisplayAlert("No se pudo actualizar", "No se pudo cambiar el estado del proyecto.", "Aceptar");
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
        if (inicio < 0) return "El proyecto todavía tiene dependencias operativas.";
        inicio += marcador.Length;
        var fin = mensaje.IndexOf('"', inicio);
        return fin > inicio ? mensaje[inicio..fin] : "El proyecto todavía tiene dependencias operativas.";
    }

    private void OnBuscarChanged(object sender, TextChangedEventArgs e) => AplicarFiltro();
    private async void OnRefreshRequested(object sender, EventArgs e) => await CargarAsync(false);
    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();

    private sealed record ProyectoVisual(
        Guid ProyectoId,
        string Nombre,
        string CodigoTexto,
        string EstadoTexto,
        string AccionEstadoTexto,
        string UsoTexto);
}
