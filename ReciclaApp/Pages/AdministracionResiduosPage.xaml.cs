using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class AdministracionResiduosPage : ContentPage
{
    private IReadOnlyCollection<AdministracionResiduoCatalogoDto> _items = Array.Empty<AdministracionResiduoCatalogoDto>();
    private bool _loading;

    public AdministracionResiduosPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
        if (!session.IsAuthenticated && !await session.RestoreSessionAsync()) { await AppNavigator.IrAlLoginAsync(); return; }
        if (session.CurrentUser?.Roles.Contains("ADMINISTRADOR") != true) { await AppNavigator.IrAControlesGeneracionAsync(); return; }
        await CargarAsync(_items.Count == 0);
    }

    private async Task CargarAsync(bool loader)
    {
        if (_loading) return;
        _loading = true;
        ErrorBorder.IsVisible = false;
        if (loader) { ActivityIndicator.IsVisible = true; ActivityIndicator.IsRunning = true; }

        try
        {
            var api = AppServices.Services.GetRequiredService<IAdministracionResiduoApiClient>();
            _items = await api.ListarResiduosAsync();
            Filtrar();
        }
        catch { ErrorLabel.Text = "No se pudo cargar el catálogo de residuos."; ErrorBorder.IsVisible = true; }
        finally { _loading = false; Refresh.IsRefreshing = false; ActivityIndicator.IsVisible = false; ActivityIndicator.IsRunning = false; }
    }

    private void Filtrar()
    {
        var f = BuscarEntry.Text?.Trim();
        Collection.ItemsSource = _items
            .Where(x => string.IsNullOrWhiteSpace(f) ||
                        x.Nombre.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        x.Codigo.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        x.TipoNombre.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                        x.ClasificacionNombre.Contains(f, StringComparison.OrdinalIgnoreCase))
            .Select(x => new ResiduoVisual(
                x.ResiduoId,
                x.Nombre,
                $"Código: {x.Codigo}",
                x.ClasificacionNombre,
                $"{x.TipoNombre} · Unidad predeterminada: {x.UnidadCodigo}",
                x.EsActivo ? "Activo" : "Inactivo",
                x.EsActivo ? "Desactivar" : "Activar",
                x.TieneUsoHistorico ? "Con historial operativo" : "Sin uso operativo"))
            .ToArray();
    }

    private async void OnNuevoClicked(object sender, EventArgs e) => await AppNavigator.IrANuevoAdministracionResiduoAsync();

    private async void OnEditarClicked(object sender, EventArgs e)
    {
        if (sender is Button b && Guid.TryParse(b.CommandParameter?.ToString(), out var id))
            await AppNavigator.IrAEditarAdministracionResiduoAsync(id);
    }

    private async void OnEstadoClicked(object sender, EventArgs e)
    {
        if (_loading || sender is not Button b || !Guid.TryParse(b.CommandParameter?.ToString(), out var id)) return;
        var item = _items.FirstOrDefault(x => x.ResiduoId == id);
        if (item is null) return;
        var activar = !item.EsActivo;
        if (!await DisplayAlert(activar ? "Activar residuo" : "Desactivar residuo",
            activar ? $"¿Activar nuevamente {item.Nombre}?" : $"¿Desactivar {item.Nombre}? Dejará de estar disponible para nuevos registros.",
            activar ? "Activar" : "Desactivar", "Cancelar")) return;

        try
        {
            _loading = true;
            var api = AppServices.Services.GetRequiredService<IAdministracionResiduoApiClient>();
            await api.CambiarEstadoResiduoAsync(id, activar);
            _items = await api.ListarResiduosAsync();
            Filtrar();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            await DisplayAlert("No se puede cambiar el estado", ExtraerDetalle(ex.Message), "Aceptar");
        }
        finally { _loading = false; }
    }

    private static string ExtraerDetalle(string mensaje)
    {
        const string m = "\"detail\":\""; var i = mensaje.IndexOf(m, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return "El residuo tiene una dependencia que impide la operación.";
        i += m.Length; var j = mensaje.IndexOf('"', i);
        return j > i ? mensaje[i..j] : "El residuo tiene una dependencia que impide la operación.";
    }

    private void OnBuscarChanged(object sender, TextChangedEventArgs e) => Filtrar();
    private async void OnRefreshRequested(object sender, EventArgs e) => await CargarAsync(false);
    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();

    private sealed record ResiduoVisual(Guid ResiduoId, string Nombre, string CodigoTexto, string ClasificacionTexto, string TipoUnidadTexto, string EstadoTexto, string AccionEstadoTexto, string UsoTexto);
}
