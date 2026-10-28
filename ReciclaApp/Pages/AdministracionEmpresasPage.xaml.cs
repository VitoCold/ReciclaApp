using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class AdministracionEmpresasPage : ContentPage
{
    private IReadOnlyCollection<AdministracionEmpresaDto> _items = Array.Empty<AdministracionEmpresaDto>();
    private bool _loading;

    public AdministracionEmpresasPage() => InitializeComponent();

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
            var api = AppServices.Services.GetRequiredService<IAdministracionEmpresaApiClient>();
            _items = await api.ListarAsync();
            Filtrar();
        }
        catch
        {
            ErrorLabel.Text = "No se pudieron cargar las empresas.";
            ErrorBorder.IsVisible = true;
        }
        finally
        {
            _loading = false;
            Refresh.IsRefreshing = false;
            ActivityIndicator.IsVisible = false;
            ActivityIndicator.IsRunning = false;
        }
    }

    private void Filtrar()
    {
        var f = BuscarEntry.Text?.Trim();

        Collection.ItemsSource = _items
            .Where(x =>
                string.IsNullOrWhiteSpace(f) ||
                x.Codigo.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                x.RazonSocial.Contains(f, StringComparison.OrdinalIgnoreCase) ||
                (x.NombreComercial?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (x.Ruc?.Contains(f, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(x => new EmpresaVisual(
                x.EmpresaId,
                x.NombreComercial ?? x.RazonSocial,
                $"Código: {x.Codigo}" + (string.IsNullOrWhiteSpace(x.Ruc) ? "" : $" · RUC: {x.Ruc}"),
                x.EsGestoraResiduos ? "EO-RS" : "Empresa responsable",
                x.EsActivo
                    ? ConstruirUso(x)
                    : "Inactiva · " + ConstruirUso(x),
                x.EsActivo ? "Desactivar" : "Activar",
                x.EsGestoraResiduos,
                string.IsNullOrWhiteSpace(x.NumeroAutorizacion)
                    ? "Sin autorización/registro consignado"
                    : $"Autorización/registro: {x.NumeroAutorizacion}"))
            .ToArray();
    }

    private static string ConstruirUso(AdministracionEmpresaDto x)
    {
        var partes = new List<string>();
        if (x.ControlesAbiertos > 0) partes.Add($"{x.ControlesAbiertos} control(es) abierto(s)");
        if (x.RetirosRegistrados > 0) partes.Add($"{x.RetirosRegistrados} retiro(s)");
        if (x.DisposicionesRegistradas > 0) partes.Add($"{x.DisposicionesRegistradas} disposición(es)");
        if (x.TieneUsoHistorico && partes.Count == 0) partes.Add("con historial operativo");
        return partes.Count == 0 ? "Sin uso operativo" : string.Join(" · ", partes);
    }

    private async void OnNuevaClicked(object sender, EventArgs e) => await AppNavigator.IrANuevaAdministracionEmpresaAsync();

    private async void OnEditarClicked(object sender, EventArgs e)
    {
        if (sender is Button b && Guid.TryParse(b.CommandParameter?.ToString(), out var id))
            await AppNavigator.IrAEditarAdministracionEmpresaAsync(id);
    }

    private async void OnEstadoClicked(object sender, EventArgs e)
    {
        if (_loading || sender is not Button b || !Guid.TryParse(b.CommandParameter?.ToString(), out var id)) return;
        var item = _items.FirstOrDefault(x => x.EmpresaId == id);
        if (item is null) return;

        var activar = !item.EsActivo;
        if (!await DisplayAlert(
            activar ? "Activar empresa" : "Desactivar empresa",
            activar
                ? $"¿Activar nuevamente {item.NombreComercial ?? item.RazonSocial}?"
                : $"¿Desactivar {item.NombreComercial ?? item.RazonSocial}? Dejará de estar disponible para nuevas operaciones.",
            activar ? "Activar" : "Desactivar",
            "Cancelar")) return;

        try
        {
            _loading = true;
            var api = AppServices.Services.GetRequiredService<IAdministracionEmpresaApiClient>();
            await api.CambiarEstadoAsync(id, activar);
            _items = await api.ListarAsync();
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
        const string m = "\"detail\":\"";
        var i = mensaje.IndexOf(m, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return "La empresa todavía tiene dependencias operativas.";
        i += m.Length;
        var j = mensaje.IndexOf('"', i);
        return j > i ? mensaje[i..j] : "La empresa todavía tiene dependencias operativas.";
    }

    private void OnBuscarChanged(object sender, TextChangedEventArgs e) => Filtrar();
    private async void OnRefreshRequested(object sender, EventArgs e) => await CargarAsync(false);
    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();

    private sealed record EmpresaVisual(
        Guid EmpresaId,
        string Nombre,
        string IdentificacionTexto,
        string TipoTexto,
        string EstadoUsoTexto,
        string AccionEstadoTexto,
        bool EsEors,
        string AutorizacionTexto);
}
