using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class AdministracionUsuariosPage : ContentPage
{
    private IReadOnlyCollection<AdministracionUsuarioDto> _usuarios = Array.Empty<AdministracionUsuarioDto>();
    private bool _isLoading;

    public AdministracionUsuariosPage() => InitializeComponent();

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

        await CargarAsync(_usuarios.Count == 0);
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
            var api = AppServices.Services.GetRequiredService<IAdministracionUsuarioApiClient>();
            _usuarios = await api.ListarAsync();
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
            AppServices.Services.GetService<ILogger<AdministracionUsuariosPage>>()?
                .LogError(ex, "Error cargando usuarios administrativos.");
            ErrorLabel.Text = "No se pudieron cargar los usuarios.";
            ErrorBorder.IsVisible = true;
        }
        finally
        {
            _isLoading = false;
            UsuariosRefreshView.IsRefreshing = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private void AplicarFiltro()
    {
        var filtro = BuscarEntry.Text?.Trim();
        var items = _usuarios
            .Where(x =>
                string.IsNullOrWhiteSpace(filtro) ||
                NombreCompleto(x).Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                x.Usuario.Contains(filtro, StringComparison.OrdinalIgnoreCase) ||
                (x.Email?.Contains(filtro, StringComparison.OrdinalIgnoreCase) ?? false))
            .Select(x => new UsuarioVisual(
                x.UsuarioId,
                NombreCompleto(x),
                string.IsNullOrWhiteSpace(x.Email) ? $"@{x.Usuario}" : $"@{x.Usuario} · {x.Email}",
                x.Roles.Count == 0 ? "Sin rol" : string.Join(" · ", x.Roles.Select(r => r.Nombre)),
                x.Sedes.Count == 0 ? "Acceso global / sin sedes asignadas" : string.Join(" · ", x.Sedes.Select(s => s.Nombre)),
                x.EsActivo ? "Activo" : "Inactivo",
                x.EsActivo ? "Desactivar" : "Activar",
                x.EsActivo))
            .ToArray();

        UsuariosCollectionView.ItemsSource = items;
    }

    private static string NombreCompleto(AdministracionUsuarioDto x) =>
        string.Join(" ", new[] { x.Nombres, x.Apellidos }.Where(v => !string.IsNullOrWhiteSpace(v)));

    private async void OnNuevoClicked(object sender, EventArgs e) => await AppNavigator.IrANuevoAdministracionUsuarioAsync();

    private async void OnEditarClicked(object sender, EventArgs e)
    {
        if (sender is Button button && Guid.TryParse(button.CommandParameter?.ToString(), out var id))
            await AppNavigator.IrAEditarAdministracionUsuarioAsync(id);
    }

    private async void OnEstadoClicked(object sender, EventArgs e)
    {
        if (_isLoading || sender is not Button button ||
            !Guid.TryParse(button.CommandParameter?.ToString(), out var id))
            return;

        var usuario = _usuarios.FirstOrDefault(x => x.UsuarioId == id);
        if (usuario is null)
            return;

        var activar = !usuario.EsActivo;
        var confirmar = await DisplayAlert(
            activar ? "Activar usuario" : "Desactivar usuario",
            activar
                ? $"¿Permitir nuevamente el acceso de {NombreCompleto(usuario)}?"
                : $"¿Desactivar el acceso de {NombreCompleto(usuario)}? Su historial se conservará.",
            activar ? "Activar" : "Desactivar",
            "Cancelar");

        if (!confirmar)
            return;

        try
        {
            _isLoading = true;
            var api = AppServices.Services.GetRequiredService<IAdministracionUsuarioApiClient>();
            await api.CambiarEstadoAsync(id, activar);
            await CargarForzadoAsync();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            await DisplayAlert("No se puede cambiar el estado", "No puedes desactivar tu propia cuenta o al último administrador activo.", "Aceptar");
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<AdministracionUsuariosPage>>()?
                .LogError(ex, "Error cambiando estado del usuario {UsuarioId}.", id);
            await DisplayAlert("No se pudo actualizar", "No se pudo cambiar el estado del usuario.", "Aceptar");
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task CargarForzadoAsync()
    {
        var api = AppServices.Services.GetRequiredService<IAdministracionUsuarioApiClient>();
        _usuarios = await api.ListarAsync();
        AplicarFiltro();
    }

    private void OnBuscarChanged(object sender, TextChangedEventArgs e) => AplicarFiltro();
    private async void OnRefreshRequested(object sender, EventArgs e) => await CargarAsync(false);
    private async void OnBackTapped(object sender, TappedEventArgs e) => await AppNavigator.VolverAsync();

    private sealed record UsuarioVisual(
        Guid UsuarioId,
        string NombreCompleto,
        string CuentaTexto,
        string RolesTexto,
        string SedesTexto,
        string EstadoTexto,
        string AccionEstadoTexto,
        bool EsActivo);
}
