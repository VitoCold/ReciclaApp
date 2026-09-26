using System.Collections.ObjectModel;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Recicla.Shared.Contracts;
using Recicla.Shared.Services;
using ReciclaApp.Navigation;
using ReciclaApp.Services;

namespace ReciclaApp.Pages;

public partial class ControlesGeneracionPage : ContentPage
{
    private readonly ObservableCollection<ControlGeneracionListItemDto> _controles = new();
    private bool _isLoading;

    public ControlesGeneracionPage()
    {
        InitializeComponent();
        ControlesCollectionView.ItemsSource = _controles;
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

        ConfigurarExperiencia(session.CurrentUser);
        await CargarControlesAsync(_controles.Count == 0);
    }

    private void ConfigurarExperiencia(UsuarioDto? usuario)
    {
        if (usuario is null)
            return;

        UsuarioHeaderLabel.Text = string.Join(" ", new[] { usuario.Nombres, usuario.Apellidos }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        var esAmbiental = usuario.Roles.Contains("AMBIENTAL");
        var esResponsable = usuario.Roles.Contains("RESPONSABLE_OPERATIVO");

        TituloLabel.Text = esAmbiental ? "Todos los controles" : "Mis controles";
        SubtituloLabel.Text = esAmbiental
            ? "Supervisión global de los controles de generación"
            : "Controles donde tienes una asignación vigente";
        NuevoControlButton.IsVisible = esResponsable;
        EmptyHintLabel.Text = esResponsable
            ? "Crea un control para solicitar la aprobación de Ambiental."
            : esAmbiental
                ? "Los controles creados por los responsables aparecerán aquí."
                : "Cuando te asignen a un control aparecerá aquí.";
    }

    private async Task CargarControlesAsync(bool showLoader)
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
            var apiClient = AppServices.Services.GetRequiredService<IReciclaApiClient>();
            var controles = await apiClient.ListarControlesGeneracionAsync();

            _controles.Clear();
            foreach (var control in controles)
                _controles.Add(control);

            CantidadLabel.Text = _controles.Count == 1 ? "1 control" : $"{_controles.Count} controles";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var session = AppServices.Services.GetRequiredService<IAuthSessionService>();
            await session.LogoutAsync();
            await AppNavigator.IrAlLoginAsync();
        }
        catch (Exception ex)
        {
            AppServices.Services.GetService<ILogger<ControlesGeneracionPage>>()?
                .LogError(ex, "Error cargando controles de generación.");
            ErrorLabel.Text = "No se pudieron cargar los controles. Verifica la API e inténtalo nuevamente.";
            ErrorBorder.IsVisible = true;
        }
        finally
        {
            _isLoading = false;
            ControlesRefreshView.IsRefreshing = false;
            ActivityIndicator.IsRunning = false;
            ActivityIndicator.IsVisible = false;
        }
    }

    private void OnMenuTapped(object sender, TappedEventArgs e) => Shell.Current.FlyoutIsPresented = true;

    private async void OnPerfilTapped(object sender, TappedEventArgs e) => await AppNavigator.IrAPerfilAsync();

    private async void OnNuevoControlClicked(object sender, EventArgs e) => await AppNavigator.IrANuevoControlGeneracionAsync();

    private async void OnControlTapped(object sender, TappedEventArgs e)
    {
        if (Guid.TryParse(e.Parameter?.ToString(), out var controlId))
            await AppNavigator.IrADetalleControlGeneracionAsync(controlId);
    }

    private async void OnRefreshRequested(object sender, EventArgs e) => await CargarControlesAsync(false);

    private async void OnReintentarTapped(object sender, TappedEventArgs e) => await CargarControlesAsync(_controles.Count == 0);
}
